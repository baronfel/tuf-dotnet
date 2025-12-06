# Migrating from Other TUF Implementations

This guide helps developers migrate existing TUF integrations from other implementations (python-tuf, go-tuf, rust-tuf) to TUF .NET.

## Overview

TUF .NET is designed for maximum compatibility with the TUF specification and other implementations. The core concepts and metadata formats are identical, but the APIs and integration patterns differ between languages.

## Migration from Python TUF

### API Mapping

| Python TUF (ngclient) | TUF .NET | Notes |
|------------------------|----------|--------|
| `ngclient.Updater()` | `new Updater(config)` | Configuration differs |
| `updater.refresh()` | `await updater.RefreshAsync()` | Async by default in .NET |
| `updater.get_targetinfo()` | `await updater.GetTargetInfo()` | Returns nullable tuple |
| `updater.download_target()` | `await updater.DownloadTarget()` | Built-in progress reporting |

### Configuration Migration

**Python TUF:**
```python
from tuf.ngclient import Updater

updater = Updater(
    metadata_dir="/tmp/metadata",
    metadata_base_url="https://example.com/metadata/",
    target_dir="/tmp/targets", 
    target_base_url="https://example.com/targets/",
    fetcher=RequestsFetcher()
)

# Load trusted root from file
with open("root.json", "rb") as f:
    updater.refresh(f.read())
```

**TUF .NET:**
```csharp
using TUF;

// Load trusted root from file
byte[] trustedRoot = await File.ReadAllBytesAsync("root.json");

var config = new UpdaterConfig(trustedRoot, new Uri("https://example.com/metadata/"))
{
    LocalMetadataDir = "/tmp/metadata",
    LocalTargetsDir = "/tmp/targets",
    TargetBaseUrl = new Uri("https://example.com/targets/"),
    Client = new HttpClient() // Custom HTTP client if needed
};

var updater = new Updater(config);
await updater.RefreshAsync();
```

### Target Download Migration

**Python TUF:**
```python
# Get target info
target_info = updater.get_targetinfo("myfile.txt")

# Download target
path = updater.download_target(target_info)
```

**TUF .NET:**
```csharp
// Get target info (returns nullable tuple)
var targetResult = await updater.GetTargetInfo("myfile.txt");
if (targetResult is (var path, var targetInfo))
{
    // Download target with progress reporting
    var result = await updater.DownloadTarget(targetInfo, path, null, 
        new Progress<(long downloaded, long total)>(progress => 
            Console.WriteLine($"Downloaded {progress.downloaded}/{progress.total} bytes")));
    
    // Access downloaded data
    byte[] data = result.Data;
    string filePath = result.FilePath;
}
```

### Multi-Repository Migration

**Python TUF:**
```python
# Python TUF doesn't have built-in multi-repository support
# (TAP 4 not implemented in python-tuf as of 2024)
```

**TUF .NET:**
```csharp
using TUF;

// TUF .NET has full TAP 4 multi-repository support
var client = new MultiRepositoryClient();

// Add multiple repositories with map.json configuration
var mapConfig = await File.ReadAllTextAsync("map.json");
client.LoadConfiguration(mapConfig);

// Get consensus target info from multiple repositories
var targetInfo = await client.GetTargetInfoAsync("myfile.txt");
```

## Migration from Go TUF

### Client API Migration

**Go TUF:**
```go
import "github.com/theupdateframework/go-tuf/client"

// Create client
opts := &client.ClientOptions{
    MetadataCacheDir: "/tmp/metadata",
    TargetsCacheDir: "/tmp/targets", 
}
c := client.NewClient(repo, opts)

// Update metadata
_, err := c.Update()

// Get target
target, err := c.Target("myfile.txt")
```

**TUF .NET:**
```csharp
using TUF;

byte[] trustedRoot = await File.ReadAllBytesAsync("root.json");
var config = new UpdaterConfig(trustedRoot, new Uri("https://example.com/metadata/"))
{
    LocalMetadataDir = "/tmp/metadata",
    LocalTargetsDir = "/tmp/targets"
};

var updater = new Updater(config);
await updater.RefreshAsync();

var result = await updater.GetTargetInfo("myfile.txt");
```

### Repository Creation Migration

**Go TUF:**
```go
import "github.com/theupdateframework/go-tuf/repo"

// Create repository
r, err := repo.NewRepo(store)

// Add targets
target := &data.TargetFileMeta{
    FileMeta: data.FileMeta{
        Length: int64(len(content)),
        Hashes: hashes,
    },
}
r.AddTarget("myfile.txt", target)

// Sign metadata
err = r.Sign("targets.json")
```

**TUF .NET:**
```csharp
using TUF.Repository;

// Create repository builder
var builder = new RepositoryBuilder();

// Add target file
var targetFile = new TargetFile
{
    Length = content.Length,
    Hashes = new Dictionary<string, byte[]>
    {
        ["sha256"] = SHA256.HashData(content)
    }
};

builder.AddTarget("myfile.txt", targetFile);

// Sign with cryptographic key
await builder.SignAsync("targets", signer);

// Serialize metadata
string targetsMetadata = builder.GetMetadata("targets");
```

## Migration from Rust TUF

Since Rust TUF is still in beta/experimental stage, migration patterns are limited. The core concepts remain the same:

**Rust TUF (conceptual):**
```rust
use tuf::client::Client;

let client = Client::new(repo_url, metadata_dir, targets_dir)?;
client.update()?;
```

**TUF .NET equivalent:**
```csharp
using TUF;

var config = new UpdaterConfig(trustedRoot, new Uri("https://repo.example.com/metadata/"))
{
    LocalMetadataDir = "metadata",
    LocalTargetsDir = "targets"
};

var updater = new Updater(config);
await updater.RefreshAsync();
```

## Common Migration Challenges

### 1. Async/Await Patterns

.NET TUF is async-first, while some other implementations are synchronous:

```csharp
// Always use await with TUF .NET operations
await updater.RefreshAsync();
var info = await updater.GetTargetInfo("file.txt");
var result = await updater.DownloadTarget(info.targetInfo, info.path, null, progress);
```

### 2. Exception Handling

TUF .NET uses typed exceptions for different error scenarios:

```csharp
try
{
    await updater.RefreshAsync();
}
catch (TUFSignatureException ex)
{
    // Handle signature verification failures
    Console.WriteLine($"Signature error: {ex.Message}");
}
catch (TUFNetworkException ex) 
{
    // Handle network/download failures
    Console.WriteLine($"Network error: {ex.Message}");
}
catch (TUFMetadataException ex)
{
    // Handle metadata parsing/validation errors
    Console.WriteLine($"Metadata error: {ex.Message}");
}
```

### 3. Configuration Management

TUF .NET uses a structured configuration object:

```csharp
var config = new UpdaterConfig(trustedRoot, metadataBaseUrl)
{
    // Optional settings
    LocalMetadataDir = "/custom/metadata",
    LocalTargetsDir = "/custom/targets",
    TargetBaseUrl = new Uri("https://targets.example.com/"),
    Client = customHttpClient,
    
    // Caching behavior
    DisableLocalCache = false,
    MaxMetadataAge = TimeSpan.FromHours(24),
    
    // Progress reporting
    ProgressReporter = new Progress<DownloadProgress>(progress => {
        Console.WriteLine($"Progress: {progress.Percentage:F1}%");
    })
};
```

### 4. Trusted Root Management

All TUF implementations require a trusted root, but the loading mechanism differs:

```csharp
// From embedded resource
byte[] trustedRoot = await GetEmbeddedResourceAsync("trusted-root.json");

// From configuration file with validation
byte[] trustedRoot = await LoadAndValidateTrustedRoot("config/root.json");

// From secure configuration service
byte[] trustedRoot = await secureConfig.GetTrustedRootAsync();

var config = new UpdaterConfig(trustedRoot, metadataUrl);
```

## Metadata Compatibility

TUF .NET produces and consumes metadata that is 100% compatible with other TUF implementations:

- **Canonical JSON**: Uses the same canonical JSON serialization as python-tuf
- **Signature Formats**: Supports Ed25519, RSA-PSS, and ECDSA signatures
- **Metadata Structure**: Identical JSON structure to specification
- **TAP Support**: Implements TAP 4 (multi-repository) with full compatibility

## Validation and Testing

### Interoperability Testing

Validate your migration by testing with metadata from other implementations:

```csharp
// Test with metadata generated by python-tuf
byte[] pythonGeneratedRoot = await File.ReadAllBytesAsync("python-root.json");
var config = new UpdaterConfig(pythonGeneratedRoot, metadataUrl);
var updater = new Updater(config);

// Should work seamlessly
await updater.RefreshAsync();
```

### Conformance Testing

TUF .NET includes official conformance test integration:

```bash
# Run conformance tests to validate compatibility
cd examples/TufConformanceCli
dotnet run -- --help
```

## Performance Considerations

### .NET Advantages

- **AOT Compilation**: Faster startup times with native compilation
- **Memory Efficiency**: Optimized memory usage with Span<T> and Memory<T>
- **Async Performance**: True async I/O for better scalability

### Benchmarking Migration Impact

```csharp
// Built-in performance measurement
var stopwatch = Stopwatch.StartNew();
await updater.RefreshAsync();
stopwatch.Stop();

Console.WriteLine($"Refresh completed in {stopwatch.ElapsedMilliseconds}ms");
```

## Next Steps

1. **Start with Basic Client**: Use the `examples/BasicClient` to validate your metadata
2. **Test Interoperability**: Validate with your existing TUF repositories
3. **Run Conformance Tests**: Ensure specification compliance
4. **Performance Testing**: Benchmark against your current implementation
5. **Integration Testing**: Test in your production environment with staging data

## Getting Help

- **Documentation**: See [Building Clients](building-clients.md) for detailed API usage
- **Examples**: Check `examples/` directory for practical implementation patterns
- **Conformance**: Use [Conformance Testing](conformance-testing.md) for validation
- **Interoperability**: See [Interoperability Testing](interoperability.md) for cross-implementation validation

The TUF .NET implementation prioritizes compatibility, performance, and developer experience while maintaining full compliance with the TUF specification.