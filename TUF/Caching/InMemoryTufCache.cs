using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace TUF.Caching;

/// <summary>
/// High-performance in-memory TUF cache with automatic expiration and size limits
/// </summary>
public sealed class InMemoryTufCache : ITufCache, IDisposable
{
    private readonly ConcurrentDictionary<string, CachedMetadata> _metadataCache = new();
    private readonly ConcurrentDictionary<string, CachedTarget> _targetCache = new();
    private readonly ILogger<InMemoryTufCache>? _logger;
    private readonly Timer _cleanupTimer;
    private readonly InMemoryTufCacheOptions _options;

    // Statistics tracking
    private int _metadataHits;
    private int _metadataMisses;
    private int _targetHits;
    private int _targetMisses;
    private DateTime _lastCleanupTime = DateTime.UtcNow;

    public InMemoryTufCache(InMemoryTufCacheOptions? options = null, ILogger<InMemoryTufCache>? logger = null)
    {
        _options = options ?? new InMemoryTufCacheOptions();
        _logger = logger;

        // Start cleanup timer for expired entries
        _cleanupTimer = new Timer(CleanupCallback, null, _options.CleanupInterval, _options.CleanupInterval);
        
        _logger?.LogDebug("InMemoryTufCache initialized with max size: {MaxSizeBytes}, cleanup interval: {CleanupInterval}",
            _options.MaxSizeBytes, _options.CleanupInterval);
    }

    public async ValueTask SetMetadataAsync(string key, ReadOnlyMemory<byte> data, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        
        if (data.Length > _options.MaxMetadataSizeBytes)
        {
            _logger?.LogWarning("Metadata too large for cache: {Size} > {MaxSize}", data.Length, _options.MaxMetadataSizeBytes);
            return;
        }

        var cachedItem = new CachedMetadata(data.ToArray(), expiresAt);
        _metadataCache.AddOrUpdate(key, cachedItem, (_, _) => cachedItem);

        _logger?.LogDebug("Cached metadata {Key} ({Size} bytes, expires: {ExpiresAt})", key, data.Length, expiresAt);
        
        // Trigger cleanup if cache is getting large
        await CheckCacheSizeAndCleanupAsync(cancellationToken);
    }

    public ValueTask<byte[]?> GetMetadataAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_metadataCache.TryGetValue(key, out var cached))
        {
            if (cached.ExpiresAt > DateTime.UtcNow)
            {
                Interlocked.Increment(ref _metadataHits);
                _logger?.LogDebug("Metadata cache hit for {Key}", key);
                return ValueTask.FromResult<byte[]?>(cached.Data);
            }
            
            // Remove expired item immediately
            _metadataCache.TryRemove(key, out _);
            _logger?.LogDebug("Removed expired metadata {Key}", key);
        }

        Interlocked.Increment(ref _metadataMisses);
        _logger?.LogDebug("Metadata cache miss for {Key}", key);
        return ValueTask.FromResult<byte[]?>(null);
    }

    public async ValueTask SetTargetAsync(string targetPath, ReadOnlyMemory<byte> data, string expectedHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);

        if (data.Length > _options.MaxTargetSizeBytes)
        {
            _logger?.LogWarning("Target file too large for cache: {Size} > {MaxSize}", data.Length, _options.MaxTargetSizeBytes);
            return;
        }

        // Verify hash before caching
        var actualHash = await ComputeHashAsync(data, cancellationToken);
        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogError("Hash mismatch for target {Path}: expected {Expected}, got {Actual}", targetPath, expectedHash, actualHash);
            return;
        }

        var cachedItem = new CachedTarget(data.ToArray(), expectedHash);
        _targetCache.AddOrUpdate(targetPath, cachedItem, (_, _) => cachedItem);

        _logger?.LogDebug("Cached target {Path} ({Size} bytes, hash: {Hash})", targetPath, data.Length, expectedHash);
        
        await CheckCacheSizeAndCleanupAsync(cancellationToken);
    }

    public async ValueTask<byte[]?> GetTargetAsync(string targetPath, string expectedHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);

        if (_targetCache.TryGetValue(targetPath, out var cached))
        {
            if (string.Equals(cached.Hash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                // Double-check hash for security
                var actualHash = await ComputeHashAsync(cached.Data, cancellationToken);
                if (string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref _targetHits);
                    _logger?.LogDebug("Target cache hit for {Path}", targetPath);
                    return cached.Data;
                }
                
                // Hash mismatch - remove corrupted entry
                _targetCache.TryRemove(targetPath, out _);
                _logger?.LogWarning("Removed corrupted target {Path} from cache", targetPath);
            }
        }

        Interlocked.Increment(ref _targetMisses);
        _logger?.LogDebug("Target cache miss for {Path}", targetPath);
        return null;
    }

    public bool ContainsValidMetadata(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        
        if (_metadataCache.TryGetValue(key, out var cached))
        {
            return cached.ExpiresAt > DateTime.UtcNow;
        }
        
        return false;
    }

    public bool ContainsValidTarget(string targetPath, string expectedHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);
        
        if (_targetCache.TryGetValue(targetPath, out var cached))
        {
            return string.Equals(cached.Hash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        
        return false;
    }

    public ValueTask CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var removedCount = 0;

        // Clean expired metadata
        var expiredMetadataKeys = _metadataCache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredMetadataKeys)
        {
            if (_metadataCache.TryRemove(key, out _))
            {
                removedCount++;
            }
        }

        _lastCleanupTime = now;
        
        if (removedCount > 0)
        {
            _logger?.LogDebug("Cleanup removed {Count} expired cache entries", removedCount);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        var metadataCount = _metadataCache.Count;
        var targetCount = _targetCache.Count;
        
        _metadataCache.Clear();
        _targetCache.Clear();
        
        // Reset statistics
        _metadataHits = 0;
        _metadataMisses = 0;
        _targetHits = 0;
        _targetMisses = 0;
        
        _logger?.LogInformation("Cleared cache: {MetadataCount} metadata, {TargetCount} targets", metadataCount, targetCount);
        
        return ValueTask.CompletedTask;
    }

    public TufCacheStatistics GetStatistics()
    {
        var totalEntries = _metadataCache.Count + _targetCache.Count;
        var totalSize = _metadataCache.Values.Sum(v => v.Data.Length) + _targetCache.Values.Sum(v => v.Data.Length);

        return new TufCacheStatistics(
            MetadataHits: _metadataHits,
            MetadataMisses: _metadataMisses,
            TargetHits: _targetHits,
            TargetMisses: _targetMisses,
            TotalEntries: totalEntries,
            TotalSizeBytes: totalSize,
            LastCleanupTime: _lastCleanupTime
        );
    }

    private async ValueTask CheckCacheSizeAndCleanupAsync(CancellationToken cancellationToken)
    {
        var stats = GetStatistics();
        if (stats.TotalSizeBytes > _options.MaxSizeBytes)
        {
            _logger?.LogDebug("Cache size {CurrentSize} exceeds limit {MaxSize}, triggering cleanup", 
                stats.TotalSizeBytes, _options.MaxSizeBytes);
            await CleanupExpiredAsync(cancellationToken);
            
            // If still over limit after cleanup, remove oldest entries
            await EvictOldestEntriesIfNeededAsync(cancellationToken);
        }
    }

    private ValueTask EvictOldestEntriesIfNeededAsync(CancellationToken cancellationToken)
    {
        var stats = GetStatistics();
        if (stats.TotalSizeBytes <= _options.MaxSizeBytes)
        {
            return ValueTask.CompletedTask;
        }

        // Remove oldest metadata entries until under size limit
        var sortedMetadata = _metadataCache
            .OrderBy(kvp => kvp.Value.ExpiresAt)
            .Take(_metadataCache.Count / 2) // Remove half
            .ToList();

        foreach (var (key, _) in sortedMetadata)
        {
            _metadataCache.TryRemove(key, out _);
        }

        _logger?.LogInformation("Evicted {Count} oldest cache entries to stay under size limit", sortedMetadata.Count);
        
        return ValueTask.CompletedTask;
    }

    private static async ValueTask<string> ComputeHashAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await Task.Run(() => sha256.ComputeHash(data.ToArray()), cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private async void CleanupCallback(object? state)
    {
        try
        {
            await CleanupExpiredAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during cache cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _metadataCache.Clear();
        _targetCache.Clear();
    }

    private record CachedMetadata(byte[] Data, DateTime ExpiresAt);
    private record CachedTarget(byte[] Data, string Hash);
}

/// <summary>
/// Configuration options for InMemoryTufCache
/// </summary>
public class InMemoryTufCacheOptions
{
    /// <summary>
    /// Maximum total cache size in bytes (default: 100MB)
    /// </summary>
    public long MaxSizeBytes { get; init; } = 100 * 1024 * 1024;

    /// <summary>
    /// Maximum size for individual metadata entries (default: 5MB)
    /// </summary>
    public long MaxMetadataSizeBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>
    /// Maximum size for individual target files (default: 50MB)
    /// </summary>
    public long MaxTargetSizeBytes { get; init; } = 50 * 1024 * 1024;

    /// <summary>
    /// How often to run cleanup of expired entries (default: 10 minutes)
    /// </summary>
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromMinutes(10);
}