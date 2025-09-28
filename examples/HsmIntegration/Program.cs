using System.CommandLine;
using System.Text;
using HsmIntegration;
using Microsoft.Extensions.Logging;
using TUF.Models;

/// <summary>
/// TUF .NET HSM Integration Demonstration
/// 
/// This example demonstrates how to integrate Hardware Security Modules (HSMs) with TUF .NET
/// for enterprise-grade secure signing operations. Shows both Azure Key Vault HSM and 
/// PKCS#11 standard HSM integration patterns.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("TUF .NET HSM Integration Example")
        {
            Description = """
            Demonstrates enterprise Hardware Security Module (HSM) integration with TUF .NET.
            
            This example shows how to:
            • Integrate Azure Key Vault Premium/Managed HSM for cloud-based signing
            • Use PKCS#11 standard for on-premises HSM integration
            • Perform TUF metadata signing with hardware-backed private keys
            • Implement production security patterns and audit logging
            
            Prerequisites:
            • Azure Key Vault Premium tier or Managed HSM (for Azure demos)
            • Appropriate IAM permissions for key access
            • PKCS#11 compatible HSM (for PKCS#11 demos)
            """
        };

        // Azure Key Vault HSM commands
        var azureCommand = new Command("azure", "Azure Key Vault HSM integration examples");

        var vaultUriOption = new Option<string>("--vault-uri")
        {
            Description = "Azure Key Vault URI (e.g., https://myvault.vault.azure.net/)",
            Required = true
        };
        var keyNameOption = new Option<string>("--key-name")
        {
            Description = "Name of the key in Key Vault",
            Required = true
        };
        
        var azureInfoCommand = new Command("info", "Display information about Azure Key Vault HSM key")
        {
            vaultUriOption,
            keyNameOption
        };
        azureInfoCommand.SetAction(async (parseResult, token) =>
        {
            var vaultUri = parseResult.GetValue(vaultUriOption)!;
            var keyName = parseResult.GetValue(keyNameOption)!;
            await AzureKeyVaultInfoAsync(vaultUri, keyName);
            return 0;
        });

        var dataOption = new Option<string>("--data")
        {
            Description = "Data to sign",
            Required = true
        };
        var azureSignCommand = new Command("sign", "Sign data using Azure Key Vault HSM")
        {
            vaultUriOption,
            keyNameOption,
            dataOption
        };
        azureSignCommand.SetAction(async (parseResult, token) =>
        {
            var vaultUri = parseResult.GetValue(vaultUriOption)!;
            var keyName = parseResult.GetValue(keyNameOption)!;
            var data = parseResult.GetValue(dataOption)!;
            await AzureKeyVaultSignAsync(vaultUri, keyName, data);
            return 0;
        });

        azureCommand.Add(azureInfoCommand);
        azureCommand.Add(azureSignCommand);

        // PKCS#11 HSM commands  
        var pkcs11Command = new Command("pkcs11", "PKCS#11 HSM integration examples");

        var pkcs11InfoCommand = new Command("info", "Display information about PKCS#11 HSM capabilities");
        pkcs11InfoCommand.SetAction((parseResult, token) =>
        {
            Pkcs11InfoCommand();
            return Task.FromResult(0);
        });

        var pkcs11GuideCommand = new Command("guide", "Show production PKCS#11 integration guidance");
        pkcs11GuideCommand.SetAction((parseResult, token) =>
        {
            Pkcs11GuideCommand();
            return Task.FromResult(0);
        });

        pkcs11Command.Add(pkcs11InfoCommand);
        pkcs11Command.Add(pkcs11GuideCommand);

        // General TUF HSM examples
        var tufCommand = new Command("tuf-demo", "TUF metadata signing with HSM demonstration");

        var createRootCommand = new Command("create-root", "Create TUF root metadata with HSM signing");
        createRootCommand.SetAction((parseResult, token) =>
        {
            TufHsmDemoCommand();
            return Task.FromResult(0);
        });

        tufCommand.Add(createRootCommand);

        // Security and compliance information
        var securityCommand = new Command("security", "HSM security model and compliance information");
        securityCommand.SetAction((parseResult, token) =>
        {
            SecurityInfoCommand();
            return Task.FromResult(0);
        });

        rootCommand.Add(azureCommand);
        rootCommand.Add(pkcs11Command);
        rootCommand.Add(tufCommand);
        rootCommand.Add(securityCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }

    /// <summary>
    /// Displays information about an Azure Key Vault HSM key.
    /// </summary>
    static async Task AzureKeyVaultInfoAsync(string vaultUri, string keyName)
    {
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<AzureKeyVaultSigner>();

        try
        {
            Console.WriteLine($"🔑 Retrieving Azure Key Vault HSM key information...\n");
            
            var signer = await AzureKeyVaultSigner.CreateAsync(new Uri(vaultUri), keyName, logger);
            var keyInfo = signer.GetKeyInfo();

            Console.WriteLine($"Key Vault Key Information:");
            Console.WriteLine($"  Key Name: {keyInfo.KeyName}");
            Console.WriteLine($"  Key Type: {keyInfo.KeyType}");
            Console.WriteLine($"  Key Size: {keyInfo.KeySize} bits");
            Console.WriteLine($"  Curve: {keyInfo.CurveName ?? "N/A"}");
            Console.WriteLine($"  Hardware Backed: {(keyInfo.IsHardwareBacked ? "✅ Yes" : "❌ No")}");
            Console.WriteLine($"  HSM Platform: {keyInfo.HsmPlatform ?? "Software"}");
            Console.WriteLine($"  Created: {keyInfo.CreatedOn}");
            Console.WriteLine($"  Updated: {keyInfo.UpdatedOn}");
            Console.WriteLine($"  Vault URI: {keyInfo.KeyVaultUri}");
            Console.WriteLine();

            Console.WriteLine($"TUF Key Information:");
            Console.WriteLine($"  TUF Key Type: {signer.Key.KeyType}");
            Console.WriteLine($"  TUF Scheme: {signer.Key.Scheme}");
            Console.WriteLine($"  Key ID: {signer.Key.GetKeyId()}");
            Console.WriteLine($"  Public Key: {signer.Key.KeyVal.Public[..80]}...");
            Console.WriteLine();

            if (keyInfo.IsHardwareBacked)
            {
                Console.WriteLine("✅ This key is backed by FIPS 140-2 Level 3 certified HSM hardware");
                Console.WriteLine("✅ Private key material never leaves the HSM");
                Console.WriteLine("✅ All cryptographic operations performed in tamper-resistant hardware");
            }
            else
            {
                Console.WriteLine("⚠️  This key is software-backed (not HSM)");
                Console.WriteLine("   For production TUF signing, use Premium Key Vault or Managed HSM");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error accessing Azure Key Vault: {ex.Message}");
            Console.WriteLine("   Ensure you have proper authentication and permissions configured.");
        }
    }

    /// <summary>
    /// Signs data using Azure Key Vault HSM.
    /// </summary>
    static async Task AzureKeyVaultSignAsync(string vaultUri, string keyName, string data)
    {
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<AzureKeyVaultSigner>();

        try
        {
            Console.WriteLine($"🔐 Signing data with Azure Key Vault HSM...\n");
            
            var signer = await AzureKeyVaultSigner.CreateAsync(new Uri(vaultUri), keyName, logger);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            
            Console.WriteLine($"Data to sign: {data}");
            Console.WriteLine($"Data length: {dataBytes.Length} bytes");
            Console.WriteLine();

            var signature = signer.SignBytes(dataBytes);

            Console.WriteLine($"✅ Signing successful!");
            Console.WriteLine($"  Signature Algorithm: {signer.Key.Scheme}");
            Console.WriteLine($"  Key ID: {signature.KeyId}");
            Console.WriteLine($"  Signature: {signature.Sig[..80]}...");
            Console.WriteLine($"  Signature Length: {signature.Sig.Length / 2} bytes");
            Console.WriteLine();

            Console.WriteLine($"TUF Signature Object:");
            Console.WriteLine($"{{");
            Console.WriteLine($"  \"keyid\": \"{signature.KeyId}\",");
            Console.WriteLine($"  \"sig\": \"{signature.Sig}\"");
            Console.WriteLine($"}}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error signing with Azure Key Vault: {ex.Message}");
        }
    }

    /// <summary>
    /// Displays PKCS#11 HSM information and capabilities.
    /// </summary>
    static void Pkcs11InfoCommand()
    {
        Console.WriteLine("🔐 PKCS#11 HSM Integration Information\n");

        Console.WriteLine("PKCS#11 provides a vendor-neutral interface to Hardware Security Modules:");
        Console.WriteLine("• Industry standard for HSM integration (RFC 3335)");
        Console.WriteLine("• Support for diverse HSM hardware vendors");
        Console.WriteLine("• FIPS 140-2 Level 2/3 compliance depending on hardware");
        Console.WriteLine("• High-performance cryptographic operations");
        Console.WriteLine("• Tamper resistance and hardware-based key generation");
        Console.WriteLine();

        Console.WriteLine("Supported TUF Operations:");
        Console.WriteLine("• RSA-PSS signing with SHA-256 (rsassa-pss-sha256)");
        Console.WriteLine("• ECDSA signing with P-256 curve (ecdsa-sha2-nistp256)");
        Console.WriteLine("• Hardware-based random number generation");
        Console.WriteLine("• Secure key storage and management");
        Console.WriteLine();

        var vendors = Pkcs11IntegrationGuide.GetRecommendedVendors();
        Console.WriteLine("Recommended PKCS#11 HSM Vendors:");
        foreach (var vendor in vendors)
        {
            Console.WriteLine($"  • {vendor.Name} ({vendor.Company})");
            Console.WriteLine($"    Products: {vendor.Products}");
            Console.WriteLine($"    Website: {vendor.Website}");
            Console.WriteLine();
        }

        Console.WriteLine("Integration Requirements:");
        Console.WriteLine("• PKCS#11 library (.so/.dll/.dylib) from HSM vendor");
        Console.WriteLine("• Pkcs11Interop NuGet package for .NET integration");
        Console.WriteLine("• HSM initialization and token setup");
        Console.WriteLine("• Authentication credentials (PIN/certificate)");
        Console.WriteLine("• Network connectivity (for network-attached HSMs)");
    }

    /// <summary>
    /// Shows PKCS#11 integration code guidance.
    /// </summary>
    static void Pkcs11GuideCommand()
    {
        Console.WriteLine("📋 PKCS#11 Production Integration Guide\n");

        Console.WriteLine("This example includes a demonstration PKCS#11 signer showing the integration");
        Console.WriteLine("pattern. For production use, integrate with Pkcs11Interop library:\n");

        var example = Pkcs11IntegrationGuide.GetIntegrationExample();
        Console.WriteLine(example);
        Console.WriteLine();

        Console.WriteLine("Production Deployment Steps:");
        Console.WriteLine("1. Install HSM hardware and initialize tokens");
        Console.WriteLine("2. Generate or import TUF signing keys in HSM");
        Console.WriteLine("3. Configure PKCS#11 library path and credentials");
        Console.WriteLine("4. Implement production error handling and retry logic");
        Console.WriteLine("5. Set up comprehensive audit logging and monitoring");
        Console.WriteLine("6. Test failover and disaster recovery procedures");
        Console.WriteLine();

        Console.WriteLine("Security Considerations:");
        Console.WriteLine("• Use role-based authentication with strong PINs");
        Console.WriteLine("• Implement HSM clustering for high availability");
        Console.WriteLine("• Regular firmware updates and security patches");
        Console.WriteLine("• Physical security controls for HSM access");
        Console.WriteLine("• Comprehensive audit trail and log monitoring");
    }

    /// <summary>
    /// Demonstrates TUF root metadata creation with HSM signing.
    /// </summary>
    static void TufHsmDemoCommand()
    {
        Console.WriteLine("🛡️ TUF HSM Signing Demonstration\n");

        Console.WriteLine("This demonstrates the pattern for creating TUF metadata with HSM-backed signatures:");
        Console.WriteLine();

        Console.WriteLine("1. HSM Integration Pattern:");
        Console.WriteLine("   • Implement ISigner interface with HSM backend");
        Console.WriteLine("   • Private keys remain in hardware at all times");
        Console.WriteLine("   • All signing operations performed in HSM");
        Console.WriteLine("   • Public keys exported for TUF metadata");
        Console.WriteLine();

        Console.WriteLine("2. TUF Root Metadata Creation:");
        Console.WriteLine("   • Generate root signing keys in HSM");
        Console.WriteLine("   • Create initial root metadata with key information");
        Console.WriteLine("   • Sign root metadata with HSM-backed root key");
        Console.WriteLine("   • Distribute signed root metadata to clients");
        Console.WriteLine();

        Console.WriteLine("3. Operational Security:");
        Console.WriteLine("   • Root keys stored in offline HSM for maximum security");
        Console.WriteLine("   • Online HSM for timestamp/snapshot signing");
        Console.WriteLine("   • Key rotation procedures with HSM key generation");
        Console.WriteLine("   • Multi-person authorization for critical operations");
        Console.WriteLine();

        Console.WriteLine("Example TUF metadata signing workflow with HSM:");
        Console.WriteLine();
        Console.WriteLine("```csharp");
        Console.WriteLine("// 1. Create HSM signer");
        Console.WriteLine("var hsmSigner = await AzureKeyVaultSigner.CreateAsync(vaultUri, keyName);");
        Console.WriteLine();
        Console.WriteLine("// 2. Create TUF root metadata");
        Console.WriteLine("var rootMetadata = new Root");
        Console.WriteLine("{");
        Console.WriteLine("    Version = 1,");
        Console.WriteLine("    SpecVersion = \"1.0.0\",");
        Console.WriteLine("    Expires = DateTime.UtcNow.AddYears(1),");
        Console.WriteLine("    Keys = { [hsmSigner.Key.GetKeyId()] = hsmSigner.Key },");
        Console.WriteLine("    Roles = new Dictionary<string, Role>");
        Console.WriteLine("    {");
        Console.WriteLine("        [\"root\"] = new Role { KeyIds = [hsmSigner.Key.GetKeyId()], Threshold = 1 }");
        Console.WriteLine("    }");
        Console.WriteLine("};");
        Console.WriteLine();
        Console.WriteLine("// 3. Sign with HSM");
        Console.WriteLine("var metadata = new Metadata<Root> { Signed = rootMetadata };");
        Console.WriteLine("var canonicalBytes = CanonicalJson.Serialize(rootMetadata);");
        Console.WriteLine("var signature = hsmSigner.SignBytes(canonicalBytes);");
        Console.WriteLine("metadata.Signatures = [signature];");
        Console.WriteLine("```");
        Console.WriteLine();

        Console.WriteLine("This pattern ensures:");
        Console.WriteLine("✅ Private keys never leave HSM hardware");
        Console.WriteLine("✅ TUF specification compliance");
        Console.WriteLine("✅ Enterprise-grade security controls");
        Console.WriteLine("✅ Audit trail of all signing operations");
    }

    /// <summary>
    /// Displays HSM security model and compliance information.
    /// </summary>
    static void SecurityInfoCommand()
    {
        Console.WriteLine("🛡️ HSM Security Model and Compliance\n");

        Console.WriteLine("Hardware Security Modules provide the highest level of cryptographic security:");
        Console.WriteLine();

        Console.WriteLine("FIPS 140-2 Compliance Levels:");
        Console.WriteLine("  Level 1: Software cryptography (not recommended for production)");
        Console.WriteLine("  Level 2: Tamper-evident hardware with role-based authentication");
        Console.WriteLine("  Level 3: Tamper-resistant with identity-based authentication ⭐");
        Console.WriteLine("  Level 4: Tamper-responsive (zeroizes on physical intrusion)");
        Console.WriteLine();

        Console.WriteLine("TUF Security Benefits with HSM:");
        Console.WriteLine("✅ Private keys never exist outside secure hardware");
        Console.WriteLine("✅ Hardware-based random number generation");
        Console.WriteLine("✅ Tamper resistance prevents key extraction");
        Console.WriteLine("✅ Role-based access controls and authentication");
        Console.WriteLine("✅ Comprehensive audit logging of operations");
        Console.WriteLine("✅ High availability through clustering");
        Console.WriteLine("✅ Performance optimized cryptographic operations");
        Console.WriteLine();

        Console.WriteLine("Compliance Standards Supported:");
        Console.WriteLine("• FIPS 140-2 Level 2/3 (Federal Information Processing Standard)");
        Console.WriteLine("• Common Criteria EAL4+ (International security evaluation)");
        Console.WriteLine("• PKCS#11 v2.40 (Industry standard HSM interface)");
        Console.WriteLine("• ISO 15408 (Security evaluation criteria)");
        Console.WriteLine("• ITSEC E3 High (European security evaluation)");
        Console.WriteLine();

        Console.WriteLine("TUF Threat Mitigation with HSM:");
        Console.WriteLine("• Key compromise: Hardware protection prevents extraction");
        Console.WriteLine("• Insider threats: Multi-person authorization required");
        Console.WriteLine("• Physical attacks: Tamper resistance and detection");
        Console.WriteLine("• Side-channel attacks: Hardware countermeasures");
        Console.WriteLine("• Supply chain attacks: Secure key generation in HSM");
        Console.WriteLine();

        Console.WriteLine("Enterprise Deployment Architecture:");
        Console.WriteLine("┌─────────────┐    ┌──────────────┐    ┌─────────────┐");
        Console.WriteLine("│   Offline   │    │   Online     │    │   Client    │");
        Console.WriteLine("│   Root HSM  │───▶│ HSM Cluster  │───▶│ Validation  │");
        Console.WriteLine("│ (Air-gapped)│    │ (timestamp/  │    │  (public    │");
        Console.WriteLine("│             │    │  snapshot)   │    │   keys)     │");
        Console.WriteLine("└─────────────┘    └──────────────┘    └─────────────┘");
        Console.WriteLine();

        Console.WriteLine("This architecture provides:");
        Console.WriteLine("• Maximum security for root keys (offline/air-gapped)");
        Console.WriteLine("• High availability for online operations (clustering)");
        Console.WriteLine("• Separation of duties between key roles");
        Console.WriteLine("• Disaster recovery and business continuity");
    }
}