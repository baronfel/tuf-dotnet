using System.CommandLine;
using System.Text;
using TUF.HSM;
using TUF.Models;

namespace HsmSigning;

/// <summary>
/// Demonstrates Hardware Security Module (HSM) signing capabilities for TUF.
/// Shows how to use YubiKey PIV, PKCS#11 devices, and Windows CNG providers.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("TUF HSM Signing Examples")
        {
            CreateYubiKeyCommand(),
            CreatePkcs11Command(),
            CreateWindowsCngCommand(),
            CreateCompareSignersCommand()
        };

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Creates the YubiKey PIV signing command.
    /// </summary>
    private static Command CreateYubiKeyCommand()
    {
        var slotOption = new Option<string>("--slot", () => "9C", "PIV slot (9A, 9C, 9D, 9E)");
        var pinOption = new Option<string>("--pin", () => "123456", "YubiKey PIN");
        var dataOption = new Option<string>("--data", () => "Hello TUF!", "Data to sign");

        var command = new Command("yubikey", "Sign data using YubiKey PIV")
        {
            slotOption,
            pinOption,
            dataOption
        };

        command.SetHandler(async (slot, pin, data) =>
        {
            await SignWithYubiKey(slot, pin, data);
        }, slotOption, pinOption, dataOption);

        return command;
    }

    /// <summary>
    /// Creates the generic PKCS#11 signing command.
    /// </summary>
    private static Command CreatePkcs11Command()
    {
        var libraryOption = new Option<string>("--library", "Path to PKCS#11 library");
        var slotIdOption = new Option<ulong>("--slot-id", () => 0, "PKCS#11 slot ID");
        var keyLabelOption = new Option<string>("--key-label", () => "TUF Key", "Key label");
        var keyTypeOption = new Option<string>("--key-type", () => "rsa", "Key type (rsa, ecdsa)");
        var pinOption = new Option<string>("--pin", () => "123456", "Token PIN");
        var dataOption = new Option<string>("--data", () => "Hello TUF!", "Data to sign");

        libraryOption.IsRequired = true;

        var command = new Command("pkcs11", "Sign data using PKCS#11 device")
        {
            libraryOption,
            slotIdOption,
            keyLabelOption,
            keyTypeOption,
            pinOption,
            dataOption
        };

        command.SetHandler(async (library, slotId, keyLabel, keyType, pin, data) =>
        {
            await SignWithPkcs11(library, slotId, keyLabel, keyType, pin, data);
        }, libraryOption, slotIdOption, keyLabelOption, keyTypeOption, pinOption, dataOption);

        return command;
    }

    /// <summary>
    /// Creates the Windows CNG signing command.
    /// </summary>
    private static Command CreateWindowsCngCommand()
    {
        var thumbprintOption = new Option<string>("--thumbprint", "Certificate thumbprint");
        var subjectOption = new Option<string>("--subject", "Certificate subject name");
        var dataOption = new Option<string>("--data", () => "Hello TUF!", "Data to sign");

        var command = new Command("windows-cng", "Sign data using Windows CNG certificate")
        {
            thumbprintOption,
            subjectOption,
            dataOption
        };

        command.SetHandler(async (thumbprint, subject, data) =>
        {
            if (!string.IsNullOrEmpty(thumbprint))
                await SignWithWindowsCng(thumbprint, null, data);
            else if (!string.IsNullOrEmpty(subject))
                await SignWithWindowsCng(null, subject, data);
            else
            {
                Console.WriteLine("Error: Either --thumbprint or --subject must be specified");
                Environment.Exit(1);
            }
        }, thumbprintOption, subjectOption, dataOption);

        return command;
    }

    /// <summary>
    /// Creates a command to compare different signer implementations.
    /// </summary>
    private static Command CreateCompareSignersCommand()
    {
        var command = new Command("compare", "Compare software vs HSM signing performance");

        command.SetHandler(async () =>
        {
            await CompareSigners();
        });

        return command;
    }

    /// <summary>
    /// Demonstrates YubiKey PIV signing.
    /// </summary>
    private static async Task SignWithYubiKey(string slotString, string pin, string data)
    {
        try
        {
            Console.WriteLine("🔐 YubiKey PIV Signing Demo");
            Console.WriteLine($"Slot: {slotString}, Data: '{data}'");
            Console.WriteLine();

            // Parse PIV slot
            var slot = slotString.ToUpperInvariant() switch
            {
                "9A" => PivSlot.Authentication,
                "9C" => PivSlot.DigitalSignature,
                "9D" => PivSlot.KeyManagement,
                "9E" => PivSlot.CardAuthentication,
                _ => throw new ArgumentException($"Invalid PIV slot: {slotString}")
            };

            Console.WriteLine("Initializing YubiKey signer...");
            using var signer = await YubiKeySigner.CreateAsync(slot, pin);

            Console.WriteLine("✅ YubiKey signer created successfully!");
            Console.WriteLine($"Provider: {signer.ProviderInfo.Name} v{signer.ProviderInfo.Version}");
            Console.WriteLine($"Description: {signer.ProviderInfo.Description}");
            
            if (signer.ProviderInfo.Metadata != null)
            {
                Console.WriteLine("Metadata:");
                foreach (var (key, value) in signer.ProviderInfo.Metadata)
                {
                    Console.WriteLine($"  {key}: {value}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("TUF Key Information:");
            Console.WriteLine($"  Key Type: {signer.Key.KeyType}");
            Console.WriteLine($"  Scheme: {signer.Key.Scheme}");
            Console.WriteLine($"  Key ID: {signer.Key.GetKeyId()}");
            Console.WriteLine();

            if (slot == PivSlot.DigitalSignature)
            {
                Console.WriteLine("⚠️  PIV slot 9C requires user presence - please touch your YubiKey when it blinks");
                Console.WriteLine();
            }

            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signature = await signer.SignBytesAsync(dataBytes);

            Console.WriteLine("✅ Data signed successfully!");
            Console.WriteLine($"Signature: {signature.Sig}");
            Console.WriteLine($"Key ID: {signature.KeyId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (ex.Message.Contains("YKCS11"))
            {
                Console.WriteLine();
                Console.WriteLine("💡 Tip: Make sure YubiKey PIV Tool is installed:");
                Console.WriteLine("  Windows: https://developers.yubico.com/yubico-piv-tool/Releases/");
                Console.WriteLine("  macOS: brew install yubico-piv-tool");
                Console.WriteLine("  Linux: sudo apt install yubico-piv-tool");
            }
        }
    }

    /// <summary>
    /// Demonstrates generic PKCS#11 device signing.
    /// </summary>
    private static async Task SignWithPkcs11(string library, ulong slotId, string keyLabel, string keyType, string pin, string data)
    {
        try
        {
            Console.WriteLine("🔐 PKCS#11 HSM Signing Demo");
            Console.WriteLine($"Library: {library}");
            Console.WriteLine($"Slot: {slotId}, Key: {keyLabel}, Type: {keyType}");
            Console.WriteLine();

            Console.WriteLine("Initializing PKCS#11 signer...");
            using var signer = await Pkcs11Signer.CreateAsync(library, slotId, keyLabel, keyType);

            Console.WriteLine("✅ PKCS#11 signer created successfully!");
            Console.WriteLine($"Provider: {signer.ProviderInfo.Name} v{signer.ProviderInfo.Version}");
            Console.WriteLine($"Description: {signer.ProviderInfo.Description}");
            Console.WriteLine();

            Console.WriteLine("Authenticating with PIN...");
            var authenticated = await signer.AuthenticateAsync(new PinCredentials(pin));
            if (!authenticated)
            {
                Console.WriteLine("❌ Authentication failed - check PIN");
                return;
            }

            Console.WriteLine("✅ Authenticated successfully!");
            Console.WriteLine();

            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signature = await signer.SignBytesAsync(dataBytes);

            Console.WriteLine("✅ Data signed successfully!");
            Console.WriteLine($"Signature: {signature.Sig}");
            Console.WriteLine($"Key ID: {signature.KeyId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Demonstrates Windows CNG certificate signing.
    /// </summary>
    private static async Task SignWithWindowsCng(string? thumbprint, string? subject, string data)
    {
        try
        {
            Console.WriteLine("🔐 Windows CNG Certificate Signing Demo");
            Console.WriteLine($"Certificate: {thumbprint ?? subject}");
            Console.WriteLine();

            Console.WriteLine("Initializing Windows CNG signer...");
            using var signer = thumbprint != null
                ? await WindowsCngSigner.CreateFromCertificateAsync(thumbprint)
                : await WindowsCngSigner.CreateFromSubjectAsync(subject!);

            Console.WriteLine("✅ Windows CNG signer created successfully!");
            Console.WriteLine($"Provider: {signer.ProviderInfo.Name} v{signer.ProviderInfo.Version}");
            Console.WriteLine($"Description: {signer.ProviderInfo.Description}");
            
            if (signer.ProviderInfo.Metadata != null)
            {
                Console.WriteLine("Certificate Info:");
                foreach (var (key, value) in signer.ProviderInfo.Metadata)
                {
                    Console.WriteLine($"  {key}: {value}");
                }
            }
            Console.WriteLine();

            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signature = await signer.SignBytesAsync(dataBytes);

            Console.WriteLine("✅ Data signed successfully!");
            Console.WriteLine($"Signature: {signature.Sig}");
            Console.WriteLine($"Key ID: {signature.KeyId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (OperatingSystem.IsWindows())
            {
                Console.WriteLine();
                Console.WriteLine("💡 Tip: Make sure you have a signing certificate in your Windows certificate store");
                Console.WriteLine("  You can view certificates with: certlm.msc (Local Machine) or certmgr.msc (Current User)");
            }
            else
            {
                Console.WriteLine("❌ Windows CNG is only available on Windows platforms");
            }
        }
    }

    /// <summary>
    /// Compares software vs HSM signing performance and capabilities.
    /// </summary>
    private static async Task CompareSigners()
    {
        Console.WriteLine("⚖️  TUF Signer Comparison");
        Console.WriteLine();

        var testData = "TUF signature performance test data"u8.ToArray();
        const int iterations = 100;

        // Software signers comparison
        Console.WriteLine("📊 Software Signers:");
        await BenchmarkSoftwareSigner("Ed25519", () => Ed25519Signer.Generate(), testData, iterations);
        await BenchmarkSoftwareSigner("RSA-PSS", () => RsaSigner.Generate(2048), testData, iterations);
        await BenchmarkSoftwareSigner("ECDSA", () => EcdsaSigner.Generate(), testData, iterations);
        
        Console.WriteLine();
        Console.WriteLine("🔐 HSM Signer Capabilities:");
        
        // Show available HSM types
        Console.WriteLine("  YubiKey PIV:");
        Console.WriteLine("    ✅ Hardware key protection");
        Console.WriteLine("    ✅ PIN authentication");
        Console.WriteLine("    ✅ User presence verification (slot 9C)");
        Console.WriteLine("    ✅ Air-gapped signing capability");
        Console.WriteLine("    ✅ FIPS 140-2 Level 2 (FIPS models)");
        Console.WriteLine("    📋 Supported: RSA 1024/2048, ECDSA P-256/P-384");
        
        Console.WriteLine();
        Console.WriteLine("  PKCS#11 Devices:");
        Console.WriteLine("    ✅ Universal HSM compatibility");
        Console.WriteLine("    ✅ Network-attached HSM support");
        Console.WriteLine("    ✅ Enterprise-grade security");
        Console.WriteLine("    ✅ Audit logging and access controls");
        Console.WriteLine("    📋 Device-dependent key types");
        
        Console.WriteLine();
        Console.WriteLine("  Windows CNG:");
        Console.WriteLine("    ✅ Native Windows integration");
        Console.WriteLine("    ✅ Smart card and TPM support");
        Console.WriteLine("    ✅ Certificate-based authentication");
        Console.WriteLine("    ✅ Enterprise PKI integration");
        Console.WriteLine("    📋 Supported: RSA, ECDSA");

        Console.WriteLine();
        Console.WriteLine("🎯 Recommendations:");
        Console.WriteLine("  Root Keys:     YubiKey PIV slot 9C (requires touch)");
        Console.WriteLine("  Targets Keys:  YubiKey PIV slot 9A or Windows CNG");
        Console.WriteLine("  Timestamp:     PKCS#11 HSM or Windows CNG (automated)");
        Console.WriteLine("  Snapshot:      PKCS#11 HSM or Windows CNG (automated)");
    }

    /// <summary>
    /// Benchmarks a software signer implementation.
    /// </summary>
    private static async Task BenchmarkSoftwareSigner<T>(string name, Func<T> signerFactory, byte[] data, int iterations) 
        where T : ISigner, IDisposable
    {
        using var signer = signerFactory();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            _ = signer.SignBytes(data);
        }

        stopwatch.Stop();
        var avgMs = stopwatch.ElapsedMilliseconds / (double)iterations;
        var signaturesPerSecond = (int)(1000.0 / avgMs);

        Console.WriteLine($"  {name,-10}: {avgMs:F2}ms/signature, {signaturesPerSecond:N0} sig/sec");
    }
}