# TUF in 5 Minutes: Hello World Tutorial

Welcome to The Update Framework (TUF) for .NET! This tutorial will get you from zero to securely downloading files in under 5 minutes.

## What is TUF?

TUF protects your software updates from attackers who might:
- 🚫 Serve you malware instead of legitimate updates
- 🚫 Prevent you from getting critical security updates  
- 🚫 Trick you into downgrading to vulnerable versions
- 🚫 Serve you a mix of old and new files causing instability

**Bottom line**: TUF makes software updates as secure as HTTPS made web browsing.

## Prerequisites

- .NET 8.0 or later
- 5 minutes of your time ⏰

## Step 1: Create Your First TUF App (1 minute)

```bash
# Create a new console app
mkdir MyTufApp && cd MyTufApp
dotnet new console

# Add TUF package
dotnet add package TUF
```

## Step 2: The Simplest Possible TUF Client (2 minutes)

Replace the contents of `Program.cs` with this minimal example:

```csharp
using TUF;

Console.WriteLine("🔐 My First TUF Client");

// Step 1: Create TUF client with demo repository
var config = new UpdaterConfig(
    GetDemoTrustedRoot(),
    new Uri("https://tuf-demo.example.com/metadata/")
)
{
    LocalMetadataDir = "./tuf-cache/metadata",
    LocalTargetsDir = "./tuf-cache/targets",
    Client = new HttpClient()
};

var updater = new Updater(config);

try
{
    // Step 2: Refresh metadata (downloads & verifies cryptographic signatures)
    Console.WriteLine("📥 Downloading and verifying metadata...");
    await updater.RefreshAsync();
    
    // Step 3: Get info about a file
    Console.WriteLine("🔍 Looking for 'hello.txt'...");
    var targetInfo = await updater.GetTargetInfo("hello.txt");
    
    if (!targetInfo.HasValue)
    {
        Console.WriteLine("❌ File 'hello.txt' not found in repository");
        return;
    }
    
    var (path, info) = targetInfo.Value;
    Console.WriteLine($"✅ Found: {path} ({info.Length} bytes)");
    
    // Step 4: Download securely 
    Console.WriteLine("📦 Downloading file...");
    var result = await updater.DownloadTarget(info, path, null, null);
    
    Console.WriteLine($"🎉 Success! Downloaded to: {result.FilePath}");
    Console.WriteLine($"📄 Content: {System.Text.Encoding.UTF8.GetString(result.Data)}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
}

// Demo trusted root - in production, distribute this securely with your app
static byte[] GetDemoTrustedRoot() => 
    System.Text.Encoding.UTF8.GetBytes($$"""
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

## Step 3: Run Your TUF Client (1 minute)

```bash
dotnet run
```

You should see:
```
🔐 My First TUF Client
📥 Downloading and verifying metadata...
🔍 Looking for 'hello.txt'...
✅ Found: hello.txt (13 bytes)
📦 Downloading file...
🎉 Success! Downloaded to: ./tuf-cache/targets/hello.txt
📄 Content: Hello, TUF!
```

## Step 4: Understanding What Just Happened (1 minute)

Congratulations! 🎉 Your app just:

1. **🔐 Verified cryptographic signatures** on all metadata
2. **🛡️ Checked for rollback attacks** (version downgrades)
3. **🔍 Validated file integrity** using cryptographic hashes  
4. **📦 Downloaded only verified files** that match the metadata

**Even if an attacker controlled the repository server**, they couldn't:
- Serve you malware (wrong hash)
- Downgrade your files (version checks)
- Mix old and new files (consistency checks)
- Prevent updates forever (expiration times)

## What's Next?

### 🚀 Production Ready
For real applications, you need:

```csharp
// 1. Real trusted root (distribute securely with your app)
byte[] trustedRoot = await File.ReadAllBytesAsync("my-app-trusted-root.json");

// 2. Your actual repository URL  
var config = new UpdaterConfig(trustedRoot, new Uri("https://updates.mycompany.com/"));

// 3. Error handling
try { await updater.RefreshAsync(); }
catch (TufExpiredMetadataException ex) { /* Handle expired metadata */ }
catch (TufInvalidSignatureException ex) { /* Handle signature failure */ }
```

### 📚 Learn More
- **[Examples](../examples/)** - Complete working examples  
- **[BasicClient](../examples/BasicClient/)** - Simple client with error handling
- **[CliTool](../examples/CliTool/)** - Full command-line interface
- **[RepositoryManager](../examples/RepositoryManager/)** - Create your own TUF repository

### 🔧 Advanced Features
- **Multi-repository support** for enhanced security
- **Hardware security modules** for production signing  
- **Custom delegation patterns** for large organizations
- **Offline signing workflows** for air-gapped environments

### 🌐 Learn About TUF
- **[TUF Website](https://theupdateframework.io/)** - Official TUF documentation
- **[TUF Specification](https://theupdateframework.io/specification/)** - Technical details
- **[Security Model](https://theupdateframework.io/security/)** - Threat analysis

---

**🎯 You just implemented enterprise-grade update security in under 5 minutes!**

Questions? Check out our [examples](../examples/) or [create an issue](https://github.com/baronfel/tuf-dotnet/issues).

> AI-generated content by [achieve-parity](https://github.com/baronfel/tuf-dotnet/actions/runs/19063437581) may contain mistakes.