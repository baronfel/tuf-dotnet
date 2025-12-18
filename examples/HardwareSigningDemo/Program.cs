using System.Text;

using Microsoft.Extensions.Logging;

using TUF.Models;

using static TUF.Models.MetadataProxy;

namespace HardwareSigningDemo;

/// <summary>
/// Demonstrates hardware-based signing for TUF operations using PIV smart cards,
/// YubiKeys, and other hardware security modules (HSMs).
/// 
/// This example shows how to:
/// 1. Discover available hardware signing devices
/// 2. List compatible keys for TUF operations
/// 3. Perform air-gapped signing workflows
/// 4. Create TUF metadata with hardware-backed signatures
/// 5. Handle hardware signing errors and user interaction
/// </summary>
/// <remarks>
/// Hardware signing provides enhanced security for TUF deployments by:
/// - Keeping private keys in tamper-resistant hardware
/// - Requiring physical presence/PIN for signing operations
/// - Enabling air-gapped signing workflows for critical root keys
/// - Providing audit trails and non-repudiation
/// 
/// This demo focuses on PIV (Personal Identity Verification) cards and YubiKeys,
/// but the architecture supports extending to other HSM types.
/// </remarks>
public class Program
{
    private static readonly ILogger Logger = LoggerFactory.Create(builder =>
        builder.AddConsole().SetMinimumLevel(LogLevel.Information)
    ).CreateLogger<Program>();

    public static async Task Main(string[] args)
    {
        Logger.LogInformation("TUF Hardware Signing Demo");
        Logger.LogInformation("Demonstrating secure TUF operations with PIV cards and YubiKeys");

        try
        {
            await RunHardwareSigningDemo();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Hardware signing demo failed");
            Environment.Exit(1);
        }
    }

    private static async Task RunHardwareSigningDemo()
    {
        // Step 1: Initialize hardware signing manager
        Logger.LogInformation("\n=== Step 1: Initialize Hardware Signing Manager ===");

        var manager = new HardwareSigningManager();
        Logger.LogInformation($"Initialized hardware signing manager with {manager.Providers.Count} providers:");

        foreach (var provider in manager.Providers)
        {
            var status = provider.IsAvailable ? "✓ Available" : "✗ Not Available";
            Logger.LogInformation($"  - {provider.ProviderName}: {status}");
        }

        // Step 2: Discover available hardware keys
        Logger.LogInformation("\n=== Step 2: Discover Available Hardware Keys ===");

        var allKeys = manager.GetAllAvailableKeys().ToList();
        if (allKeys.Count == 0)
        {
            Logger.LogWarning("No hardware signing keys found.");
            Logger.LogInformation("\nTo use this demo, you need:");
            Logger.LogInformation("  1. A PIV-compatible smart card or YubiKey");
            Logger.LogInformation("  2. A certificate with private key installed in your Windows certificate store");
            Logger.LogInformation("  3. The certificate should be accessible to the current user");
            Logger.LogInformation("\nFor YubiKey setup, see: https://developers.yubico.com/PIV/");
            return;
        }

        Logger.LogInformation($"Found {allKeys.Count} hardware signing keys:");
        for (int i = 0; i < allKeys.Count; i++)
        {
            var (provider, key) = allKeys[i];
            Logger.LogInformation($"  [{i + 1}] {key.Identifier}");
            Logger.LogInformation($"      Provider: {provider.ProviderName}");
            Logger.LogInformation($"      Algorithm: {key.Algorithm} {key.KeySize}");
            Logger.LogInformation($"      Certificate: {(key.Certificate != null ? "Present" : "None")}");

            if (key.Certificate != null)
            {
                Logger.LogInformation($"      Valid: {key.Certificate.NotBefore:yyyy-MM-dd} to {key.Certificate.NotAfter:yyyy-MM-dd}");
            }
        }

        // Step 3: Filter for TUF-compatible keys
        Logger.LogInformation("\n=== Step 3: Filter for TUF-Compatible Keys ===");

        var tufKeys = manager.GetTufCompatibleKeys().ToList();
        Logger.LogInformation($"Found {tufKeys.Count} TUF-compatible keys:");

        foreach (var (provider, key) in tufKeys)
        {
            var scheme = GetTufScheme(key);
            Logger.LogInformation($"  - {key.Identifier} → TUF Scheme: {scheme}");
        }

        if (tufKeys.Count == 0)
        {
            Logger.LogWarning("No TUF-compatible keys found. Keys must be:");
            Logger.LogInformation("  - RSA: 2048+ bits (3072+ recommended)");
            Logger.LogInformation("  - ECDSA: P-256 curve (256-bit)");
            Logger.LogInformation("  - Ed25519: Full key (if supported by hardware)");
            return;
        }

        // Step 4: Demonstrate hardware signing
        Logger.LogInformation("\n=== Step 4: Demonstrate Hardware Signing ===");

        var selectedKey = tufKeys.First();
        Logger.LogInformation($"Using key: {selectedKey.Key.Identifier}");

        try
        {
            // Create hardware-backed signer
            var signer = manager.CreateSigner(selectedKey);
            Logger.LogInformation($"Created hardware signer with Key ID: {signer.Key.GetKeyId()}");

            // Demonstrate signing operation
            var testData = "This is test data for TUF hardware signing demonstration."u8.ToArray();
            Logger.LogInformation("Performing hardware signing operation...");
            Logger.LogInformation("(If prompted, please enter your PIV PIN)");

            var signature = signer.SignBytes(testData);
            Logger.LogInformation("✓ Hardware signing successful!");
            Logger.LogInformation($"  Key ID: {signature.KeyId}");
            Logger.LogInformation($"  Signature: {signature.Sig[..16]}... ({signature.Sig.Length / 2} bytes)");

            // Dispose signer to clean up hardware resources
            if (signer is IDisposable disposableSigner)
            {
                disposableSigner.Dispose();
                Logger.LogInformation("✓ Hardware resources cleaned up");
            }
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogError($"Hardware signing failed: {ex.Message}");
            Logger.LogInformation("Common causes:");
            Logger.LogInformation("  - PIV PIN not entered or incorrect");
            Logger.LogInformation("  - Smart card/YubiKey not present");
            Logger.LogInformation("  - Certificate private key not accessible");
            Logger.LogInformation("  - Hardware device driver issues");
        }

        // Step 5: Demonstrate TUF metadata signing workflow
        Logger.LogInformation("\n=== Step 5: TUF Metadata Signing Workflow ===");
        await DemonstrateTufMetadataSigning(manager, tufKeys);

        // Step 6: Air-gapped signing demonstration
        Logger.LogInformation("\n=== Step 6: Air-Gapped Signing Workflow ===");
        DemonstrateAirGappedSigning(manager, tufKeys);

        Logger.LogInformation("\n=== Hardware Signing Demo Complete ===");
        Logger.LogInformation("Hardware signing provides enhanced security for TUF deployments.");
        Logger.LogInformation("Consider using hardware keys for root and critical intermediate signing operations.");
    }

    private static async Task DemonstrateTufMetadataSigning(
        HardwareSigningManager manager,
        List<(IHardwareSigningProvider Provider, HardwareSigningKey Key)> tufKeys)
    {
        Logger.LogInformation("Creating sample TUF Root metadata with hardware signature...");

        try
        {
            var selectedKey = tufKeys.First();
            var signer = manager.CreateSigner(selectedKey);

            // Create sample Root metadata
            var rootMetadata = new Root
            {
                SpecVersion = "1.0.0",
                Version = 1,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Keys = new Dictionary<string, Key>
                {
                    [signer.Key.GetKeyId()] = signer.Key
                },
                Roles = new Roles
                {
                    Root = new RoleKeys
                    {
                        KeyIds = [signer.Key.GetKeyId()],
                        Threshold = 1
                    },
                    Timestamp = new RoleKeys
                    {
                        KeyIds = [signer.Key.GetKeyId()],
                        Threshold = 1
                    },
                    Snapshot = new RoleKeys
                    {
                        KeyIds = [signer.Key.GetKeyId()],
                        Threshold = 1
                    },
                    Targets = new RoleKeys
                    {
                        KeyIds = [signer.Key.GetKeyId()],
                        Threshold = 1
                    }
                },
                ConsistentSnapshot = false
            };

            // Create signed metadata
            var metadata = new Metadata<Root>
            {
                Signatures = [],
                Signed = rootMetadata
            };

            // Sign with hardware key
            Logger.LogInformation("Signing TUF Root metadata with hardware key...");
            var canonicalBytes = CanonicalJson.Serializer.Serialize(rootMetadata);
            var signature = signer.SignBytes(canonicalBytes);

            metadata.Signatures.Add(signature);

            Logger.LogInformation("✓ TUF Root metadata signed with hardware key!");
            Logger.LogInformation($"  Metadata Version: {rootMetadata.Version}");
            Logger.LogInformation($"  Expires: {rootMetadata.Expires:yyyy-MM-dd}");
            Logger.LogInformation($"  Hardware Key ID: {signature.KeyId}");
            Logger.LogInformation($"  Hardware Signature: {signature.Sig[..16]}...");

            // Verify the signature
            Logger.LogInformation("Verifying hardware signature...");
            var isValid = signer.Key.VerifySignature(signature.Sig, canonicalBytes);
            Logger.LogInformation($"✓ Signature verification: {(isValid ? "VALID" : "INVALID")}");

            // Serialize to JSON for inspection
            var json = CanonicalJson.Serializer.Serialize<Metadata<Root>, Ser<Root>>(metadata);
            var jsonString = Encoding.UTF8.GetString(json);
            Logger.LogInformation($"Generated TUF metadata size: {jsonString.Length} characters");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TUF metadata signing failed");
        }
    }

    private static void DemonstrateAirGappedSigning(
        HardwareSigningManager manager,
        List<(IHardwareSigningProvider Provider, HardwareSigningKey Key)> tufKeys)
    {
        Logger.LogInformation("Air-gapped signing workflow demonstration:");
        Logger.LogInformation("");
        Logger.LogInformation("Air-gapped signing is critical for TUF root key security:");
        Logger.LogInformation("");

        Logger.LogInformation("1. OFFLINE PREPARATION:");
        Logger.LogInformation("   - Root signing key stored on YubiKey/PIV card");
        Logger.LogInformation("   - YubiKey physically isolated from network");
        Logger.LogInformation("   - Metadata prepared on online system");
        Logger.LogInformation("");

        Logger.LogInformation("2. OFFLINE SIGNING:");
        Logger.LogInformation("   - Transfer unsigned metadata to air-gapped system");
        Logger.LogInformation("   - Insert YubiKey into air-gapped system");
        Logger.LogInformation("   - Sign metadata using hardware key");
        Logger.LogInformation("   - Extract signed metadata");
        Logger.LogInformation("");

        Logger.LogInformation("3. ONLINE DEPLOYMENT:");
        Logger.LogInformation("   - Transfer signed metadata back to online system");
        Logger.LogInformation("   - Verify signatures before deployment");
        Logger.LogInformation("   - Deploy to TUF repository");
        Logger.LogInformation("");

        if (tufKeys.Any())
        {
            var selectedKey = tufKeys.First();
            Logger.LogInformation($"Example air-gapped setup with: {selectedKey.Key.Identifier}");
            Logger.LogInformation($"Hardware provider: {selectedKey.Provider.ProviderName}");
            Logger.LogInformation($"Key algorithm: {selectedKey.Key.Algorithm} {selectedKey.Key.KeySize}");

            if (selectedKey.Key.Certificate != null)
            {
                Logger.LogInformation($"Certificate subject: {selectedKey.Key.Certificate.Subject}");
                Logger.LogInformation($"Certificate expires: {selectedKey.Key.Certificate.NotAfter:yyyy-MM-dd}");
            }
        }

        Logger.LogInformation("");
        Logger.LogInformation("Benefits of hardware-based air-gapped signing:");
        Logger.LogInformation("✓ Private keys never leave secure hardware");
        Logger.LogInformation("✓ Physical isolation prevents remote attacks");
        Logger.LogInformation("✓ PIN/biometric authentication required");
        Logger.LogInformation("✓ Audit trail of signing operations");
        Logger.LogInformation("✓ Compliance with high-security requirements");
    }

    private static string GetTufScheme(HardwareSigningKey key)
    {
        return key.Algorithm switch
        {
            "RSA" => "rsassa-pss-sha256",
            "ECDSA" when key.KeySize == "256" => "ecdsa-sha2-nistp256",
            "Ed25519" => "ed25519",
            _ => "unknown"
        };
    }
}