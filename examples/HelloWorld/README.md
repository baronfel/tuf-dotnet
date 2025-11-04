# TUF Hello World Example

**The simplest possible TUF client** - demonstrates core security features in under 5 minutes.

## What Makes This Special?

This isn't just another "Hello World" - it's a **complete secure update client** that:

- 🔐 **Verifies cryptographic signatures** on all metadata
- 🛡️ **Prevents rollback attacks** (downgrade protection)
- 🔍 **Validates file integrity** using cryptographic hashes
- 📦 **Downloads only verified files** that match the metadata

**Even if an attacker controlled your update server**, they couldn't serve you malware, prevent updates, or trick you into downgrading.

## Running This Example

```bash
# Clone and navigate to the example
cd examples/HelloWorld

# Build and run
dotnet build
dotnet run
```

**Expected output:**
```
🔐 TUF Hello World - The Simplest Secure Update Client
=====================================================

Step 1: Setting up TUF client...
✅ TUF client configured

Step 2: Refreshing metadata (downloading & verifying signatures)...
✅ Metadata verified successfully!

Step 3: Looking for target file 'hello.txt'...
✅ Found target: hello.txt
   Size: 19 bytes
   Hashes: sha256:a1b2c3d4e5f6...

Step 4: Securely downloading file...
✅ Downloaded to: /tmp/tuf-hello-world/.../targets/hello.txt

Step 5: Displaying file contents...
📄 File content: "Hello from TUF .NET!"

🎉 SUCCESS! You've just:
   ✅ Verified cryptographic signatures on metadata
   ✅ Protected against rollback attacks  
   ✅ Validated file integrity with cryptographic hashes
   ✅ Securely downloaded a verified file

🛡️  Even if an attacker controlled the repository, they couldn't:
   🚫 Serve you malware (hash verification would fail)
   🚫 Downgrade your files (version checks prevent this) 
   🚫 Mix old and new files (consistency checks prevent this)
```

## Understanding the Code

This example demonstrates the **core TUF client workflow**:

1. **Initialize** - Create an `Updater` with trusted root metadata
2. **Refresh** - Download and verify current metadata from repository
3. **Query** - Get information about target files
4. **Download** - Securely download files with integrity verification

```csharp
// 1. Configure TUF client
var config = new UpdaterConfig(trustedRoot, metadataUrl) { /* ... */ };
var updater = new Updater(config);

// 2. Refresh metadata (cryptographic verification happens here)
await updater.RefreshAsync();

// 3. Query for target file
var (path, info) = await updater.GetTargetInfo("hello.txt");

// 4. Download securely
var result = await updater.DownloadTarget(info, path, null, null);
```

## Self-Contained Demo

This example creates its own minimal TUF repository for demonstration. **In production**, you would:

- Have a real TUF repository with proper cryptographic signatures
- Distribute a trusted root metadata file securely with your application  
- Point to your actual update server URL

## Next Steps

### 📚 Learn More
- **[TUF in 5 Minutes Tutorial](../../docs/HelloWorld.md)** - Complete step-by-step guide
- **[BasicClient Example](../BasicClient/)** - Production-ready client with error handling
- **[RepositoryManager Example](../RepositoryManager/)** - Create and manage TUF repositories

### 🔧 Production Usage
- **[TUF Specification](https://theupdateframework.io/specification/)** - Official TUF documentation
- **[Security Model](https://theupdateframework.io/security/)** - Understanding TUF's security guarantees
- **[.NET TUF Documentation](../../README.md)** - Full library documentation

### 🚀 Advanced Examples
- **[CliTool](../CliTool/)** - Complete command-line interface
- **[SigningDemo](../SigningDemo/)** - Cryptographic signing and key management
- **[ErrorHandlingDemo](../ErrorHandlingDemo/)** - Robust error handling patterns

---

**🎯 You've just implemented enterprise-grade update security!**

This simple example provides the same security guarantees used by:
- 🐍 Python package managers (pip, conda)
- 🦀 Rust package manager (cargo) 
- 🐧 Linux distributions (Debian, Ubuntu)
- ☁️ Cloud platforms (AWS, Azure, GCP)

> AI-generated content by [achieve-parity](https://github.com/baronfel/tuf-dotnet/actions/runs/19063437581) may contain mistakes.