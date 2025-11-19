using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using TUF;

namespace NuGetIntegration;

/// <summary>
/// Example demonstrating how to integrate TUF security with NuGet package management.
/// This example shows how to verify package integrity using TUF before installation.
/// 
/// Security Model:
/// 1. NuGet packages are treated as TUF targets
/// 2. TUF provides cryptographic verification of package metadata
/// 3. Package hashes are validated against TUF-secured metadata
/// 4. Only packages that pass TUF verification are installed
/// </summary>
class Program
{
    private static readonly ILogger<Program> Logger = 
        LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
                    .CreateLogger<Program>();

    static async Task Main(string[] args)
    {
        Console.WriteLine("🔐 TUF-Secured NuGet Package Integration Demo");
        Console.WriteLine("=============================================");
        
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run <tuf-repository-url> <package-name> [version]");
            Console.WriteLine("Example: dotnet run https://secure-packages.example.com Newtonsoft.Json 13.0.1");
            Console.WriteLine();
            Console.WriteLine("This example demonstrates:");
            Console.WriteLine("  • Verifying NuGet packages against TUF metadata");
            Console.WriteLine("  • Secure package installation workflow");
            Console.WriteLine("  • Integration with existing package management");
            return;
        }

        var tufRepositoryUrl = args[0];
        var packageName = args[1];
        var packageVersion = args.Length > 2 ? args[2] : "latest";

        try
        {
            var securePackageManager = new TufSecurePackageManager(tufRepositoryUrl);
            await securePackageManager.InitializeAsync();
            
            Logger.LogInformation("🔍 Verifying package {Package} version {Version}", packageName, packageVersion);
            
            var verificationResult = await securePackageManager.VerifyPackageAsync(packageName, packageVersion);
            
            if (verificationResult.IsValid)
            {
                Logger.LogInformation("✅ Package verification successful!");
                Logger.LogInformation("   Package: {Package} v{Version}", verificationResult.PackageName, verificationResult.Version);
                Logger.LogInformation("   Size: {Size:N0} bytes", verificationResult.Size);
                Logger.LogInformation("   SHA256: {Hash}", verificationResult.Sha256Hash?[..16] + "...");
                
                Logger.LogInformation("📦 Installing verified package...");
                await securePackageManager.InstallPackageAsync(verificationResult);
                Logger.LogInformation("🎉 Package installed successfully with TUF security guarantees!");
            }
            else
            {
                Logger.LogError("❌ Package verification failed: {Reason}", verificationResult.FailureReason);
                Logger.LogError("   This package may be compromised or not available in the secure repository.");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "💥 Error during secure package operation");
            Environment.Exit(1);
        }
    }
}

/// <summary>
/// Integrates TUF security with NuGet package management operations.
/// Ensures all package installations are cryptographically verified.
/// </summary>
public class TufSecurePackageManager
{
    private readonly string _tufRepositoryUrl;
    private readonly string _tempDir;
    private readonly ILogger<TufSecurePackageManager> _logger;
    private Updater? _tufUpdater;

    public TufSecurePackageManager(string tufRepositoryUrl)
    {
        _tufRepositoryUrl = tufRepositoryUrl;
        _tempDir = Path.Combine(Path.GetTempPath(), "tuf-nuget-integration", Guid.NewGuid().ToString());
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
                              .CreateLogger<TufSecurePackageManager>();
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("🔧 Initializing TUF-secured package manager");
        
        Directory.CreateDirectory(_tempDir);
        var metadataDir = Path.Combine(_tempDir, "metadata");
        var targetsDir = Path.Combine(_tempDir, "packages");
        
        Directory.CreateDirectory(metadataDir);
        Directory.CreateDirectory(targetsDir);

        // In production, this should be a real trusted root distributed with your application
        var trustedRoot = await GetTrustedRootAsync();
        
        var config = new UpdaterConfig(trustedRoot, new Uri(_tufRepositoryUrl))
        {
            LocalMetadataDir = metadataDir,
            LocalTargetsDir = targetsDir,
            Client = new HttpClient()
        };

        _tufUpdater = new Updater(config);
        
        _logger.LogInformation("🔄 Refreshing TUF metadata...");
        await _tufUpdater.RefreshAsync();
        _logger.LogInformation("✅ TUF initialization complete");
    }

    public async Task<PackageVerificationResult> VerifyPackageAsync(string packageName, string version)
    {
        if (_tufUpdater == null)
            throw new InvalidOperationException("TUF updater not initialized. Call InitializeAsync first.");

        try
        {
            // Convert NuGet package identifier to TUF target path
            var targetPath = $"packages/{packageName}/{version}/{packageName}.{version}.nupkg";
            
            _logger.LogInformation("🔍 Looking for TUF target: {TargetPath}", targetPath);
            
            var targetResult = await _tufUpdater.GetTargetInfo(targetPath);
            if (targetResult is not (var path, var targetInfo))
            {
                return new PackageVerificationResult(
                    PackageName: packageName,
                    Version: version,
                    IsValid: false,
                    FailureReason: $"Package {packageName} v{version} not found in TUF repository"
                );
            }

            // Extract expected hash from TUF metadata
            var sha256Hash = targetInfo.Hashes.FirstOrDefault(h => h.Key == "sha256").Value;
            if (string.IsNullOrEmpty(sha256Hash))
            {
                return new PackageVerificationResult(
                    PackageName: packageName,
                    Version: version,
                    IsValid: false,
                    FailureReason: "No SHA256 hash found in TUF metadata"
                );
            }

            return new PackageVerificationResult(
                PackageName: packageName,
                Version: version,
                IsValid: true,
                Size: targetInfo.Length,
                Sha256Hash: sha256Hash,
                TufTargetPath: path,
                TargetInfo: targetInfo
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying package {Package} v{Version}", packageName, version);
            return new PackageVerificationResult(
                PackageName: packageName,
                Version: version,
                IsValid: false,
                FailureReason: $"Verification error: {ex.Message}"
            );
        }
    }

    public async Task InstallPackageAsync(PackageVerificationResult verificationResult)
    {
        if (!verificationResult.IsValid || verificationResult.TargetInfo == null)
            throw new ArgumentException("Cannot install invalid package");

        _logger.LogInformation("📥 Downloading verified package from TUF repository...");
        
        // Download the package through TUF (ensures integrity verification)
        var downloadResult = await _tufUpdater!.DownloadTarget(
            verificationResult.TargetInfo, 
            verificationResult.TufTargetPath!, 
            null, 
            null
        );

        _logger.LogInformation("✅ Package downloaded and verified: {FilePath}", downloadResult.FilePath);
        
        // Additional integrity check using NuGet's package reader
        await VerifyPackageIntegrityAsync(downloadResult.FilePath, verificationResult);
        
        // In a real application, you would now install the package using NuGet APIs
        _logger.LogInformation("📦 Package ready for installation");
        _logger.LogInformation("   Location: {Location}", downloadResult.FilePath);
        _logger.LogInformation("   Verified Size: {Size:N0} bytes", downloadResult.Data.Length);
    }

    private async Task VerifyPackageIntegrityAsync(string packagePath, PackageVerificationResult expected)
    {
        _logger.LogInformation("🔐 Performing additional package integrity verification...");
        
        // Verify file hash matches TUF metadata
        using var fileStream = File.OpenRead(packagePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(fileStream);
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();
        
        if (!hashString.Equals(expected.Sha256Hash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Package hash mismatch! Expected: {expected.Sha256Hash}, Actual: {hashString}");
        }

        // Verify it's a valid NuGet package
        try
        {
            using var packageReader = new PackageArchiveReader(packagePath);
            var packageIdentity = packageReader.GetIdentity();
            
            _logger.LogInformation("📋 Package validation complete:");
            _logger.LogInformation("   Package ID: {Id}", packageIdentity.Id);
            _logger.LogInformation("   Version: {Version}", packageIdentity.Version);
            _logger.LogInformation("   Hash verified: ✅");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Invalid NuGet package format: {ex.Message}", ex);
        }
    }

    private async Task<byte[]> GetTrustedRootAsync()
    {
        // WARNING: This is a placeholder for demonstration purposes
        // In production, distribute a real trusted root with your application
        _logger.LogWarning("⚠️  Using placeholder trusted root for demonstration");
        
        return System.Text.Encoding.UTF8.GetBytes($$"""
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
    }
}

/// <summary>
/// Result of TUF-based package verification operation.
/// </summary>
public record PackageVerificationResult(
    string PackageName,
    string Version,
    bool IsValid,
    string? FailureReason = null,
    long Size = 0,
    string? Sha256Hash = null,
    string? TufTargetPath = null,
    TUF.Models.TargetFile? TargetInfo = null
);