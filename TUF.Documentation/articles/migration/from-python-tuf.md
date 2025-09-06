# Migrating from Python TUF to TUF .NET

This guide helps developers migrate from [python-tuf](https://github.com/theupdateframework/python-tuf) to TUF .NET, highlighting key differences and providing equivalent code patterns.

## Architecture Differences

### Python TUF Structure
```python
from tuf.ngclient import Updater, UpdaterConfig
from tuf.api.metadata import Metadata, Root, Timestamp, Snapshot, Targets
```

### TUF .NET Equivalent
```csharp
using TUF;
using TUF.Models;
// Core classes: Updater, UpdaterConfig, Metadata<T>, Root, Timestamp, Snapshot, Targets
```

## Key Conceptual Mappings

| Python TUF | TUF .NET | Notes |
|------------|----------|-------|
| `tuf.ngclient.Updater` | `TUF.Updater` | Same core functionality |
| `tuf.ngclient.UpdaterConfig` | `TUF.UpdaterConfig` | Similar configuration options |
| `tuf.api.metadata.Root` | `TUF.Models.Root` | Equivalent metadata models |
| `tuf.repository_lib` | `TUF.Repository.RepositoryBuilder` | Repository creation APIs |

## Client Migration Examples

### Initialization

**Python TUF:**
```python
from tuf.ngclient import Updater, UpdaterConfig

# Load trusted root
with open("root.json", "rb") as f:
    trusted_root = f.read()

# Configure updater
config = UpdaterConfig(
    map_file=None,
    local_dir="./cache",
    metadata_base_url="https://repo.example.com/metadata/",
    targets_base_url="https://repo.example.com/targets/",
    target_dir="./downloads"
)

updater = Updater(
    metadata_dir=config.metadata_dir,
    metadata_base_url=config.metadata_base_url,
    target_dir=config.target_dir,
    target_base_url=config.targets_base_url
)
```

**TUF .NET:**
```csharp
using TUF;

// Load trusted root
var trustedRoot = await File.ReadAllBytesAsync("root.json");

// Configure updater
var config = new UpdaterConfig(trustedRoot, new Uri("https://repo.example.com/metadata/"))
{
    LocalMetadataDir = "./cache/metadata",
    LocalTargetsDir = "./cache/targets", 
    RemoteTargetsUrl = new Uri("https://repo.example.com/targets/"),
    Client = new HttpClient()
};

var updater = new Updater(config);
```

### Refreshing Metadata

**Python TUF:**
```python
# Refresh metadata
updater.refresh()
```

**TUF .NET:**
```csharp
// Refresh metadata (async)
await updater.RefreshAsync();

// With cancellation support
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
await updater.RefreshAsync(cts.Token);
```

### Getting Target Information

**Python TUF:**
```python
# Get target info
target_info = updater.get_targetinfo("app.exe")
if target_info is not None:
    print(f"Target length: {target_info.length}")
    print(f"Target hashes: {target_info.hashes}")
```

**TUF .NET:**
```csharp
// Get target info
var targetInfo = await updater.GetTargetInfo("app.exe");
if (targetInfo.HasValue)
{
    var (remotePath, targetFile) = targetInfo.Value;
    Console.WriteLine($"Target length: {targetFile.Length}");
    Console.WriteLine($"Target hashes: {string.Join(", ", targetFile.Hashes.Keys)}");
}
```

### Downloading Targets

**Python TUF:**
```python
# Download target
path, target_file = updater.download_target(target_info, "./downloads/", "app.exe")
print(f"Downloaded to: {path}")
```

**TUF .NET:**
```csharp
// Download target
var (localPath, data) = await updater.DownloadTarget(
    targetFile, 
    "app.exe", 
    "./downloads/app.exe"
);
Console.WriteLine($"Downloaded to: {localPath}");
```

## Repository Management Migration

### Python Repository Tools

**Python TUF:**
```python
from tuf.repository_lib import create_new_repository, import_ed25519_privatekey_from_file

# Create repository
repository = create_new_repository("./repo")

# Load private key
private_key = import_ed25519_privatekey_from_file("key.json", password="password")
repository.root.add_signing_key(private_key)

# Add target
repository.targets.add_target("./files/app.exe")

# Generate metadata
repository.write()
```

**TUF .NET:**
```csharp
using TUF.Repository;
using TUF.Models;

// Create repository builder
var builder = new RepositoryBuilder();

// Load and add signer
var signer = Ed25519Signer.LoadFromPem(File.ReadAllText("key.pem"));
builder.AddSigner("root", signer);
builder.AddSigner("targets", signer);

// Add target file
builder.AddTarget("app.exe", File.ReadAllBytes("./files/app.exe"));

// Build repository
var repository = await builder.BuildAsync("./repo");
```

## Error Handling Differences

### Python TUF Exceptions
```python
from tuf.ngclient import TufError, RepositoryError

try:
    updater.refresh()
except TufError as e:
    print(f"TUF error: {e}")
except RepositoryError as e:
    print(f"Repository error: {e}")
```

### TUF .NET Exceptions
```csharp
using TUF;

try
{
    await updater.RefreshAsync();
}
catch (TufException ex)
{
    Console.WriteLine($"TUF error: {ex.Message}");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"Network error: {ex.Message}");
}
```

## Configuration Migration

### Python Configuration Options
```python
config = UpdaterConfig(
    map_file="map.json",  # Multi-repo map
    local_dir="./cache",
    metadata_base_url="https://repo.example.com/metadata/",
    targets_base_url="https://repo.example.com/targets/",
    target_dir="./targets",
    prefix_targets_with_hash=True
)
```

### TUF .NET Configuration
```csharp
var config = new UpdaterConfig(trustedRoot, metadataUrl)
{
    // Basic settings
    LocalMetadataDir = "./cache/metadata",
    LocalTargetsDir = "./cache/targets",
    RemoteTargetsUrl = targetsUrl,
    PrefixTargetsWithHash = true,
    
    // Security limits (customizable)
    MaxRootRotations = 32,
    MaxDelegations = 256,
    
    // HTTP configuration
    Client = httpClient,
    
    // Logging integration
    Logger = logger
};
```

## Advanced Features

### Multi-Repository Support (TAP 4)

**Python TUF:**
```python
# Multi-repository requires map.json file
config = UpdaterConfig(
    map_file="map.json",
    # ... other config
)
```

**TUF .NET:**
```csharp
using TUF;

// Multi-repository client with map.json
var multiConfig = new MultiRepositoryConfig("map.json");
var multiClient = new MultiRepositoryClient(multiConfig);
await multiClient.InitializeAsync();

var targetInfo = await multiClient.GetTargetInfo("app.exe");
```

### Custom Metadata

**Python TUF:**
```python
# Access custom metadata fields
custom_data = target_info.custom
```

**TUF .NET:**
```csharp
// Access custom metadata (strongly typed with Serde.NET)
var customData = targetFile.Custom;
// Or access raw dictionary
var rawCustom = targetFile.AdditionalProperties;
```

## Key Differences Summary

### Async/Await Pattern
- **Python TUF**: Primarily synchronous APIs
- **TUF .NET**: Async/await throughout for non-blocking operations

### Type Safety
- **Python TUF**: Dynamic typing, runtime errors possible
- **TUF .NET**: Strong typing, compile-time error detection

### Error Handling  
- **Python TUF**: Exception-based with try/except
- **TUF .NET**: Exception-based with try/catch, more specific exception types

### HTTP Client
- **Python TUF**: Built-in HTTP handling
- **TUF .NET**: Uses injected HttpClient for full control

### Logging
- **Python TUF**: Python logging module
- **TUF .NET**: Microsoft.Extensions.Logging integration

## Performance Considerations

### Memory Usage
- **Python TUF**: Interpreted language, higher memory overhead
- **TUF .NET**: Compiled, optimized memory usage, AOT support

### Startup Time
- **Python TUF**: Module import overhead
- **TUF .NET**: Fast startup, especially with AOT compilation

### Serialization
- **Python TUF**: JSON with standard library
- **TUF .NET**: Optimized canonical JSON with Serde.NET

## Deployment Differences

### Python TUF Deployment
```bash
pip install tuf
# Or with specific version
pip install tuf==2.0.0
```

### TUF .NET Deployment
```xml
<PackageReference Include="TUF" Version="1.0.0" />
<PackageReference Include="CanonicalJson" Version="1.0.0" />
```

## Testing Migration

### Python TUF Tests
```python
import unittest
from tuf.ngclient import Updater

class TestTufClient(unittest.TestCase):
    def test_refresh(self):
        updater = create_test_updater()
        updater.refresh()
        self.assertTrue(updater.trusted_set.root is not None)
```

### TUF .NET Tests  
```csharp
using TUnit;
using TUF;

public class TufClientTests
{
    [Test]
    public async Task RefreshShouldUpdateMetadata()
    {
        var updater = CreateTestUpdater();
        await updater.RefreshAsync();
        var trustedMetadata = updater.GetTrustedMetadataSet();
        Assert.IsNotNull(trustedMetadata.Root);
    }
}
```

## Common Migration Gotchas

### 1. Async/Await Required
```csharp
// ❌ Wrong - will not compile
updater.RefreshAsync();  

// ✅ Correct
await updater.RefreshAsync();
```

### 2. Nullable Reference Types
```csharp
// TUF .NET uses nullable reference types
var targetInfo = await updater.GetTargetInfo("app.exe");
if (targetInfo.HasValue)  // Check for null
{
    var (remotePath, targetFile) = targetInfo.Value;
    // Use targetFile safely
}
```

### 3. HttpClient Lifecycle
```csharp
// ❌ Don't create new HttpClient for each request  
var config = new UpdaterConfig(root, url)
{
    Client = new HttpClient() // Will be disposed
};

// ✅ Use dependency injection or singleton HttpClient
var config = new UpdaterConfig(root, url)
{
    Client = httpClientFactory.CreateClient("tuf")
};
```

### 4. Configuration Differences
- Python TUF uses `local_dir` for both metadata and targets
- TUF .NET separates `LocalMetadataDir` and `LocalTargetsDir`

## Migration Checklist

- [ ] Update initialization code to use `UpdaterConfig` constructor
- [ ] Convert synchronous calls to async/await pattern
- [ ] Update error handling to use TUF .NET exception types
- [ ] Configure HttpClient appropriately
- [ ] Update repository management code to use `RepositoryBuilder`
- [ ] Test multi-repository functionality if used
- [ ] Update deployment configuration (NuGet instead of pip)
- [ ] Convert unit tests to use async test patterns
- [ ] Update logging to use Microsoft.Extensions.Logging

## Getting Help

If you encounter issues during migration:

1. Check the [TUF .NET Examples](https://github.com/baronfel/tuf-dotnet/tree/main/examples) for working code
2. Compare with [Python TUF documentation](https://theupdateframework.readthedocs.io/) for concept clarification
3. Open an issue on [GitHub](https://github.com/baronfel/tuf-dotnet/issues) with specific migration questions

The TUF .NET implementation is designed to provide the same security guarantees as python-tuf while offering better performance and integration with the .NET ecosystem.