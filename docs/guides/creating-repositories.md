# Creating TUF Repositories

This guide walks you through creating and managing TUF repositories with TUF .NET. Whether you're setting up a simple development repository or a production-grade distribution system, this guide has you covered.

## Overview

TUF repositories contain two main components:
- **Metadata files** - Cryptographically signed files describing the repository structure, security roles, and available targets
- **Target files** - The actual files you want to distribute securely (applications, updates, etc.)

TUF .NET provides multiple approaches for creating repositories:
1. **Programmatic API** - Use the `RepositoryBuilder` class for maximum flexibility
2. **Example Tools** - Use the `RepositoryManager` example for quick setup
3. **CLI Integration** - Use CLI tools for automation and scripting

## Prerequisites

- **.NET 8.0 or later** 
- **Basic understanding of TUF concepts** - Read [What is TUF?](./what-is-tuf.md) and [Core Concepts](./core-concepts.md) first
- **Secure key management plan** - Understand how you'll generate, store, and protect signing keys

## Quick Start: Creating Your First Repository

The fastest way to create a TUF repository is using the `RepositoryManager` example:

```bash
# Clone and build the TUF .NET project
git clone https://github.com/baronfel/tuf-dotnet.git
cd tuf-dotnet
dotnet build

# Create a sample repository
cd examples/RepositoryManager
dotnet run ./my-tuf-repo

# Test the repository with a client
cd ../BasicClient
dotnet run file://$(pwd)/../RepositoryManager/my-tuf-repo/metadata hello.txt
```

This creates a complete TUF repository with:
- All four metadata roles (root, timestamp, snapshot, targets)
- Sample target files with proper cryptographic hashes
- Valid signatures for all metadata

## Understanding Repository Structure

A TUF repository has this standard structure:

```
my-tuf-repo/
├── metadata/          # Signed metadata files
│   ├── root.json      # Root of trust, contains all role keys
│   ├── timestamp.json # Freshness guarantee, updated frequently  
│   ├── snapshot.json  # Consistency guarantee, references all metadata
│   └── targets.json   # Available files with hashes and signatures
└── targets/           # Actual target files
    ├── hello.txt      # Example target file
    └── config/        # Target files can be organized in directories
        └── app.json
```

### Metadata Roles Explained

Each metadata file serves a specific security purpose:

- **Root** (`root.json`) - Contains trusted public keys for all roles and establishes the foundation of trust
- **Timestamp** (`timestamp.json`) - References the latest snapshot, prevents freeze attacks, updated frequently
- **Snapshot** (`snapshot.json`) - References specific versions of all targets metadata, prevents mix-and-match attacks
- **Targets** (`targets.json`) - Lists available target files with their cryptographic hashes and metadata

## Programmatic Repository Creation

For production scenarios or integration into existing systems, use the `RepositoryBuilder` API directly.

### Basic Repository Creation

```csharp
using TUF.Repository;
using TUF.Models;

// Generate signing keys (in production, load from secure storage)
var rootSigner = Ed25519Signer.Generate();
var timestampSigner = Ed25519Signer.Generate(); 
var snapshotSigner = Ed25519Signer.Generate();
var targetsSigner = Ed25519Signer.Generate();

// Create repository builder
var builder = new RepositoryBuilder()
    .AddSigner("root", rootSigner)
    .AddSigner("timestamp", timestampSigner)
    .AddSigner("snapshot", snapshotSigner)
    .AddSigner("targets", targetsSigner)
    .SetDefaultExpiry(DateTimeOffset.UtcNow.AddYears(1))
    .SetConsistentSnapshots(true);

// Add target files
builder.AddTarget("app.exe", File.ReadAllBytes("path/to/app.exe"));
builder.AddTarget("config.json", File.ReadAllBytes("path/to/config.json"));

// Build and write repository
var repository = builder.Build();
repository.WriteToDirectory("./my-repository");

Console.WriteLine("✅ Repository created successfully!");
```

### Adding Target Files

You can add target files in several ways:

```csharp
// From byte array with custom metadata
var content = System.Text.Encoding.UTF8.GetBytes("Hello, TUF!");
builder.AddTarget("greeting.txt", content, new Dictionary<string, object>
{
    ["description"] = "Greeting message",
    ["version"] = "1.0.0"
});

// From existing file on disk
builder.AddTargetFromFile("application.exe", "./build/output/app.exe", new Dictionary<string, object>
{
    ["type"] = "application",
    ["platform"] = "windows"
});

// Multiple files in subdirectories
builder.AddTargetFromFile("configs/production.json", "./configs/prod.json");
builder.AddTargetFromFile("configs/staging.json", "./configs/stage.json");
```

### Configuration Options

Control repository behavior with various configuration options:

```csharp
var builder = new RepositoryBuilder()
    // Set custom expiry times (default: 1 year)
    .SetDefaultExpiry(DateTimeOffset.UtcNow.AddMonths(6))
    
    // Enable/disable consistent snapshots (default: true)
    .SetConsistentSnapshots(false)
    
    // Add multiple signers for threshold signatures
    .AddSigner("root", primaryRootSigner)
    .AddSigner("root", backupRootSigner)   // Threshold = 2 for root role
    
    // Add signers with explicit IDs
    .AddSigner("targets", targetsSigner, "targets-key-1");
```

## Advanced Repository Management

### Key Management Best Practices

**For Development:**
```csharp
// Generate ephemeral keys (fine for development)
var signer = Ed25519Signer.Generate();
```

**For Production:**
```csharp
// Load keys from secure storage
var privateKeyBytes = await LoadFromSecureStorage("root-private-key");
var signer = Ed25519Signer.LoadFromBytes(privateKeyBytes);

// Or use hardware security modules (HSMs)
// var signer = new YubiKeySigner("9c"); // See HSM integration docs
```

### Multiple Signature Algorithms

TUF .NET supports multiple signature algorithms:

```csharp
using TUF.Models;

// Ed25519 (recommended - fast and secure)
var ed25519Signer = Ed25519Signer.Generate();
builder.AddSigner("targets", ed25519Signer);

// RSA-PSS (enterprise compatibility)  
var rsaSigner = RsaSigner.Generate(2048);
builder.AddSigner("snapshot", rsaSigner);

// ECDSA P-256 (standards compliance)
var ecdsaSigner = EcdsaSigner.Generate();
builder.AddSigner("timestamp", ecdsaSigner);
```

### Threshold Signatures

Require multiple signatures for enhanced security:

```csharp
// Add multiple signers to the same role
var rootSigner1 = Ed25519Signer.Generate();
var rootSigner2 = Ed25519Signer.Generate();
var rootSigner3 = Ed25519Signer.Generate();

var builder = new RepositoryBuilder()
    .AddSigner("root", rootSigner1)
    .AddSigner("root", rootSigner2) 
    .AddSigner("root", rootSigner3);

// RepositoryBuilder automatically sets threshold = number of signers
// Clients will require ALL signatures to be valid
```

### Custom Metadata

Add custom metadata to target files for your application needs:

```csharp
builder.AddTarget("app-v1.2.3.exe", appBytes, new Dictionary<string, object>
{
    ["version"] = "1.2.3",
    ["architecture"] = "x64", 
    ["platform"] = "windows",
    ["changelog_url"] = "https://example.com/changelog/v1.2.3",
    ["minimum_client_version"] = "1.0.0",
    ["features"] = new[] { "dark_mode", "offline_sync" }
});
```

## Production Deployment Patterns

### Automated Repository Management

Create repositories as part of your CI/CD pipeline:

```csharp
public class TufRepositoryManager
{
    private readonly IKeyStore _keyStore;
    private readonly ILogger _logger;
    
    public async Task<TufRepository> CreateReleaseRepository(
        string version, 
        Dictionary<string, byte[]> artifacts,
        string outputPath)
    {
        _logger.LogInformation("Creating TUF repository for version {Version}", version);
        
        // Load production keys securely
        var signers = await _keyStore.LoadProductionSigners();
        
        var builder = new RepositoryBuilder()
            .AddSigner("root", signers.Root)
            .AddSigner("timestamp", signers.Timestamp)
            .AddSigner("snapshot", signers.Snapshot)
            .AddSigner("targets", signers.Targets)
            .SetDefaultExpiry(DateTimeOffset.UtcNow.AddMonths(3));
        
        // Add release artifacts
        foreach (var artifact in artifacts)
        {
            var metadata = new Dictionary<string, object>
            {
                ["version"] = version,
                ["build_timestamp"] = DateTimeOffset.UtcNow,
                ["build_id"] = Environment.GetEnvironmentVariable("BUILD_ID")
            };
            
            builder.AddTarget(artifact.Key, artifact.Value, metadata);
        }
        
        var repository = builder.Build();
        repository.WriteToDirectory(outputPath);
        
        _logger.LogInformation("Repository created at {Path}", outputPath);
        return repository;
    }
}
```

### Multi-Environment Repositories

Organize repositories for different environments:

```
repositories/
├── production/
│   ├── metadata/
│   └── targets/
├── staging/
│   ├── metadata/
│   └── targets/
└── development/
    ├── metadata/
    └── targets/
```

### Repository Validation

Always validate repositories after creation:

```csharp
public async Task ValidateRepository(string repositoryPath)
{
    try
    {
        // Test repository with a client
        var rootPath = Path.Combine(repositoryPath, "metadata", "root.json");
        var rootBytes = await File.ReadAllBytesAsync(rootPath);
        
        var config = new UpdaterConfig(
            rootBytes, 
            new Uri($"file://{Path.GetFullPath(repositoryPath)}/metadata"))
        {
            LocalMetadataDir = Path.GetTempPath(),
            LocalTargetsDir = Path.GetTempPath()
        };
        
        var updater = new Updater(config);
        await updater.RefreshAsync();
        
        Console.WriteLine("✅ Repository validation successful!");
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Repository validation failed: {ex.Message}", ex);
    }
}
```

## Security Considerations

### Key Generation and Storage

**Development:**
- Generate keys with `Ed25519Signer.Generate()`
- Store in project files (not committed to source control)
- Use different keys for each environment

**Production:**
- Use hardware security modules (HSMs) or secure key management services
- Implement proper access controls and audit logging
- Consider key rotation policies and procedures
- Keep root keys offline when possible

### Repository Validation

Always validate repositories after creation by testing with a TUF client to ensure:
- All metadata signatures are valid
- Target file hashes match content
- Metadata expiration times are appropriate
- Repository structure follows TUF specification

## Integration Examples

### ASP.NET Core Integration

Serve TUF repositories over HTTP:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Serve metadata files
app.MapGet("/metadata/{filename}", async (string filename) =>
{
    var metadataPath = Path.Combine("./repository/metadata", filename);
    if (!File.Exists(metadataPath))
        return Results.NotFound();
    
    var content = await File.ReadAllBytesAsync(metadataPath);
    return Results.File(content, "application/json");
});

// Serve target files  
app.MapGet("/targets/{*targetPath}", async (string targetPath) =>
{
    var filePath = Path.Combine("./repository/targets", targetPath);
    if (!File.Exists(filePath))
        return Results.NotFound();
        
    return Results.File(filePath);
});

app.Run();
```

## Troubleshooting

### Common Issues

**"No signers configured for [role]" error:**
```csharp
// Make sure all required roles have signers
var builder = new RepositoryBuilder()
    .AddSigner("root", rootSigner)      // Required
    .AddSigner("timestamp", tsSigner)   // Required
    .AddSigner("snapshot", snapSigner)  // Required
    .AddSigner("targets", targetsSigner); // Required
```

**Signature verification failures:**
- Ensure consistent key usage between repository creation and client usage
- Check that metadata hasn't expired
- Verify all signatures are applied correctly

**Target file not found errors:**
- Ensure target files are added to the builder before calling `Build()`
- Check file paths and directory structure
- Verify `WriteToDirectory()` completed successfully

### Debugging Tools

Enable detailed logging:

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

var logger = loggerFactory.CreateLogger<RepositoryBuilder>();

// Use logger throughout your repository creation process
logger.LogInformation("Creating repository with {TargetCount} targets", targetCount);
```

## Next Steps

After creating your first repository:

1. **Test with clients** - Use the [BasicClient example](../../examples/BasicClient/) to verify your repository works
2. **Set up automation** - Integrate repository creation into your build pipeline  
3. **Plan key management** - Implement secure key storage and rotation procedures
4. **Monitor and maintain** - Set up monitoring for repository health and update frequency
5. **Scale deployment** - Consider CDN integration and multi-region distribution

## Examples and Tools

### Available Examples
- **[RepositoryManager](../../examples/RepositoryManager/)** - Complete repository creation demonstration
- **[SigningDemo](../../examples/SigningDemo/)** - Cryptographic signing examples with multiple algorithms
- **[CliTool](../../examples/CliTool/)** - Command-line interface for TUF operations

### Related Documentation
- **[What is TUF?](./what-is-tuf.md)** - Understanding the security framework
- **[Core Concepts](./core-concepts.md)** - Essential TUF concepts for .NET developers  
- **[Building Clients](./building-clients.md)** - Creating applications that consume TUF repositories
- **[Quick Start Guide](./quick-start.md)** - Get up and running quickly

### External Resources
- **[TUF Specification](https://theupdateframework.github.io/specification/latest/)** - Official TUF specification
- **[Security Best Practices](https://theupdateframework.io/security/)** - General TUF security guidance
- **[Community Examples](https://github.com/theupdateframework/community)** - Real-world TUF implementations

---

This guide covers the essential aspects of creating TUF repositories with TUF .NET. For specific use cases or advanced scenarios not covered here, refer to the API documentation or examine the comprehensive examples in the repository.