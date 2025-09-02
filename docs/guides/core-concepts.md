# TUF Core Concepts for .NET Developers

This guide explains the fundamental concepts of The Update Framework (TUF) from a .NET developer perspective, showing how TUF's security model maps to practical .NET implementations.

## What is TUF?

The Update Framework (TUF) is a security framework designed to protect software update systems from various attacks. Unlike traditional update mechanisms that rely solely on HTTPS, TUF provides cryptographic guarantees about the integrity and authenticity of software updates.

### Key Security Goals

1. **Protect against compromised update servers** - Even if the update server is hacked, attackers cannot serve malicious updates
2. **Prevent rollback attacks** - Attackers cannot force clients to downgrade to vulnerable versions
3. **Mitigate key compromise** - Multiple keys with different roles limit the impact of any single key compromise
4. **Ensure freshness** - Prevent replay of old, potentially vulnerable updates

## TUF Metadata Roles

TUF uses a system of **metadata roles**, each with specific responsibilities and signing keys. Think of these as different "certificates" that vouch for different aspects of your software distribution.

### Root Role (`root.json`)

The **root of trust** for your TUF repository - like a certificate authority (CA) root certificate.

```csharp
// In .NET TUF, root metadata is represented by the Root class
var root = new Root
{
    SpecVersion = "1.0.0",
    Version = 1,
    Expires = DateTimeOffset.UtcNow.AddYears(1),
    Keys = rootKeys,  // Public keys for all roles
    Roles = roleAssignments,  // Which keys can sign which roles
    ConsistentSnapshot = true
};
```

**Characteristics:**
- **Long-lived**: Expires infrequently (typically 1 year)
- **Rarely updated**: Only changes when rotating keys or updating role assignments
- **Critical security**: Compromise means complete repository compromise
- **Offline signing**: Root keys should be kept offline for maximum security

### Timestamp Role (`timestamp.json`)

Provides **freshness guarantees** - prevents replay attacks by indicating when the repository was last updated.

```csharp
var timestamp = new Timestamp
{
    SpecVersion = "1.0.0", 
    Version = incrementalVersion,
    Expires = DateTimeOffset.UtcNow.AddDays(1), // Short-lived
    Meta = snapshotMetadata  // Points to current snapshot
};
```

**Characteristics:**
- **Short-lived**: Expires frequently (typically daily)
- **Frequently updated**: Updated with every repository change
- **Small size**: Only references the snapshot metadata
- **Online signing**: Key can be kept online for automated updates

### Snapshot Role (`snapshot.json`)

Provides **consistency guarantees** - ensures all metadata files are from the same point in time.

```csharp
var snapshot = new Snapshot
{
    SpecVersion = "1.0.0",
    Version = incrementalVersion,
    Expires = DateTimeOffset.UtcNow.AddMonths(1),
    Meta = new Dictionary<string, FileMetadata>
    {
        ["targets.json"] = new FileMetadata 
        {
            Version = targetsVersion,
            Length = targetsSize,
            Hashes = targetsHashes
        }
        // Additional delegated targets...
    }
};
```

**Characteristics:**
- **Medium-lived**: Expires monthly
- **Updated frequently**: Changes when any targets metadata changes
- **Consistency**: Prevents mix-and-match attacks across metadata versions

### Targets Role (`targets.json`)

Describes the **actual files** available for download and their cryptographic hashes.

```csharp
var targets = new Targets
{
    SpecVersion = "1.0.0",
    Version = incrementalVersion,
    Expires = DateTimeOffset.UtcNow.AddMonths(3),
    TargetMap = new Dictionary<string, TargetFile>
    {
        ["myapp.exe"] = new TargetFile
        {
            Length = fileSize,
            Hashes = new Dictionary<string, string>
            {
                ["sha256"] = "a1b2c3...",
                ["sha512"] = "x1y2z3..."
            },
            Custom = new JsonObject { ["version"] = "2.1.0" }
        }
    }
};
```

**Characteristics:**
- **Medium-lived**: Expires quarterly
- **Updated regularly**: Changes when files are added, removed, or updated
- **File verification**: Contains cryptographic hashes for integrity checking

## Key Concepts in Practice

### Metadata Verification Chain

In .NET TUF, metadata verification follows a specific chain:

```csharp
// 1. Verify timestamp against root
var trustedTimestamp = trustedRoot.VerifyTimestamp(timestampMetadata);

// 2. Verify snapshot against timestamp
var trustedSnapshot = trustedTimestamp.VerifySnapshot(snapshotMetadata);

// 3. Verify targets against snapshot
var trustedTargets = trustedSnapshot.VerifyTargets(targetsMetadata);

// 4. Verify target file against targets
var targetInfo = trustedTargets.GetTargetInfo("myapp.exe");
if (targetInfo.HasValue)
{
    var isValid = await VerifyDownloadedFile(localFile, targetInfo.Value.File);
}
```

### Signature Verification

Every metadata file contains both the **signed content** and the **signatures**:

```csharp
// Metadata structure in .NET TUF
public class Metadata<T>
{
    public T Signed { get; set; }  // The actual metadata content
    public List<SignatureObject> Signatures { get; set; }  // Cryptographic signatures
}

// Verification process
var signedBytes = metadata.GetSignedBytes();  // Canonical JSON representation
var isValid = signature.Verify(signedBytes, publicKey);
```

### Consistent Snapshots

When enabled, consistent snapshots ensure that all files (both metadata and targets) are named with their version or hash:

```csharp
// Without consistent snapshots
// metadata/targets.json
// targets/myapp.exe

// With consistent snapshots  
// metadata/2.targets.json  (version-prefixed)
// targets/a1b2c3...myapp.exe  (hash-prefixed)
```

This prevents race conditions where clients might download mismatched files during repository updates.

## Security Properties

### Threshold Signatures

TUF supports requiring multiple signatures for enhanced security:

```csharp
var roleKeys = new RoleKeys
{
    KeyIds = new List<string> { "key1", "key2", "key3" },
    Threshold = 2  // Requires 2 out of 3 signatures
};
```

### Key Rotation

When keys are compromised, TUF supports key rotation:

```csharp
// Create new root metadata with updated keys
var newRoot = new Root
{
    Version = oldRoot.Version + 1,  // Increment version
    Keys = updatedKeys,  // New public keys
    Roles = updatedRoles  // Updated role assignments
};

// Sign with both old AND new keys for transition period
SignMetadata(newRoot, oldKeys.Concat(newKeys));
```

### Delegation

Large repositories can delegate trust to sub-repositories:

```csharp
var delegatedRole = new DelegatedRole
{
    Name = "projects/myproject",
    Paths = new List<string> { "myproject/*" },
    KeyIds = projectKeys,
    Threshold = 1
};

// Add to targets metadata
targets.Delegations = new Delegations
{
    Keys = delegatedKeys,
    Roles = new List<DelegatedRole> { delegatedRole }
};
```

## .NET-Specific Implementations

### Canonical JSON Serialization

TUF requires **canonical JSON** for signature verification. .NET TUF uses Serde.NET for precise control:

```csharp
// Using CanonicalJson library
var canonicalBytes = CanonicalJson.Serializer.Serialize<Metadata<Root>, MetadataProxy.Ser<Root>>(rootMetadata);

// Properties are lexicographically sorted
// No extra whitespace
// Consistent Unicode escaping
```

### Async/Await Patterns

All I/O operations use async patterns:

```csharp
public async Task UpdateSoftware()
{
    var updater = new Updater(config);
    
    // All metadata operations are async
    await updater.RefreshAsync();
    
    var targetInfo = await updater.GetTargetInfo("myapp.exe");
    if (targetInfo.HasValue)
    {
        var (localPath, data) = await updater.DownloadTarget(
            targetInfo.Value.File, "myapp.exe");
    }
}
```

### Exception Handling

TUF operations can fail in various ways:

```csharp
try
{
    await updater.RefreshAsync();
}
catch (TufMetadataException ex) when (ex.ErrorCode == TufErrorCode.SignatureVerificationFailed)
{
    // Handle signature verification failure
    logger.LogError("Metadata signature verification failed: {Message}", ex.Message);
}
catch (TufNetworkException ex)
{
    // Handle network-related failures
    logger.LogWarning("Network error during refresh: {Message}", ex.Message);
}
```

## Integration Patterns

### Dependency Injection

TUF clients integrate naturally with .NET dependency injection:

```csharp
// Startup.cs
services.AddSingleton<HttpClient>();
services.AddScoped<IUpdater>(provider => 
{
    var httpClient = provider.GetRequiredService<HttpClient>();
    var config = new UpdaterConfig(trustedRoot, metadataUrl)
    {
        Client = httpClient
    };
    return new Updater(config);
});
```

### Configuration System

Use .NET configuration for TUF settings:

```csharp
// appsettings.json
{
  "TUF": {
    "MetadataUrl": "https://updates.example.com/metadata/",
    "LocalMetadataDir": "./tuf-metadata",
    "LocalTargetsDir": "./downloads",
    "TrustedRootPath": "./trusted-root.json"
  }
}

// Configuration binding
var tufConfig = configuration.GetSection("TUF").Get<TufConfiguration>();
```

### Logging

TUF operations integrate with .NET logging:

```csharp
services.AddLogging(builder => 
{
    builder.AddConsole();
    builder.AddFile("tuf-client.log");
});

// In your TUF client code
logger.LogInformation("Refreshing TUF metadata from {MetadataUrl}", config.RemoteMetadataUrl);
logger.LogWarning("Target {TargetPath} not found in repository", targetPath);
```

## Common Patterns

### Update Check Loop

```csharp
public class UpdateService : BackgroundService
{
    private readonly IUpdater _updater;
    private readonly ILogger<UpdateService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _updater.RefreshAsync();
                await CheckForUpdates();
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update check failed");
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }
    }
}
```

### Secure File Downloads

```csharp
public async Task<bool> SecureDownload(string targetPath, string localPath)
{
    try
    {
        // Get verified target information
        var targetInfo = await updater.GetTargetInfo(targetPath);
        if (!targetInfo.HasValue)
        {
            return false;
        }

        // Download and verify in one operation
        var (downloadPath, data) = await updater.DownloadTarget(
            targetInfo.Value.File, localPath);
            
        logger.LogInformation("Successfully downloaded {TargetPath} ({Size} bytes)", 
            targetPath, data.Length);
        return true;
    }
    catch (TufTargetNotFoundException)
    {
        logger.LogWarning("Target {TargetPath} not found", targetPath);
        return false;
    }
}
```

## Next Steps

- **[Quick Start Guide](quick-start.md)** - Get up and running with TUF in .NET
- **[Building Clients](building-clients.md)** - Production client development patterns
- **[Security Model](../security/security-model.md)** - Deep dive into TUF security guarantees
- **[API Reference](../api/)** - Complete API documentation