# Repository Builder API

The `RepositoryBuilder` class provides a high-level, fluent API for creating and managing TUF repositories. It handles the complex details of metadata generation, signing, and repository structure creation while providing an intuitive interface for repository setup.

## Quick Start

```csharp
using TUF.Repository;
using TUF.Signing;

// Create keys for repository roles
var rootSigner = Ed25519Signer.Generate();
var timestampSigner = Ed25519Signer.Generate();
var snapshotSigner = Ed25519Signer.Generate();
var targetsSigner = Ed25519Signer.Generate();

// Build a repository
var repository = new RepositoryBuilder()
    .AddSigner("root", rootSigner)
    .AddSigner("timestamp", timestampSigner)
    .AddSigner("snapshot", snapshotSigner)
    .AddSigner("targets", targetsSigner)
    .SetDefaultExpiry(DateTimeOffset.UtcNow.AddMonths(6))
    .AddTargetFromFile("myapp.exe", "./dist/myapp.exe")
    .AddTarget("config.json", configData)
    .Build();

// Write to filesystem
repository.WriteToDirectory("./my-tuf-repo");
```

## Core Classes

### RepositoryBuilder

The main builder class for creating TUF repositories.

#### Methods

##### `AddSigner(string role, ISigner signer, string? signerId = null)`

Adds a signer for a specific TUF role.

**Parameters:**
- `role` - The TUF role ("root", "timestamp", "snapshot", "targets")
- `signer` - An implementation of `ISigner` (e.g., `Ed25519Signer`, `RsaSigner`)
- `signerId` - Optional unique identifier for the signer (auto-generated if not provided)

**Returns:** The builder instance for method chaining.

**Example:**
```csharp
var builder = new RepositoryBuilder()
    .AddSigner("root", Ed25519Signer.Generate())
    .AddSigner("targets", RsaSigner.Generate(2048));
```

##### `SetDefaultExpiry(DateTimeOffset expiry)`

Sets the default expiration time for all metadata files.

**Parameters:**
- `expiry` - The expiration date/time for metadata

**Returns:** The builder instance for method chaining.

**Example:**
```csharp
var builder = new RepositoryBuilder()
    .SetDefaultExpiry(DateTimeOffset.UtcNow.AddYears(1));
```

##### `SetConsistentSnapshots(bool enabled)`

Configures whether the repository should use consistent snapshots.

**Parameters:**
- `enabled` - `true` to enable consistent snapshots, `false` to disable

**Returns:** The builder instance for method chaining.

**Note:** Consistent snapshots provide stronger security guarantees but require more careful repository management.

##### `AddTarget(string path, byte[] content, Dictionary<string, object>? custom = null)`

Adds a target file to the repository using binary content.

**Parameters:**
- `path` - The logical path for the target file within the repository
- `content` - The binary content of the file
- `custom` - Optional custom metadata fields

**Returns:** The builder instance for method chaining.

**Example:**
```csharp
var config = Encoding.UTF8.GetBytes("""{"version": "1.0"}""");
var builder = new RepositoryBuilder()
    .AddTarget("config.json", config, new() { ["type"] = "configuration" });
```

##### `AddTargetFromFile(string targetPath, string filePath, Dictionary<string, object>? custom = null)`

Adds a target file by reading from the local filesystem.

**Parameters:**
- `targetPath` - The logical path for the target file within the repository
- `filePath` - The local filesystem path to read the file from
- `custom` - Optional custom metadata fields

**Returns:** The builder instance for method chaining.

**Example:**
```csharp
var builder = new RepositoryBuilder()
    .AddTargetFromFile("app.exe", "./build/release/app.exe")
    .AddTargetFromFile("docs/readme.txt", "./README.txt");
```

##### `Build()`

Builds the complete TUF repository with all metadata and target files.

**Returns:** A `TufRepository` instance containing all repository data.

**Throws:**
- `InvalidOperationException` - If required signers are missing or configuration is invalid

### TufRepository

Represents a complete TUF repository with all metadata and target files.

#### Properties

- `Root` - The signed root metadata
- `Timestamp` - The signed timestamp metadata  
- `Snapshot` - The signed snapshot metadata
- `Targets` - The signed targets metadata
- `TargetFiles` - Dictionary of target files with their content

#### Methods

##### `WriteToDirectory(string basePath)`

Writes the complete repository to a filesystem directory structure.

**Parameters:**
- `basePath` - The base directory where the repository should be written

**Directory Structure Created:**
```
basePath/
├── metadata/
│   ├── root.json
│   ├── timestamp.json
│   ├── snapshot.json
│   └── targets.json
└── targets/
    ├── app.exe
    └── config.json
```

## Complete Example

```csharp
using TUF.Repository;
using TUF.Signing;

public async Task CreateProductionRepository()
{
    // Generate production keys (store securely!)
    var rootKey = Ed25519Signer.Generate();
    var timestampKey = Ed25519Signer.Generate();
    var snapshotKey = Ed25519Signer.Generate(); 
    var targetsKey = RsaSigner.Generate(4096);

    // Create repository with production settings
    var repository = new RepositoryBuilder()
        // Configure signing keys
        .AddSigner("root", rootKey)
        .AddSigner("timestamp", timestampKey)
        .AddSigner("snapshot", snapshotKey)  
        .AddSigner("targets", targetsKey)
        
        // Set reasonable expiry times
        .SetDefaultExpiry(DateTimeOffset.UtcNow.AddMonths(3))
        
        // Enable consistent snapshots for production
        .SetConsistentSnapshots(true)
        
        // Add application files
        .AddTargetFromFile("myapp.exe", "./release/myapp.exe")
        .AddTargetFromFile("myapp.dll", "./release/myapp.dll")
        
        // Add configuration with custom metadata
        .AddTarget("config.json", configBytes, new() 
        { 
            ["config_version"] = "2.1",
            ["environment"] = "production"
        })
        
        .Build();

    // Write to production repository location
    repository.WriteToDirectory("/var/www/tuf-repo");
    
    Console.WriteLine("Repository created successfully!");
}
```

## Best Practices

### Key Management

1. **Use strong keys**: Ed25519 for metadata signing, RSA-4096 for targets
2. **Separate keys by role**: Never reuse keys across different TUF roles
3. **Secure storage**: Store private keys securely, especially root keys
4. **Regular rotation**: Plan for key rotation, especially timestamp keys

### Repository Configuration

1. **Reasonable expiry times**:
   - Root: 1 year (infrequent updates)
   - Targets: 3-6 months (moderate updates)
   - Snapshot: 1 month (frequent updates)
   - Timestamp: 1 day (very frequent updates)

2. **Consistent snapshots**: Enable for production repositories
3. **Custom metadata**: Use sparingly, only for essential application-specific data

### Error Handling

The builder validates configuration during `Build()`. Common errors:

- **Missing signers**: All four roles (root, timestamp, snapshot, targets) require signers
- **Invalid paths**: Target paths should use forward slashes
- **File not found**: `AddTargetFromFile()` will throw if the file doesn't exist

```csharp
try 
{
    var repository = builder.Build();
}
catch (InvalidOperationException ex)
{
    // Handle configuration errors
    Console.WriteLine($"Repository build failed: {ex.Message}");
}
```

## Related APIs

- **[Updater API](updater.md)** - Client-side TUF operations
- **[Multi-Repository Client](multi-repository-client.md)** - Multi-repository consensus
- **[Signing Guide](../guides/creating-repositories.md)** - Detailed repository creation guide