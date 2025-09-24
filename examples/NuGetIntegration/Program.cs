using System.Security.Cryptography;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using TUF;

namespace NuGetIntegration;

/// <summary>
/// Demonstrates secure NuGet package validation using TUF (The Update Framework).
/// This example shows how to integrate TUF with NuGet to verify package integrity
/// and authenticity before installation, protecting against supply chain attacks.
/// </summary>
public class Program
{
    private static readonly ILogger Logger = NullLogger.Instance;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("🔒 TUF-Secured NuGet Package Validation Demo");
        Console.WriteLine("============================================");
        Console.WriteLine();

        if (args.Length == 4 && args[0] == "validate")
        {
            // Command line usage: dotnet run validate <package-id> <version> <tuf-metadata-url>
            await ValidatePackage(args[1], args[2], args[3]);
        }
        else
        {
            // Run interactive demo
            await RunDemo();
        }
    }

    private static async Task ValidatePackage(string packageId, string version, string tufMetadataUrl, 
        string sourceUrl = "https://api.nuget.org/v3/index.json")
    {
        Console.WriteLine("📦 Package Validation");
        Console.WriteLine($"Package: {packageId} v{version}");
        Console.WriteLine($"Source: {sourceUrl}");
        Console.WriteLine($"TUF Metadata: {tufMetadataUrl}");
        Console.WriteLine();

        try
        {
            // Step 1: Set up TUF client
            Console.WriteLine("Step 1: Initializing TUF client...");
            var tufClient = await SetupTufClient(tufMetadataUrl);
            Console.WriteLine("✅ TUF client initialized");

            // Step 2: Download package metadata from NuGet
            Console.WriteLine($"Step 2: Fetching package metadata from {sourceUrl}...");
            var packageMetadata = await GetPackageMetadata(packageId, version, sourceUrl);
            if (packageMetadata == null)
            {
                Console.WriteLine($"❌ Package {packageId} v{version} not found");
                return;
            }
            Console.WriteLine($"✅ Found package: {packageMetadata.Identity.Id} v{packageMetadata.Identity.Version}");

            // Step 3: Download and hash the package
            Console.WriteLine("Step 3: Downloading and hashing package...");
            var packageData = await DownloadPackage(packageMetadata, sourceUrl);
            var packageHash = ComputePackageHash(packageData);
            Console.WriteLine($"✅ Package downloaded ({packageData.Length:N0} bytes)");
            Console.WriteLine($"   SHA256: {packageHash}");

            // Step 4: Validate against TUF metadata
            Console.WriteLine("Step 4: Validating package against TUF metadata...");
            var targetFileName = $"{packageId}.{version}.nupkg";
            var isValid = await ValidatePackageWithTuf(tufClient, targetFileName, packageHash, packageData.Length);

            if (isValid)
            {
                Console.WriteLine("✅ Package validation successful!");
                Console.WriteLine("   The package integrity and authenticity have been verified using TUF.");
                Console.WriteLine("   It is safe to install this package.");
            }
            else
            {
                Console.WriteLine("❌ Package validation failed!");
                Console.WriteLine("   The package does not match the expected TUF metadata.");
                Console.WriteLine("   DO NOT install this package - it may be compromised!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during validation: {ex.Message}");
            Console.WriteLine("   Package validation could not be completed.");
        }
    }

    private static async Task RunDemo()
    {
        Console.WriteLine("🎯 TUF + NuGet Integration Demo");
        Console.WriteLine("===============================");
        Console.WriteLine();

        Console.WriteLine("This demo shows how TUF can be integrated with NuGet to provide:");
        Console.WriteLine("• Supply chain attack protection");
        Console.WriteLine("• Package integrity verification");
        Console.WriteLine("• Rollback attack prevention");
        Console.WriteLine("• Secure package metadata distribution");
        Console.WriteLine();

        Console.WriteLine("📋 Demo Scenarios:");
        Console.WriteLine();

        // Demo 1: Show TUF metadata structure
        await DemoTufMetadataStructure();

        // Demo 2: Show package validation workflow
        await DemoPackageValidationWorkflow();

        // Demo 3: Show security benefits
        await DemoSecurityBenefits();

        Console.WriteLine("🎉 Demo completed!");
        Console.WriteLine();
        Console.WriteLine("To validate real packages, use:");
        Console.WriteLine("   dotnet run validate <package-id> <version> <tuf-metadata-url>");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("   dotnet run validate Newtonsoft.Json 13.0.3 https://example.com/tuf/metadata");
    }

    private static async Task<Updater> SetupTufClient(string metadataUrl)
    {
        // Create temporary directories
        var tempDir = Path.Combine(Path.GetTempPath(), "tuf-nuget-integration", Guid.NewGuid().ToString());
        var metadataDir = Path.Combine(tempDir, "metadata");
        var targetsDir = Path.Combine(tempDir, "targets");

        Directory.CreateDirectory(metadataDir);
        Directory.CreateDirectory(targetsDir);

        // In a real implementation, this trusted root would be:
        // 1. Embedded in the application at build time
        // 2. Distributed through a secure channel (signed installer, etc.)
        // 3. Managed by the package registry operator (NuGet.org, etc.)
        var trustedRoot = GetDemoTrustedRoot();

        var config = new UpdaterConfig(trustedRoot, new Uri(metadataUrl))
        {
            LocalMetadataDir = metadataDir,
            LocalTargetsDir = targetsDir,
            Client = new HttpClient()
        };

        var updater = new Updater(config);
        await updater.RefreshAsync();

        return updater;
    }

    private static async Task<IPackageSearchMetadata?> GetPackageMetadata(string packageId, string version, string sourceUrl)
    {
        var packageSource = new PackageSource(sourceUrl);
        var sourceRepository = Repository.Factory.GetCoreV3(packageSource);
        var resource = await sourceRepository.GetResourceAsync<PackageSearchResource>();

        var searchFilter = new SearchFilter(includePrerelease: true);
        var packages = await resource.SearchAsync(packageId, searchFilter, 0, 10, Logger, CancellationToken.None);

        return packages.FirstOrDefault(p => 
            string.Equals(p.Identity.Id, packageId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Identity.Version.ToString(), version, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<byte[]> DownloadPackage(IPackageSearchMetadata package, string sourceUrl)
    {
        var packageSource = new PackageSource(sourceUrl);
        var sourceRepository = Repository.Factory.GetCoreV3(packageSource);
        var downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>();

        using var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
            package.Identity,
            new PackageDownloadContext(new NullSourceCacheContext()),
            Path.GetTempPath(),
            Logger,
            CancellationToken.None);

        using var memoryStream = new MemoryStream();
        await downloadResult.PackageStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private static string ComputePackageHash(byte[] packageData)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(packageData);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static async Task<bool> ValidatePackageWithTuf(Updater tufClient, string targetFileName, string expectedHash, long expectedLength)
    {
        try
        {
            // Get target information from TUF metadata
            if (await tufClient.GetTargetInfo(targetFileName) is not (var path, var targetInfo))
            {
                Console.WriteLine($"   Target '{targetFileName}' not found in TUF metadata");
                return false;
            }

            // Validate length
            if (targetInfo.Length != expectedLength)
            {
                Console.WriteLine($"   Length mismatch: expected {targetInfo.Length}, got {expectedLength}");
                return false;
            }

            // Validate hash (assuming SHA256 is used)
            if (targetInfo.Hashes.TryGetValue("sha256", out var tufHash))
            {
                if (!string.Equals(tufHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"   Hash mismatch: expected {tufHash}, got {expectedHash}");
                    return false;
                }
            }
            else
            {
                Console.WriteLine("   No SHA256 hash found in TUF metadata");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   TUF validation error: {ex.Message}");
            return false;
        }
    }

    private static async Task DemoTufMetadataStructure()
    {
        Console.WriteLine("1. 📁 TUF Metadata Structure for NuGet Integration");
        Console.WriteLine("   ------------------------------------------------");
        Console.WriteLine("   TUF uses four key metadata files to secure package distribution:");
        Console.WriteLine();
        Console.WriteLine("   • root.json     - Contains trusted signing keys and defines roles");
        Console.WriteLine("   • timestamp.json - Prevents freeze attacks, references snapshot.json");
        Console.WriteLine("   • snapshot.json  - Prevents mix-and-match attacks, references targets.json");
        Console.WriteLine("   • targets.json   - Contains package hashes and metadata");
        Console.WriteLine();
        Console.WriteLine("   For NuGet integration, targets.json would contain entries like:");
        Console.WriteLine("   {");
        Console.WriteLine("     \"Newtonsoft.Json.13.0.3.nupkg\": {");
        Console.WriteLine("       \"length\": 1234567,");
        Console.WriteLine("       \"hashes\": {");
        Console.WriteLine("         \"sha256\": \"abc123...\"");
        Console.WriteLine("       }");
        Console.WriteLine("     }");
        Console.WriteLine("   }");
        Console.WriteLine();
        await Task.Delay(500); // Small delay for demo pacing
    }

    private static async Task DemoPackageValidationWorkflow()
    {
        Console.WriteLine("2. ⚡ Package Validation Workflow");
        Console.WriteLine("   -------------------------------");
        Console.WriteLine("   The TUF + NuGet integration workflow:");
        Console.WriteLine();
        Console.WriteLine("   1. Initialize TUF client with trusted root metadata");
        Console.WriteLine("   2. Refresh TUF metadata (root → timestamp → snapshot → targets)");
        Console.WriteLine("   3. User requests package installation (e.g., 'dotnet add package Newtonsoft.Json')");
        Console.WriteLine("   4. Download package from NuGet source");
        Console.WriteLine("   5. Compute package hash (SHA256)");
        Console.WriteLine("   6. Look up package in TUF targets.json metadata");
        Console.WriteLine("   7. Compare computed hash with TUF metadata hash");
        Console.WriteLine("   8. Validate package length matches TUF metadata");
        Console.WriteLine("   9. If validation passes, install package; otherwise, reject");
        Console.WriteLine();
        await Task.Delay(500);
    }

    private static async Task DemoSecurityBenefits()
    {
        Console.WriteLine("3. 🛡️ Security Benefits");
        Console.WriteLine("   --------------------");
        Console.WriteLine("   TUF integration provides protection against common supply chain attacks:");
        Console.WriteLine();
        Console.WriteLine("   • Arbitrary software installation: Only packages in TUF metadata are trusted");
        Console.WriteLine("   • Rollback attacks: TUF prevents downgrade to vulnerable versions");
        Console.WriteLine("   • Indefinite freeze attacks: Timestamp.json has expiration dates");
        Console.WriteLine("   • Mix-and-match attacks: Snapshot.json ensures metadata consistency");
        Console.WriteLine("   • Wrong software installation: Package hashes must match exactly");
        Console.WriteLine("   • Extraneous dependencies: Only declared dependencies are allowed");
        Console.WriteLine();
        Console.WriteLine("   Real-world scenarios TUF would prevent:");
        Console.WriteLine("   ❌ Compromised packages (like event-stream, ua-parser-js incidents)");
        Console.WriteLine("   ❌ Typosquatting attacks (packages with similar names)");
        Console.WriteLine("   ❌ Dependency confusion attacks");
        Console.WriteLine("   ❌ Malicious package updates");
        Console.WriteLine();
        await Task.Delay(500);
    }

    private static byte[] GetDemoTrustedRoot()
    {
        // This is a demo implementation. In production, this would be:
        // 1. A real trusted root metadata signed by the package registry
        // 2. Embedded in the application or obtained through secure channels
        // 3. Regularly rotated and updated following TUF key management practices

        Console.WriteLine("⚠️  Using demo trusted root - not for production use!");

        return System.Text.Encoding.UTF8.GetBytes(@"{
            ""signatures"": [],
            ""signed"": {
                ""_type"": ""root"",
                ""spec_version"": ""1.0.0"",
                ""version"": 1,
                ""expires"": """ + DateTimeOffset.UtcNow.AddMonths(6).ToString("yyyy-MM-ddTHH:mm:ssZ") + @""",
                ""keys"": {
                    ""demo_key_1"": {
                        ""keytype"": ""ed25519"",
                        ""scheme"": ""ed25519"",
                        ""keyval"": {""public"": ""abcd1234...""}
                    }
                },
                ""roles"": {
                    ""root"": {""keyids"": [""demo_key_1""], ""threshold"": 1},
                    ""targets"": {""keyids"": [""demo_key_1""], ""threshold"": 1},
                    ""snapshot"": {""keyids"": [""demo_key_1""], ""threshold"": 1},
                    ""timestamp"": {""keyids"": [""demo_key_1""], ""threshold"": 1}
                }
            }
        }");
    }
}