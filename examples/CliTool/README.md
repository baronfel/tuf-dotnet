# TUF CLI Tool Example

This example demonstrates a complete command-line interface for TUF operations using the TUF .NET library.

## What This Example Shows

- How to build a comprehensive CLI tool using `System.CommandLine`
- Four main TUF operations: refresh, download, info, and inspect
- Proper command-line argument handling and validation
- Error handling and user-friendly output
- Advanced metadata inspection and validation capabilities

## Prerequisites

- .NET 8.0 or later
- Access to a TUF repository
- A trusted root metadata file

## Commands

### Refresh Metadata

Downloads and verifies the latest metadata from a TUF repository:

```bash
dotnet run -- refresh \
  --metadata-url https://example.com/metadata \
  --metadata-dir ./metadata \
  --targets-dir ./targets \
  --trusted-root ./trusted-root.json
```

### Download Target File

Downloads a specific target file with integrity verification:

```bash
dotnet run -- download \
  --metadata-url https://example.com/metadata \
  --metadata-dir ./metadata \
  --targets-dir ./targets \
  --trusted-root ./trusted-root.json \
  --target-file file.txt
```

### Show Repository Information

Displays information about the repository or a specific target file:

```bash
# Show repository information
dotnet run -- info \
  --metadata-url https://example.com/metadata \
  --metadata-dir ./metadata \
  --targets-dir ./targets \
  --trusted-root ./trusted-root.json

# Show information about a specific target
dotnet run -- info \
  --metadata-url https://example.com/metadata \
  --metadata-dir ./metadata \
  --targets-dir ./targets \
  --trusted-root ./trusted-root.json \
  --target-file file.txt
```

### Inspect Metadata Files

Analyzes and validates TUF metadata files locally (offline inspection):

```bash
# Basic metadata inspection
dotnet run -- inspect \
  --metadata-file ./metadata/root.json

# Detailed inspection with verbose output
dotnet run -- inspect \
  --metadata-file ./metadata/targets.json \
  --verbose

# Validate signatures (for root metadata, self-validation)
dotnet run -- inspect \
  --metadata-file ./metadata/root.json \
  --validate-signatures

# Validate signatures for non-root metadata (requires trusted root)
dotnet run -- inspect \
  --metadata-file ./metadata/timestamp.json \
  --validate-signatures \
  --trusted-root ./metadata/root.json
```

## Features Demonstrated

### Command-Line Interface
- Uses `System.CommandLine` for modern CLI development
- Proper option parsing and validation
- Help text and usage information
- Subcommands for different operations

### TUF Operations
- **Metadata refresh**: Downloads and verifies all metadata files
- **Target download**: Securely downloads target files with verification
- **Repository inspection**: Shows repository and target information
- **Metadata inspection**: Offline analysis and validation of metadata files
- **Local caching**: Uses local directories for metadata and target storage

### Error Handling
- User-friendly error messages
- Proper exception handling
- Visual indicators (✅ for success, ❌ for errors)

## Building and Distribution

```bash
# Build the CLI tool
dotnet build

# Create a standalone executable (optional)
dotnet publish -c Release --self-contained -r win-x64
dotnet publish -c Release --self-contained -r linux-x64
dotnet publish -c Release --self-contained -r osx-x64
```

## Usage in Production

This CLI tool can serve as:
1. **Development tool**: For testing TUF repositories during development
2. **CI/CD integration**: For automating secure downloads in build pipelines
3. **Template**: As a starting point for building your own TUF-enabled applications
4. **Troubleshooting**: For debugging TUF repository issues
5. **Security auditing**: For validating metadata files and signatures offline
6. **Metadata analysis**: For inspecting repository structure and metadata integrity

## Security Considerations

- Always obtain trusted root metadata through secure, out-of-band channels
- Verify the integrity of your trusted root file
- Use HTTPS URLs for metadata repositories
- Keep your trusted root metadata up to date with key rotations
- Monitor metadata expiration times

## Next Steps

- Explore the `BasicClient` example for a simpler introduction
- Review the TUF specification for deeper understanding
- Consider implementing additional commands for your specific use case