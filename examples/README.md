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

### [TufConformanceCli](./TufConformanceCli/)
A specialized CLI tool that implements the [TUF conformance test suite protocol](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md):
- Implements `init`, `refresh`, and `download` commands per conformance specification
- Enables .NET TUF implementation to participate in official TUF conformance testing
- Follows exact client-under-test protocol for compatibility testing
- Provides foundation for systematic TUF protocol validation

**Best for**: TUF conformance testing, validating implementation correctness, and interoperability testing.

## Getting Started

1. **Prerequisites**
   - .NET 8.0 or later
   - Access to a TUF repository (for testing, you can use the examples with placeholder data)

2. **Build Examples**
   ```bash
   # From the repository root
   dotnet build examples/BasicClient/
   dotnet build examples/CliTool/
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

   # TUF conformance CLI
   cd examples/TufConformanceCli
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