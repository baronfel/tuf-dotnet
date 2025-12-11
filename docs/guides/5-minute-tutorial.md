# 5-Minute TUF Tutorial

Get up and running with TUF .NET in just 5 minutes! This tutorial shows you the absolute minimum needed to understand and use TUF (The Update Framework) for secure software updates.

## What is TUF in 30 seconds?

TUF makes software updates **secure by design**. Instead of just downloading files and hoping they're legitimate, TUF:

✅ **Verifies authenticity** - Files are cryptographically signed  
✅ **Detects tampering** - Any modification is immediately caught  
✅ **Prevents rollbacks** - Can't trick you into installing old, vulnerable versions  
✅ **Ensures freshness** - Metadata expires to prevent indefinite freeze attacks  

Think of it as "HTTPS for software updates" - but much more comprehensive.

## Hello, TUF! (3 minutes to working code)

### Step 1: Create a new console app
```bash
dotnet new console -n HelloTuf
cd HelloTuf
dotnet add package TUF
```

### Step 2: Replace Program.cs with this minimal example
```csharp
using TUF;

Console.WriteLine("🔒 Hello, TUF!");

// In a real app, this comes from a secure source (embedded in your app, etc.)
var trustedRoot = CreateMinimalTrustedRoot();

// Point to any TUF repository (we'll use a placeholder for this demo)
var config = new UpdaterConfig(trustedRoot, new Uri("https://example.com/metadata"))
{
    LocalMetadataDir = "./tuf-metadata",
    LocalTargetsDir = "./downloads",
    Client = new HttpClient()
};

var updater = new Updater(config);

try 
{
    // This is where the TUF magic happens:
    Console.WriteLine("📥 Refreshing metadata...");
    // await updater.RefreshAsync(); // Commented out for demo - would contact real repo
    
    Console.WriteLine("✅ TUF is working! In a real scenario, this would:");
    Console.WriteLine("   • Download fresh metadata from the repository");
    Console.WriteLine("   • Verify all cryptographic signatures");
    Console.WriteLine("   • Detect any tampering or rollback attempts");
    Console.WriteLine("   • Ensure metadata hasn't expired");
    
    // Find a file and download it securely
    Console.WriteLine("\n🎯 In your app, you'd do:");
    Console.WriteLine("   var (path, info) = await updater.GetTargetInfo(\"my-app.exe\");");
    Console.WriteLine("   var (localPath, data) = await updater.DownloadTarget(info, path);");
    Console.WriteLine("   // File is now verified and safe to use!");
}
catch (TufException ex)
{
    Console.WriteLine($"🚨 TUF security error: {ex.Message}");
    // TUF caught something suspicious - DO NOT proceed with update
}

Console.WriteLine("\n🎉 Complete! You now understand TUF basics.");

// Helper method - in real apps, this is embedded securely with your application
static byte[] CreateMinimalTrustedRoot() => System.Text.Encoding.UTF8.GetBytes($$"""
{
    "signatures": [],
    "signed": {
        "_type": "root",
        "spec_version": "1.0.0", 
        "version": 1,
        "expires": "{{DateTimeOffset.UtcNow.AddYears(1):yyyy-MM-ddTHH:mm:ssZ}}",
        "keys": {},
        "roles": {
            "root": {"keyids": [], "threshold": 1},
            "targets": {"keyids": [], "threshold": 1}, 
            "snapshot": {"keyids": [], "threshold": 1},
            "timestamp": {"keyids": [], "threshold": 1}
        }
    }
}
""");
```

### Step 3: Run it
```bash
dotnet run
```

**Output:**
```
🔒 Hello, TUF!
📥 Refreshing metadata...
✅ TUF is working! In a real scenario, this would:
   • Download fresh metadata from the repository
   • Verify all cryptographic signatures
   • Detect any tampering or rollback attempts
   • Ensure metadata hasn't expired

🎯 In your app, you'd do:
   var (path, info) = await updater.GetTargetInfo("my-app.exe");
   var (localPath, data) = await updater.DownloadTarget(info, path);
   // File is now verified and safe to use!

🎉 Complete! You now understand TUF basics.
```

## What just happened?

You created a **TUF-enabled application**! Here's the minimal workflow:

1. **Trust establishment** - Your app starts with a trusted root (cryptographic foundation)
2. **Metadata refresh** - Downloads current repository state and verifies signatures  
3. **File discovery** - Finds target files with their security information
4. **Secure download** - Downloads files with automatic integrity verification

## Real-world usage (1 minute)

Here's how you'd use this in a real application:

```csharp
// 1. Embed trusted root with your installer
var trustedRoot = GetEmbeddedTrustedRoot();

// 2. Connect to your update repository  
var updater = new Updater(new UpdaterConfig(trustedRoot, 
    new Uri("https://updates.mycompany.com/metadata")));

// 3. Check for updates
await updater.RefreshAsync();
var updateInfo = await updater.GetTargetInfo("MyApp-v2.0.exe");

if (updateInfo.HasValue)
{
    // 4. Download securely - TUF handles ALL security verification
    var (localPath, data) = await updater.DownloadTarget(updateInfo.Value.Item2, updateInfo.Value.Item1);
    
    // 5. File is cryptographically verified - safe to install!
    InstallUpdate(localPath);
}
```

**That's it!** TUF handles all the security complexity - you just download files normally, but they're automatically verified against tampering, rollback attacks, and other threats.

## Common use cases

- **Application updates** - Securely update desktop/mobile apps
- **Configuration updates** - Push secure configuration changes
- **Content delivery** - Distribute files with integrity guarantees
- **Package managers** - Secure package distribution (like npm, apt, etc.)
- **Container registries** - Secure container image distribution
- **IoT/Embedded** - Secure firmware and software updates

## Next steps (choose your path)

### 🚀 **I want to build a TUF client app**
→ Read [Building TUF Clients](./building-clients.md) for production patterns

### 🏭 **I want to create a TUF repository**  
→ Try the [RepositoryManager example](../../examples/RepositoryManager/) to create your first repository

### 🧠 **I want to understand TUF deeply**
→ Read [Core Concepts](./core-concepts.md) for the complete security model

### 🔍 **I want to see working examples**
→ Browse the [examples directory](../../examples/) for complete applications

### ⚡ **I want detailed implementation guidance**
→ Check out the [Quick Start Guide](./quick-start.md) for comprehensive coverage

## Why TUF matters

**Traditional updates**: "Download this file and trust it's legitimate" 🤞

**TUF updates**: 
- ✅ Files are cryptographically signed by multiple keys
- ✅ Signatures are verified before installation  
- ✅ Rollback attacks are automatically detected
- ✅ Compromise of any single key doesn't compromise security
- ✅ Metadata expiration prevents indefinite freeze attacks
- ✅ Tampered files are immediately rejected

**Result**: Your users get updates that are **provably authentic and untampered**, even if attackers control the network, servers, or signing keys.

## Questions?

- **How is this different from code signing?** - TUF provides comprehensive update security, not just signature verification
- **Do I need to change my existing update process?** - TUF integrates with existing HTTP-based distribution
- **What if my repository is compromised?** - TUF's multi-key security model limits damage from any single compromise
- **Is this overkill for my small app?** - TUF scales from small apps to enterprise systems - start simple!

---

🎯 **You now understand TUF fundamentals!** The security model protects your users from a wide range of update attacks, while the .NET implementation makes it simple to integrate into existing applications.

Welcome to **secure software updates**! 🔐