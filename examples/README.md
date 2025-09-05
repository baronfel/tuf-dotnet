# TUF .NET Examples

This directory contains comprehensive examples demonstrating how to use the TUF .NET library for secure software updates across different .NET application types and scenarios.

## 🚀 Quick Start

**New to TUF?** Follow this step-by-step guide:

1. **Learn the Basics**: Start with [BasicClient](./BasicClient/) to understand core TUF concepts
2. **See It in Action**: Run [RepositoryManager](./RepositoryManager/) to create your first TUF repository
3. **Build Something Real**: Try [AspNetCoreIntegration](./AspNetCoreIntegration/) for web applications
4. **Handle Production Scenarios**: Explore [ErrorHandlingDemo](./ErrorHandlingDemo/) for robust applications

## 📋 Available Examples

### [BasicClient](./BasicClient/)
A simple console application that demonstrates the fundamental TUF client workflow:
- Initializing a TUF updater
- Refreshing metadata from a remote repository
- Querying target file information
- Downloading target files with integrity verification

**Best for**: Getting started with TUF concepts and basic client operations.

### [CliTool](./CliTool/)
A comprehensive command-line interface built with `System.CommandLine` that provides:
- Multiple TUF operations (refresh, download, info)
- Proper command-line argument handling
- Production-ready error handling and output
- Local metadata and target caching

**Best for**: Understanding how to build production TUF applications or using as a development tool.

### [RepositoryManager](./RepositoryManager/)
A repository creation and management tool that demonstrates TUF repository setup:
- Complete TUF repository creation with all required metadata
- Key generation for different TUF roles (Ed25519 and RSA)
- Target file management and metadata signing
- Production-ready security warnings and best practices

**Best for**: Creating TUF repositories for testing and understanding repository management workflows.

### [SigningDemo](./SigningDemo/)
Demonstrates TUF signing capabilities and key management:
- Ed25519 and RSA-PSS key generation and signing
- Signature verification workflows
- Security best practices for key handling
- Integration patterns for signing operations

**Best for**: Understanding TUF cryptographic operations and implementing signing workflows.

### [AspNetCoreIntegration](./AspNetCoreIntegration/)
A complete ASP.NET Core web application demonstrating TUF integration patterns:
- Dependency injection setup for TUF services
- RESTful API endpoints for secure file distribution
- Health checks for TUF repository monitoring
- Production-ready error handling and logging
- Web interface for testing and demonstration

**Best for**: Web applications, APIs, microservices, and any server-side .NET application requiring secure file distribution.

### [ErrorHandlingDemo](./ErrorHandlingDemo/)
Comprehensive demonstration of TUF .NET's error handling and telemetry capabilities:
- 13 specific exception types for different error scenarios
- High-performance structured logging with LoggerMessage source generator
- System.Diagnostics.Activity integration for distributed tracing
- Security event logging for SIEM integration
- Production-ready configuration validation

**Best for**: Production applications requiring robust error handling, monitoring, and observability.

### [MultiRepositoryClient](./MultiRepositoryClient/)
Advanced client demonstrating multi-repository TUF workflows:
- Managing multiple TUF repositories simultaneously
- Repository prioritization and fallback strategies
- Consensus mechanisms and conflict resolution
- Performance optimizations for multiple sources

**Best for**: Applications that need to pull updates from multiple sources or require high availability through redundancy.

### [TufConformanceCli](./TufConformanceCli/)
Official TUF conformance testing CLI that implements the [CLIENT-CLI.md specification](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md):
- Standard `init`, `refresh`, and `download` commands
- Compatible with the official TUF conformance test suite
- Enables validation of .NET TUF implementation compliance
- Automated testing integration via GitHub Actions

**Best for**: TUF conformance testing, validation against the official TUF specification, and ensuring compatibility with other TUF implementations.

## 🏁 Complete Getting Started Walkthrough

### Step 1: Prerequisites
```bash
# Verify .NET 8.0 or later is installed
dotnet --version

# Clone the repository if you haven't already
git clone https://github.com/baronfel/tuf-dotnet.git
cd tuf-dotnet
```

### Step 2: Build All Examples
```bash
# Build the entire solution including all examples
dotnet build ./TUF.slnx --configuration Release

# Or build individual examples
dotnet build examples/BasicClient/
dotnet build examples/RepositoryManager/
dotnet build examples/AspNetCoreIntegration/
dotnet build examples/CliTool/
dotnet build examples/SigningDemo/
dotnet build examples/ErrorHandlingDemo/
dotnet build examples/MultiRepositoryClient/
```

### Step 3: Your First TUF Repository
Create a test repository to experiment with:
```bash
cd examples/RepositoryManager
dotnet run ./my-first-tuf-repo

# This creates:
# ./my-first-tuf-repo/metadata/  (TUF metadata files)
# ./my-first-tuf-repo/targets/   (Target files to distribute)
```

### Step 4: Download Files Securely
Use the basic client to download from your repository:
```bash
cd examples/BasicClient
dotnet run file://$(pwd)/../RepositoryManager/my-first-tuf-repo/metadata sample-file.txt
```

### Step 5: Try the Web Integration
Run the ASP.NET Core example for web-based scenarios:
```bash
cd examples/AspNetCoreIntegration
dotnet run

# Open https://localhost:5001 in your browser
# Try the endpoints like /secure-files and /tuf-status
```

### Step 6: Explore Advanced Features
```bash
# See comprehensive error handling
cd examples/ErrorHandlingDemo
dotnet run

# Try the full-featured CLI tool
cd examples/CliTool
dotnet run -- --help
dotnet run -- refresh --metadata-url file://$(pwd)/../RepositoryManager/my-first-tuf-repo/metadata
```

## 🔧 Common Usage Patterns

### Pattern 1: Console Application Updates
```csharp
var updater = new Updater(config);
await updater.RefreshAsync();
var appBytes = await updater.DownloadTargetAsync("myapp.exe");
```

### Pattern 2: Web API Secure Downloads  
```csharp
app.MapGet("/download/{file}", async (string file, ITufUpdater updater) => 
{
    var bytes = await updater.DownloadTargetAsync(file);
    return Results.File(bytes, "application/octet-stream", file);
});
```

### Pattern 3: Background Service Updates
```csharp
public class UpdateBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _updater.RefreshAsync();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
```

## Understanding TUF

The Update Framework (TUF) is a framework designed to secure software update systems. Key concepts:

- **Metadata**: Information about repository state, target files, and cryptographic keys
- **Roles**: Different types of metadata (root, timestamp, snapshot, targets) with specific responsibilities
- **Trusted Root**: The starting point of trust, distributed out-of-band with applications
- **Consistent Snapshots**: Ensures all metadata refers to the same repository state
- **Delegation**: Allows distributing signing responsibility across multiple parties

## Security Model

TUF protects against several classes of attacks:
- **Freeze attacks**: Prevented by metadata expiration times
- **Rollback attacks**: Prevented by version number verification
- **Indefinite freeze attacks**: Prevented by timestamp role freshness
- **Fast-forward attacks**: Prevented by consistent snapshot mechanisms
- **Mix-and-match attacks**: Prevented by snapshot role integrity
- **Wrong software attacks**: Prevented by targets role verification
- **Compromise resilience**: Multiple key compromises required for successful attacks

## Repository Structure

A TUF repository typically has this structure:
```
repository/
├── metadata/           # TUF metadata files
│   ├── root.json      # Root metadata (keys and roles)
│   ├── timestamp.json # Freshness guarantee
│   ├── snapshot.json  # Metadata consistency
│   └── targets.json   # Target file information
└── targets/           # Actual target files
    ├── file1.txt
    └── file2.exe
```

## Next Steps

1. **Start with BasicClient**: Understand the fundamental TUF workflow
2. **Explore CliTool**: See how to build production-ready TUF applications
3. **Read the TUF Specification**: https://theupdateframework.io/specification/
4. **Check out other implementations**: python-tuf, go-tuf for comparison
5. **Consider TUF for your applications**: Evaluate if TUF fits your update security needs

## Contributing

These examples are part of the effort to achieve parity with other TUF implementations. Contributions welcome:
- Additional examples for specific use cases
- Improvements to existing examples
- Better documentation and explanations
- Integration examples with popular .NET frameworks

## Resources

- [TUF Specification](https://theupdateframework.io/specification/)
- [TUF Website](https://theupdateframework.io/)
- [Python TUF (reference implementation)](https://github.com/theupdateframework/python-tuf)
- [Go TUF](https://github.com/theupdateframework/go-tuf)
- [TUF Conformance Test Suite](https://github.com/theupdateframework/tuf-conformance)