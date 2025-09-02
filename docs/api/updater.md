# Updater API

The `Updater` class is the primary client for TUF repository operations in .NET. It implements the complete TUF client workflow including metadata refresh, target discovery, and secure download verification.

## Class Definition

```csharp
namespace TUF;

public class Updater
{
    public Updater(UpdaterConfig config)
    public async Task RefreshAsync()
    public Dictionary<string, TargetFile> GetTopLevelTargets()
    public TrustedMetadata GetTrustedMetadataSet()
    public async Task<(string RemotePath, TargetFile File)?> GetTargetInfo(string targetPath)
    public async Task<(string FilePath, byte[] Data)> DownloadTarget(TargetFile targetFile, string targetPath, string? destinationFilePath = null, Uri? baseUrl = null)
}
```

## Constructor

### `Updater(UpdaterConfig config)`

Creates a new TUF updater client with the specified configuration.

**Parameters:**
- `config` - Configuration object containing repository URLs, local directories, and security parameters

**Throws:**
- `ArgumentNullException` - If config is null or contains invalid required values
- `TufConfigurationException` - If configuration validation fails

**Example:**
```csharp
var config = new UpdaterConfig
{
    LocalTrustedRoot = trustedRootBytes,
    RemoteMetadataUrl = new Uri("https://example.com/metadata/"),
    RemoteTargetsUrl = new Uri("https://example.com/targets/"),
    LocalMetadataDir = "./metadata",
    LocalTargetsDir = "./downloads",
    Client = httpClient
};

var updater = new Updater(config);
```

## Core Methods

### `RefreshAsync()`

Performs a complete TUF metadata refresh workflow, updating all metadata from the remote repository according to TUF specification security rules.

**Returns:** `Task` - Completes when metadata refresh is successful

**Throws:**
- `TufNetworkException` - Network or HTTP transport errors
- `TufSignatureException` - Signature verification failures  
- `TufRollbackException` - Rollback attack detection
- `TufExpiredException` - Expired metadata detected
- `TufThresholdException` - Insufficient signatures for role threshold

**Security Guarantees:**
- Validates signature thresholds for all metadata roles
- Enforces metadata expiration times
- Prevents rollback attacks via version checking
- Verifies complete signature chain from root to timestamp

**Example:**
```csharp
try
{
    await updater.RefreshAsync();
    Console.WriteLine("Metadata successfully refreshed");
}
catch (TufRollbackException ex)
{
    logger.LogWarning("Rollback attack detected: {Message}", ex.Message);
}
catch (TufSignatureException ex)
{
    logger.LogError("Signature verification failed: {Message}", ex.Message);
}
```

### `GetTopLevelTargets()`

Returns all top-level targets (non-delegated) from the current trusted targets metadata.

**Returns:** `Dictionary<string, TargetFile>` - Map of target paths to target file information

**Throws:**
- `InvalidOperationException` - If trusted metadata is not fully loaded (call `RefreshAsync()` first)

**Example:**
```csharp
await updater.RefreshAsync();
var targets = updater.GetTopLevelTargets();

foreach (var (path, targetFile) in targets)
{
    Console.WriteLine($"Target: {path}, Size: {targetFile.Length} bytes");
}
```

### `GetTargetInfo(string targetPath)`

Finds target information through the complete delegation tree, following TUF delegation rules.

**Parameters:**
- `targetPath` - The target file path to search for

**Returns:** `Task<(string RemotePath, TargetFile File)?>` - Target information if found, null if not found

**Behavior:**
- Automatically calls `RefreshAsync()` if metadata is not loaded
- Performs depth-first delegation tree traversal
- Respects delegation termination rules
- Handles path pattern matching

**Example:**
```csharp
var targetInfo = await updater.GetTargetInfo("app/installer.exe");
if (targetInfo.HasValue)
{
    var (remotePath, targetFile) = targetInfo.Value;
    Console.WriteLine($"Found target: {remotePath}, Hash: {targetFile.Hashes.First()}");
}
else
{
    Console.WriteLine("Target not found in repository");
}
```

### `DownloadTarget(TargetFile targetFile, string targetPath, string? destinationFilePath = null, Uri? baseUrl = null)`

Downloads and verifies a target file with full TUF security validation.

**Parameters:**
- `targetFile` - Target file metadata (from `GetTargetInfo()`)
- `targetPath` - Target path in repository
- `destinationFilePath` - Optional local destination path (defaults to LocalTargetsDir + targetPath)
- `baseUrl` - Optional base URL override (defaults to RemoteTargetsUrl)

**Returns:** `Task<(string FilePath, byte[] Data)>` - Local file path and downloaded data

**Security Features:**
- Verifies file length matches metadata
- Validates all hashes in target metadata
- Supports consistent snapshot URL transformation
- Prevents download of tampered files

**Throws:**
- `TufNetworkException` - Download network errors
- `TufIntegrityException` - Hash or length verification failures
- `TufNotFoundException` - Target file not found on server

**Example:**
```csharp
var targetInfo = await updater.GetTargetInfo("app.exe");
if (targetInfo.HasValue)
{
    var (remotePath, targetFile) = targetInfo.Value;
    var (localPath, data) = await updater.DownloadTarget(targetFile, "app.exe");
    Console.WriteLine($"Downloaded {data.Length} bytes to {localPath}");
}
```

## Utility Methods

### `GetTrustedMetadataSet()`

Returns the current trusted metadata set for inspection or advanced operations.

**Returns:** `TrustedMetadata` - Current trusted metadata state

**Use Cases:**
- Custom delegation tree traversal
- Metadata inspection and debugging
- Advanced security policy implementation
- Integration with external validation tools

**Example:**
```csharp
var trustedMetadata = updater.GetTrustedMetadataSet();
if (trustedMetadata is CompleteTrustedMetadata complete)
{
    Console.WriteLine($"Root version: {complete.Root.Signed.Version}");
    Console.WriteLine($"Targets version: {complete.TopLevelTargets.Signed.Version}");
}
```

## Configuration Deep Dive

The `Updater` behavior is controlled through `UpdaterConfig`:

```csharp
public class UpdaterConfig
{
    // Security limits
    public uint MaxRootRotations { get; init; } = 256;
    public uint MaxDelegations { get; init; } = 32;
    public uint RootMaxLength { get; init; } = 512000;
    public uint TimestampMaxLength { get; init; } = 16384;
    public int SnapshotMaxLength { get; init; } = 2000000;
    public int TargetsMaxLength { get; init; } = 5000000;

    // Required configuration
    public byte[] LocalTrustedRoot { get; init; }
    public required string LocalMetadataDir { get; init; }
    public required string LocalTargetsDir { get; init; }
    public required HttpClient Client { get; init; }
    
    // Repository URLs
    public Uri RemoteMetadataUrl { get; init; }
    public Uri RemoteTargetsUrl { get; init; }
    
    // Behavior options
    public bool PrefixTargetsWithHash { get; init; } = true;
    public bool DisableLocalCache { get; init; } = false;
}
```

### Security Limits
- `MaxRootRotations`: Prevents infinite root key rotation attacks
- `MaxDelegations`: Limits delegation depth to prevent DoS
- Metadata size limits prevent resource exhaustion attacks

### Required Configuration
- `LocalTrustedRoot`: Initial trusted root metadata bytes
- `LocalMetadataDir`: Directory for cached metadata files
- `LocalTargetsDir`: Directory for downloaded target files
- `Client`: HTTP client for network operations

### Behavior Options
- `PrefixTargetsWithHash`: Enable consistent snapshot file naming
- `DisableLocalCache`: Disable local file caching (for testing)

## Error Handling Best Practices

### Transient vs Permanent Errors

```csharp
public async Task<bool> TryRefreshWithRetry(int maxRetries = 3)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            await updater.RefreshAsync();
            return true;
        }
        catch (TufNetworkException ex) when (attempt < maxRetries - 1)
        {
            // Retry transient network errors
            logger.LogWarning("Network error on attempt {Attempt}: {Message}", attempt + 1, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponential backoff
        }
        catch (TufSignatureException ex)
        {
            // Don't retry signature failures - these indicate compromise
            logger.LogError("Signature verification failed - possible repository compromise: {Message}", ex.Message);
            return false;
        }
    }
    return false;
}
```

### Graceful Degradation

```csharp
public async Task<bool> IsUpdateAvailable(string targetPath)
{
    try
    {
        await updater.RefreshAsync();
        var targetInfo = await updater.GetTargetInfo(targetPath);
        return targetInfo.HasValue;
    }
    catch (TufException ex)
    {
        logger.LogWarning("Update check failed: {Message}", ex.Message);
        return false; // Assume no update available on TUF errors
    }
}
```

## Thread Safety

### Safe Operations
- Multiple concurrent calls to `GetTargetInfo()` are safe
- Reading `GetTopLevelTargets()` after `RefreshAsync()` is safe
- `GetTrustedMetadataSet()` is always safe

### Unsafe Operations  
- Concurrent calls to `RefreshAsync()` are not supported
- `DownloadTarget()` concurrent with `RefreshAsync()` is not supported

### Recommended Pattern
```csharp
// Use single-threaded access for state-changing operations
private readonly SemaphoreSlim _updateSemaphore = new(1, 1);

public async Task SafeRefreshAsync()
{
    await _updateSemaphore.WaitAsync();
    try
    {
        await updater.RefreshAsync();
    }
    finally
    {
        _updateSemaphore.Release();
    }
}
```

## Performance Optimization

### Metadata Caching
- Local metadata files are automatically cached
- Subsequent `RefreshAsync()` calls only download changed metadata
- Use `DisableLocalCache = false` for optimal performance

### HTTP Client Configuration
```csharp
var httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromSeconds(30);
httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MyApp", "1.0"));

var config = new UpdaterConfig { Client = httpClient, /* ... */ };
```

### Target Download Optimization
- Large files are streamed to minimize memory usage
- Hash verification occurs during streaming
- Enable consistent snapshot for better CDN caching