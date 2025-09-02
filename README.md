# TUF for .NET

A comprehensive .NET implementation of [The Update Framework (TUF)](https://theupdateframework.io/), providing secure software update capabilities and utilities for canonical JSON handling.

[![Build Status](https://github.com/baronfel/tuf-dotnet/actions/workflows/build.yml/badge.svg)](https://github.com/baronfel/tuf-dotnet/actions)
[![NuGet Version](https://img.shields.io/nuget/v/TUF.svg)](https://www.nuget.org/packages/TUF/)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-blue.svg)](https://dotnet.microsoft.com/)

## Overview

TUF .NET brings enterprise-grade security to software distribution in the .NET ecosystem. It protects against repository compromise, man-in-the-middle attacks, rollback attacks, and other threats that traditional update mechanisms cannot defend against.

**Key Features:**
- 🔐 **Complete TUF specification implementation** with full security guarantees
- 🚀 **High-performance** canonical JSON handling optimized for .NET
- 🛠️ **Production-ready** with comprehensive error handling and logging
- 🔑 **Multi-algorithm support** (Ed25519, RSA-PSS, ECDSA)
- 🌐 **Multi-repository support** (TAP 4) for enhanced security
- ⚡ **AOT compatible** for minimal startup overhead
- 🧪 **Conformance tested** against official TUF specification

## Quick Start

### Installation
```shell
dotnet add package TUF
```

### Basic Usage
```csharp
using TUF;

// Configure TUF client
var config = new UpdaterConfig
{
    LocalTrustedRoot = await File.ReadAllBytesAsync("trusted-root.json"),
    RemoteMetadataUrl = new Uri("https://example.com/metadata/"),
    LocalMetadataDir = "./metadata",
    LocalTargetsDir = "./downloads",
    Client = new HttpClient()
};

var updater = new Updater(config);

// Securely download and verify files
await updater.RefreshAsync();
var targetInfo = await updater.GetTargetInfo("app.exe");
if (targetInfo.HasValue)
{
    var (localPath, data) = await updater.DownloadTarget(
        targetInfo.Value.File, "app.exe");
    Console.WriteLine($"Downloaded {data.Length} bytes to {localPath}");
}
```

## Documentation

### 📚 **Getting Started**
- **[Quick Start Guide](docs/guides/quick-start.md)** - Get up and running in 5 minutes
- **[What is TUF?](docs/guides/what-is-tuf.md)** - Understanding the security framework
- **[Core Concepts](docs/guides/core-concepts.md)** - Essential TUF concepts for .NET developers

### 📖 **API Documentation**
- **[Updater API](docs/api/updater.md)** - Primary client for TUF operations
- **[Repository Builder](docs/api/repository-builder.md)** - Create and manage TUF repositories  
- **[Multi-Repository Client](docs/api/multi-repository-client.md)** - Multi-repository consensus (TAP 4)
- **[Complete API Reference](docs/api/)** - Comprehensive API documentation

### 🛡️ **Security**
- **[Security Model](docs/security/security-model.md)** - Comprehensive security documentation
- **[Threat Analysis](docs/security/threat-analysis.md)** - Attack prevention and detection
- **[Best Practices](docs/security/implementation-practices.md)** - Secure implementation guidance

### 📋 **Guides**
- **[Building Clients](docs/guides/building-clients.md)** - Production client development
- **[Creating Repositories](docs/guides/creating-repositories.md)** - Repository setup and management
- **[Migration Guide](docs/guides/migration.md)** - Migrating from other TUF implementations
- **[All Guides](docs/guides/)** - Complete guide directory

## Project Structure

Root solution: `TUF.slnx` (requires .NET 10 SDK, libraries target .NET 8+)

### Core Libraries
- **`TUF/`** — Complete TUF implementation with client, repository, and signing APIs
- **`CanonicalJson/`** — High-performance canonical JSON serialization

### Examples & Tools
- **`examples/BasicClient/`** — Simple TUF client demonstration
- **`examples/RepositoryManager/`** — Create and manage TUF repositories
- **`examples/MultiRepositoryClient/`** — Multi-repository consensus example
- **`examples/TufConformanceCli/`** — TUF conformance testing CLI
- **`examples/SigningDemo/`** — Cryptographic signing demonstrations
- **`examples/CliTool/`** — Command-line TUF operations interface

### Testing
- **`TUF.Tests/`** — Comprehensive unit and integration tests
- **`CanonicalJson.Tests/`** — Canonical JSON serialization tests  
- **`TUF.ConformanceTests/`** — Official TUF specification conformance tests

## Development

### Prerequisites
- .NET SDK 10.0+ (for solution format - libraries target .NET 8+)
- Modern operating system (Windows, macOS, or Linux)

### Build
```shell
# Restore and build all projects
dotnet restore
dotnet build --configuration Release

# Run all tests
dotnet test --configuration Release

# Build specific examples
cd examples/BasicClient
dotnet build
```

### Examples
```shell
# Create a TUF repository
cd examples/RepositoryManager
dotnet run ./my-repo

# Use the repository with a client
cd examples/BasicClient
dotnet run file://./my-repo/metadata app.exe

# Try multi-repository consensus
cd examples/MultiRepositoryClient
dotnet run

# Explore CLI operations
cd examples/CliTool
dotnet run -- --help
```

## Contributing

1. Fork the repository and create a feature branch.
2. Add tests for new behavior or bug fixes.
3. Run `dotnet build` and `dotnet test` locally.
4. Open a pull request with a concise description of changes.