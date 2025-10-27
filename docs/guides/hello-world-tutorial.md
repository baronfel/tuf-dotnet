# TUF in 5 Minutes - Complete Hello World Tutorial

This tutorial will get you from zero to a working TUF setup in just 5 minutes. No external dependencies, no complex setup - just a complete, runnable example that demonstrates the entire TUF workflow.

## What You'll Build

By the end of this tutorial, you'll have:
- ✅ Created a TUF repository with proper keys and metadata
- ✅ Added a target file to the repository
- ✅ Built a TUF client that securely downloads and verifies files
- ✅ Understood the core TUF security guarantees

All in a single, self-contained example!

## Prerequisites

- .NET 8.0 or later
- 5 minutes of your time

## Complete Self-Contained Example

Create a new console application and copy this complete example:

```bash
mkdir tuf-hello-world
cd tuf-hello-world
dotnet new console
dotnet add package TUF
```

Replace the contents of `Program.cs` with this complete working example:

```csharp
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TUF;
using TUF.Models;
using TUF.Repository;

namespace TufHelloWorld
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 TUF .NET Hello World - Complete Tutorial");
            Console.WriteLine("===========================================");

            try
            {
                // Step 1: Create a complete TUF repository from scratch
                await CreateTufRepository();
                Console.WriteLine("✅ Step 1: Created TUF repository");

                // Step 2: Use TUF client to securely download files
                await UseTufClient();
                Console.WriteLine("✅ Step 2: TUF client downloaded and verified file");

                Console.WriteLine("\n🎉 Success! You've completed the TUF Hello World tutorial!");
                Console.WriteLine("\nWhat happened:");
                Console.WriteLine("• Created a TUF repository with cryptographic keys");
                Console.WriteLine("• Added a target file with secure metadata");
                Console.WriteLine("• Downloaded the file with full integrity verification");
                Console.WriteLine("• Protected against tampering, rollbacks, and other attacks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine("\n🔍 Troubleshooting:");
                Console.WriteLine("• Make sure you have write permissions in the current directory");
                Console.WriteLine("• Check that .NET 8.0+ is installed: dotnet --version");
                Console.WriteLine("• Verify the TUF package is installed: dotnet list package");
            }
        }

        static async Task CreateTufRepository()
        {
            Console.WriteLine("\n📁 Creating TUF repository...");

            // Create directories
            Directory.CreateDirectory("repository/metadata");
            Directory.CreateDirectory("repository/targets");
            Directory.CreateDirectory("client-cache");

            // Create a sample target file
            var sampleContent = "Hello, TUF World! 🌍\n\nThis file was securely downloaded using The Update Framework.\nIt's cryptographically verified and protected against tampering!";
            await File.WriteAllTextAsync("repository/targets/hello.txt", sampleContent);
            Console.WriteLine("  • Created target file: hello.txt");

            // Generate keys for all TUF roles
            var rootSigner = Ed25519Signer.Generate();
            var targetsSigner = Ed25519Signer.Generate(); 
            var snapshotSigner = Ed25519Signer.Generate();
            var timestampSigner = Ed25519Signer.Generate();
            Console.WriteLine("  • Generated cryptographic keys (Ed25519)");

            // Create repository builder
            var builder = new RepositoryBuilder();

            // Add keys to roles
            builder.AddRootKey(rootSigner.Key)
                   .AddTargetsKey(targetsSigner.Key)
                   .AddSnapshotKey(snapshotSigner.Key)
                   .AddTimestampKey(timestampSigner.Key);

            // Add our target file
            builder.AddTarget("hello.txt", "repository/targets/hello.txt");
            Console.WriteLine("  • Added target file to repository metadata");

            // Build and sign the repository
            var signers = new Dictionary<string, ISigner>
            {
                [rootSigner.Key.Id.Value] = rootSigner,
                [targetsSigner.Key.Id.Value] = targetsSigner,
                [snapshotSigner.Key.Id.Value] = snapshotSigner,
                [timestampSigner.Key.Id.Value] = timestampSigner
            };

            await builder.BuildAndSaveAsync("repository/metadata", signers);
            Console.WriteLine("  • Built and signed all TUF metadata");
            Console.WriteLine("  • Repository ready at: ./repository/");
        }

        static async Task UseTufClient()
        {
            Console.WriteLine("\n🔄 Using TUF client to download files...");

            // Load the trusted root metadata (in production, this comes from a secure source)
            var trustedRoot = await File.ReadAllBytesAsync("repository/metadata/root.json");
            Console.WriteLine("  • Loaded trusted root metadata");

            // Configure TUF client
            var metadataUri = new Uri(Path.GetFullPath("repository/metadata") + "/");
            var targetsUri = new Uri(Path.GetFullPath("repository/targets") + "/");

            var config = new UpdaterConfig(trustedRoot, metadataUri)
            {
                LocalMetadataDir = "client-cache/metadata",
                LocalTargetsDir = "client-cache/targets",
                Client = new HttpClient(),
                TargetsBaseUrl = targetsUri
            };

            var updater = new Updater(config);
            Console.WriteLine("  • Configured TUF client");

            // Refresh metadata (downloads and verifies all metadata)
            await updater.RefreshAsync();
            Console.WriteLine("  • Refreshed and verified metadata");

            // Find our target file
            var targetInfo = await updater.GetTargetInfo("hello.txt");
            if (!targetInfo.HasValue)
            {
                throw new Exception("Target file not found in repository");
            }

            var (remotePath, targetFile) = targetInfo.Value;
            Console.WriteLine($"  • Found target: {remotePath}");
            Console.WriteLine($"    Length: {targetFile.Length} bytes");
            Console.WriteLine($"    Hashes: {string.Join(", ", targetFile.Hashes.Keys)}");

            // Download and verify the file
            var result = await updater.DownloadTarget(targetFile, "hello.txt", null, null);
            Console.WriteLine($"  • Downloaded to: {result.FilePath}");
            Console.WriteLine($"  • Verified {result.Data.Length} bytes");

            // Show the content to prove it worked
            var content = Encoding.UTF8.GetString(result.Data);
            Console.WriteLine($"\n📄 Downloaded file content:");
            Console.WriteLine($"   {content.Replace("\n", "\n   ")}");
        }
    }
}
```

## Run the Example

```bash
dotnet run
```

You should see output like this:

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

What happened:
• Created a TUF repository with cryptographic keys
• Added a target file with secure metadata
• Downloaded the file with full integrity verification
• Protected against tampering, rollbacks, and other attacks
```

## What Just Happened?

This example demonstrated the complete TUF workflow:

### 🔐 **Repository Creation** (Server Side)
1. **Key Generation**: Created Ed25519 cryptographic keys for all TUF roles (root, targets, snapshot, timestamp)
2. **Metadata Creation**: Built TUF metadata describing our target files
3. **Cryptographic Signing**: Signed all metadata with the appropriate keys
4. **Repository Publishing**: Saved the complete repository structure

### 📥 **Secure Downloads** (Client Side)
1. **Trust Bootstrap**: Loaded the trusted root metadata (the foundation of trust)
2. **Metadata Refresh**: Downloaded and verified all current metadata
3. **Target Discovery**: Found the target file in the repository
4. **Secure Download**: Downloaded the file with full cryptographic verification

### 🛡️ **Security Guarantees**
Your download was protected against:
- **Tampering**: File hashes are cryptographically verified
- **Man-in-the-Middle**: All metadata is signed with trusted keys
- **Rollback Attacks**: Version numbers prevent downgrade attacks
- **Repository Compromise**: Multiple keys limit damage from key compromise

## Understanding the File Structure

After running the example, you'll have this structure:

```
tuf-hello-world/
├── repository/                 # TUF Repository
│   ├── metadata/              
│   │   ├── root.json          # Root metadata (trusted keys)
│   │   ├── targets.json       # Targets metadata (file info)
│   │   ├── snapshot.json      # Snapshot metadata (consistency)
│   │   └── timestamp.json     # Timestamp metadata (freshness)
│   └── targets/
│       └── hello.txt          # The actual target file
└── client-cache/              # Client Cache
    ├── metadata/              # Cached metadata
    └── targets/
        └── hello.txt          # Downloaded and verified file
```

## Next Steps

Now that you understand the basics:

1. **🚀 Production Usage**: Read the [Quick Start Guide](./quick-start.md) for production patterns
2. **🏗️ Real Repositories**: Check out [Repository Builder](../../examples/RepositoryManager/) for complex scenarios
3. **🔍 Advanced Features**: Explore [Multi-Repository Support](../../examples/MultiRepositoryClient/)
4. **🛡️ Security Deep-Dive**: Read the [Security Model](../security/security-model.md)

## Troubleshooting

**"File not found" errors:**
- Make sure you have write permissions in the current directory
- Check that all directories were created successfully

**Package reference errors:**
```bash
dotnet add package TUF  # Make sure the TUF package is installed
dotnet restore          # Restore all dependencies
```

**Runtime errors:**
- Ensure you're using .NET 8.0 or later: `dotnet --version`
- Check the full exception message for specific guidance

## Key Concepts You've Learned

- **📋 TUF Repository**: A structured collection of metadata and target files
- **🔑 Cryptographic Keys**: Ed25519 keys for signing and verification
- **📄 Metadata Files**: root.json, targets.json, snapshot.json, timestamp.json
- **🔒 Trust Chain**: How TUF establishes and maintains trust
- **✅ Verification**: Cryptographic verification of downloads

You now understand the fundamentals of TUF and are ready to build secure update systems in .NET! 🎉

---

*This tutorial created a complete, working TUF setup in under 5 minutes. For production use, you'll want to use proper key management, secure distribution channels, and follow security best practices.*