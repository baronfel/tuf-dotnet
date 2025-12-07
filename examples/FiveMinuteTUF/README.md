# 5-Minute TUF Tutorial

**The fastest way to understand TUF .NET** - A complete, runnable example that demonstrates the entire TUF workflow in under 5 minutes.

## What This Tutorial Shows

This example creates a complete TUF implementation from scratch and demonstrates:

🔐 **Repository Creation**
- Generate cryptographic keys for all TUF roles
- Create and sign all required TUF metadata (root, targets, snapshot, timestamp)
- Publish sample files to a TUF repository

👤 **Client Usage**  
- Configure a TUF client with trusted root metadata
- Refresh metadata with full signature verification
- Discover available files securely
- Download files with cryptographic hash verification

🛡️ **Security Features**
- Metadata integrity through digital signatures
- File integrity through cryptographic hashes
- Rollback protection via version numbers
- Consistent snapshot guarantees

## Quick Start

```bash
# Clone the repository (if you haven't already)
git clone https://github.com/baronfel/tuf-dotnet.git
cd tuf-dotnet

# Build and run the tutorial
cd examples/FiveMinuteTUF
dotnet run
```

That's it! The tutorial is completely self-contained and will walk you through every step.

## Expected Output

The tutorial produces detailed output showing each step:

```
🚀 5-Minute TUF .NET Tutorial
============================

This example will:
1. Create a complete TUF repository with real cryptographic keys
2. Add sample files to the repository
3. Show how a client can securely download those files

📁 Working directory: /tmp/tuf-5min-tutorial/abc123...

🔐 Step 1: Creating TUF repository with cryptographic keys
────────────────────────────────────────────────────────
  • Generating Ed25519 keys for TUF roles...
  • Root key ID: ed25519:1a2b3c4d...
  • Targets key ID: ed25519:5e6f7g8h...
  • Repository structure configured ✓

📄 Step 2: Adding sample files to the repository
─────────────────────────────────────────────
  • Created: hello.txt (25 bytes)
  • Created: app-v1.0.exe (48 bytes)
  • Created: config.json (54 bytes)
  • Created: readme.md (78 bytes)

... [continuing through all steps] ...

🎉 Tutorial Complete!
```

## What You Learn

### TUF Concepts Demonstrated

**Metadata Hierarchy**: See how TUF uses multiple metadata files (root, timestamp, snapshot, targets) for different purposes:
- **Root**: Establishes trust and defines other roles
- **Timestamp**: Provides freshness guarantees  
- **Snapshot**: Ensures metadata consistency
- **Targets**: Describes available files and their hashes

**Key Management**: Understand how TUF uses separate keys for different roles to limit blast radius of key compromise.

**Client Workflow**: Experience the complete TUF client process from trusted root to verified file download.

### Security Properties

**Integrity**: Every metadata file is cryptographically signed, every target file is verified against cryptographic hashes.

**Freshness**: Timestamp metadata ensures you're getting current information, not stale or replayed data.

**Consistency**: Snapshot metadata ensures all metadata files are from the same repository state.

**Compromise Resilience**: Multiple different keys must be compromised for successful attacks.

## Real-World Usage

This tutorial demonstrates the exact same APIs and patterns you'd use in production:

```csharp
// Production TUF client setup
var config = new UpdaterConfig(trustedRoot, metadataUrl)
{
    RemoteTargetsUrl = targetsUrl,
    LocalMetadataDir = "./cache/metadata",
    LocalTargetsDir = "./cache/targets",
    Client = httpClient
};

var updater = new Updater(config);

// Refresh metadata (do this periodically)
await updater.RefreshAsync();

// Download a specific file
if (await updater.GetTargetInfo("my-app.exe") is var (path, info))
{
    var result = await updater.DownloadTarget(info, path, null, null);
    // File is now verified and ready to use
}
```

## Production Differences

In production applications, you would:

1. **Distribute trusted root securely**: Embed the trusted root metadata with your application or obtain it through a secure out-of-band channel
2. **Use HTTPS repositories**: Point to real HTTPS URLs instead of local file:// URLs
3. **Implement proper key management**: Use hardware security modules or secure key storage for repository signing keys
4. **Add error handling**: Implement retry logic, fallback repositories, and comprehensive error handling
5. **Configure HTTP client properly**: Set timeouts, proxy settings, and other HTTP client configuration

## Next Steps

After completing this tutorial:

1. **Explore Other Examples**
   - [BasicClient](../BasicClient/) - Simple client implementation
   - [RepositoryManager](../RepositoryManager/) - Advanced repository management
   - [CliTool](../CliTool/) - Command-line TUF operations

2. **Read Documentation**
   - [Core Concepts](../../docs/guides/core-concepts.md) - Deep dive into TUF concepts
   - [Building Clients](../../docs/guides/building-clients.md) - Production client development
   - [Security Model](../../docs/security/security-model.md) - Understanding TUF's security guarantees

3. **Production Integration**
   - Study the [API documentation](../../docs/api/) for advanced usage
   - Review [security best practices](../../docs/security/implementation-practices.md)
   - Consider [multi-repository setups](../MultiRepositoryClient/) for enhanced security

## Troubleshooting

**Build errors**: Ensure you have .NET 8.0 or later installed:
```bash
dotnet --version  # Should be 8.0.0 or higher
```

**Permission errors**: The tutorial uses temporary directories, so no special permissions should be needed.

**"File not found" errors**: This tutorial is completely self-contained and doesn't require external files or network access.

**Want to see the files created?**: The tutorial displays the temporary directory path where all files are created. You can browse these to see the exact TUF repository structure.

## Implementation Notes

This tutorial uses:
- **Ed25519 signatures** for all roles (fastest and most secure)
- **Local file:// URLs** to avoid network dependencies  
- **Temporary directories** to avoid cluttering your filesystem
- **Real cryptographic operations** - not mocked or simplified

The code demonstrates production-ready patterns while keeping the example simple and educational.

## Comparison with Other TUF Implementations

This tutorial provides similar functionality to:
- **Python TUF**: `basic_repository.py` and `basic_client.py` examples
- **Go TUF**: `basic_repository.go` and `client_example.go` examples  
- **AWS Tough**: Repository creation and client usage via `tuftool`

The .NET implementation achieves feature parity with these implementations while providing:
- **Type safety** through C#'s strong type system
- **Performance** through AOT compilation and zero-allocation patterns
- **Integration** with the broader .NET ecosystem and tooling