using System.Diagnostics.CodeAnalysis;

namespace TUF.Caching;

/// <summary>
/// Interface for TUF metadata and target caching with expiration support
/// </summary>
public interface ITufCache
{
    /// <summary>
    /// Store metadata in cache with expiration based on metadata expiry time
    /// </summary>
    /// <param name="key">Cache key (typically metadata type + version)</param>
    /// <param name="data">Metadata bytes to cache</param>
    /// <param name="expiresAt">When the cached item should expire</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask SetMetadataAsync(string key, ReadOnlyMemory<byte> data, DateTime expiresAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve metadata from cache if not expired
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached data if found and not expired, null otherwise</returns>
    ValueTask<byte[]?> GetMetadataAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store target file in cache with hash-based validation
    /// </summary>
    /// <param name="targetPath">Target file path</param>
    /// <param name="data">Target file data</param>
    /// <param name="expectedHash">Expected hash for integrity validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask SetTargetAsync(string targetPath, ReadOnlyMemory<byte> data, string expectedHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve target file from cache with integrity validation
    /// </summary>
    /// <param name="targetPath">Target file path</param>
    /// <param name="expectedHash">Expected hash for validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Target file data if found and hash matches, null otherwise</returns>
    ValueTask<byte[]?> GetTargetAsync(string targetPath, string expectedHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if metadata exists in cache and is not expired
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    bool ContainsValidMetadata(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if target exists in cache with valid hash
    /// </summary>
    /// <param name="targetPath">Target file path</param>
    /// <param name="expectedHash">Expected hash</param>
    /// <param name="cancellationToken">Cancellation token</param>
    bool ContainsValidTarget(string targetPath, string expectedHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove expired entries from cache
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask CleanupExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all cached data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache statistics for monitoring
    /// </summary>
    TufCacheStatistics GetStatistics();
}

/// <summary>
/// Cache statistics for monitoring and diagnostics
/// </summary>
public record TufCacheStatistics(
    int MetadataHits,
    int MetadataMisses,
    int TargetHits,
    int TargetMisses,
    int TotalEntries,
    long TotalSizeBytes,
    DateTime LastCleanupTime);