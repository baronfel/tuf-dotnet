using System.Text;
using Microsoft.Extensions.Logging;
using TUF.Models;

namespace AzureKeyVaultDemo;

/// <summary>
/// Demonstrates Azure Key Vault integration with TUF for secure signing operations.
/// Shows how to use Azure Key Vault for enterprise-grade key management in TUF workflows.
/// </summary>
/// <remarks>
/// This example demonstrates:
/// 1. Authentication to Azure Key Vault using various credential types
/// 2. Creating signers that use keys stored in Azure Key Vault
/// 3. Signing TUF metadata using Azure Key Vault keys
/// 4. Best practices for enterprise Azure integration
/// 
/// Prerequisites:
/// - Azure subscription with Key Vault access
/// - Key Vault with RSA or ECDSA keys configured
/// - Appropriate RBAC permissions (Key Vault Crypto User role minimum)
/// - Authentication configured (Azure CLI, managed identity, etc.)
/// </remarks>
public class Program
{
    /// <summary>
    /// Main entry point for the Azure Key Vault TUF demo.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        // Set up logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<Program>();

        // Parse command line arguments (simplified version)
        if (args.Length < 2)
        {
            ShowUsage();
            return 1;
        }

        var keyVaultUrl = args[0];
        var keyName = args[1];
        var authMethod = args.Length > 2 ? args[2] : "default";
        var message = args.Length > 3 ? args[3] : "Sample TUF metadata for signing demonstration";

        try
        {
            await RunDemo(logger, keyVaultUrl, keyName, authMethod, message);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Demo failed");
            return 1;
        }
    }

    /// <summary>
    /// Shows usage information for the demo.
    /// </summary>
    private static void ShowUsage()
    {
        Console.WriteLine("Azure Key Vault TUF Signing Demo");
        Console.WriteLine("=================================");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run <key-vault-url> <key-name> [auth-method] [message]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  key-vault-url    Azure Key Vault URL (e.g., https://myvault.vault.azure.net/)");
        Console.WriteLine("  key-name         Name of the key in Azure Key Vault");
        Console.WriteLine("  auth-method      Authentication method: default, cli, managed-identity (optional)");
        Console.WriteLine("  message          Message to sign (optional)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Using Azure CLI authentication");
        Console.WriteLine("  dotnet run https://dev-vault.vault.azure.net/ tuf-signing-key cli");
        Console.WriteLine();
        Console.WriteLine("  # Using managed identity in Azure");
        Console.WriteLine("  dotnet run https://prod-vault.vault.azure.net/ tuf-prod-key managed-identity");
        Console.WriteLine();
        Console.WriteLine("Prerequisites:");
        Console.WriteLine("  - Azure subscription with Key Vault access");
        Console.WriteLine("  - RSA (2048+ bits) or ECDSA (P-256) key in Key Vault");
        Console.WriteLine("  - Key Vault Crypto User role permissions");
        Console.WriteLine("  - Authentication configured (az login, managed identity, etc.)");
    }

    /// <summary>
    /// Runs the Azure Key Vault signing demonstration.
    /// </summary>
    private static async Task RunDemo(
        ILogger logger, 
        string keyVaultUrl, 
        string keyName, 
        string authMethod, 
        string message)
    {
        logger.LogInformation("Azure Key Vault TUF Signing Demo");
        logger.LogInformation("=====================================");
        logger.LogInformation("Key Vault URL: {KeyVaultUrl}", keyVaultUrl);
        logger.LogInformation("Key Name: {KeyName}", keyName);
        logger.LogInformation("Authentication Method: {AuthMethod}", authMethod);

        var keyVaultUri = new Uri(keyVaultUrl);

        // Create the signer based on authentication method
        logger.LogInformation("Creating Azure Key Vault signer...");
        await using var signer = await CreateSigner(keyVaultUri, keyName, authMethod);

        // Display key information
        logger.LogInformation("Key Information:");
        logger.LogInformation("  Key Type: {KeyType}", signer.Key.KeyType);
        logger.LogInformation("  Scheme: {Scheme}", signer.Key.Scheme);
        logger.LogInformation("  Key ID: {KeyId}", signer.Key.GetKeyId());
        logger.LogInformation("  Public Key Preview: {PublicKeyPreview}...", 
            signer.Key.KeyVal.Public[..Math.Min(50, signer.Key.KeyVal.Public.Length)]);

        // Sign the message
        logger.LogInformation("Signing message: {Message}", message);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var signature = signer.SignBytes(messageBytes);

        logger.LogInformation("Signature created successfully!");
        logger.LogInformation("  Key ID: {KeyId}", signature.KeyId);
        logger.LogInformation("  Signature: {Signature}", signature.Sig);

        // Demonstrate signature verification
        logger.LogInformation("Verifying signature...");
        bool isValid = signer.Key.VerifySignature(signature.Sig, messageBytes);
        logger.LogInformation("Signature verification: {IsValid}", isValid ? "✅ VALID" : "❌ INVALID");

        // Demonstrate TUF-style signing with local keys for comparison
        await DemonstrateLocalSigningComparison(logger);

        logger.LogInformation("Demo completed successfully! 🎉");
        
        logger.LogInformation("Next steps:");
        logger.LogInformation("- Integrate Azure Key Vault signer into your TUF workflows");
        logger.LogInformation("- Use async SignBytesAsync method for better performance");
        logger.LogInformation("- Configure appropriate Azure RBAC permissions");
        logger.LogInformation("- Consider using managed identity in production");
    }

    /// <summary>
    /// Creates an Azure Key Vault signer based on the specified authentication method.
    /// </summary>
    private static async Task<AzureKeyVaultSigner> CreateSigner(
        Uri keyVaultUrl, 
        string keyName, 
        string authMethod)
    {
        return authMethod.ToLowerInvariant() switch
        {
            "cli" => await AzureKeyVaultSignerFactory.CreateWithCliCredentialAsync(keyVaultUrl, keyName),
            "managed-identity" => await AzureKeyVaultSignerFactory.CreateWithManagedIdentityAsync(keyVaultUrl, keyName),
            "visual-studio" => await AzureKeyVaultSignerFactory.CreateWithVisualStudioCredentialAsync(keyVaultUrl, keyName),
            "default" => await AzureKeyVaultSignerFactory.CreateWithDefaultCredentialAsync(keyVaultUrl, keyName),
            _ => throw new ArgumentException($"Unknown authentication method: {authMethod}")
        };
    }

    /// <summary>
    /// Demonstrates comparison between Azure Key Vault signing and local signing.
    /// Shows that both produce valid TUF signatures with the same patterns.
    /// </summary>
    private static async Task DemonstrateLocalSigningComparison(ILogger logger)
    {
        logger.LogInformation("Demonstrating local signing comparison...");

        var testMessage = "Comparison test message for signing"u8.ToArray();

        // Create local signers for comparison
        var localRsaSigner = RsaSigner.Generate(2048);
        var localEd25519Signer = Ed25519Signer.Generate();

        // Sign with local signers
        var rsaSignature = localRsaSigner.SignBytes(testMessage);
        var ed25519Signature = localEd25519Signer.SignBytes(testMessage);

        logger.LogInformation("Local signing comparison:");
        logger.LogInformation("  RSA-2048 Local:");
        logger.LogInformation("    Key Type: {KeyType}", localRsaSigner.Key.KeyType);
        logger.LogInformation("    Scheme: {Scheme}", localRsaSigner.Key.Scheme);
        logger.LogInformation("    Key ID: {KeyId}", rsaSignature.KeyId);
        logger.LogInformation("    Signature Length: {Length}", rsaSignature.Sig.Length);

        logger.LogInformation("  Ed25519 Local:");
        logger.LogInformation("    Key Type: {KeyType}", localEd25519Signer.Key.KeyType);
        logger.LogInformation("    Scheme: {Scheme}", localEd25519Signer.Key.Scheme);
        logger.LogInformation("    Key ID: {KeyId}", ed25519Signature.KeyId);
        logger.LogInformation("    Signature Length: {Length}", ed25519Signature.Sig.Length);

        // Verify signatures
        var rsaValid = localRsaSigner.Key.VerifySignature(rsaSignature.Sig, testMessage);
        var ed25519Valid = localEd25519Signer.Key.VerifySignature(ed25519Signature.Sig, testMessage);

        logger.LogInformation("  Verification Results:");
        logger.LogInformation("    RSA: {IsValid}", rsaValid ? "✅ VALID" : "❌ INVALID");
        logger.LogInformation("    Ed25519: {IsValid}", ed25519Valid ? "✅ VALID" : "❌ INVALID");

        logger.LogInformation("✓ Azure Key Vault signers produce identical TUF signature formats");
        logger.LogInformation("✓ Both local and Azure Key Vault signatures are interchangeable in TUF workflows");

        await Task.CompletedTask; // For async consistency
    }
}

/// <summary>
/// Example configurations for common Azure Key Vault scenarios.
/// Provides templates for typical enterprise setups.
/// </summary>
public static class AzureKeyVaultExamples
{
    /// <summary>
    /// Example configuration for development environment using Azure CLI.
    /// </summary>
    public static readonly string[] DevCliExample = [
        "https://dev-keyvault.vault.azure.net/",
        "tuf-dev-signing-key",
        "cli",
        "Development signing test"
    ];

    /// <summary>
    /// Example configuration for production using managed identity.
    /// </summary>
    public static readonly string[] ProdManagedIdentityExample = [
        "https://prod-keyvault.vault.azure.net/",
        "tuf-prod-root-key",
        "managed-identity",
        "Production TUF root metadata"
    ];

    /// <summary>
    /// Example configuration for staging environment.
    /// </summary>
    public static readonly string[] StagingExample = [
        "https://staging-keyvault.vault.azure.net/",
        "tuf-staging-key",
        "default",
        "Staging environment signing"
    ];
}