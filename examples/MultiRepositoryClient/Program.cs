using System.Text.Json;
using TUF;
using TUF.MultiRepository;

namespace MultiRepositoryClient;

/// <summary>
/// Example demonstrating multi-repository TUF client usage (TAP 4).
/// 
/// This example shows how to:
/// 1. Configure multiple TUF repositories with a map.json file
/// 2. Use consensus validation across repositories
/// 3. Download targets with multi-repository verification
/// 
/// Security Note: This example uses sample data for demonstration.
/// In production, use proper trusted roots and secure key management.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🔗 TUF Multi-Repository Client Demo (TAP 4)");
        Console.WriteLine("==========================================");
        
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run <map.json> <target-file>");
            Console.WriteLine();
            Console.WriteLine("Example: dotnet run ./demo-map.json app.exe");
            Console.WriteLine();
            await CreateSampleMapFile();
            return;
        }

        var mapFile = args[0];
        var targetFile = args[1];

        try
        {
            // Initialize multi-repository client
            var config = new MultiRepositoryConfig
            {
                MapFilePath = mapFile,
                MetadataDir = "./metadata",
                TargetsDir = "./targets"
            };

            var client = new TUF.MultiRepositoryClient(config);
            
            Console.WriteLine($"📋 Loading configuration from: {mapFile}");
            await client.InitializeAsync();
            
            Console.WriteLine("🔄 Refreshing metadata from all repositories...");
            await client.RefreshAsync();
            
            Console.WriteLine($"🔍 Searching for target: {targetFile}");
            var result = await client.GetTargetInfoAsync(targetFile);
            
            DisplayTargetResult(result);
            
            if (result.IsValid && result.TargetInfo != null)
            {
                var downloadPath = Path.Combine("./downloads", targetFile);
                Directory.CreateDirectory("./downloads");
                
                Console.WriteLine($"⬇️  Downloading to: {downloadPath}");
                var success = await client.DownloadTargetAsync(targetFile, downloadPath);
                
                if (success)
                {
                    Console.WriteLine("✅ Download completed successfully!");
                    Console.WriteLine($"📄 File size: {new FileInfo(downloadPath).Length} bytes");
                }
                else
                {
                    Console.WriteLine("❌ Download failed");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("💡 Troubleshooting tips:");
            Console.WriteLine("  - Ensure map.json file exists and is valid");
            Console.WriteLine("  - Check that trusted root files exist");
            Console.WriteLine("  - Verify repository URLs are accessible");
            Console.WriteLine("  - Confirm target file exists in repositories");
        }
    }

    static void DisplayTargetResult(MultiRepositoryTargetResult result)
    {
        Console.WriteLine();
        Console.WriteLine("📊 Multi-Repository Search Results:");
        Console.WriteLine($"   Target Path: {result.TargetPath}");
        Console.WriteLine($"   Agreement Count: {result.AgreementCount}/{result.RepositoriesChecked.Length}");
        Console.WriteLine($"   Required Threshold: {result.RequiredThreshold}");
        Console.WriteLine($"   Repositories Checked: [{string.Join(", ", result.RepositoriesChecked)}]");
        
        if (result.IsValid && result.TargetInfo != null)
        {
            Console.WriteLine("✅ Consensus Achieved - Target Valid");
            Console.WriteLine($"   File Length: {result.TargetInfo.Length} bytes");
            Console.WriteLine($"   Hashes: {string.Join(", ", result.TargetInfo.Hashes.Select(h => $"{h.Algorithm}:{h.Digest[..8]}..."))}");
        }
        else if (result.TargetInfo != null)
        {
            Console.WriteLine("⚠️  Target Found but Insufficient Consensus");
            Console.WriteLine($"   Need {result.RequiredThreshold} agreements, got {result.AgreementCount}");
        }
        else
        {
            Console.WriteLine("❌ Target Not Found");
        }
        Console.WriteLine();
    }

    static async Task CreateSampleMapFile()
    {
        Console.WriteLine("📝 Creating sample map.json file...");
        
        var sampleMap = new MultiRepositoryMap(
            Repositories: new Dictionary<string, RepositoryInfo>
            {
                ["repo-a"] = new RepositoryInfo(
                    Name: "repo-a",
                    MetadataUrl: "https://example.com/repo-a/metadata",
                    TargetsUrl: "https://example.com/repo-a/targets", 
                    TrustedRootPath: "./trusted-roots/repo-a-root.json"
                ),
                ["repo-b"] = new RepositoryInfo(
                    Name: "repo-b", 
                    MetadataUrl: "https://example.com/repo-b/metadata",
                    TargetsUrl: "https://example.com/repo-b/targets",
                    TrustedRootPath: "./trusted-roots/repo-b-root.json"
                )
            },
            Mapping: new[]
            {
                // Critical files require both repositories to agree
                new Mapping(
                    Paths: new[] { new TUF.Models.Primitives.PathPattern("critical/*.exe") },
                    Repositories: new[] { "repo-a", "repo-b" },
                    Threshold: 2,
                    Terminating: true
                ),
                // Regular files need agreement from at least 1 repository
                new Mapping(
                    Paths: new[] { new TUF.Models.Primitives.PathPattern("*") },
                    Repositories: new[] { "repo-a", "repo-b" },
                    Threshold: 1,
                    Terminating: false
                )
            }
        );

        var json = JsonSerializer.Serialize(sampleMap, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await File.WriteAllTextAsync("./demo-map.json", json);
        Console.WriteLine("✅ Created ./demo-map.json");
        Console.WriteLine();
        Console.WriteLine("📁 You'll also need to create:");
        Console.WriteLine("   ./trusted-roots/repo-a-root.json");  
        Console.WriteLine("   ./trusted-roots/repo-b-root.json");
        Console.WriteLine();
        Console.WriteLine("🔧 Then run: dotnet run ./demo-map.json <target-file>");
    }
}