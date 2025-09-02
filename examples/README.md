# TUF .NET Examples

This directory contains examples demonstrating how to use the TUF .NET library for secure software updates.

## Available Examples

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

### [SigningDemo](./SigningDemo/)
Demonstrates TUF signing capabilities and key management:
- Ed25519 and RSA-PSS key generation and signing
- Signature verification workflows
- Security best practices for key handling
- Integration patterns for signing operations

**Best for**: Understanding TUF cryptographic operations and implementing signing workflows.

### [TufConformanceCli](./TufConformanceCli/)
Official TUF conformance testing CLI that implements the [CLIENT-CLI.md specification](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md):
- Standard `init`, `refresh`, and `download` commands
- Compatible with the official TUF conformance test suite
- Enables validation of .NET TUF implementation compliance
- Automated testing integration via GitHub Actions

**Best for**: TUF conformance testing, validation against the official TUF specification, and ensuring compatibility with other TUF implementations.

## Getting Started

1. **Prerequisites**
   - .NET 8.0 or later
   - Access to a TUF repository (for testing, you can use the examples with placeholder data)

2. **Build Examples**
   ```bash
   # From the repository root
   dotnet build examples/BasicClient/
   dotnet build examples/CliTool/
   dotnet build examples/SigningDemo/
   dotnet build examples/TufConformanceCli/
   ```

3. **Run Examples**
   ```bash
   # Basic client example
   cd examples/BasicClient
   dotnet run https://example.com/metadata file.txt

   # CLI tool example
   cd examples/CliTool
   dotnet run -- --help
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