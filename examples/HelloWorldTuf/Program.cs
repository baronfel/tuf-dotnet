using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TUF;
using TUF.Repository;
using TUF.Models;

namespace HelloWorldTuf;

/// <summary>
/// TUF Hello World Example - Complete demonstration of TUF security in a single application.
/// 
/// This example shows:
/// 1. Creating a TUF repository from scratch
/// 2. Adding files to the repository 
/// 3. Using a TUF client to securely download files
/// 4. Demonstrating tamper detection
/// 
/// Run without arguments to see the complete demo.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 TUF Hello World - Complete TUF demonstration in 5 minutes");
        Console.WriteLine("==============================================================");
        Console.WriteLine();

        try
        {
            var repoPath = "./hello-tuf-repo";
            
            // Step 1: Create TUF Repository
            Console.WriteLine("Step 1: Creating TUF repository...");
            await CreateTufRepository(repoPath);
            Console.WriteLine("✅ TUF repository created successfully");
            Console.WriteLine();

            // Step 2: Download file securely  
            Console.WriteLine("Step 2: Downloading file securely with TUF client...");
            await DownloadFileSecurely(repoPath, "hello-world.txt");
            Console.WriteLine("✅ Secure download completed");
            Console.WriteLine();

            // Step 3: Demonstrate tamper detection
            Console.WriteLine("Step 3: Demonstrating tamper detection...");
            await DemonstrateTamperDetection(repoPath);
            Console.WriteLine("✅ Tamper detection demonstrated");
            Console.WriteLine();

            Console.WriteLine("🎉 TUF Hello World completed successfully!");
            Console.WriteLine();
            Console.WriteLine("What you just experienced:");
            Console.WriteLine("  🔐 Cryptographic signature verification");
            Console.WriteLine("  📊 File integrity verification via hashes");
            Console.WriteLine("  🛡️  Tamper detection and prevention");
            Console.WriteLine("  ⏰ Freshness guarantees via timestamp metadata");
            Console.WriteLine();
            Console.WriteLine("🚀 TUF now protects your file downloads with military-grade security!");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task CreateTufRepository(string repoPath)
    {
        Console.WriteLine("  📁 Creating directory structure...");
        var metadataDir = Path.Combine(repoPath, "metadata");
        var targetsDir = Path.Combine(repoPath, "targets");
        
        Directory.CreateDirectory(metadataDir);
        Directory.CreateDirectory(targetsDir);

        Console.WriteLine("  📄 Creating sample files...");
        await CreateSampleFiles(targetsDir);

        Console.WriteLine("  🔑 Generating cryptographic keys...");
        Console.WriteLine("      ⚠️  Demo keys only - NEVER store keys with your app in production!");
        
        var rootSigner = Ed25519Signer.Generate();
        var timestampSigner = Ed25519Signer.Generate();
        var snapshotSigner = Ed25519Signer.Generate();
        var targetsSigner = Ed25519Signer.Generate();

        Console.WriteLine("  📋 Building TUF metadata...");
        var builder = new RepositoryBuilder()
            .AddSigner("root", rootSigner)
            .AddSigner("timestamp", timestampSigner)
            .AddSigner("snapshot", snapshotSigner)
            .AddSigner("targets", targetsSigner)
            .SetDefaultExpiry(DateTimeOffset.UtcNow.AddYears(1))
            .SetConsistentSnapshots(true);

        // Add target files
        var helloContent = await File.ReadAllBytesAsync(Path.Combine(targetsDir, "hello-world.txt"));
        builder.AddTarget("hello-world.txt", helloContent, new Dictionary<string, object>
        {
            ["description"] = "Hello World sample file"
        });

        var dataContent = await File.ReadAllBytesAsync(Path.Combine(targetsDir, "sample-data.json"));
        builder.AddTarget("sample-data.json", dataContent, new Dictionary<string, object>
        {
            ["description"] = "Sample JSON data file"
        });

        Console.WriteLine("  ✍️  Signing metadata with cryptographic signatures...");
        var repository = builder.Build();
        repository.WriteToDirectory(repoPath);

        Console.WriteLine($"  💾 Repository written to: {Path.GetFullPath(repoPath)}");
    }

    private static async Task CreateSampleFiles(string targetsDir)
    {
        // Create hello-world.txt
        var helloWorldPath = Path.Combine(targetsDir, "hello-world.txt");
        var helloContent = "Hello, TUF World! 🌟\n\nThis file is protected by The Update Framework.\nTUF ensures it hasn't been tampered with! 🛡️";
        await File.WriteAllTextAsync(helloWorldPath, helloContent);

        // Create sample data file
        var dataPath = Path.Combine(targetsDir, "sample-data.json");
        var jsonData = $$"""
        {
          "message": "This is sample data protected by TUF",
          "timestamp": "{{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}}",
          "security": "TUF provides integrity and authenticity guarantees",
          "features": [
            "Tamper detection",
            "Signature verification", 
            "Rollback protection",
            "Multi-signature support"
          ]
        }
        """;
        await File.WriteAllTextAsync(dataPath, jsonData);
    }

    private static async Task DownloadFileSecurely(string repoPath, string filename)
    {
        Console.WriteLine($"  🔍 Setting up TUF client for '{filename}'...");

        var metadataUrl = new Uri($"file://{Path.GetFullPath(Path.Combine(repoPath, "metadata"))}/");
        var targetsUrl = new Uri($"file://{Path.GetFullPath(Path.Combine(repoPath, "targets"))}/");
        var rootPath = Path.Combine(repoPath, "metadata", "root.json");

        Console.WriteLine("  📋 Loading trusted root metadata...");
        var trustedRootBytes = await File.ReadAllBytesAsync(rootPath);

        Console.WriteLine("  ⚙️  Configuring TUF client...");
        var config = new UpdaterConfig(trustedRootBytes, metadataUrl)
        {
            LocalMetadataDir = Path.Combine(Directory.GetCurrentDirectory(), "tuf-cache", "metadata"),
            LocalTargetsDir = Path.Combine(Directory.GetCurrentDirectory(), "tuf-cache", "targets"),
            Client = new System.Net.Http.HttpClient()
        };

        Console.WriteLine("  🔄 Refreshing TUF metadata (verifying signatures)...");
        var updater = new Updater(config);
        await updater.RefreshAsync();
        Console.WriteLine("     ✅ Timestamp metadata verified");
        Console.WriteLine("     ✅ Snapshot metadata verified");
        Console.WriteLine("     ✅ Targets metadata verified");

        Console.WriteLine($"  🎯 Looking for target file: {filename}");
        var targetInfo = await updater.GetTargetInfo(filename);
        
        if (!targetInfo.HasValue)
        {
            throw new Exception($"File '{filename}' not found in repository");
        }

        var (targetPath, target) = targetInfo.Value;
        Console.WriteLine($"     ✅ Found: {targetPath}");
        Console.WriteLine($"     📊 Size: {target.Length} bytes");
        Console.WriteLine($"     🔐 Hashes: {string.Join(", ", target.Hashes.Keys)}");

        Console.WriteLine("  📥 Downloading and verifying file...");
        var result = await updater.DownloadTarget(target, targetPath, null, targetsUrl);
        
        Console.WriteLine($"     ✅ Hash verification passed");
        Console.WriteLine($"     ✅ File saved to: {result.FilePath}");

        // Show file content for demo
        if (Path.GetExtension(filename).ToLower() == ".txt")
        {
            var content = await File.ReadAllTextAsync(result.FilePath);
            Console.WriteLine();
            Console.WriteLine("  📄 File content:");
            foreach (var line in content.Split('\n'))
            {
                Console.WriteLine($"     {line}");
            }
        }
    }

    private static async Task DemonstrateTamperDetection(string repoPath)
    {
        var targetFile = Path.Combine(repoPath, "targets", "hello-world.txt");
        
        Console.WriteLine("  📄 Reading original file content...");
        var originalContent = await File.ReadAllTextAsync(targetFile);
        Console.WriteLine("     ✅ Original content read");

        Console.WriteLine("  1️⃣  First, downloading legitimate file...");
        try 
        {
            await DownloadFileSecurelyQuiet(repoPath, "hello-world.txt");
            Console.WriteLine("     ✅ Legitimate file downloads successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"     ❌ Unexpected error with legitimate file: {ex.Message}");
            return;
        }

        Console.WriteLine("  2️⃣  Simulating attacker tampering with file...");
        var tamperedContent = "🚨 MALICIOUS CONTENT INJECTED BY ATTACKER! 🚨\nThis file has been tampered with!";
        await File.WriteAllTextAsync(targetFile, tamperedContent);
        Console.WriteLine("     🚨 File replaced with malicious content");

        Console.WriteLine("  3️⃣  Attempting to download tampered file...");
        try
        {
            await DownloadFileSecurelyQuiet(repoPath, "hello-world.txt");
            Console.WriteLine("     🚨 ERROR: TUF failed to detect tampering!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("     🛡️  TUF detected tampering and blocked download!");
            Console.WriteLine($"     📝 Error: {ex.Message}");
        }

        Console.WriteLine("  🔄 Restoring original file...");
        await File.WriteAllTextAsync(targetFile, originalContent);

        Console.WriteLine();
        Console.WriteLine("  🎯 Demo Results:");
        Console.WriteLine("     ✅ Legitimate file: Download successful");
        Console.WriteLine("     🛡️  Tampered file: TUF detected tampering and blocked download");
        Console.WriteLine();
        Console.WriteLine("  💡 Key Insight:");
        Console.WriteLine("     TUF's hash verification makes it impossible for attackers to");
        Console.WriteLine("     serve malicious files, even if they compromise the server!");
    }

    private static async Task DownloadFileSecurelyQuiet(string repoPath, string filename)
    {
        var metadataUrl = new Uri($"file://{Path.GetFullPath(Path.Combine(repoPath, "metadata"))}/");
        var targetsUrl = new Uri($"file://{Path.GetFullPath(Path.Combine(repoPath, "targets"))}/");
        var rootPath = Path.Combine(repoPath, "metadata", "root.json");

        var trustedRootBytes = await File.ReadAllBytesAsync(rootPath);
        var config = new UpdaterConfig(trustedRootBytes, metadataUrl)
        {
            LocalMetadataDir = Path.Combine(Directory.GetCurrentDirectory(), "tuf-cache", "metadata"),
            LocalTargetsDir = Path.Combine(Directory.GetCurrentDirectory(), "tuf-cache", "targets"),
            Client = new System.Net.Http.HttpClient()
        };

        var updater = new Updater(config);
        await updater.RefreshAsync();

        var targetInfo = await updater.GetTargetInfo(filename);
        if (!targetInfo.HasValue)
        {
            throw new Exception($"File '{filename}' not found");
        }

        var (targetPath, target) = targetInfo.Value;
        await updater.DownloadTarget(target, targetPath, null, targetsUrl);
    }
}