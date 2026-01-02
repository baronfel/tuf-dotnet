# TUF CLI Tool - Complete Repository Management

This example demonstrates a comprehensive command-line interface for complete TUF repository management and client operations, achieving **parity with mature TUF implementations** like tuftool (Rust) and RSTUF CLI (Python).

## What This Example Shows

- **Repository Management**: Complete lifecycle from initialization to maintenance
- **Cryptographic Key Operations**: Generate and manage signing keys for all TUF roles
- **Client Operations**: Secure metadata refresh, target download, and repository inspection
- **Production-Ready CLI**: Professional command-line interface using `System.CommandLine`
- **Cross-Platform Support**: Works on Windows, Linux, and macOS with native key storage

## Prerequisites

- .NET 8.0 or later
- Access to a TUF repository
- A trusted root metadata file

## Repository Management Commands

### Initialize Repository Structure

Creates the basic directory structure for a new TUF repository:

```bash
dotnet run -- init --output-dir ./my-tuf-repo
```

### Generate Cryptographic Keys

Generate signing keys for TUF repository roles:

```bash
# Generate Ed25519 key (recommended)
dotnet run -- generate-key --key-type ed25519 --output keys/root.pem

# Generate RSA key with custom size
dotnet run -- generate-key --key-type rsa --key-size 3072 --output keys/targets.pem

# Generate ECDSA P-256 key
dotnet run -- generate-key --key-type ecdsa --output keys/snapshot.pem
```

### Create New Repository

Create a complete TUF repository with metadata and targets:

```bash
dotnet run -- create \
  --output-dir ./my-tuf-repo \
  --root-key keys/root.pem \
  --targets-key keys/targets.pem \
  --snapshot-key keys/snapshot.pem \
  --timestamp-key keys/timestamp.pem \
  --targets-directory ./files-to-distribute \
  --expiry-days 365 \
  --consistent-snapshots true
```

### Update Existing Repository

Add new targets or update an existing repository:

```bash
dotnet run -- update \
  --repository-dir ./my-tuf-repo \
  --root-key keys/root.pem \
  --targets-key keys/targets.pem \
  --snapshot-key keys/snapshot.pem \
  --timestamp-key keys/timestamp.pem \
  --add-target new-file.txt \
  --add-target another-file.zip \
  --remove-target old-file.txt \
  --expiry-days 365
```

### Sign Metadata Files

Sign TUF metadata with private keys:

```bash
dotnet run -- sign \
  --metadata-file metadata/root.json \
  --key-file keys/root.pem \
  --output signed-root.json
```

## Client Commands

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

## Features Demonstrated

### Complete Repository Lifecycle Management
- **Repository initialization**: Creates proper directory structures and workflows
- **Key generation**: Support for Ed25519, RSA-PSS, and ECDSA P-256 cryptographic keys  
- **Repository creation**: Full metadata generation with all TUF roles (root, targets, snapshot, timestamp)
- **Repository updates**: Add/remove targets, refresh metadata with version increments
- **Metadata signing**: Sign individual metadata files with appropriate keys

### Advanced TUF Operations  
- **Consistent snapshots**: Support for consistent snapshot repositories
- **Multiple signature schemes**: Ed25519, RSA-PSS-SHA256, and ECDSA-SHA2-NISTP256
- **Configurable expiry**: Custom metadata expiration timeframes
- **Batch operations**: Multiple target addition/removal in single operations
- **Cross-platform key storage**: Platform-appropriate file permissions and key security

### Professional CLI Design
- **Modern CLI framework**: Uses `System.CommandLine` with comprehensive help system
- **Intuitive subcommands**: Organized command hierarchy matching established tools
- **Rich validation**: Proper option parsing, file validation, and error reporting
- **Interactive feedback**: Progress indicators and clear success/failure messaging
- **Shell completions**: Tab completion support for commands and options (when available)

### Production-Ready Security
- **Secure key handling**: Proper file permissions and key storage recommendations
- **Multiple algorithm support**: Industry-standard cryptographic algorithms
- **Metadata validation**: Complete TUF specification compliance
- **Error resilience**: Comprehensive error handling with actionable messages

## Building and Distribution

```bash
# Build the CLI tool
dotnet build

# Create a standalone executable (optional)
dotnet publish -c Release --self-contained -r win-x64
dotnet publish -c Release --self-contained -r linux-x64
dotnet publish -c Release --self-contained -r osx-x64
```

## Usage Scenarios

### Repository Management
1. **TUF Repository Setup**: Initialize and configure new TUF repositories from scratch
2. **Key Management**: Generate and manage cryptographic keys for repository signing
3. **Content Distribution**: Add/remove software packages and maintain metadata integrity
4. **Repository Maintenance**: Update expiry dates, rotate keys, and manage repository lifecycle

### Client Operations
5. **Secure Downloads**: Integrate TUF-secured downloads into applications and scripts
6. **CI/CD Pipelines**: Automate secure software distribution in build and deployment processes
7. **Development Testing**: Test TUF repositories during development and staging
8. **Repository Validation**: Inspect and troubleshoot TUF repository configurations

### Production Deployment
9. **Enterprise Software Distribution**: Deploy internal software with TUF security guarantees
10. **Package Repositories**: Create secure package repositories (similar to APT, YUM, npm)
11. **Container Image Distribution**: Secure container image distribution with metadata integrity
12. **Firmware Updates**: Implement secure firmware update mechanisms

## Security Considerations

- Always obtain trusted root metadata through secure, out-of-band channels
- Verify the integrity of your trusted root file
- Use HTTPS URLs for metadata repositories
- Keep your trusted root metadata up to date with key rotations
- Monitor metadata expiration times

## Quick Start Example

Here's a complete example of setting up a TUF repository:

```bash
# 1. Initialize repository structure
dotnet run -- init --output-dir ./my-repo

# 2. Generate keys for all roles  
dotnet run -- generate-key --key-type ed25519 --output ./my-repo/keys/root.pem
dotnet run -- generate-key --key-type ed25519 --output ./my-repo/keys/targets.pem  
dotnet run -- generate-key --key-type ed25519 --output ./my-repo/keys/snapshot.pem
dotnet run -- generate-key --key-type ed25519 --output ./my-repo/keys/timestamp.pem

# 3. Create some files to distribute
mkdir -p ./my-repo/files
echo "Hello, secure world!" > ./my-repo/files/hello.txt
echo "Sample application v1.0" > ./my-repo/files/app.txt

# 4. Create the repository
dotnet run -- create \
  --output-dir ./my-repo \
  --root-key ./my-repo/keys/root.pem \
  --targets-key ./my-repo/keys/targets.pem \
  --snapshot-key ./my-repo/keys/snapshot.pem \
  --timestamp-key ./my-repo/keys/timestamp.pem \
  --targets-directory ./my-repo/files

# 5. Use the repository with TUF client commands
dotnet run -- info \
  --metadata-url file://$(pwd)/my-repo/metadata \
  --metadata-dir ./client-cache/metadata \
  --targets-dir ./client-cache/targets \
  --trusted-root ./my-repo/metadata/root.json
```

## Comparison with Other TUF Implementations

| Feature | TUF .NET CLI | tuftool (Rust) | RSTUF CLI (Python) |
|---------|-------------|----------------|-------------------|
| Repository Creation | ✅ | ✅ | ✅ |
| Key Generation | ✅ | ✅ | ✅ |  
| Multiple Signature Schemes | ✅ (Ed25519, RSA, ECDSA) | ✅ | ✅ |
| Repository Updates | ✅ | ✅ | ✅ |
| Consistent Snapshots | ✅ | ✅ | ✅ |
| Cross-Platform | ✅ | ✅ | ✅ |
| AOT Compilation | ✅ | ✅ | ❌ |
| Interactive Repository Setup | ✅ | ❌ | ✅ |
| Client Operations | ✅ | ❌ | ❌ |

## Next Steps

- **Explore Advanced Features**: Review other examples like `MultiRepositoryClient` for complex scenarios
- **Production Deployment**: Refer to security documentation for production key management
- **Custom Integration**: Use this CLI as a template for building TUF-enabled applications
- **Community Contribution**: This enhancement contributes to achieving parity with mature TUF implementations