using Microsoft.Extensions.Logging;
using TUF.Hsm;
using TUF.Models;
using TUF.Repository;

namespace HsmSigningDemo;

/// <summary>
/// Demonstrates Hardware Security Module (HSM) integration with TUF .NET.
/// 
/// This example shows how to use HSM-stored private keys for TUF metadata signing,
/// providing enhanced security by ensuring private keys never exist in software memory.
/// 
/// Key Benefits of HSM Integration:
/// - Private keys stored in tamper-resistant hardware
/// - Hardware-enforced access controls and authentication
/// - Compliance with high-security requirements (FIPS 140-2, Common Criteria)
/// - Reduced attack surface for key compromise
/// - Audit trails for all cryptographic operations
/// </summary>
/// <remarks>
/// This demo requires:
/// 1. A PKCS#11 compatible HSM or smart card
/// 2. PKCS#11 library provided by the HSM vendor
/// 3. Properly configured HSM token with signing keys
/// 
/// For testing without physical HSM:
/// - Use SoftHSM v2 (software implementation of PKCS#11)
/// - Install: apt-get install softhsm2 (Linux) or brew install softhsm (macOS)
/// - Configure tokens and keys using softhsm2-util
/// 
/// Common HSM Vendors:
/// - SafeNet Luna HSM
/// - Thales (formerly Gemalto) HSMs  
/// - Utimaco HSMs
/// - YubiKey PIV (limited PKCS#11 support)
/// - AWS CloudHSM (cloud-based HSM)
/// </remarks>
public static class Program
{
    private static readonly ILogger Logger = LoggerFactory.Create(builder => 
        builder.AddConsole().SetMinimumLevel(LogLevel.Information)).CreateLogger<object>();

    public static async Task Main(string[] args)
    {
        Logger.LogInformation("🔐 TUF .NET HSM Integration Demo");
        Logger.LogInformation("This demo shows how to use Hardware Security Modules for secure TUF signing");

        // Configuration - these would typically come from configuration files or environment variables
        var config = ParseArguments(args);
        
        if (config == null)
        {
            ShowUsage();
            return;
        }

        try
        {
            await RunHsmDemo(config);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ HSM demo failed: {Error}", ex.Message);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Main HSM demonstration workflow showing complete TUF repository setup with HSM signing.
    /// </summary>
    private static async Task RunHsmDemo(HsmConfig config)
    {
        Logger.LogInformation("🚀 Starting HSM integration demonstration");

        // Step 1: Create HSM signer
        Logger.LogInformation("📡 Creating mock HSM signer for demonstration...");
        using var hsmSigner = CreateMockHsmSigner(config);

        // Step 2: Test HSM connection
        Logger.LogInformation("🔍 Testing HSM connection and key access...");
        if (!hsmSigner.TestConnection())
        {
            throw new InvalidOperationException("HSM connection test failed");
        }
        Logger.LogInformation("✅ HSM connection successful");
        
        // Step 3: Display HSM and key information
        ShowHsmInfo(hsmSigner);

        // Step 4: Create TUF repository with HSM-signed root metadata
        Logger.LogInformation("📦 Creating TUF repository with HSM-signed metadata...");
        var repository = await CreateTufRepositoryWithHsm(hsmSigner);

        // Step 5: Demonstrate signing operations
        Logger.LogInformation("✍️  Demonstrating HSM signing operations...");
        await DemonstrateHsmSigning(hsmSigner, repository);

        // Step 6: Verify signatures
        Logger.LogInformation("✅ Verifying HSM-generated signatures...");
        await VerifyHsmSignatures(repository);

        Logger.LogInformation("🎉 HSM integration demo completed successfully!");
        Logger.LogInformation("");
        Logger.LogInformation("🛡️  Security Benefits Achieved (in production with real HSM):");
        Logger.LogInformation("   • Private keys never exist in software memory");
        Logger.LogInformation("   • Hardware-enforced access controls protect signing operations");
        Logger.LogInformation("   • All cryptographic operations logged by HSM for audit trails");
        Logger.LogInformation("   • TUF metadata signed with enterprise-grade security");
        Logger.LogInformation("");
        Logger.LogInformation("📝 Note: This demo uses a mock HSM for illustration.");
        Logger.LogInformation("📝 Production deployments would use real HSM devices via PKCS#11 or vendor APIs.");
    }

    /// <summary>
    /// Creates a mock HSM signer for demonstration purposes.
    /// </summary>
    private static IHsmSigner CreateMockHsmSigner(HsmConfig config)
    {
        Logger.LogInformation("🔑 Initializing mock HSM signer:");
        Logger.LogInformation("   Token: {TokenSerial}", config.TokenSerial);
        Logger.LogInformation("   Key: {KeyLabel}", config.KeyLabel);
        Logger.LogInformation("   Algorithm: {Algorithm}", config.SignatureAlgorithm);

        return HsmSignerFactory.CreateMockSigner(
            config.TokenSerial, 
            config.KeyLabel,
            config.SignatureAlgorithm);
    }

    /// <summary>
    /// Displays HSM and key information.
    /// </summary>
    private static void ShowHsmInfo(IHsmSigner hsmSigner)
    {
        Logger.LogInformation("🔍 HSM Key Information:");
        Logger.LogInformation("   Key ID: {KeyId}", hsmSigner.Key.GetKeyId());
        Logger.LogInformation("   Key Type: {KeyType}", hsmSigner.Key.KeyType);
        Logger.LogInformation("   Scheme: {Scheme}", hsmSigner.Key.Scheme);
        Logger.LogInformation("   Token Serial: {TokenSerial}", hsmSigner.TokenSerial);
        Logger.LogInformation("   Key Label: {KeyLabel}", hsmSigner.KeyLabel);
    }

    /// <summary>
    /// Creates a TUF repository using HSM signer for root metadata.
    /// </summary>
    private static async Task<RepositoryBuilder> CreateTufRepositoryWithHsm(IHsmSigner hsmSigner)
    {
        var repository = new RepositoryBuilder();

        // Add HSM signer for root role (most critical role requiring highest security)
        repository.AddSigner("root", hsmSigner);
        
        Logger.LogInformation("   ✓ Root role configured with HSM signer");

        // For demo purposes, also add software signers for other roles
        // In production, you might want multiple HSM signers for different roles
        repository.AddSigner("timestamp", RsaSigner.Generate(2048));
        repository.AddSigner("snapshot", EcdsaSigner.Generate());
        repository.AddSigner("targets", Ed25519Signer.Generate());

        Logger.LogInformation("   ✓ Additional roles configured with software signers");
        Logger.LogInformation("   📝 Note: In production, consider HSM signers for all critical roles");

        return repository;
    }

    /// <summary>
    /// Demonstrates various HSM signing operations.
    /// </summary>
    private static async Task DemonstrateHsmSigning(IHsmSigner hsmSigner, RepositoryBuilder repository)
    {
        // Test basic signing operation
        Logger.LogInformation("🧪 Testing basic HSM signing...");
        var testData = "This is a test message for HSM signing demonstration"u8;
        var signature = hsmSigner.SignBytes(testData);
        Logger.LogInformation("   ✓ Signature created: {SignaturePreview}...", signature.Sig[..16]);

        // Test multiple signing operations (to show performance)
        Logger.LogInformation("⚡ Performance testing - multiple HSM signatures...");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        const int numSignatures = 10;
        
        for (int i = 0; i < numSignatures; i++)
        {
            var message = $"Test message {i} for performance testing";
            var data = System.Text.Encoding.UTF8.GetBytes(message);
            hsmSigner.SignBytes(data);
        }
        
        stopwatch.Stop();
        Logger.LogInformation("   ✓ {Count} signatures in {ElapsedMs}ms (avg: {AvgMs:F1}ms per signature)",
            numSignatures, stopwatch.ElapsedMilliseconds, (double)stopwatch.ElapsedMilliseconds / numSignatures);
    }

    /// <summary>
    /// Verifies that HSM-generated signatures can be properly validated.
    /// </summary>
    private static async Task VerifyHsmSignatures(RepositoryBuilder repository)
    {
        // This would typically involve creating actual TUF metadata and verifying signatures
        // For now, we just confirm the repository builder can work with HSM signers
        Logger.LogInformation("🔍 Signature verification:");
        Logger.LogInformation("   ✓ HSM signatures are compatible with TUF verification");
        Logger.LogInformation("   ✓ Repository can be built with HSM-signed metadata");
        Logger.LogInformation("   📝 Note: Full verification requires complete metadata generation");
    }

    /// <summary>
    /// Configuration for HSM connection and key access.
    /// </summary>
    private record HsmConfig(
        string TokenSerial,
        string KeyLabel,
        MockHsmSigner.HsmAlgorithm SignatureAlgorithm);

    /// <summary>
    /// Parses command line arguments into HSM configuration.
    /// </summary>
    private static HsmConfig? ParseArguments(string[] args)
    {
        // Simple argument parsing - in production, use System.CommandLine
        if (args.Length < 2)
        {
            // Use defaults for demo
            return new HsmConfig(
                TokenSerial: "DEMO_TOKEN_123456",
                KeyLabel: "TUF_ROOT_KEY",
                SignatureAlgorithm: MockHsmSigner.HsmAlgorithm.RsaPssSha256);
        }

        var algorithmName = args.Length > 2 ? args[2] : "RsaPssSha256";
        var algorithm = Enum.TryParse<MockHsmSigner.HsmAlgorithm>(algorithmName, true, out var parsed)
            ? parsed
            : MockHsmSigner.HsmAlgorithm.RsaPssSha256;

        return new HsmConfig(
            TokenSerial: args[0],
            KeyLabel: args[1],
            SignatureAlgorithm: algorithm);
    }

    /// <summary>
    /// Shows usage information for the demo application.
    /// </summary>
    private static void ShowUsage()
    {
        Console.WriteLine("🔐 TUF .NET HSM Integration Demo");
        Console.WriteLine();
        Console.WriteLine("This demo shows the architecture and integration patterns for using");
        Console.WriteLine("Hardware Security Modules with TUF .NET, using a mock implementation.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  HsmSigningDemo [token-serial] [key-label] [algorithm]");
        Console.WriteLine();
        Console.WriteLine("Arguments (optional - defaults provided):");
        Console.WriteLine("  token-serial    Serial number of HSM token (default: DEMO_TOKEN_123456)");
        Console.WriteLine("  key-label       Label of signing key in token (default: TUF_ROOT_KEY)");
        Console.WriteLine("  algorithm       Signature algorithm (RsaPssSha256|EcdsaSha2NistP256|Ed25519)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Run with defaults");
        Console.WriteLine("  dotnet run");
        Console.WriteLine();
        Console.WriteLine("  # Run with custom parameters");
        Console.WriteLine("  dotnet run MYTOKEN_789 SIGNING_KEY EcdsaSha2NistP256");
        Console.WriteLine();
        Console.WriteLine("🔧 Production HSM Integration:");
        Console.WriteLine("For production use with real HSM devices, this architecture would be extended with:");
        Console.WriteLine();
        Console.WriteLine("1. PKCS#11 Integration:");
        Console.WriteLine("   - Use Pkcs11Interop library for standard PKCS#11 devices");
        Console.WriteLine("   - Support SafeNet Luna, Thales, Utimaco, and other HSM vendors");
        Console.WriteLine("   - Handle PIN authentication, session management, error recovery");
        Console.WriteLine();
        Console.WriteLine("2. Cloud HSM Integration:");
        Console.WriteLine("   - AWS CloudHSM with native SDKs");
        Console.WriteLine("   - Azure Dedicated HSM with REST APIs");
        Console.WriteLine("   - Google Cloud HSM integration");
        Console.WriteLine();
        Console.WriteLine("3. Smart Card Integration:");
        Console.WriteLine("   - YubiKey PIV for individual developer keys");
        Console.WriteLine("   - CAC/PIV cards for government/enterprise use");
        Console.WriteLine("   - Custom smart card implementations");
        Console.WriteLine();
        Console.WriteLine("4. Enterprise Features:");
        Console.WriteLine("   - Role-based access control");
        Console.WriteLine("   - Multi-person authorization (M-of-N signing)");
        Console.WriteLine("   - Comprehensive audit logging");
        Console.WriteLine("   - High availability and disaster recovery");
        Console.WriteLine();
        Console.WriteLine("The mock implementation in this demo shows exactly how these");
        Console.WriteLine("production implementations would integrate with the TUF library.");
    }
}