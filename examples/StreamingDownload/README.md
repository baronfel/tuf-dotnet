# TUF Streaming Download Example

This example demonstrates memory-efficient streaming downloads of large files using the TUF .NET implementation, addressing a key parity gap with other mature TUF implementations.

## Features

- **Memory-efficient downloads**: Stream large files without loading entire content into memory
- **Progress reporting**: Real-time download progress with throughput metrics
- **Hash verification**: Continuous verification during streaming for security
- **Cancellation support**: Graceful cancellation of long-running downloads
- **Cross-platform**: Works on Windows, macOS, and Linux

## Parity Achievement

This example addresses the **"Streaming support for large file downloads"** gap identified in the TUF .NET parity analysis. Other mature TUF implementations (Python, Go, Rust) provide streaming capabilities that allow:

1. **Large file handling** without memory exhaustion
2. **Progress reporting** during downloads  
3. **Cancellation** of long-running operations
4. **Efficient resource usage** in server environments

## Usage

### Basic Download
```bash
dotnet run -- \
  --metadata-url "https://example.com/metadata/" \
  --trusted-root "./trusted-root.json" \
  --target-path "large-file.bin"
```

### Download with Progress
```bash
dotnet run -- \
  --metadata-url "https://example.com/metadata/" \
  --trusted-root "./trusted-root.json" \
  --target-path "large-file.bin" \
  --output "./downloads/large-file.bin" \
  --progress
```

## API Usage

The streaming API can be used directly in applications:

```csharp
// Set up TUF updater
var config = new UpdaterConfig(trustedRootBytes, metadataUrl)
{
    LocalMetadataDir = "./metadata",
    LocalTargetsDir = "./targets", 
    Client = new HttpClient()
};
var updater = new Updater(config);

// Refresh metadata
await updater.RefreshAsync();

// Find target file
var targetInfo = await updater.GetTargetInfo("large-file.bin");
if (targetInfo != null)
{
    var (remotePath, targetFile) = targetInfo.Value;
    
    // Progress reporting (optional)
    var progress = new Progress<(long downloaded, long total)>(report =>
    {
        Console.WriteLine($"Downloaded: {report.downloaded}/{report.total} bytes");
    });
    
    // Stream download to file
    using var outputStream = File.Create("./large-file.bin");
    await updater.DownloadTargetToStream(
        targetFile, 
        "large-file.bin", 
        outputStream,
        progress: progress,
        cancellationToken: cancellationToken);
}
```

## Performance Benefits

### Memory Usage
- **Before**: Loading 1GB file requires ~1GB RAM
- **After**: Streaming 1GB file requires ~64KB RAM (buffer size)

### Scalability  
- **Server applications**: Can handle multiple concurrent large downloads
- **Resource-constrained environments**: IoT devices, containers with memory limits
- **Long-running processes**: Reduced GC pressure and memory fragmentation

## Implementation Details

### Core Components

1. **`StreamingUtils.CopyStreamWithHashVerification`**: Core streaming with hash verification
2. **`Updater.DownloadTargetToStream`**: TUF-aware streaming download method
3. **`StreamingUtils.DownloadToStreamAsync`**: HTTP streaming download utility

### Security Features

- **Continuous hash verification**: Hashes computed during streaming, not after
- **Length validation**: File size verified during download
- **Multiple hash algorithms**: Supports SHA-256, SHA-512, SHA-1, MD5
- **Integrity exceptions**: Clear error messages for verification failures

### Performance Optimizations

- **Configurable buffer size**: Default 64KB, tunable for different scenarios
- **Async/await patterns**: Proper async throughout for scalability  
- **Memory<T> support**: Modern .NET memory management patterns
- **AOT compatibility**: Works with Native AOT compilation

## Error Handling

The streaming implementation provides comprehensive error handling:

```csharp
try
{
    await updater.DownloadTargetToStream(targetFile, path, stream);
}
catch (FileSizeException ex)
{
    // File size doesn't match expected length
}
catch (TargetIntegrityException ex) 
{
    // Hash verification failed
}
catch (OperationCanceledException ex)
{
    // Download was cancelled
}
```

## Testing

The implementation includes comprehensive tests:

- **Small file streaming**: Verifies basic functionality
- **Large file handling**: Tests with 1MB+ files  
- **Error conditions**: Hash mismatches, size errors, cancellation
- **Progress reporting**: Validates progress callback functionality
- **Multiple hash algorithms**: Tests SHA-256, SHA-512 verification

Run tests:
```bash
dotnet test TUF.Tests -t StreamingTests
```

## Comparison with Other Implementations

| Feature | TUF .NET (Before) | TUF .NET (After) | Python TUF | Go TUF | Rust TUF |
|---------|------------------|------------------|------------|--------|-----------|
| Streaming Downloads | ❌ | ✅ | ✅ | ✅ | ✅ |
| Progress Reporting | ❌ | ✅ | ✅ | ✅ | ✅ |
| Cancellation Support | ❌ | ✅ | ✅ | ✅ | ✅ |
| Memory Efficiency | ❌ (Full load) | ✅ (64KB buffer) | ✅ | ✅ | ✅ |
| Hash During Stream | ❌ | ✅ | ✅ | ✅ | ✅ |

This implementation brings TUF .NET to full parity with other mature implementations for large file handling capabilities.