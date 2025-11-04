# TUF .NET Quick Start Guide

Get up and running with secure software updates in minutes! This interactive guide will walk you through your first TUF implementation.

## 🎯 Choose Your Path

### Path 1: "Just Show Me" (2 minutes)
**Perfect if you want to see TUF working immediately**

```bash
# Clone and run the hello world example
git clone https://github.com/baronfel/tuf-dotnet.git
cd tuf-dotnet/examples/HelloWorld
dotnet run
```

**Result**: You'll see a complete secure update workflow with explanations of what's happening.

### Path 2: "I Want to Understand" (5 minutes)
**Perfect if you want to build your own TUF client from scratch**

Follow the **[TUF in 5 Minutes Tutorial](./HelloWorld.md)** - you'll create your own project and understand every line of code.

### Path 3: "Make It Production Ready" (15 minutes)
**Perfect if you're evaluating TUF for a real project**

Continue reading this guide for production considerations and real-world patterns.

---

## 🛠️ Production Setup Wizard

### Step 1: Installation

```bash
# Create your project
dotnet new console -n MySecureUpdater
cd MySecureUpdater

# Add TUF package
dotnet add package TUF

# Optional: Add logging for production
dotnet add package Microsoft.Extensions.Logging.Console
```

### Step 2: Basic Configuration

```csharp
using TUF;
using Microsoft.Extensions.Logging;

// Production-ready configuration
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Updater>();

var config = new UpdaterConfig(
    trustedRoot: await GetTrustedRoot(),           // ← Your trusted root
    remoteMetadataUrl: new Uri("https://your-tuf-repo.com/metadata/")
)
{
    LocalMetadataDir = GetMetadataDir(),           // ← Persistent storage
    LocalTargetsDir = GetTargetsDir(),             // ← Download directory
    Client = CreateHttpClient(),                   // ← Configured HTTP client
    Logger = logger                                // ← Production logging
};

var updater = new Updater(config);
```

### Step 3: Error Handling

```csharp
try 
{
    await updater.RefreshAsync();
    var targetInfo = await updater.GetTargetInfo("my-app.exe");
    
    if (targetInfo.HasValue)
    {
        var result = await updater.DownloadTarget(
            targetInfo.Value.Info, 
            targetInfo.Value.Path, 
            null, 
            null
        );
        
        Console.WriteLine($"✅ Downloaded: {result.FilePath}");
    }
}
catch (TufExpiredMetadataException ex)
{
    // Metadata has expired - handle gracefully
    logger.LogWarning("TUF metadata expired: {Message}", ex.Message);
    // Maybe fall back to cached version or show user warning
}
catch (TufInvalidSignatureException ex)
{
    // Signature verification failed - security issue!
    logger.LogError("TUF signature verification failed: {Message}", ex.Message);
    // This is serious - don't proceed with update
    throw;
}
catch (HttpRequestException ex)
{
    // Network issues - handle with retry logic
    logger.LogWarning("Network error during TUF update: {Message}", ex.Message);
    // Maybe retry with exponential backoff
}
```

### Step 4: Configuration Helpers

Add these production-ready helper methods:

```csharp
static async Task<byte[]> GetTrustedRoot()
{
    // Option 1: Embedded resource (recommended)
    var assembly = Assembly.GetExecutingAssembly();
    using var stream = assembly.GetManifestResourceStream("MyApp.trusted-root.json");
    if (stream != null)
    {
        using var reader = new StreamReader(stream);
        return Encoding.UTF8.GetBytes(await reader.ReadToEndAsync());
    }
    
    // Option 2: Secure configuration
    var configPath = Path.Combine(GetConfigDir(), "trusted-root.json");
    if (File.Exists(configPath))
    {
        return await File.ReadAllBytesAsync(configPath);
    }
    
    throw new InvalidOperationException("Trusted root not found. Distribute securely with your app.");
}

static string GetMetadataDir() => 
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                 "MyApp", "TUF", "metadata");

static string GetTargetsDir() => 
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                 "MyApp", "TUF", "targets");

static HttpClient CreateHttpClient()
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0 TUF.NET");
    client.Timeout = TimeSpan.FromMinutes(5);
    return client;
}
```

## 🔐 Security Checklist

Before going to production, ensure:

### ✅ Trusted Root Distribution
- [ ] Trusted root is embedded as a resource in your application
- [ ] Trusted root is obtained through a secure, out-of-band channel
- [ ] You have a plan for trusted root key rotation

### ✅ Network Security
- [ ] Repository URLs use HTTPS
- [ ] You have proper timeout and retry logic
- [ ] You handle network failures gracefully

### ✅ Storage Security  
- [ ] Metadata and targets are stored in appropriate directories
- [ ] File permissions are configured correctly
- [ ] You clean up old/expired metadata

### ✅ Error Handling
- [ ] Signature verification failures are treated as security issues
- [ ] Expired metadata is handled appropriately
- [ ] Network errors don't crash your application
- [ ] You log security-relevant events

### ✅ Operational Readiness
- [ ] You have monitoring for update success/failure rates
- [ ] You can roll back updates if needed
- [ ] Your update process is tested
- [ ] You have a plan for emergency updates

## 🚀 Real-World Patterns

### Pattern 1: Background Updates
```csharp
public class BackgroundUpdateService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdates();
                await Task.Delay(TimeSpan.FromHours(4), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background update check failed");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }
}
```

### Pattern 2: Progressive Updates
```csharp
public async Task<UpdateResult> UpdateWithProgress(IProgress<UpdateProgress> progress)
{
    progress?.Report(new UpdateProgress("Checking for updates..."));
    await updater.RefreshAsync();
    
    progress?.Report(new UpdateProgress("Downloading files...", 0.5));
    var result = await updater.DownloadTarget(/* ... */);
    
    progress?.Report(new UpdateProgress("Update complete!", 1.0));
    return result;
}
```

### Pattern 3: Conditional Updates
```csharp
public async Task<bool> UpdateIfNewer(string targetPath, Version currentVersion)
{
    var targetInfo = await updater.GetTargetInfo(targetPath);
    if (!targetInfo.HasValue) return false;
    
    // Check if update is newer (implement your version logic)
    var targetVersion = ExtractVersion(targetInfo.Value.Info);
    if (targetVersion <= currentVersion) return false;
    
    await updater.DownloadTarget(targetInfo.Value.Info, targetInfo.Value.Path, null, null);
    return true;
}
```

## 🎓 Next Steps

### Learn TUF Concepts
- **[TUF Specification](https://theupdateframework.io/specification/)** - Official specification
- **[Security Model](https://theupdateframework.io/security/)** - How TUF prevents attacks
- **[Threat Model](https://theupdateframework.io/attackers.html)** - What attacks TUF defends against

### Explore Examples
- **[BasicClient](../examples/BasicClient/)** - Simple client with error handling
- **[CliTool](../examples/CliTool/)** - Full command-line interface
- **[RepositoryManager](../examples/RepositoryManager/)** - Create and manage repositories

### Production Planning
- **[Deployment Guide](./Production.md)** - Production deployment considerations *(coming soon)*
- **[Key Management](./KeyManagement.md)** - Managing cryptographic keys *(coming soon)*
- **[Monitoring Guide](./Monitoring.md)** - Monitoring TUF in production *(coming soon)*

## ❓ FAQ

**Q: Do I need to create my own TUF repository?**
A: Yes, for production. Use the `RepositoryManager` example to get started.

**Q: Can I use TUF with existing update mechanisms?**  
A: Yes! TUF can wrap existing update systems to add cryptographic verification.

**Q: What if my trusted root key is compromised?**
A: You'll need to distribute a new trusted root with your next application release. Plan for this scenario.

**Q: How does TUF compare to code signing?**
A: TUF provides repository-level security while code signing provides file-level security. They're complementary.

**Q: Can I use TUF offline?**
A: Partially. You can verify cached metadata and targets, but eventually you need network access to get fresh metadata.

---

**🎉 Ready to secure your updates?** 

Choose your path above and start implementing enterprise-grade update security today!

> AI-generated content by [achieve-parity](https://github.com/baronfel/tuf-dotnet/actions/runs/19063437581) may contain mistakes.