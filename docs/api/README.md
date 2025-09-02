# TUF .NET API Documentation

This directory contains comprehensive API documentation for the TUF .NET implementation, organized by functional area.

## Core APIs

### Client APIs
- **[Updater](./updater.md)** - Primary client for TUF repository operations
- **[TrustedMetadata](./trusted-metadata.md)** - Metadata validation and management
- **[MultiRepositoryClient](./multi-repository-client.md)** - Multi-repository consensus validation (TAP 4)

### Metadata Models
- **[Metadata<T>](./metadata.md)** - Generic metadata container with signatures
- **[Root](./root.md)** - Root metadata (trusted keys and roles)
- **[Targets](./targets.md)** - Target file specifications
- **[Snapshot](./snapshot.md)** - Metadata snapshot information
- **[Timestamp](./timestamp.md)** - Timestamp metadata for freshness

### Repository Management
- **[RepositoryBuilder](./repository-builder.md)** - Repository creation and management
- **[Signing](./signing.md)** - Cryptographic signing operations

### Configuration & Utilities
- **[UpdaterConfig](./updater-config.md)** - Client configuration options
- **[Exceptions](./exceptions.md)** - TUF-specific exception hierarchy
- **[Extensions](./extensions.md)** - Utility extension methods

## Quick Reference

### Basic Client Usage
```csharp
// Initialize TUF client
var config = new UpdaterConfig
{
    MetadataUrl = new Uri("https://example.com/metadata/"),
    TargetsUrl = new Uri("https://example.com/targets/"),
    MetadataDir = "./metadata",
    TargetsDir = "./downloads"
};

var updater = new Updater(config);

// Initialize with trusted root
await updater.InitializeAsync(trustedRootBytes);

// Refresh metadata
await updater.RefreshAsync();

// Download and verify target
await updater.DownloadTargetAsync("file.txt");
```

### Repository Creation
```csharp
// Create new TUF repository
var repository = new RepositoryBuilder()
    .AddSigner("root", Ed25519Signer.Generate())
    .AddSigner("timestamp", Ed25519Signer.Generate())
    .AddSigner("snapshot", Ed25519Signer.Generate())
    .AddSigner("targets", Ed25519Signer.Generate())
    .AddTarget("app.exe", fileBytes)
    .Build();

// Export to filesystem
repository.WriteToDirectory("./my-tuf-repo");
```

### Multi-Repository Setup
```csharp
var config = new MultiRepositoryConfig
{
    MapFilePath = "./map.json",
    MetadataDir = "./metadata",
    TargetsDir = "./targets"
};

var client = new MultiRepositoryClient(config);
await client.InitializeAsync();

var result = await client.GetTargetInfoAsync("critical-update.exe");
if (result.IsValid && result.AgreementCount >= result.RequiredThreshold)
{
    await client.DownloadTargetAsync("critical-update.exe", "./downloads/");
}
```

## API Design Principles

### Type Safety
All TUF operations use strongly-typed models with compile-time validation:
- `Metadata<Root>` vs `Metadata<Targets>` prevent metadata type confusion
- Nullable reference types catch missing data at compile time
- Generic constraints ensure proper metadata relationships

### Security by Default
- All signature verification is automatic and mandatory
- Metadata expiration checking is enforced
- Threshold validation prevents single points of failure
- Role separation is built into the type system

### Performance Optimization
- Canonical JSON serialization is optimized for repeated operations
- Metadata caching reduces network requests
- Streaming operations for large file downloads
- AOT compilation support for minimal startup overhead

### .NET Integration
- Native async/await patterns throughout
- Microsoft.Extensions.Logging integration
- Standard .NET configuration patterns
- Exception handling follows .NET conventions

## Error Handling

The TUF .NET library uses a comprehensive exception hierarchy for different error scenarios:

```csharp
try
{
    await updater.RefreshAsync();
}
catch (TufRollbackException ex)
{
    // Handle rollback attack detection
    logger.LogWarning("Rollback attack detected: {Message}", ex.Message);
}
catch (TufSignatureException ex)
{
    // Handle signature verification failures
    logger.LogError("Signature verification failed: {Message}", ex.Message);
}
catch (TufNetworkException ex)
{
    // Handle network/transport errors
    logger.LogError("Network error: {Message}", ex.Message);
}
```

## Thread Safety

### Thread-Safe Operations
- **Metadata reading**: All metadata access is thread-safe
- **Signature verification**: Cryptographic operations are thread-safe
- **Configuration access**: Configuration objects are immutable after creation

### Not Thread-Safe Operations
- **Updater state mutations**: `RefreshAsync()` and `DownloadTargetAsync()` should not be called concurrently
- **Repository building**: `RepositoryBuilder` is not thread-safe during construction

### Recommended Patterns
```csharp
// Safe: Multiple readers
var targetInfo = await updater.GetTargetInfoAsync("file.txt");
var otherInfo = await updater.GetTargetInfoAsync("other.txt");

// Unsafe: Concurrent state mutations
// DON'T DO THIS:
await Task.WhenAll(
    updater.RefreshAsync(),
    updater.DownloadTargetAsync("file.txt")
);

// Safe: Sequential operations
await updater.RefreshAsync();
await updater.DownloadTargetAsync("file.txt");
```

## Performance Considerations

### Startup Performance
- TUF operations typically happen at application startup
- Canonical JSON operations are optimized for minimal allocations
- Metadata caching reduces subsequent operation overhead

### Memory Usage
- Large target files are streamed to avoid memory pressure
- Metadata objects use value types where possible
- Signature verification uses stackalloc for temporary data

### Network Optimization
- HTTP client reuse across operations
- Conditional requests when metadata hasn't changed
- Parallel metadata downloads when safe

## AOT Compatibility

All TUF .NET libraries are fully compatible with Native AOT compilation:

```xml
<PropertyGroup>
    <PublishAot>true</PublishAot>
</PropertyGroup>
```

### AOT-Friendly Features
- No reflection in critical paths
- Serde.NET source generators for serialization
- Static analysis friendly code patterns
- Minimal external dependencies