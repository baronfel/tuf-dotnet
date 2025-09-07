# TUF CLI Tool - Comprehensive Repository Management

This example demonstrates a complete command-line interface for both TUF client operations and repository management using the TUF .NET library.

## What This Example Shows

- **Client Operations**: Refresh, download, and inspect TUF repositories
- **Repository Management**: Initialize, manage keys, add targets, and validate repositories
- **Professional CLI**: Built with `System.CommandLine` following .NET CLI patterns
- **Production Ready**: Error handling, validation, and user-friendly output

## Prerequisites

- .NET 8.0 or later
- Access to a TUF repository (for client operations)
- Write access to filesystem (for repository management)

## Client Operations

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

## Repository Management 🆕

### Initialize Repository

Creates a complete TUF repository with all required metadata and keys:

```bash
dotnet run -- init-repo --repo-path ./my-tuf-repo
```

This command:
- Creates the standard TUF directory structure (`metadata/`, `targets/`, `keys/`)
- Generates Ed25519 keys for all roles (root, timestamp, snapshot, targets)
- Creates initial metadata files with 1-year expiration
- Sets up consistent snapshot mode
- Provides clear warnings about key storage security

### Generate Cryptographic Keys

Generate keys for specific TUF roles with different algorithms:

```bash
# Generate Ed25519 key (recommended)
dotnet run -- gen-keys --type ed25519 --output-dir ./keys --role root

# Generate RSA key (3072-bit)
dotnet run -- gen-keys --type rsa --output-dir ./keys --role timestamp

# Generate ECDSA key (P-256 curve)  
dotnet run -- gen-keys --type ecdsa --output-dir ./keys --role snapshot
```

Supported key types:
- **ed25519**: Fast, secure, recommended for new deployments
- **rsa**: Compatible with existing PKI infrastructure (minimum 2048-bit)
- **ecdsa**: NIST P-256 curve, good security/performance balance

### Add Target Files

Add files to your TUF repository:

```bash
dotnet run -- add-target \
  --repo-path ./my-tuf-repo \
  --file ./application.exe \
  --target-path "releases/v1.0/application.exe"
```

This copies the file to the repository's `targets/` directory. You'll need to update and re-sign metadata separately.

### Sign Metadata

Sign repository metadata with a specific key:

```bash
dotnet run -- sign \
  --repo-path ./my-tuf-repo \
  --key-path ./keys/root_ed25519_key.json \
  --role root
```

*Note: Full metadata update/signing is simplified in this example. Production implementations would handle metadata reconstruction and multi-signature workflows.*

### Validate Repository

Validate repository structure and metadata:

```bash
dotnet run -- validate --repo-path ./my-tuf-repo
```

This checks:
- Directory structure completeness
- Required metadata files presence
- Basic metadata parsing and validation
- Expiration warnings
- Signature counts

## Features Demonstrated

### Command-Line Interface
- Modern `System.CommandLine` usage with subcommands
- Comprehensive help text and validation
- Consistent option naming and behavior
- Visual feedback with emoji indicators

### TUF Client Operations
- **Metadata refresh**: Downloads and verifies all metadata files
- **Target download**: Securely downloads target files with verification
- **Repository inspection**: Shows repository and target information
- **Local caching**: Uses local directories for metadata and target storage

### TUF Repository Management 🆕
- **Repository initialization**: Complete setup with secure defaults
- **Multi-algorithm key generation**: Ed25519, RSA, ECDSA support
- **Target file management**: Add files with proper path handling
- **Metadata operations**: Signing workflow foundation
- **Repository validation**: Structure and content verification

### Production Quality
- Comprehensive error handling and user feedback
- Security warnings and best practices guidance
- Cross-platform compatibility
- AOT compilation ready

## Building and Distribution

```bash
# Build the CLI tool
dotnet build

# Create a standalone executable
dotnet publish -c Release --self-contained -r win-x64
dotnet publish -c Release --self-contained -r linux-x64
dotnet publish -c Release --self-contained -r osx-x64

# Create framework-dependent executable (smaller)
dotnet publish -c Release -r win-x64
```

## Usage Scenarios

### Development and Testing
```bash
# Quick repository setup for testing
dotnet run -- init-repo --repo-path ./test-repo
dotnet run -- validate --repo-path ./test-repo

# Add test files
echo "Hello TUF!" > test-file.txt
dotnet run -- add-target --repo-path ./test-repo --file test-file.txt --target-path "test-file.txt"
```

### Production Repository Management
```bash
# Generate production keys with proper storage
dotnet run -- gen-keys --type ed25519 --output-dir /secure/keys --role root
# (Move keys to HSM or secure storage)

# Initialize production repository
dotnet run -- init-repo --repo-path /var/tuf-repo

# Validate before deployment
dotnet run -- validate --repo-path /var/tuf-repo
```

### CI/CD Integration
```bash
# Automated secure downloads in build pipeline
dotnet run -- refresh --metadata-url $METADATA_URL --metadata-dir ./metadata --targets-dir ./targets --trusted-root $TRUSTED_ROOT
dotnet run -- download --target-file application.exe --metadata-url $METADATA_URL --metadata-dir ./metadata --targets-dir ./targets --trusted-root $TRUSTED_ROOT
```

## Comparison with Other TUF Implementations

This CLI tool provides functionality comparable to:

| Feature | TUF .NET CLI | python-tuf | go-tuf | tuf-js |
|---------|-------------|------------|--------|--------|
| Repository initialization | ✅ | ✅ | ✅ | ❌ |
| Multi-algorithm keys | ✅ Ed25519/RSA/ECDSA | ✅ Ed25519/RSA/ECDSA | ✅ Ed25519/RSA/ECDSA | ✅ Ed25519 |
| Target management | ✅ | ✅ | ✅ | ❌ |
| Metadata signing | 🔶 Basic | ✅ Full | ✅ Full | ❌ |
| Repository validation | ✅ | ✅ | ✅ | ❌ |
| Cross-platform | ✅ | ✅ | ✅ | ✅ |

## Security Considerations

### Key Management
- **Development**: Keys are stored as JSON files for simplicity
- **Production**: Use HSMs, encrypted storage, or external signing services
- **Key rotation**: Implement proper key rotation procedures
- **Backup**: Securely backup keys with proper access controls

### Repository Security
- Use HTTPS for all metadata URLs
- Verify trusted root metadata through out-of-band channels
- Monitor metadata expiration times
- Implement proper access controls on repository infrastructure
- Regular security audits and updates

### Operational Security
- Run validation checks regularly
- Monitor for unexpected metadata changes
- Implement alerting for expiring metadata
- Use infrastructure as code for reproducible deployments

## Next Steps

1. **Basic Usage**: Start with `init-repo` to create a test repository
2. **Key Management**: Implement secure key storage for production use
3. **Automation**: Integrate with CI/CD pipelines for automated operations
4. **Monitoring**: Add logging and metrics for production monitoring
5. **Advanced Features**: Implement delegations and multi-repository support

## Related Examples

- **`BasicClient`**: Simple TUF client for beginners
- **`RepositoryManager`**: Advanced repository management patterns
- **`SigningDemo`**: Detailed cryptographic signing examples
- **`MultiRepositoryClient`**: Multi-repository consensus operations

This CLI tool demonstrates the maturity of TUF .NET's repository management capabilities, bringing .NET to parity with mature TUF implementations in other languages.