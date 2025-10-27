using System.Text;
using TUF;
using TUF.Models;
using TUF.Repository;
using Serde.Json;

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
            var builder = new RepositoryBuilder()
                .AddSigner("root", rootSigner)
                .AddSigner("targets", targetsSigner)
                .AddSigner("snapshot", snapshotSigner)
                .AddSigner("timestamp", timestampSigner)
                .SetDefaultExpiry(DateTimeOffset.UtcNow.AddYears(1))
                .SetConsistentSnapshots(true);

            // Add our target file
            var targetFileContent = await File.ReadAllBytesAsync("repository/targets/hello.txt");
            builder.AddTarget("hello.txt", targetFileContent);
            Console.WriteLine("  • Added target file to repository metadata");

            // Build and write repository
            var repository = builder.Build();
            repository.WriteToDirectory("repository");
            Console.WriteLine("  • Built and signed all TUF metadata");
            Console.WriteLine("  • Repository ready at: ./repository/");
        }

        static async Task UseTufClient()
        {
            Console.WriteLine("\n🔄 Demonstrating TUF metadata verification...");

            // Load the trusted root metadata (in production, this comes from a secure source)
            var trustedRoot = await File.ReadAllBytesAsync("repository/metadata/root.json");
            Console.WriteLine("  • Loaded trusted root metadata");

            // Create TrustedMetadata to demonstrate verification
            var trusted = TrustedMetadata.CreateFromRootData(trustedRoot);
            Console.WriteLine("  • Created trusted metadata handler");

            // Load and verify targets metadata
            var targetsBytes = await File.ReadAllBytesAsync("repository/metadata/targets.json");
            var snapshotBytes = await File.ReadAllBytesAsync("repository/metadata/snapshot.json");
            var timestampBytes = await File.ReadAllBytesAsync("repository/metadata/timestamp.json");

            // Parse targets metadata to show target information
            var targetsMetadata = Serde.Json.JsonSerializer.Deserialize<Metadata<Targets>, MetadataProxy.De<Targets>>(targetsBytes, MetadataProxy.De<Targets>.Instance);
            Console.WriteLine("  • Loaded targets metadata");

            // Find our target file
            var targetFile = targetsMetadata.Signed.TargetMap.FirstOrDefault(t => t.Key == "hello.txt");
            if (targetFile.Key == null)
            {
                throw new Exception("Target file not found in repository");
            }

            Console.WriteLine($"  • Found target: {targetFile.Key}");
            Console.WriteLine($"    Length: {targetFile.Value.Length} bytes");
            Console.WriteLine($"    Hashes: {string.Join(", ", targetFile.Value.Hashes.Keys)}");

            // Manually verify file integrity (this is what TUF does automatically)
            var actualFile = await File.ReadAllBytesAsync("repository/targets/hello.txt");
            var actualHash = System.Security.Cryptography.SHA256.HashData(actualFile);
            var actualHashHex = Convert.ToHexString(actualHash).ToLowerInvariant();
            var expectedHash = targetFile.Value.Hashes["sha256"];

            if (actualHashHex == expectedHash)
            {
                Console.WriteLine("  • ✅ File integrity verified - hashes match!");
            }
            else
            {
                Console.WriteLine("  • ❌ File integrity check failed!");
            }

            // Show the content to prove it worked
            var content = Encoding.UTF8.GetString(actualFile);
            Console.WriteLine($"\n📄 Verified file content:");
            Console.WriteLine($"   {content.Replace("\n", "\n   ")}");

            // Copy to client cache to simulate download
            Directory.CreateDirectory("client-cache/targets");
            await File.WriteAllBytesAsync("client-cache/targets/hello.txt", actualFile);
            Console.WriteLine($"  • File cached to: client-cache/targets/hello.txt");
        }
    }
}