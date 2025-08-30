# TUF Repository Manager CLI

A command-line tool for creating and managing TUF (The Update Framework) repositories. This tool demonstrates repository management capabilities that are essential for any TUF implementation.

## Features

- ✅ **Repository Creation**: Create complete TUF repositories with all required metadata
- ✅ **Key Generation**: Generate signing keys for different TUF roles  
- ⚠️ **Target Management**: Basic framework (full implementation pending)
- 🔒 **Security Warnings**: Clear guidance on production security requirements

## Usage

### Create a TUF Repository

```bash
# Create a basic repository
dotnet run -- create --output ./my-repo

# Create with custom expiry (1 month from now)
dotnet run -- create --output ./my-repo --expiry "2025-10-01"

# Create without consistent snapshots
dotnet run -- create --output ./my-repo --consistent-snapshots false
```

This creates a complete TUF repository structure:
```
my-repo/
├── metadata/           # TUF metadata files
│   ├── root.json      # Root metadata (keys and roles)
│   ├── timestamp.json # Freshness guarantee
│   ├── snapshot.json  # Metadata consistency
│   └── targets.json   # Target file information
└── targets/           # Target files
    └── hello.txt      # Sample target file
```

### Generate Signing Keys

```bash
# Generate Ed25519 keys (default, recommended)
dotnet run -- generate-keys --output ./keys

# Generate RSA keys
dotnet run -- generate-keys --output ./keys --type rsa
```

Creates key files for all TUF roles:
- `root_public.json` / `root_private.key`
- `timestamp_public.json` / `timestamp_private.key`  
- `snapshot_public.json` / `snapshot_private.key`
- `targets_public.json` / `targets_private.key`

⚠️ **Note**: The current implementation generates placeholder private key files. In production, implement secure key storage with proper encryption.

### Add Target Files (Planned)

```bash
# This functionality is planned but not yet implemented
dotnet run -- add-target --repository ./my-repo --file ./myapp.exe --target-path "v1.0/myapp.exe"
```

## Security Considerations

This CLI tool is designed for **demonstration and development purposes**. For production use:

### 🔒 **Key Management**
- **Ephemeral keys**: Current implementation generates temporary keys for demos
- **Secure storage**: Implement encrypted private key storage (PKCS#8, PEM with passphrase)
- **Hardware Security Modules**: Use HSMs for root and targets keys
- **Key separation**: Keep root keys offline, automate timestamp/snapshot signing

### 🔐 **Signing Workflow** 
- **Threshold signatures**: Support multiple signers per role
- **Key rotation**: Implement proper key rotation procedures
- **Audit trails**: Log all signing operations
- **Access control**: Restrict signing key access

### 📦 **Repository Security**
- **Consistent snapshots**: Always enable for production (prevents rollback attacks)
- **Metadata expiry**: Set appropriate expiration times
- **Mirror validation**: Verify repository mirrors
- **Transport security**: Use HTTPS for repository distribution

## Integration with TUF Ecosystem

This tool addresses **repository management** gaps identified in the TUF .NET parity analysis:

- ✅ **Repository Creation**: Comparable to python-tuf's `create_repository()`
- ✅ **Metadata Generation**: Complete TUF metadata workflow
- ✅ **Key Integration**: Works with Ed25519 and RSA signing
- ✅ **Developer Experience**: Clear CLI interface matching other implementations

## Development Notes

### Architecture

The CLI uses the `TUF.Repository.RepositoryBuilder` class, which provides:
- Fluent API for repository configuration
- Automatic metadata generation and signing
- Support for multiple signers per role
- Proper TUF specification compliance

### Testing

```bash
# Build and test the CLI
dotnet build
dotnet run -- create --output ./test-repo

# Verify repository structure
ls -la test-repo/metadata/
file test-repo/metadata/*.json
```

### Future Enhancements

1. **Target Management**: Full implementation of adding/removing targets
2. **Key Import/Export**: Support for standard key formats (PEM, PKCS#8)
3. **Remote Repositories**: Direct integration with repository hosting
4. **Delegation Support**: Advanced delegation workflows
5. **Batch Operations**: Bulk target management
6. **Configuration Files**: Repository templates and presets

## Examples

### Basic Workflow

```bash
# 1. Generate keys (development only)
dotnet run -- generate-keys --output ./keys

# 2. Create repository
dotnet run -- create --output ./my-software-repo

# 3. Distribute repository
# Copy my-software-repo/ to your CDN or file server

# 4. Clients can now use the TUF BasicClient example to fetch updates
```

### Integration with Other Examples

This repository manager creates repositories that work with other TUF .NET examples:

```bash
# Create repository
dotnet run --project examples/RepositoryManager -- create --output ./demo-repo

# Use with BasicClient
dotnet run --project examples/BasicClient -- file:///path/to/demo-repo/metadata hello.txt

# Use with CLI Tool  
dotnet run --project examples/CliTool -- --metadata-url file:///path/to/demo-repo/metadata refresh
```

## Contributing

This CLI is part of the TUF .NET parity improvement effort. Areas for contribution:
- Enhanced key management workflows
- Integration with cloud signing services
- Advanced delegation support
- Repository validation and health checks
- Performance optimizations for large repositories

## Resources

- [TUF Specification](https://theupdateframework.io/specification/)
- [TUF Repository Management Best Practices](https://theupdateframework.io/security/)
- [Python TUF Repository API](https://github.com/theupdateframework/python-tuf)
- [Go TUF Repository Examples](https://github.com/theupdateframework/go-tuf)

---

> This tool contributes to **TUF .NET parity** by providing repository management capabilities comparable to other TUF implementations.