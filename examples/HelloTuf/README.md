# HelloTuf Example

The simplest possible TUF .NET example - demonstrates TUF basics in under 50 lines of code!

This is the companion code for the [🚀 5-Minute TUF Tutorial](../../docs/guides/5-minute-tutorial.md).

## What this example shows

✅ **TUF initialization** - Creating a basic TUF updater  
✅ **Configuration** - Minimal configuration for TUF operations  
✅ **Error handling** - Basic TUF exception handling  
✅ **Core concepts** - The fundamental TUF workflow  

## Run the example

```bash
cd examples/HelloTuf
dotnet run
```

**Expected output:**
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

## Understanding the code

### Minimal TUF workflow
```csharp
// 1. Create trusted root (foundation of trust)
var trustedRoot = CreateMinimalTrustedRoot();

// 2. Configure updater 
var config = new UpdaterConfig(trustedRoot, repoUrl);
var updater = new Updater(config);

// 3. Use TUF for secure downloads
await updater.RefreshAsync();
var target = await updater.GetTargetInfo("file.exe");
var (path, data) = await updater.DownloadTarget(target.File, target.Path);
```

This is **all you need** to get TUF security in your application!

## Real-world usage

For production applications:

1. **Embed trusted root** - Include it with your installer/app bundle
2. **Use HTTPS repositories** - Point to your actual update server
3. **Handle TufExceptions** - These indicate security threats
4. **Cache metadata locally** - For offline resilience

## Next steps

🚀 **Ready for more?** Check out these examples:
- **[BasicClient](../BasicClient/)** - Complete working client with real repositories
- **[RepositoryManager](../RepositoryManager/)** - Create your own TUF repository  
- **[CliTool](../CliTool/)** - Production-ready command-line interface

📚 **Want to learn more?**
- **[5-Minute Tutorial](../../docs/guides/5-minute-tutorial.md)** - Extended tutorial with more details
- **[Core Concepts](../../docs/guides/core-concepts.md)** - Deep dive into TUF security model
- **[Building Clients](../../docs/guides/building-clients.md)** - Production application development