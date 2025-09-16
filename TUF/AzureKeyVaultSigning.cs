using System.Security.Cryptography;
using System.Text;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace TUF.Models;

/// <summary>
/// Azure Key Vault signer implementation for TUF signing operations.
/// Provides integration with Azure Key Vault for secure key storage and signing.
/// </summary>
/// <remarks>
/// Azure Key Vault integration provides:
/// - Hardware Security Module (HSM) backed key storage
/// - Centralized key management and rotation
/// - Azure RBAC integration for access control
/// - Audit logging for all cryptographic operations
/// - High availability and disaster recovery
/// 
/// Supported Key Types:
/// - RSA keys (2048, 3072, 4096 bits) with RSA-PSS-SHA256
/// - ECDSA keys (P-256 curve) with ECDSA-SHA256
/// 
/// Authentication:
/// - Uses Azure.Identity DefaultAzureCredential for authentication
/// - Supports managed identity, Azure CLI, Visual Studio, etc.
/// 
/// Performance Considerations:
/// - Network latency for each signing operation
/// - Consider caching key metadata when possible
/// - Rate limiting applies based on Key Vault tier
/// </remarks>
public sealed class AzureKeyVaultSigner : ISigner, IAsyncDisposable
{
    private readonly KeyClient _keyClient;
    private readonly CryptographyClient _cryptoClient;
    private readonly string _keyName;
    private readonly KeyVaultKey _keyVaultKey;

    /// <summary>
    /// The TUF Key information for this Azure Key Vault signer.
    /// Key type and scheme are determined by the Azure Key Vault key properties.
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// Creates an Azure Key Vault signer for the specified key.
    /// </summary>
    /// <param name="keyVaultUrl">Azure Key Vault URL (e.g., https://myvault.vault.azure.net/)</param>
    /// <param name="keyName">Name of the key in Azure Key Vault</param>
    /// <param name="credential">Azure credential for authentication (optional, uses DefaultAzureCredential if null)</param>
    /// <exception cref="ArgumentException">Thrown if key vault URL or key name is invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown if key type is not supported</exception>
    public static async Task<AzureKeyVaultSigner> CreateAsync(
        Uri keyVaultUrl, 
        string keyName, 
        Azure.Core.TokenCredential? credential = null)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("Key name cannot be null or empty", nameof(keyName));

        credential ??= new DefaultAzureCredential();
        var keyClient = new KeyClient(keyVaultUrl, credential);
        
        // Get the key from Azure Key Vault
        var keyResponse = await keyClient.GetKeyAsync(keyName);
        var keyVaultKey = keyResponse.Value;

        // Create cryptography client for signing operations
        var cryptoClient = new CryptographyClient(keyVaultKey.Id, credential);

        return new AzureKeyVaultSigner(keyClient, cryptoClient, keyName, keyVaultKey);
    }

    private AzureKeyVaultSigner(
        KeyClient keyClient, 
        CryptographyClient cryptoClient, 
        string keyName, 
        KeyVaultKey keyVaultKey)
    {
        _keyClient = keyClient;
        _cryptoClient = cryptoClient;
        _keyName = keyName;
        _keyVaultKey = keyVaultKey;

        // Create TUF Key based on Azure Key Vault key type
        Key = CreateTufKey(keyVaultKey);
    }

    /// <summary>
    /// Creates a TUF Key object from an Azure Key Vault key.
    /// Maps Azure Key Vault key types to TUF key types and schemes.
    /// </summary>
    private static Key CreateTufKey(KeyVaultKey keyVaultKey)
    {
        return keyVaultKey.KeyType.ToString() switch
        {
            "Rsa" => CreateRsaTufKey(keyVaultKey),
            "Ec" => CreateEcTufKey(keyVaultKey),
            _ => throw new InvalidOperationException($"Unsupported Azure Key Vault key type: {keyVaultKey.KeyType}")
        };
    }

    /// <summary>
    /// Creates a TUF Key for RSA keys from Azure Key Vault.
    /// </summary>
    private static Key CreateRsaTufKey(KeyVaultKey keyVaultKey)
    {
        var rsaKey = keyVaultKey.Key.ToRSA();
        var keySize = rsaKey.KeySize;
        
        if (keySize < 2048)
            throw new InvalidOperationException($"RSA key size {keySize} is below minimum 2048 bits for TUF compliance");

        // Export public key as PEM for TUF compatibility
        var publicKeyPem = rsaKey.ExportRSAPublicKeyPem();

        return new Key
        {
            KeyType = "rsa",                    // TUF standard RSA key type
            Scheme = "rsassa-pss-sha256",       // TUF standard RSA-PSS scheme
            KeyVal = new KeyValue { Public = publicKeyPem }
        };
    }

    /// <summary>
    /// Creates a TUF Key for ECDSA keys from Azure Key Vault.
    /// </summary>
    private static Key CreateEcTufKey(KeyVaultKey keyVaultKey)
    {
        var ecKey = keyVaultKey.Key.ToECDsa();
        
        // Validate curve - TUF requires P-256
        if (ecKey.KeySize != 256)
            throw new InvalidOperationException($"ECDSA key must use P-256 curve (256-bit), found {ecKey.KeySize}-bit key");

        // Export public key as PEM for TUF compatibility
        var publicKeyPem = ecKey.ExportSubjectPublicKeyInfoPem();

        return new Key
        {
            KeyType = "ecdsa",                      // TUF standard ECDSA key type
            Scheme = "ecdsa-sha2-nistp256",         // TUF standard ECDSA-SHA256 scheme
            KeyVal = new KeyValue { Public = publicKeyPem }
        };
    }

    /// <summary>
    /// Signs data using Azure Key Vault and returns a TUF signature object.
    /// The signing operation is performed within Azure Key Vault for maximum security.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>TUF signature object with key ID and hex-encoded signature</returns>
    /// <exception cref="InvalidOperationException">Thrown if signing fails or key type is unsupported</exception>
    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        // Azure Key Vault signing operations are async, but TUF ISigner interface is sync
        // We need to use async-over-sync pattern here
        // Convert ReadOnlySpan<byte> to ReadOnlyMemory<byte> for async API
        var dataArray = data.ToArray();
        return SignBytesAsync(dataArray).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Async version of SignBytes for better performance and resource utilization.
    /// Preferred method when async context is available.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TUF signature object with key ID and hex-encoded signature</returns>
    public async Task<SignatureObject> SignBytesAsync(
        ReadOnlyMemory<byte> data, 
        CancellationToken cancellationToken = default)
    {
        SignResult signResult = _keyVaultKey.KeyType.ToString() switch
        {
            "Rsa" => await SignRsaAsync(data, cancellationToken),
            "Ec" => await SignEcdsaAsync(data, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported key type for signing: {_keyVaultKey.KeyType}")
        };

        var signatureHex = Convert.ToHexString(signResult.Signature).ToLowerInvariant();

        return new SignatureObject
        {
            KeyId = Key.GetKeyId(),
            Sig = signatureHex
        };
    }

    /// <summary>
    /// Signs data using RSA-PSS-SHA256 via Azure Key Vault.
    /// </summary>
    private async Task<SignResult> SignRsaAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        // For RSA-PSS, we need to hash the data first with SHA-256
        var hash = SHA256.HashData(data.Span);
        
        // Sign the hash using Azure Key Vault with RSA-PSS-SHA256
        return await _cryptoClient.SignAsync(SignatureAlgorithm.PS256, hash, cancellationToken);
    }

    /// <summary>
    /// Signs data using ECDSA-SHA256 via Azure Key Vault.
    /// </summary>
    private async Task<SignResult> SignEcdsaAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        // For ECDSA, Azure Key Vault handles the hashing internally
        return await _cryptoClient.SignDataAsync(SignatureAlgorithm.ES256, data.ToArray(), cancellationToken);
    }

    /// <summary>
    /// Async dispose pattern for proper resource cleanup.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // KeyClient and CryptographyClient don't have explicit dispose methods
        // but we should clean up any resources if needed
        await Task.CompletedTask;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration options for Azure Key Vault signers.
/// Provides convenient setup for common Azure Key Vault scenarios.
/// </summary>
public record AzureKeyVaultSignerOptions
{
    /// <summary>
    /// Azure Key Vault URL (e.g., https://myvault.vault.azure.net/).
    /// </summary>
    public required Uri KeyVaultUrl { get; init; }

    /// <summary>
    /// Name of the key in Azure Key Vault.
    /// </summary>
    public required string KeyName { get; init; }

    /// <summary>
    /// Azure credential for authentication. If null, uses DefaultAzureCredential.
    /// </summary>
    public Azure.Core.TokenCredential? Credential { get; init; }

    /// <summary>
    /// Timeout for Azure Key Vault operations (default: 30 seconds).
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Factory class for creating Azure Key Vault signers with common configurations.
/// Simplifies setup for typical enterprise scenarios.
/// </summary>
public static class AzureKeyVaultSignerFactory
{
    /// <summary>
    /// Creates an Azure Key Vault signer using managed identity authentication.
    /// Ideal for Azure-hosted applications (App Service, Functions, VMs, etc.).
    /// </summary>
    /// <param name="keyVaultUrl">Azure Key Vault URL</param>
    /// <param name="keyName">Name of the key in Key Vault</param>
    /// <param name="clientId">Optional managed identity client ID</param>
    /// <returns>Configured Azure Key Vault signer</returns>
    public static async Task<AzureKeyVaultSigner> CreateWithManagedIdentityAsync(
        Uri keyVaultUrl, 
        string keyName, 
        string? clientId = null)
    {
        var credential = string.IsNullOrEmpty(clientId)
            ? new ManagedIdentityCredential()
            : new ManagedIdentityCredential(clientId);

        return await AzureKeyVaultSigner.CreateAsync(keyVaultUrl, keyName, credential);
    }

    /// <summary>
    /// Creates an Azure Key Vault signer using Azure CLI authentication.
    /// Useful for development and testing scenarios.
    /// </summary>
    /// <param name="keyVaultUrl">Azure Key Vault URL</param>
    /// <param name="keyName">Name of the key in Key Vault</param>
    /// <returns>Configured Azure Key Vault signer</returns>
    public static async Task<AzureKeyVaultSigner> CreateWithCliCredentialAsync(
        Uri keyVaultUrl, 
        string keyName)
    {
        var credential = new AzureCliCredential();
        return await AzureKeyVaultSigner.CreateAsync(keyVaultUrl, keyName, credential);
    }

    /// <summary>
    /// Creates an Azure Key Vault signer using Visual Studio authentication.
    /// Convenient for development in Visual Studio.
    /// </summary>
    /// <param name="keyVaultUrl">Azure Key Vault URL</param>
    /// <param name="keyName">Name of the key in Key Vault</param>
    /// <returns>Configured Azure Key Vault signer</returns>
    public static async Task<AzureKeyVaultSigner> CreateWithVisualStudioCredentialAsync(
        Uri keyVaultUrl, 
        string keyName)
    {
        var credential = new VisualStudioCredential();
        return await AzureKeyVaultSigner.CreateAsync(keyVaultUrl, keyName, credential);
    }

    /// <summary>
    /// Creates an Azure Key Vault signer with default credential chain.
    /// Tries multiple authentication methods in order:
    /// 1. Environment variables
    /// 2. Managed identity
    /// 3. Azure CLI
    /// 4. Visual Studio
    /// </summary>
    /// <param name="keyVaultUrl">Azure Key Vault URL</param>
    /// <param name="keyName">Name of the key in Key Vault</param>
    /// <returns>Configured Azure Key Vault signer</returns>
    public static async Task<AzureKeyVaultSigner> CreateWithDefaultCredentialAsync(
        Uri keyVaultUrl, 
        string keyName)
    {
        return await AzureKeyVaultSigner.CreateAsync(keyVaultUrl, keyName);
    }
}