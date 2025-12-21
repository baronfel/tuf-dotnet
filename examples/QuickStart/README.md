# 🚀 5-Minute TUF Tutorial

**Get started with TUF .NET in under 5 minutes!** This tutorial demonstrates the complete end-to-end TUF (The Update Framework) workflow in a single, simple example.

## What You'll Learn

In just a few minutes, you'll:

✅ **Create a TUF repository** with cryptographic signatures  
✅ **Add a target file** with integrity metadata  
✅ **Download files securely** with automatic verification  
✅ **Understand TUF's security model** through hands-on experience  

## Quick Start

### Prerequisites

- .NET 8.0 or later
- 2-3 minutes of your time

### Run the Tutorial

```bash
# From the repository root
cd examples/QuickStart
dotnet run
```

That's it! The tutorial will:

1. **Generate cryptographic keys** for TUF roles
2. **Create a repository** with signed metadata
3. **Add a target file** with integrity hashes
4. **Download the file** using TUF client verification
5. **Show you exactly what happened** with detailed explanations

### Sample Output

```
🚀 Welcome to the 5-Minute TUF Tutorial!
========================================

📁 Working directory: /tmp/tuf-quickstart/12345678-1234-1234-1234-123456789abc

🔧 Step 1: Creating TUF Repository
----------------------------------
Generating cryptographic keys...
Building repository structure...
Signing metadata and writing files...
Repository structure created:
├── metadata/ (signed TUF metadata)
│   ├── root.json (root keys and roles)
│   ├── timestamp.json (freshness guarantee)
│   ├── snapshot.json (metadata consistency)
│   └── targets.json (file catalog)
└── targets/
    └── welcome-message.txt (your secure file!)
✅ Repository created successfully!

📥 Step 2: Using TUF Client
--------------------------
Setting up TUF client...
Configuring secure metadata source...
Refreshing TUF metadata from repository...
Looking up target file information...
Found target: welcome-message.txt
Size: 234 bytes
Hash algorithms: sha256
Downloading and verifying target file...
File saved to: /tmp/tuf-quickstart/.../client/targets/welcome-message.txt
Verified 234 bytes with cryptographic hashes

📄 Downloaded file contents:
┌─────────────────────────────────────────┐
│ 🎉 Congratulations!                     │
│                                         │
│ You successfully downloaded this file   │
│ using TUF (The Update Framework).       │
│                                         │
│ This file was:                          │
│ ✓ Cryptographically signed              │
│ ✓ Integrity-verified with SHA-256       │
│ ✓ Protected against tampering           │
│ ✓ Downloaded at 2024-12-21 10:30:15 UTC │
│                                         │
│ TUF .NET is working correctly! 🚀       │
└─────────────────────────────────────────┘
✅ File downloaded and verified successfully!

🎉 Tutorial Complete!
====================
What you just did:
• Created a TUF repository with cryptographic signatures
• Added a target file with integrity metadata
• Downloaded the file securely with TUF verification
• Protected against tampering, rollback, and other attacks

🔒 Security highlights:
• File integrity verified with cryptographic hashes
• Metadata signed with Ed25519 digital signatures
• Protected against man-in-the-middle attacks
• Automatic detection of compromised or stale metadata

📁 Explore the files created in: /tmp/tuf-quickstart/12345678-1234-1234-1234-123456789abc
```

## Command Line Options

```bash
# Run with debug information
dotnet run -- --debug

# Clean up temporary files after completion
dotnet run -- --cleanup

# Both options together
dotnet run -- --debug --cleanup
```

## What's Happening Under the Hood?

### Step 1: Repository Creation

The tutorial creates a minimal TUF repository with:

- **Ed25519 keys** for all roles (root, timestamp, snapshot, targets)
- **Signed metadata** files following TUF specification
- **One target file** with cryptographic hashes
- **30-day expiration** for reasonable tutorial timeframe

### Step 2: Secure Download

The TUF client performs these security steps:

1. **Validates root metadata** (trust anchor)
2. **Refreshes all metadata** with signature verification  
3. **Looks up target information** from signed targets metadata
4. **Downloads the file** from the targets directory
5. **Verifies integrity** using cryptographic hashes
6. **Detects any tampering** automatically

## Security Features Demonstrated

### 🛡️ **Attack Prevention**

- **Tampering attacks**: File hashes prevent modified content
- **Rollback attacks**: Version numbers prevent downgrade
- **Freeze attacks**: Expiration times prevent stale metadata
- **Mix-and-match attacks**: Snapshot consistency prevents inconsistent metadata

### 🔐 **Cryptographic Security**

- **Ed25519 signatures**: Fast, secure digital signatures
- **SHA-256 hashes**: Cryptographic file integrity verification
- **Key separation**: Different keys for different roles limit blast radius
- **Trust anchoring**: Root metadata establishes secure trust foundation

## Next Steps

### Explore More Examples

After completing this tutorial, check out:

- **[BasicClient](../BasicClient/)** - Simple TUF client with remote repositories
- **[RepositoryManager](../RepositoryManager/)** - Advanced repository creation and management
- **[CliTool](../CliTool/)** - Production-ready command-line TUF operations
- **[SigningDemo](../SigningDemo/)** - Cryptographic operations and key management

### Learn More About TUF

- **[TUF Specification](https://theupdateframework.io/specification/)** - Official TUF documentation
- **[TUF Security](https://theupdateframework.io/security/)** - Detailed security model
- **[Python TUF](https://github.com/theupdateframework/python-tuf)** - Reference implementation
- **[TUF Blog](https://theupdateframework.io/posts/)** - Latest TUF news and updates

### Production Considerations

This tutorial uses simplified settings for learning. For production:

- **Secure key storage**: Use HSMs or secure key vaults
- **Longer expiration times**: Balance security vs operational burden  
- **Role separation**: Use different keys/teams for different roles
- **Consistent snapshots**: Enable for high-availability deployments
- **Delegation**: Scale signing across multiple teams/systems

## Troubleshooting

### Common Issues

**Build errors**: Ensure .NET 8.0+ is installed and TUF project builds successfully
**Permission errors**: Check write permissions in temporary directory
**Network issues**: Tutorial uses local file:// URLs, no network required

### Getting Help

- **Example issues**: Check other examples in `../` directories
- **TUF .NET issues**: [GitHub Issues](https://github.com/baronfel/tuf-dotnet/issues)
- **TUF community**: [TUF community resources](https://theupdateframework.io/community/)

## Tutorial Architecture

```
QuickStart Tutorial
├── Program.cs              # Main tutorial implementation (228 lines)
├── QuickStart.csproj       # Project file with TUF dependencies
└── README.md               # This comprehensive documentation

Generated during tutorial:
└── /tmp/tuf-quickstart/{guid}/
    ├── repository/         # TUF repository
    │   ├── metadata/       # Signed TUF metadata files
    │   │   ├── root.json
    │   │   ├── timestamp.json
    │   │   ├── snapshot.json
    │   │   └── targets.json
    │   └── targets/        # Target files
    │       └── welcome-message.txt
    └── client/             # TUF client working directory
        ├── metadata/       # Downloaded metadata cache
        └── targets/        # Downloaded target files
```

This tutorial perfectly demonstrates TUF's security model in a beginner-friendly way while showcasing the power and simplicity of the TUF .NET implementation! 🎉

---

**Perfect for**: First-time TUF users, security demonstrations, integration testing, and understanding TUF fundamentals.