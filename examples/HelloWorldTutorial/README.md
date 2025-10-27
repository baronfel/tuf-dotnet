# Hello World Tutorial - Complete TUF Example

This example provides a complete, self-contained demonstration of TUF (The Update Framework) that works without any external dependencies or pre-existing repositories.

## What This Example Shows

- ✅ **Complete TUF Repository Creation** - Generate keys, build metadata, sign all files
- ✅ **Secure File Downloads** - Download and verify files with full cryptographic protection
- ✅ **End-to-End Workflow** - From repository creation to secure client downloads
- ✅ **All TUF Security Guarantees** - Protection against tampering, rollbacks, and attacks

## Perfect for Beginners

This example is designed to be the **first thing you run** after installing TUF .NET. No configuration needed!

## Running the Example

```bash
# Navigate to the example
cd examples/HelloWorldTutorial

# Run the complete example
dotnet run
```

Expected output:
```
🚀 TUF .NET Hello World - Complete Tutorial
===========================================

📁 Creating TUF repository...
  • Created target file: hello.txt
  • Generated cryptographic keys (Ed25519)
  • Added target file to repository metadata
  • Built and signed all TUF metadata
  • Repository ready at: ./repository/
✅ Step 1: Created TUF repository

🔄 Using TUF client to download files...
  • Loaded trusted root metadata
  • Configured TUF client
  • Refreshed and verified metadata
  • Found target: hello.txt
    Length: 145 bytes
    Hashes: sha256
  • Downloaded to: client-cache/targets/hello.txt
  • Verified 145 bytes

📄 Downloaded file content:
   Hello, TUF World! 🌍
   
   This file was securely downloaded using The Update Framework.
   It's cryptographically verified and protected against tampering!

✅ Step 2: TUF client downloaded and verified file

🎉 Success! You've completed the TUF Hello World tutorial!
```

## What Just Happened?

### Step 1: Repository Creation
1. **Created target file** - A simple text file to distribute securely
2. **Generated Ed25519 keys** - Cryptographic keys for all TUF roles
3. **Built TUF metadata** - Created root.json, targets.json, snapshot.json, timestamp.json
4. **Signed everything** - Applied cryptographic signatures to all metadata

### Step 2: Secure Client Download
1. **Loaded trusted root** - Established the foundation of trust
2. **Refreshed metadata** - Downloaded and verified all current metadata
3. **Found target** - Located the file in the repository
4. **Downloaded securely** - Retrieved file with full cryptographic verification

## Generated File Structure

After running, you'll have:

```
HelloWorldTutorial/
├── repository/              # Complete TUF Repository
│   ├── metadata/
│   │   ├── root.json       # Trusted keys and roles
│   │   ├── targets.json    # File information and hashes
│   │   ├── snapshot.json   # Consistency metadata
│   │   └── timestamp.json  # Freshness metadata
│   └── targets/
│       └── hello.txt       # The actual target file
└── client-cache/           # Client download cache
    ├── metadata/           # Cached metadata
    └── targets/
        └── hello.txt       # Downloaded and verified file
```

## Security Features Demonstrated

- ✅ **Cryptographic Verification** - All downloads are hash-verified
- ✅ **Signature Validation** - All metadata is cryptographically signed
- ✅ **Rollback Protection** - Version numbers prevent downgrade attacks
- ✅ **Compromise Resilience** - Multiple keys limit damage from breaches
- ✅ **Consistency Guarantees** - Snapshot metadata ensures consistency

## Key TUF Concepts

This example demonstrates all core TUF concepts:

- **🔑 Keys & Signatures** - Ed25519 cryptographic keys for signing
- **📄 Metadata Files** - The four core TUF metadata types
- **🔒 Trust Chain** - How trust flows from root to individual files
- **✅ Verification** - Multi-layer verification of downloads
- **🛡️ Attack Prevention** - Protection against common attack vectors

## Next Steps

Now that you understand TUF basics:

1. **📚 Deep Dive** - Read the [Hello World Tutorial Guide](../../docs/guides/hello-world-tutorial.md)
2. **🚀 Production** - Check out [Quick Start Guide](../../docs/guides/quick-start.md) for real-world usage
3. **🏗️ Advanced** - Explore [RepositoryManager](../RepositoryManager/) for complex repositories
4. **🌐 Multi-Repo** - Try [MultiRepositoryClient](../MultiRepositoryClient/) for advanced scenarios

## Troubleshooting

**Build errors:**
```bash
dotnet restore  # Restore dependencies
dotnet build    # Build the project
```

**Runtime errors:**
- Ensure .NET 8.0+: `dotnet --version`
- Check write permissions in current directory
- Make sure the TUF project builds successfully

**Understanding errors:**
- All TUF operations include detailed error messages
- Check the console output for specific guidance
- Review the generated files to understand the repository structure

This example provides a complete foundation for understanding TUF and building secure update systems in .NET!