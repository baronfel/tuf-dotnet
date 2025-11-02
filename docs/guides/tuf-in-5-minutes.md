# TUF in 5 Minutes: Complete Working Example

**Goal**: Get from zero to a working TUF-secured file download in under 5 minutes.

This tutorial creates a complete, self-contained example that demonstrates TUF's core security benefits with minimal setup. Perfect for developers who want to understand TUF quickly or evaluate it for their projects.

## What You'll Build

By the end of this tutorial, you'll have:
- ✅ A working TUF repository with sample files
- ✅ A TUF client that securely downloads and verifies files
- ✅ Understanding of TUF's security guarantees
- ✅ A foundation for integrating TUF into your applications

## Prerequisites

- .NET 8.0 or later
- 5 minutes of your time
- Terminal/command prompt

## Step 1: Clone and Build (30 seconds)

```bash
git clone https://github.com/baronfel/tuf-dotnet.git
cd tuf-dotnet
dotnet build
```

## Step 2: Create a Test Repository (1 minute)

Run the repository builder example to create a complete TUF repository:

```bash
cd examples/RepositoryManager
dotnet run
```

This creates:
- `./test-repo/` - A complete TUF repository
- `./test-repo/metadata/` - TUF metadata files (root.json, timestamp.json, etc.)
- `./test-repo/targets/` - Actual files to download
- `./test-repo/keys/` - Cryptographic keys (for this demo only!)

**🔒 Security Note**: In production, keys should NEVER be stored with the repository!

## Step 3: Run the TUF Client (1 minute)

Now use the basic client to securely download a file:

```bash
cd ../BasicClient
dotnet run file://../../examples/RepositoryManager/test-repo/metadata hello.txt
```

**What just happened?**
1. 📋 Client loaded trusted root metadata
2. 🔄 Client refreshed metadata (timestamp → snapshot → targets)
3. ✅ Client verified all cryptographic signatures
4. 📥 Client found and downloaded `hello.txt`
5. 🔐 Client verified the file's hash matches metadata
6. ✨ **File is now verified as authentic and untampered!**

## Step 4: See the Security in Action (2 minutes)

Let's demonstrate TUF's tamper detection by simulating an attack:

### Simulate a File Tampering Attack

```bash
# Go to the repository
cd ../../examples/RepositoryManager/test-repo/targets

# Tamper with the file (simulate an attacker modifying it)
echo "MALICIOUS CONTENT" > hello.txt

# Try to download the tampered file
cd ../../../BasicClient
dotnet run file://../../examples/RepositoryManager/test-repo/metadata hello.txt
```

**💥 TUF Detects the Attack!**
You'll see an error like:
```
TUF integrity check failed: File hash mismatch
Expected: sha256:a1b2c3d4...
Actual: sha256:e5f6g7h8...
```

This demonstrates TUF's core security guarantee: **any tampering with files is immediately detected**.

### See Valid vs Invalid Signatures

Let's also see what happens with invalid metadata signatures:

```bash
# Go to metadata directory
cd ../../examples/RepositoryManager/test-repo/metadata

# Corrupt the targets metadata (simulate metadata attack)
sed -i 's/"version":1/"version":999/' targets.json

# Try to use the client with corrupted metadata
cd ../../../BasicClient
dotnet run file://../../examples/RepositoryManager/test-repo/metadata hello.txt
```

**🛡️ TUF Detects Metadata Tampering!**
You'll see signature verification fail, preventing any malicious metadata from being trusted.

## Step 5: Understanding the Code (1 minute)

Let's look at the client code that made this secure download possible:

```csharp
// Load trusted root (foundation of trust)
byte[] trustedRootBytes = await File.ReadAllBytesAsync(rootPath);

// Configure TUF client
var config = new UpdaterConfig(trustedRootBytes, metadataUrl)
{
    LocalMetadataDir = "./tuf-metadata",
    LocalTargetsDir = "./downloads",
    Client = new HttpClient()
};

// Create updater and refresh metadata
var updater = new Updater(config);
await updater.RefreshAsync();  // Downloads & verifies all metadata

// Get target info and download securely
var targetInfo = await updater.GetTargetInfo(targetName);
if (targetInfo.HasValue)
{
    var result = await updater.DownloadTarget(
        targetInfo.Value.targetInfo, 
        targetInfo.Value.targetPath, 
        null, 
        targetBaseUrl
    );
    Console.WriteLine($"✅ Secure download completed: {result.FilePath}");
}
```

**Key Security Features Demonstrated**:
1. 🔑 **Signature Verification** - All metadata is cryptographically signed
2. 📊 **Hash Verification** - All files are verified against known-good hashes
3. ⏰ **Freshness Guarantees** - Timestamp metadata prevents rollback attacks
4. 🎯 **Consistency Checks** - Snapshot metadata ensures consistent repository state

## Next Steps: Real-World Usage

Now that you understand TUF basics, here's how to use it in real applications:

### 1. Production Repository Setup
```csharp
// DON'T store keys with your application in production!
var config = new UpdaterConfig(trustedRootBytes, new Uri("https://your-repo.com/metadata/"))
{
    LocalMetadataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YourApp", "tuf"),
    LocalTargetsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YourApp", "downloads"),
    Client = httpClient  // Configure with proper timeouts, proxies, etc.
};
```

### 2. Application Updates
```csharp
public async Task<bool> CheckForUpdatesAsync()
{
    await updater.RefreshAsync();
    var latestVersion = await updater.GetTargetInfo("MyApp.exe");
    
    if (latestVersion.HasValue && IsNewerVersion(latestVersion.Value.targetInfo))
    {
        // Download the update securely
        var result = await updater.DownloadTarget(
            latestVersion.Value.targetInfo, 
            latestVersion.Value.targetPath, 
            null, 
            targetBaseUrl
        );
        return true; // Update available and downloaded
    }
    return false; // No update needed
}
```

### 3. Package Management
```csharp
public async Task<byte[]> GetSecurePackageAsync(string packageName, string version)
{
    await updater.RefreshAsync();
    var target = await updater.GetTargetInfo($"{packageName}-{version}.nupkg");
    
    if (!target.HasValue)
        throw new PackageNotFoundException($"Package {packageName} v{version} not found");
    
    var result = await updater.DownloadTarget(
        target.Value.targetInfo, 
        target.Value.targetPath, 
        null, 
        packageBaseUrl
    );
    
    return await File.ReadAllBytesAsync(result.FilePath);
}
```

## 🎯 Success! You now understand TUF

**What you've learned**:
- ✅ TUF provides **tamper detection** for files and metadata
- ✅ TUF prevents **rollback attacks** with timestamp metadata  
- ✅ TUF ensures **consistency** across repository operations
- ✅ TUF's security works **even if the repository is compromised**
- ✅ Integration into .NET applications is **straightforward**

**What makes TUF different from simple HTTPS downloads**:
- 🔐 **Multiple signatures** - Multiple keys must be compromised to create valid fake metadata
- 📊 **Hash verification** - Files are verified against cryptographic hashes, not just downloaded
- ⏰ **Freshness** - Prevents serving old, potentially vulnerable versions
- 🎯 **Consistency** - Ensures all clients see the same repository state
- 🛡️ **Survivable** - Security guarantees hold even if servers are compromised

## What's Next?

**For Learning More**:
- 📚 [Core Concepts Guide](./core-concepts.md) - Deep dive into TUF's security model
- 🏗️ [Building Clients Guide](./building-clients.md) - Production-ready client development
- 🗂️ [Examples Directory](../../examples/) - More complete examples and use cases

**For Production Use**:
- 🔑 [Key Management Guide](./key-management.md) - Secure key generation and storage
- 🏭 [Repository Setup](./creating-repositories.md) - Production repository deployment
- 📊 [Monitoring & Logging](./logging-telemetry.md) - Operational considerations

**For Integration**:
- 🌐 [ASP.NET Core Integration](./aspnet-integration.md) - Web application patterns
- 📦 [Package Manager Integration](./integration-patterns.md) - NuGet and other package systems
- ☁️ [Cloud Deployment](./cloud-deployment.md) - Azure, AWS, and GCP patterns

---

**🎉 Congratulations!** You've just implemented secure, tamper-proof file distribution in under 5 minutes. TUF is now protecting your downloads with military-grade security guarantees.

*Ready for production? The security you just experienced scales to enterprise deployments protecting billions of downloads worldwide.*