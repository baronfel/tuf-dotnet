namespace TUF.Caching;

/// <summary>
/// Null object implementation of ITufCache that disables caching entirely.
/// Useful for scenarios where caching is not desired or during testing.
/// </summary>
public sealed class NullTufCache : ITufCache
{
    /// <summary>
    /// Singleton instance of the null cache
    /// </summary>
    public static readonly NullTufCache Instance = new();

    private NullTufCache() { }

    /// <inheritdoc />
    public ValueTask SetMetadataAsync(string key, ReadOnlyMemory<byte> data, DateTime expiresAt, CancellationToken cancellationToken = default) 
        => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask<byte[]?> GetMetadataAsync(string key, CancellationToken cancellationToken = default) 
        => ValueTask.FromResult<byte[]?>(null);

    /// <inheritdoc />
    public ValueTask SetTargetAsync(string targetPath, ReadOnlyMemory<byte> data, string expectedHash, CancellationToken cancellationToken = default) 
        => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask<byte[]?> GetTargetAsync(string targetPath, string expectedHash, CancellationToken cancellationToken = default) 
        => ValueTask.FromResult<byte[]?>(null);

    /// <inheritdoc />
    public bool ContainsValidMetadata(string key, CancellationToken cancellationToken = default) 
        => false;

    /// <inheritdoc />
    public bool ContainsValidTarget(string targetPath, string expectedHash, CancellationToken cancellationToken = default) 
        => false;

    /// <inheritdoc />
    public ValueTask CleanupExpiredAsync(CancellationToken cancellationToken = default) 
        => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default) 
        => ValueTask.CompletedTask;

    /// <inheritdoc />
    public TufCacheStatistics GetStatistics() 
        => new(0, 0, 0, 0, 0, 0, DateTime.UtcNow);
}