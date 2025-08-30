using TUF.Repository;
using TUF.Signing;

namespace RepositoryManager;

/// <summary>
/// Simple demonstration of TUF repository creation without complex CLI interface
/// </summary>
public static class SimpleExample
{
    public static void CreateSampleRepository(string outputPath)
    {
        Console.WriteLine("🔧 Creating TUF repository with repository management tools...");
        
        // Generate ephemeral keys for demonstration
        Console.WriteLine("⚠️  Generating ephemeral keys for demonstration...");
        Console.WriteLine("⚠️  In production, use secure key management!");
        
        var rootSigner = Ed25519Signer.Generate();
        var timestampSigner = Ed25519Signer.Generate();
        var snapshotSigner = Ed25519Signer.Generate();
        var targetsSigner = Ed25519Signer.Generate();

        // Create repository builder
        var builder = new RepositoryBuilder()
            .AddSigner("root", rootSigner)
            .AddSigner("timestamp", timestampSigner)
            .AddSigner("snapshot", snapshotSigner)
            .AddSigner("targets", targetsSigner)
            .SetDefaultExpiry(DateTimeOffset.UtcNow.AddYears(1))
            .SetConsistentSnapshots(true);

        // Add sample target files
        var sampleContent1 = System.Text.Encoding.UTF8.GetBytes("Hello, TUF! This is a sample target file.");
        builder.AddTarget("hello.txt", sampleContent1, new Dictionary<string, object>
        {
            ["description"] = "Sample greeting file"
        });

        var sampleContent2 = System.Text.Encoding.UTF8.GetBytes("{\"version\": \"1.0.0\", \"name\": \"sample-app\"}");
        builder.AddTarget("config/app.json", sampleContent2, new Dictionary<string, object>
        {
            ["description"] = "Application configuration",
            ["type"] = "config"
        });

        // Build and write repository
        var repository = builder.Build();
        repository.WriteToDirectory(outputPath);

        Console.WriteLine($"✅ Repository created successfully at: {outputPath}");
        Console.WriteLine();
        Console.WriteLine("Repository structure:");
        Console.WriteLine("├── metadata/");
        Console.WriteLine("│   ├── root.json");
        Console.WriteLine("│   ├── timestamp.json");
        Console.WriteLine("│   ├── snapshot.json");
        Console.WriteLine("│   └── targets.json");
        Console.WriteLine("└── targets/");
        Console.WriteLine("    ├── hello.txt");
        Console.WriteLine("    └── config/");
        Console.WriteLine("        └── app.json");
        Console.WriteLine();
        Console.WriteLine("🔒 Security Note:");
        Console.WriteLine("   - Keys generated for this demo are ephemeral");
        Console.WriteLine("   - For production, implement secure key storage");
        Console.WriteLine("   - Use hardware security modules (HSMs) when possible");
    }
}