# Getting Started with TUF .NET

This guide will walk you through integrating The Update Framework (TUF) into your .NET application, from installation to your first secure software update.

## Prerequisites

- .NET 8.0 or later
- Basic understanding of software updates and security concepts
- (Optional) Access to a TUF repository or ability to create one

## Installation

### NuGet Package Manager
```bash
# Core TUF functionality
dotnet add package TUF

# For canonical JSON (if needed)
dotnet add package CanonicalJson
```

### Package Manager Console
```powershell
Install-Package TUF
Install-Package CanonicalJson
```

## Understanding TUF Concepts

Before diving into code, let's understand key TUF concepts:

### Metadata Roles
- **Root** - Defines trusted keys and roles (root of trust)
- **Timestamp** - Provides freshness guarantees  
- **Snapshot** - Ensures metadata consistency
- **Targets** - Describes available target files

### Repository Structure
```
repository/
├── metadata/           # TUF metadata files
│   ├── root.json      # Root metadata (distributed out-of-band)
│   ├── timestamp.json # Updated frequently for freshness
│   ├── snapshot.json  # References all metadata versions
│   └── targets.json   # Target file information
└── targets/           # Actual target files
    ├── app.exe
    └── config.json
```

## Your First TUF Client

### 1. Basic Setup

```csharp
using TUF;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Load trusted root metadata (distributed with your application)
        var trustedRoot = await File.ReadAllBytesAsync("root.json");
        
        // Configure the TUF client
        var config = new UpdaterConfig(trustedRoot, new Uri("https://updates.example.com/metadata/"))
        {
            LocalMetadataDir = Path.Combine(Environment.CurrentDirectory, "cache", "metadata"),
            LocalTargetsDir = Path.Combine(Environment.CurrentDirectory, "cache", "targets"),
            Client = new HttpClient()
        };

        var updater = new Updater(config);
        
        Console.WriteLine("TUF client initialized successfully!");
        
        // Your update logic goes here...
    }
}
```

### 2. Checking for Updates

```csharp
try
{
    // Refresh metadata from the repository
    await updater.RefreshAsync();
    Console.WriteLine("Metadata refreshed successfully");

    // Get information about available targets
    var targets = updater.GetTopLevelTargets();
    Console.WriteLine($"Found {targets.Count} available targets");

    foreach (var (name, targetFile) in targets)
    {
        Console.WriteLine($"  {name}: {targetFile.Length} bytes, {targetFile.Hashes.Count} hashes");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error checking for updates: {ex.Message}");
}
```

### 3. Downloading Updates

```csharp
// Check if a specific target exists
var targetInfo = await updater.GetTargetInfo("myapp.exe");
if (targetInfo.HasValue)
{
    var (remotePath, targetFile) = targetInfo.Value;
    
    Console.WriteLine($"Downloading {remotePath}...");
    
    try
    {
        // Download and verify the target file
        var (localPath, data) = await updater.DownloadTarget(
            targetFile, 
            "myapp.exe",
            Path.Combine(Environment.CurrentDirectory, "myapp.exe")
        );
        
        Console.WriteLine($"Successfully downloaded and verified: {localPath}");
        Console.WriteLine($"File size: {data.Length} bytes");
        
        // TODO: Apply the update (restart application, etc.)
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Download failed: {ex.Message}");
    }
}
else
{
    Console.WriteLine("Target not found in repository");
}
```

## Complete Example

Here's a complete working example that ties everything together:

```csharp
using TUF;

namespace MyApp.Updates
{
    public class TufUpdateService
    {
        private readonly Updater _updater;
        private readonly ILogger<TufUpdateService> _logger;

        public TufUpdateService(ILogger<TufUpdateService> logger)
        {
            _logger = logger;
            
            // Load embedded trusted root (shipped with your application)
            var trustedRoot = LoadEmbeddedResource("root.json");
            
            var config = new UpdaterConfig(trustedRoot, new Uri("https://updates.myapp.com/metadata/"))
            {
                LocalMetadataDir = Path.Combine(AppDomain.CurrentDirectory, ".cache", "metadata"),
                LocalTargetsDir = Path.Combine(AppDomain.CurrentDirectory, ".cache", "targets"), 
                Client = CreateHttpClient(),
                Logger = logger
            };

            _updater = new Updater(config);
        }

        public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Checking for updates...");
                
                await _updater.RefreshAsync(cancellationToken);
                
                var currentVersion = GetCurrentVersion();
                var availableTargets = _updater.GetTopLevelTargets();
                
                return availableTargets.ContainsKey($"myapp-{currentVersion}.exe");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check for updates");
                return false;
            }
        }

        public async Task<string?> DownloadUpdateAsync(string version, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetName = $"myapp-{version}.exe";
                var targetInfo = await _updater.GetTargetInfo(targetName);
                
                if (!targetInfo.HasValue)
                {
                    _logger.LogWarning("Update version {Version} not found", version);
                    return null;
                }

                var (remotePath, targetFile) = targetInfo.Value;
                
                _logger.LogInformation("Downloading update {Version} ({Size} bytes)", version, targetFile.Length);
                
                var (localPath, _) = await _updater.DownloadTarget(
                    targetFile, 
                    targetName,
                    destinationFilePath: Path.Combine(Path.GetTempPath(), targetName),
                    cancellationToken: cancellationToken
                );

                _logger.LogInformation("Update downloaded and verified: {Path}", localPath);
                return localPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download update for version {Version}", version);
                return null;
            }
        }

        private byte[] LoadEmbeddedResource(string name)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream($"MyApp.Resources.{name}");
            if (stream == null)
                throw new InvalidOperationException($"Embedded resource {name} not found");
                
            using var reader = new MemoryStream();
            stream.CopyTo(reader);
            return reader.ToArray();
        }

        private HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", $"MyApp-TUF/{GetCurrentVersion()}");
            client.Timeout = TimeSpan.FromMinutes(5);
            return client;
        }

        private string GetCurrentVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly()
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "1.0.0";
        }
    }
}
```

## Dependency Injection Integration

For ASP.NET Core or other dependency injection scenarios:

```csharp
// Program.cs
builder.Services.AddSingleton<TufUpdateService>();

// Or with configuration
builder.Services.Configure<TufOptions>(builder.Configuration.GetSection("Tuf"));
builder.Services.AddSingleton<ITufClient, TufClient>();
```

## Configuration Options

The `UpdaterConfig` class provides many configuration options:

```csharp
var config = new UpdaterConfig(trustedRoot, metadataUrl)
{
    // Cache directories
    LocalMetadataDir = "./cache/metadata",
    LocalTargetsDir = "./cache/targets",
    
    // HTTP settings
    Client = httpClient,
    
    // Security limits
    MaxRootRotations = 256,
    MaxDelegations = 32,
    RootMaxLength = 512000,
    TimestampMaxLength = 16384,
    SnapshotMaxLength = 2000000,
    TargetsMaxLength = 5000000,
    
    // Repository settings
    RemoteTargetsUrl = new Uri("https://updates.example.com/targets/"),
    PrefixTargetsWithHash = true, // For consistent snapshots
    DisableLocalCache = false,
    
    // Logging
    Logger = logger
};
```

## Error Handling

TUF .NET provides specific exception types for different error conditions:

```csharp
try
{
    await updater.RefreshAsync();
}
catch (TufException ex)
{
    // TUF-specific errors (metadata validation, signature verification, etc.)
    _logger.LogError(ex, "TUF error: {Message}", ex.Message);
}
catch (HttpRequestException ex)
{
    // Network-related errors
    _logger.LogError(ex, "Network error while updating metadata");
}
catch (OperationCanceledException)
{
    // Operation was cancelled
    _logger.LogInformation("Update check was cancelled");
}
```

## Next Steps

Now that you have a basic TUF client working:

1. **[Repository Management](repository-management.md)** - Learn how to create and maintain TUF repositories
2. **[Advanced Features](advanced-features.md)** - Explore delegations, multi-repository support, and custom metadata
3. **[Security Best Practices](security-best-practices.md)** - Understand key management, threat modeling, and production deployment
4. **[Examples](https://github.com/baronfel/tuf-dotnet/tree/main/examples)** - Browse complete working examples for different scenarios

## Common Issues

### "Not all members were assigned" Error
This typically indicates metadata deserialization issues. Check that:
- Your trusted root is valid JSON and properly formatted
- The metadata URL is accessible and returns valid TUF metadata
- Your network connection allows HTTPS requests

### Signature Verification Failures  
Ensure that:
- The trusted root contains the correct public keys
- The metadata files are properly signed
- The signature algorithms match what's configured in the root

### File Download Issues
Check that:
- The remote targets URL is correct and accessible
- Target files exist at the expected locations
- File hashes in metadata match the actual files

For more troubleshooting help, see the [Troubleshooting Guide](troubleshooting.md).