# TUF .NET Documentation

Welcome to the comprehensive documentation for **TUF .NET**, a complete implementation of [The Update Framework (TUF)](https://theupdateframework.io/) for .NET applications.

## What is TUF?

The Update Framework (TUF) is a framework designed to secure software update systems. It protects against various classes of attacks including:

- **Rollback attacks** - Preventing downgrades to vulnerable versions
- **Freeze attacks** - Ensuring updates don't get indefinitely delayed  
- **Mix-and-match attacks** - Preventing inconsistent metadata
- **Wrong software attacks** - Ensuring only legitimate software is installed
- **Compromise resilience** - Requiring multiple key compromises for successful attacks

## Why TUF .NET?

TUF .NET brings enterprise-grade secure update capabilities to the .NET ecosystem with:

- ✅ **Full TUF specification compliance** - Passes official TUF conformance tests
- ✅ **Production-ready** - HTTP resilience, comprehensive error handling, logging integration
- ✅ **Performance optimized** - AOT compatible, minimal allocations, startup optimized
- ✅ **Modern .NET** - Async/await, nullable reference types, native logging
- ✅ **Comprehensive examples** - From basic client to multi-repository scenarios

## Quick Start

### Installation

```bash
# Add TUF .NET to your project
dotnet add package TUF

# For canonical JSON functionality
dotnet add package CanonicalJson
```

### Basic Usage

```csharp
using TUF;

// Initialize TUF client with trusted root metadata
var config = new UpdaterConfig(rootBytes, new Uri("https://repo.example.com/metadata/"))
{
    LocalMetadataDir = "./cache/metadata",
    LocalTargetsDir = "./cache/targets",
    Client = new HttpClient()
};

var updater = new Updater(config);

// Refresh metadata from repository
await updater.RefreshAsync();

// Get target file information
var targetInfo = await updater.GetTargetInfo("app.exe");
if (targetInfo.HasValue)
{
    var (remotePath, targetFile) = targetInfo.Value;
    
    // Download and verify target file
    var (localPath, data) = await updater.DownloadTarget(targetFile, "app.exe");
    Console.WriteLine($"Downloaded and verified {localPath}");
}
```

## Documentation Sections

### [API Reference](api/)
Complete API documentation for all TUF .NET classes and methods.

### [Developer Guides](articles/)
In-depth guides covering:
- **Getting Started** - Your first TUF integration
- **Repository Management** - Creating and maintaining TUF repositories  
- **Advanced Features** - Multi-repository, delegations, custom metadata
- **Security Best Practices** - Key management, production deployment
- **Migration Guides** - Moving from other TUF implementations

### [Examples](https://github.com/baronfel/tuf-dotnet/tree/main/examples)
Complete working examples including:
- **BasicClient** - Simple TUF client implementation
- **RepositoryManager** - Creating TUF repositories
- **MultiRepositoryClient** - TAP 4 multi-repository support
- **ASP.NET Core Integration** - Web application patterns

## Architecture Overview

TUF .NET is built with a modular architecture:

```
┌─────────────────┐    ┌──────────────────┐
│   Applications  │    │     Examples     │
└─────────┬───────┘    └─────────┬────────┘
          │                      │
┌─────────▼──────────────────────▼────────┐
│              TUF Core Library           │
│  ┌─────────────┐  ┌─────────────────────┐│
│  │   Updater   │  │  MultiRepository    ││
│  │             │  │     Client          ││
│  └─────────────┘  └─────────────────────┘│
│  ┌─────────────┐  ┌─────────────────────┐│
│  │   Metadata  │  │     Repository      ││
│  │   Models    │  │     Builder         ││
│  └─────────────┘  └─────────────────────┘│
└─────────┬──────────────────────────────────┘
          │
┌─────────▼───────┐
│ CanonicalJson   │
│    Library      │
└─────────────────┘
```

## Key Features

### 🔒 **Security First**
- Ed25519, RSA-PSS, and ECDSA signature support
- Configurable key thresholds and rotation
- Protection against all TUF-specified attack classes
- Comprehensive input validation and error handling

### ⚡ **Performance Optimized**
- AOT compilation support for fast startup
- Memory-efficient canonical JSON serialization
- Minimal allocation patterns for high-throughput scenarios
- Async/await throughout for non-blocking operations

### 🏗️ **Production Ready**
- HTTP resilience with retry policies and timeouts
- Structured logging integration via Microsoft.Extensions.Logging  
- Comprehensive error handling with specific exception types
- Configuration validation with sensible defaults

### 🌟 **Developer Friendly**
- Fluent configuration APIs
- Rich examples and documentation
- Visual Studio integration-ready
- Clear migration paths from other implementations

## Getting Help

- **Issues & Bugs**: [GitHub Issues](https://github.com/baronfel/tuf-dotnet/issues)
- **Discussions**: [GitHub Discussions](https://github.com/baronfel/tuf-dotnet/discussions)
- **Security Issues**: See [Security Policy](https://github.com/baronfel/tuf-dotnet/security/policy)
- **TUF Specification**: [Official TUF Documentation](https://theupdateframework.io/specification/)

## Contributing

TUF .NET is an open-source project welcoming contributions! See the [Contributing Guide](https://github.com/baronfel/tuf-dotnet/blob/main/CONTRIBUTING.md) for details on:

- Code contributions and pull requests
- Documentation improvements
- Example additions and enhancements
- Performance optimizations
- Cross-platform testing and validation

## License

TUF .NET is licensed under the [MIT License](https://github.com/baronfel/tuf-dotnet/blob/main/LICENSE).