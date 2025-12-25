using TUF.Models;

namespace TUF.HSM;

/// <summary>
/// Extended signer interface for Hardware Security Module (HSM) based signing operations.
/// Provides additional capabilities beyond basic software signing, including hardware-backed
/// key protection and offline signing workflows.
/// </summary>
/// <remarks>
/// HSM signers provide enhanced security by:
/// - Storing private keys in tamper-resistant hardware
/// - Performing cryptographic operations within the secure boundary
/// - Supporting air-gapped signing workflows for root key protection
/// - Providing audit trails and access controls
/// </remarks>
public interface IHsmSigner : ISigner
{
    /// <summary>
    /// Information about the HSM provider and configuration.
    /// </summary>
    HsmProviderInfo ProviderInfo { get; }

    /// <summary>
    /// Indicates whether the signer requires authentication before signing operations.
    /// </summary>
    bool RequiresAuthentication { get; }

    /// <summary>
    /// Authenticates to the HSM using the provided credentials.
    /// </summary>
    /// <param name="credentials">HSM-specific authentication credentials</param>
    /// <returns>True if authentication successful, false otherwise</returns>
    Task<bool> AuthenticateAsync(HsmCredentials credentials);

    /// <summary>
    /// Checks if the HSM signer is currently authenticated and ready for signing operations.
    /// </summary>
    /// <returns>True if authenticated and ready, false otherwise</returns>
    bool IsAuthenticated();

    /// <summary>
    /// Signs data asynchronously, which may be required for some HSM operations
    /// that involve network communication or user interaction.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>TUF signature object</returns>
    Task<SignatureObject> SignBytesAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs out from the HSM session, clearing any cached authentication state.
    /// </summary>
    Task LogoutAsync();
}

/// <summary>
/// Information about an HSM provider implementation.
/// </summary>
public sealed record HsmProviderInfo
{
    /// <summary>
    /// Name of the HSM provider (e.g., "YubiKey PIV", "Windows CNG", "PKCS#11").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Version of the provider or underlying library.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Description of the HSM capabilities and supported operations.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Supported key types by this HSM provider.
    /// </summary>
    public required IReadOnlyList<string> SupportedKeyTypes { get; init; }

    /// <summary>
    /// Additional provider-specific metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Base class for HSM authentication credentials.
/// </summary>
public abstract record HsmCredentials;

/// <summary>
/// PIN-based authentication credentials for PKCS#11 tokens.
/// </summary>
public sealed record PinCredentials(string Pin) : HsmCredentials;

/// <summary>
/// Certificate-based authentication for Windows CNG providers.
/// </summary>
public sealed record CertificateCredentials(string CertificateThumbprint, string? StoreName = null) : HsmCredentials;

/// <summary>
/// Empty credentials for HSM providers that don't require authentication.
/// </summary>
public sealed record NoCredentials : HsmCredentials
{
    /// <summary>
    /// Singleton instance for no-credential authentication.
    /// </summary>
    public static readonly NoCredentials Instance = new();

    private NoCredentials() { }
}