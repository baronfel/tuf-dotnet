# TUF Hello World Example

**The fastest way to understand TUF security in action!**

This example demonstrates TUF (The Update Framework) from zero to working in under 5 minutes. Perfect for developers who want to quickly understand TUF's security benefits or evaluate it for their projects.

## What This Example Shows

✅ **Complete TUF workflow** - Repository creation, metadata signing, and secure client downloads  
✅ **Tamper detection** - See how TUF detects and prevents file tampering  
✅ **Real security** - Cryptographic verification of file integrity and authenticity  
✅ **Production patterns** - Code structure suitable for real-world applications  

## Quick Start (3 commands, 2 minutes)

```bash
# 1. Create a TUF repository with sample files
dotnet run setup ./demo-repo

# 2. Download a file securely with TUF
dotnet run client ./demo-repo hello-world.txt

# 3. See tamper detection in action
dotnet run tamper-demo ./demo-repo
```

## Commands

### `setup <repository-path>`
Creates a complete TUF repository with:
- Sample files to download (`hello-world.txt`, `sample-data.json`)
- All required TUF metadata (root, targets, snapshot, timestamp)
- Cryptographic keys and signatures

```bash
dotnet run setup ./my-tuf-repo
```

### `client <repository-path> <filename>`
Downloads a file securely using TUF client:
- Verifies all metadata signatures
- Downloads the file
- Verifies file integrity against cryptographic hashes
- Shows detailed security information

```bash
dotnet run client ./my-tuf-repo hello-world.txt
```

### `tamper-demo <repository-path>`
Demonstrates TUF's tamper detection:
- Downloads a file normally (succeeds)
- Simulates file tampering attack
- Attempts download of tampered file (fails with security error)
- Shows how TUF prevents serving malicious content

```bash
dotnet run tamper-demo ./my-tuf-repo
```

## Sample Output

### Successful Download
```
🔐 TUF Client: Securely downloading 'hello-world.txt'...

📋 Loading trusted root metadata...
⚙️  Configuring TUF client...
🔄 Refreshing TUF metadata...
   ✅ Timestamp metadata verified
   ✅ Snapshot metadata verified  
   ✅ Targets metadata verified
🔍 Looking for target file: hello-world.txt
   ✅ Found: hello-world.txt
   📊 Size: 87 bytes
   🔐 Hashes: sha256
📥 Downloading and verifying file...
   ✅ Hash verification passed
   ✅ File saved to: ./tuf-cache/targets/hello-world.txt

📄 File content:
   Hello, TUF World! 🌟
   
   This file is protected by The Update Framework.
   TUF ensures it hasn't been tampered with! 🛡️

🎉 Secure download completed successfully!
🛡️  TUF verified the file is authentic and untampered.
```

### Tamper Detection
```
🕵️  TUF Tamper Detection Demo

1️⃣  First, let's download the legitimate file:
   ✅ Legitimate file downloads successfully

2️⃣  Now, let's simulate an attacker tampering with the file...
   📝 File tampered with malicious content

3️⃣  Attempting to download the tampered file:
   🛡️  TUF detected tampering and blocked download!
   📝 Error: File hash verification failed

🎯 Demo Results:
   ✅ Legitimate file: Download successful
   🛡️  Tampered file: TUF detected tampering and blocked download

💡 Key Insight:
   TUF's hash verification makes it impossible for attackers to
   serve malicious files, even if they compromise the server!
```

## Code Structure

The example demonstrates key TUF patterns:

### Repository Creation
```csharp
var builder = new RepositoryBuilder();
builder.AddTarget("hello-world.txt", fileBytes);
var repository = await builder.BuildAsync();
```

### Client Configuration
```csharp
var config = new UpdaterConfig(trustedRootBytes, metadataUrl)
{
    LocalMetadataDir = "./tuf-cache/metadata",
    LocalTargetsDir = "./tuf-cache/targets", 
    Client = new HttpClient()
};
```

### Secure Download
```csharp
var updater = new Updater(config);
await updater.RefreshAsync();  // Verify all metadata

var targetInfo = await updater.GetTargetInfo(filename);
var result = await updater.DownloadTarget(
    targetInfo.Value.targetInfo,
    targetInfo.Value.targetPath,
    null,
    targetUrl
);
```

## Security Features Demonstrated

🔑 **Signature Verification**
- All TUF metadata is cryptographically signed
- Invalid signatures are detected and rejected

📊 **Hash Verification**  
- Files are verified against known-good cryptographic hashes
- Any file modification is immediately detected

⏰ **Freshness Protection**
- Timestamp metadata prevents rollback attacks
- Clients always get the latest secure metadata

🎯 **Consistency Guarantees**
- Snapshot metadata ensures consistent repository state
- Prevents partial updates and inconsistent views

## Production Considerations

This example uses simplified patterns for learning. In production:

⚠️ **Never store keys with your application**
```csharp
// DON'T DO THIS IN PRODUCTION - keys should be stored securely
var keysDir = Path.Combine(repoPath, "keys");
```

✅ **Use secure key management**
```csharp
// Production: Load keys from secure storage (HSM, Key Vault, etc.)
var keyProvider = new AzureKeyVaultKeyProvider(keyVaultUrl);
```

✅ **Use HTTPS for remote repositories**
```csharp
var config = new UpdaterConfig(
    trustedRootBytes, 
    new Uri("https://your-secure-repo.com/metadata/")
);
```

✅ **Configure proper caching and directories**
```csharp
var config = new UpdaterConfig(trustedRootBytes, metadataUrl)
{
    LocalMetadataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YourApp", "tuf-metadata"
    ),
    // ... other production configuration
};
```

## Next Steps

After running this example:

📚 **Learn More**:
- [TUF in 5 Minutes Guide](../../docs/guides/tuf-in-5-minutes.md) - Comprehensive tutorial
- [Core Concepts](../../docs/guides/core-concepts.md) - Deep dive into TUF security model
- [Building Clients](../../docs/guides/building-clients.md) - Production client development

🏗️ **Build Real Applications**:
- [BasicClient Example](../BasicClient/) - More realistic client implementation
- [RepositoryManager Example](../RepositoryManager/) - Repository creation and management
- [SigningDemo Example](../SigningDemo/) - Advanced cryptographic operations

🚀 **Production Deployment**:
- [Repository Setup Guide](../../docs/guides/creating-repositories.md)
- [Key Management Guide](../../docs/guides/key-management.md)  
- [Integration Patterns](../../docs/guides/integration-patterns.md)

## Troubleshooting

**"Repository not found" error**:
```bash
# Make sure you ran setup first
dotnet run setup ./demo-repo
```

**Permission denied errors**:
```bash
# Ensure you have write permissions to the directory
chmod 755 ./demo-repo
```

**Network errors with file:// URLs**:
- This example uses local file:// URLs for simplicity
- In production, use HTTPS URLs for remote repositories

**Build errors**:
```bash
# Make sure you're in the correct directory
cd examples/HelloWorldTuf
dotnet restore
dotnet build
```

---

**🎉 You're now ready to understand TUF security!**

This example shows how TUF provides military-grade security for file distribution with just a few lines of code. The same patterns scale to protect software updates, package repositories, and critical file distributions worldwide.