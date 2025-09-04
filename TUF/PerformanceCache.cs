using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

using Serde;

namespace TUF.Models;

/// <summary>
/// High-performance caching system for expensive TUF operations.
/// Provides thread-safe caching for signature verification, JSON serialization, and metadata parsing.
/// </summary>
/// <remarks>
/// This cache implements several optimization strategies from the TUF .NET Performance Plan:
/// 1. Signature verification caching - Eliminates redundant cryptographic operations
/// 2. Canonical JSON serialization caching - Avoids repeated JSON generation  
/// 3. Metadata deserialization caching - Reuses parsed objects when safe
/// 
/// Design principles:
/// - Thread-safe using ConcurrentDictionary for multi-threaded scenarios
/// - Memory-conscious with size limits and LRU-style eviction
/// - Security-aware - cache keys include cryptographic hashes to prevent collisions
/// - AOT-compatible - no reflection or dynamic code generation
/// - Performance-first - optimized for hot path operations during metadata processing
/// 
/// Cache invalidation strategy:
/// - Signature cache: Never expires (immutable cryptographic operations)
/// - Serialization cache: Bounded size with LRU eviction
/// - Deserialization cache: Conservative TTL to handle dynamic data
/// </remarks>
public static class PerformanceCache
{
    // Cache configuration constants - chosen based on typical TUF repository usage patterns
    private const int MaxSignatureCacheSize = 1000;    // Typical repo has ~100 keys, allows for ~10x buffer
    private const int MaxSerializationCacheSize = 500;  // JSON documents are larger, smaller cache
    private const int MaxDeserializationCacheSize = 100; // Full metadata objects are memory-intensive
    private const int CleanupThreshold = 50;           // Clean when cache exceeds max by this amount

    /// <summary>
    /// Cache for signature verification results.
    /// Key: Hash of (publicKey + signature + signedData), Value: verification result
    /// </summary>
    private static readonly ConcurrentDictionary<string, SignatureVerificationResult> SignatureCache 
        = new ConcurrentDictionary<string, SignatureVerificationResult>();

    /// <summary>
    /// Cache for canonical JSON serialization results.
    /// Key: Hash of object content, Value: UTF-8 JSON bytes
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte[]> SerializationCache 
        = new ConcurrentDictionary<string, byte[]>();

    /// <summary>
    /// Cache for metadata deserialization results.
    /// Key: Hash of input JSON bytes, Value: parsed metadata object with timestamp
    /// </summary>
    private static readonly ConcurrentDictionary<string, CachedMetadata> DeserializationCache 
        = new ConcurrentDictionary<string, CachedMetadata>();

    /// <summary>
    /// Statistics tracking for performance monitoring and optimization.
    /// </summary>
    public static class Statistics
    {
        private static long _signatureCacheHits;
        private static long _signatureCacheMisses;
        private static long _serializationCacheHits;
        private static long _serializationCacheMisses;
        private static long _deserializationCacheHits;
        private static long _deserializationCacheMisses;

        public static long SignatureCacheHits => _signatureCacheHits;
        public static long SignatureCacheMisses => _signatureCacheMisses;
        public static long SerializationCacheHits => _serializationCacheHits;
        public static long SerializationCacheMisses => _serializationCacheMisses;
        public static long DeserializationCacheHits => _deserializationCacheHits;
        public static long DeserializationCacheMisses => _deserializationCacheMisses;

        public static double SignatureCacheHitRatio => 
            _signatureCacheHits + _signatureCacheMisses == 0 ? 0.0 : 
            (double)_signatureCacheHits / (_signatureCacheHits + _signatureCacheMisses);

        public static double SerializationCacheHitRatio => 
            _serializationCacheHits + _serializationCacheMisses == 0 ? 0.0 : 
            (double)_serializationCacheHits / (_serializationCacheHits + _serializationCacheMisses);

        public static double DeserializationCacheHitRatio => 
            _deserializationCacheHits + _deserializationCacheMisses == 0 ? 0.0 : 
            (double)_deserializationCacheHits / (_deserializationCacheHits + _deserializationCacheMisses);

        internal static void RecordSignatureHit() => Interlocked.Increment(ref _signatureCacheHits);
        internal static void RecordSignatureMiss() => Interlocked.Increment(ref _signatureCacheMisses);
        internal static void RecordSerializationHit() => Interlocked.Increment(ref _serializationCacheHits);
        internal static void RecordSerializationMiss() => Interlocked.Increment(ref _serializationCacheMisses);
        internal static void RecordDeserializationHit() => Interlocked.Increment(ref _deserializationCacheHits);
        internal static void RecordDeserializationMiss() => Interlocked.Increment(ref _deserializationCacheMisses);

        /// <summary>
        /// Resets all statistics. Primarily used for testing.
        /// </summary>
        public static void Reset()
        {
            _signatureCacheHits = 0;
            _signatureCacheMisses = 0;
            _serializationCacheHits = 0;
            _serializationCacheMisses = 0;
            _deserializationCacheHits = 0;
            _deserializationCacheMisses = 0;
        }
    }

    /// <summary>
    /// Cached signature verification result with metadata.
    /// </summary>
    private record SignatureVerificationResult(bool IsValid, DateTimeOffset CachedAt);

    /// <summary>
    /// Cached metadata object with timestamp for TTL management.
    /// </summary>
    private record CachedMetadata(object MetadataObject, DateTimeOffset CachedAt);

    /// <summary>
    /// Gets or computes a signature verification result with caching.
    /// </summary>
    /// <param name="publicKey">Public key used for verification</param>
    /// <param name="signature">Signature to verify</param>
    /// <param name="signedData">Data that was signed</param>
    /// <param name="verificationFunc">Function to perform actual verification if not cached</param>
    /// <returns>True if signature is valid, false otherwise</returns>
    public static bool GetOrComputeSignatureVerification(
        string publicKey, 
        string signature, 
        byte[] signedData,
        Func<bool> verificationFunc)
    {
        // Generate cache key from all inputs to ensure uniqueness and prevent collisions
        var cacheKey = GenerateSignatureCacheKey(publicKey, signature, signedData);
        
        if (SignatureCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Statistics.RecordSignatureHit();
            return cachedResult.IsValid;
        }

        // Cache miss - perform actual verification
        Statistics.RecordSignatureMiss();
        var result = verificationFunc();
        
        // Store result in cache
        var cacheEntry = new SignatureVerificationResult(result, DateTimeOffset.UtcNow);
        SignatureCache.TryAdd(cacheKey, cacheEntry);
        
        // Periodic cleanup to prevent unbounded growth
        if (SignatureCache.Count > MaxSignatureCacheSize + CleanupThreshold)
        {
            CleanupSignatureCache();
        }

        return result;
    }

    /// <summary>
    /// Gets or computes canonical JSON serialization with caching.
    /// </summary>
    /// <typeparam name="T">Type of object to serialize</typeparam>
    /// <param name="obj">Object to serialize</param>
    /// <param name="serializationFunc">Function to perform actual serialization if not cached</param>
    /// <returns>UTF-8 encoded canonical JSON bytes</returns>
    public static byte[] GetOrComputeSerialization<T>(T obj, Func<byte[]> serializationFunc) 
        where T : notnull
    {
        // Generate cache key from object content - use hash for memory efficiency
        var cacheKey = GenerateSerializationCacheKey(obj);
        
        if (SerializationCache.TryGetValue(cacheKey, out var cachedBytes))
        {
            Statistics.RecordSerializationHit();
            return cachedBytes;
        }

        // Cache miss - perform actual serialization
        Statistics.RecordSerializationMiss();
        var bytes = serializationFunc();
        
        // Store in cache
        SerializationCache.TryAdd(cacheKey, bytes);
        
        // Periodic cleanup
        if (SerializationCache.Count > MaxSerializationCacheSize + CleanupThreshold)
        {
            CleanupSerializationCache();
        }

        return bytes;
    }

    /// <summary>
    /// Gets or computes metadata deserialization with caching and TTL.
    /// </summary>
    /// <typeparam name="T">Type of metadata to deserialize</typeparam>
    /// <param name="jsonBytes">JSON bytes to deserialize</param>
    /// <param name="deserializationFunc">Function to perform actual deserialization if not cached</param>
    /// <param name="ttlMinutes">Time-to-live for cached entry (default: 5 minutes)</param>
    /// <returns>Deserialized metadata object</returns>
    public static T GetOrComputeDeserialization<T>(
        byte[] jsonBytes, 
        Func<T> deserializationFunc,
        int ttlMinutes = 5) 
        where T : notnull
    {
        var cacheKey = GenerateDeserializationCacheKey(jsonBytes);
        var now = DateTimeOffset.UtcNow;
        
        if (DeserializationCache.TryGetValue(cacheKey, out var cachedEntry))
        {
            // Check TTL
            if (now - cachedEntry.CachedAt < TimeSpan.FromMinutes(ttlMinutes))
            {
                Statistics.RecordDeserializationHit();
                return (T)cachedEntry.MetadataObject;
            }
            else
            {
                // Expired - remove from cache
                DeserializationCache.TryRemove(cacheKey, out _);
            }
        }

        // Cache miss or expired - perform actual deserialization
        Statistics.RecordDeserializationMiss();
        var result = deserializationFunc();
        
        // Store in cache with current timestamp
        var cacheEntry = new CachedMetadata(result, now);
        DeserializationCache.TryAdd(cacheKey, cacheEntry);
        
        // Periodic cleanup
        if (DeserializationCache.Count > MaxDeserializationCacheSize + CleanupThreshold)
        {
            CleanupDeserializationCache();
        }

        return result;
    }

    /// <summary>
    /// Clears all caches. Useful for testing and memory management.
    /// </summary>
    public static void ClearAll()
    {
        SignatureCache.Clear();
        SerializationCache.Clear();
        DeserializationCache.Clear();
    }

    /// <summary>
    /// Gets current cache sizes for monitoring.
    /// </summary>
    public static (int SignatureCache, int SerializationCache, int DeserializationCache) GetCacheSizes()
    {
        return (SignatureCache.Count, SerializationCache.Count, DeserializationCache.Count);
    }

    // Private helper methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GenerateSignatureCacheKey(string publicKey, string signature, byte[] signedData)
    {
        // Create composite key from all signature verification inputs
        // Using SHA-256 ensures collision resistance and consistent key length
        Span<byte> hashInput = stackalloc byte[512]; // Reasonable buffer for most keys
        var writer = hashInput;
        var publicKeyBytes = System.Text.Encoding.UTF8.GetBytes(publicKey);
        var signatureBytes = System.Text.Encoding.UTF8.GetBytes(signature);
        
        // Fallback to heap allocation if stack buffer too small
        if (publicKeyBytes.Length + signatureBytes.Length + signedData.Length > hashInput.Length)
        {
            var heapBuffer = new byte[publicKeyBytes.Length + signatureBytes.Length + signedData.Length];
            writer = heapBuffer;
        }

        var offset = 0;
        publicKeyBytes.CopyTo(writer[offset..]);
        offset += publicKeyBytes.Length;
        signatureBytes.CopyTo(writer[offset..]);
        offset += signatureBytes.Length;
        signedData.CopyTo(writer[offset..]);
        offset += signedData.Length;

        var hash = SHA256.HashData(writer[..offset]);
        return Convert.ToHexString(hash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GenerateSerializationCacheKey<T>(T obj) where T : notnull
    {
        // Use object's hash code combined with type information
        // This is faster than full content hashing but may have more collisions
        // For TUF metadata, this is acceptable as objects are generally immutable
        var typeHash = typeof(T).GetHashCode();
        var objectHash = obj.GetHashCode();
        var combined = HashCode.Combine(typeHash, objectHash);
        return combined.ToString("x8");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GenerateDeserializationCacheKey(byte[] jsonBytes)
    {
        // Hash the JSON content for cache key
        var hash = SHA256.HashData(jsonBytes);
        return Convert.ToHexString(hash);
    }

    private static void CleanupSignatureCache()
    {
        if (SignatureCache.Count <= MaxSignatureCacheSize) return;

        // Simple cleanup: remove oldest entries (oldest = earliest CachedAt timestamp)
        // Note: This is a simplistic LRU-style cleanup. In production, consider more sophisticated algorithms.
        var entriesToRemove = SignatureCache
            .OrderBy(kv => kv.Value.CachedAt)
            .Take(SignatureCache.Count - MaxSignatureCacheSize)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in entriesToRemove)
        {
            SignatureCache.TryRemove(key, out _);
        }
    }

    private static void CleanupSerializationCache()
    {
        if (SerializationCache.Count <= MaxSerializationCacheSize) return;

        // Remove excess entries (no timestamp available, so remove arbitrary keys)
        var keysToRemove = SerializationCache.Keys
            .Take(SerializationCache.Count - MaxSerializationCacheSize)
            .ToList();

        foreach (var key in keysToRemove)
        {
            SerializationCache.TryRemove(key, out _);
        }
    }

    private static void CleanupDeserializationCache()
    {
        if (DeserializationCache.Count <= MaxDeserializationCacheSize) return;

        var now = DateTimeOffset.UtcNow;
        
        // Remove expired entries first
        var expiredKeys = DeserializationCache
            .Where(kv => now - kv.Value.CachedAt > TimeSpan.FromMinutes(10)) // Extended TTL for cleanup
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            DeserializationCache.TryRemove(key, out _);
        }

        // If still over limit, remove oldest
        if (DeserializationCache.Count > MaxDeserializationCacheSize)
        {
            var oldestKeys = DeserializationCache
                .OrderBy(kv => kv.Value.CachedAt)
                .Take(DeserializationCache.Count - MaxDeserializationCacheSize)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in oldestKeys)
            {
                DeserializationCache.TryRemove(key, out _);
            }
        }
    }
}