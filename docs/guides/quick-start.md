# TUF .NET Quick Start Guide

This guide will get you up and running with TUF .NET in just a few minutes. By the end, you'll have a working TUF client that can securely download and verify files from a TUF repository.

## Prerequisites

- **.NET 8.0 or later** - TUF .NET targets .NET 8+ for optimal performance
- **Basic C# knowledge** - Familiarity with async/await and basic .NET patterns
- **HTTP access** - Your application needs to make HTTP requests to TUF repositories

## Installation

Add the TUF .NET package to your project:

```bash
dotnet add package TUF
```

For canonical JSON utilities (if needed):
```bash
dotnet add package CanonicalJson
```

## Your First TUF Client

Let's create a minimal TUF client that downloads and verifies a file:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using TUF;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Load trusted root metadata (normally from a secure source)
        var trustedRootBytes = await File.ReadAllBytesAsync("trusted-root.json");
        
        // 2. Configure the TUF client
        var config = new UpdaterConfig
        {
            LocalTrustedRoot = trustedRootBytes,
            RemoteMetadataUrl = new Uri("https://example.com/metadata/"),
            RemoteTargetsUrl = new Uri("https://example.com/targets/"),
            LocalMetadataDir = "./tuf-metadata",
            LocalTargetsDir = "./downloads",
            Client = new HttpClient()
        };

        // 3. Create the updater
        var updater = new Updater(config);

        try
        {
            // 4. Refresh metadata from the repository
            Console.WriteLine("Refreshing TUF metadata...");
            await updater.RefreshAsync();
            
            // 5. Find the target file we want
            var targetInfo = await updater.GetTargetInfo("my-app.exe");
            if (!targetInfo.HasValue)
            {
                Console.WriteLine("Target file not found in repository");
                return;
            }

            var (remotePath, targetFile) = targetInfo.Value;
            Console.WriteLine($"Found target: {remotePath}");
            Console.WriteLine($"Size: {targetFile.Length} bytes");
            Console.WriteLine($"Hashes: {string.Join(", ", targetFile.Hashes.Keys)}");

            // 6. Download and verify the file
            Console.WriteLine("Downloading and verifying file...");
            var (localPath, data) = await updater.DownloadTarget(targetFile, "my-app.exe");
            
            Console.WriteLine($"Successfully downloaded and verified {data.Length} bytes");
            Console.WriteLine($"File saved to: {localPath}");
        }
        catch (TufException ex)
        {
            Console.WriteLine($"TUF error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }
}
```

## Understanding the Example

Let's break down what this code does:

### 1. Trusted Root Metadata
```csharp
var trustedRootBytes = await File.ReadAllBytesAsync("trusted-root.json");
```
The root metadata file contains the trusted public keys and establishes the foundation of trust. In production, this file should be:
- Distributed with your application
- Verified through out-of-band means (code signing, manual verification, etc.)
- Stored securely to prevent tampering

### 2. Configuration
```csharp
var config = new UpdaterConfig
{
    LocalTrustedRoot = trustedRootBytes,
    RemoteMetadataUrl = new Uri("https://example.com/metadata/"),
    RemoteTargetsUrl = new Uri("https://example.com/targets/"),
    LocalMetadataDir = "./tuf-metadata",
    LocalTargetsDir = "./downloads",
    Client = new HttpClient()
};
```
The configuration specifies:
- **Repository URLs** - Where to fetch metadata and target files
- **Local directories** - Where to cache metadata and store downloads
- **HTTP client** - For network requests (can be customized for proxies, timeouts, etc.)

### 3. Metadata Refresh
```csharp
await updater.RefreshAsync();
```
This performs the complete TUF workflow:
- Downloads timestamp metadata for freshness
- Downloads snapshot metadata for consistency  
- Downloads targets metadata for file information
- Verifies all signatures and metadata integrity

### 4. Target Discovery
```csharp
var targetInfo = await updater.GetTargetInfo("my-app.exe");
```
This searches through the targets metadata (including delegations) to find information about the requested file.

### 5. Secure Download
```csharp
var (localPath, data) = await updater.DownloadTarget(targetFile, "my-app.exe");
```
This downloads the file and verifies:
- File length matches the metadata
- All cryptographic hashes match
- The file hasn't been tampered with

## Running the Example

To test this example, you'll need a TUF repository. You can:

1. **Use the examples** - Run the RepositoryManager example to create a test repository:
   ```bash
   cd examples/RepositoryManager
   dotnet run ./test-repo
   ```

2. **Use the BasicClient example** - A complete working example:
   ```bash
   cd examples/BasicClient
   dotnet run file://./path/to/repository/metadata my-app.exe
   ```

## Production Considerations

### Security
- **Verify root metadata** - Always verify the initial root metadata through secure channels
- **Use HTTPS** - Always use HTTPS for metadata and target URLs
- **Key management** - Follow proper key management practices for repository signing
- **Error handling** - Implement comprehensive error handling for all TUF exceptions

### Performance  
- **HTTP client reuse** - Reuse HttpClient instances to avoid socket exhaustion
- **Caching** - Enable local caching unless you have specific reasons to disable it
- **Timeouts** - Configure appropriate HTTP timeouts for your network environment

### Reliability
- **Retry logic** - Implement retry logic for transient network errors
- **Fallback repositories** - Consider using multiple repositories for high availability
- **Monitoring** - Log TUF operations for debugging and monitoring

## Next Steps

Now that you have a working TUF client:

1. **Learn the concepts** - Read [Core Concepts](./core-concepts.md) to understand TUF deeply
2. **Explore examples** - Check out the [examples directory](../../examples/) for more scenarios
3. **Production deployment** - Read [Building Clients](./building-clients.md) for production guidance
4. **Create repositories** - Learn [Creating TUF Repositories](./creating-repositories.md) if you need to manage your own repository

## Troubleshooting

### Common Issues

**"File not found" error when loading trusted root:**
```csharp
// Make sure the file exists and path is correct
if (!File.Exists("trusted-root.json"))
{
    throw new FileNotFoundException("Trusted root metadata file not found");
}
```

**Network connectivity issues:**
```csharp
var httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromSeconds(30);
// Test connectivity to your repository URLs
```

**Signature verification failures:**
- Ensure your trusted root metadata matches your repository
- Check that metadata hasn't expired
- Verify repository URLs are correct

### Getting Help

- **Check the logs** - TUF .NET provides detailed logging through Microsoft.Extensions.Logging
- **Review examples** - The examples directory contains working code for common scenarios
- **Read the troubleshooting guide** - [Troubleshooting Guide](./troubleshooting.md) covers common issues

## Example Applications

The TUF .NET repository includes several complete examples:

- **[BasicClient](../../examples/BasicClient/)** - Simple client demonstrating core workflow
- **[CliTool](../../examples/CliTool/)** - Command-line interface for TUF operations  
- **[RepositoryManager](../../examples/RepositoryManager/)** - Create and manage TUF repositories
- **[MultiRepositoryClient](../../examples/MultiRepositoryClient/)** - Multi-repository consensus
- **[SigningDemo](../../examples/SigningDemo/)** - Cryptographic signing examples

Each example includes its own README with detailed instructions.