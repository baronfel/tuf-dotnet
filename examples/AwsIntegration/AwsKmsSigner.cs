using System.Security.Cryptography;
using System.Text;

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

using TUF.Models;

namespace AwsIntegration;

/// <summary>
/// AWS KMS-backed TUF signer implementation for enterprise-grade cloud-based signing.
/// Provides HSM-level security with centralized key management and audit trails.
/// </summary>
/// <remarks>
/// This implementation provides:
/// - Hardware Security Module (HSM) level protection via AWS CloudHSM
/// - Centralized key lifecycle management
/// - Complete audit trail for all signing operations
/// - Cross-region availability and disaster recovery
/// - Integration with AWS IAM for fine-grained access control
/// 
/// Supported AWS KMS Key Specs:
/// - RSA_2048: RSA with 2048-bit key size (minimum recommended)
/// - RSA_3072: RSA with 3072-bit key size (enhanced security) 
/// - RSA_4096: RSA with 4096-bit key size (maximum security)
/// 
/// Note: AWS KMS does not natively support Ed25519 as of 2024.
/// For Ed25519, consider AWS CloudHSM with PKCS#11 integration.
/// </remarks>
public sealed class AwsKmsSigner : ISigner, IDisposable
{
    private readonly IAmazonKeyManagementService _kmsClient;
    private readonly string _keyId;
    private readonly Key _tufKey;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new AWS KMS signer with the specified key.
    /// </summary>
    /// <param name="kmsClient">AWS KMS client (will be disposed with this instance)</param>
    /// <param name="keyId">AWS KMS key ID, ARN, or alias (e.g., "alias/tuf-signing-key")</param>
    /// <param name="tufKey">Pre-computed TUF key information with public key material</param>
    public AwsKmsSigner(IAmazonKeyManagementService kmsClient, string keyId, Key tufKey)
    {
        _kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
        _keyId = keyId ?? throw new ArgumentNullException(nameof(keyId));
        _tufKey = tufKey ?? throw new ArgumentNullException(nameof(tufKey));
    }

    /// <summary>
    /// Creates an AWS KMS signer by retrieving public key information from AWS KMS.
    /// </summary>
    /// <param name="kmsClient">AWS KMS client</param>
    /// <param name="keyId">AWS KMS key ID, ARN, or alias</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configured AWS KMS signer</returns>
    /// <exception cref="InvalidOperationException">Thrown when KMS key is not suitable for signing</exception>
    public static async Task<AwsKmsSigner> CreateAsync(
        IAmazonKeyManagementService kmsClient, 
        string keyId, 
        CancellationToken cancellationToken = default)
    {
        // Get public key information from AWS KMS
        var publicKeyResponse = await kmsClient.GetPublicKeyAsync(new GetPublicKeyRequest
        {
            KeyId = keyId
        }, cancellationToken);

        // Validate key usage
        if (publicKeyResponse.KeyUsage != KeyUsageType.SIGN_VERIFY)
        {
            throw new InvalidOperationException($"AWS KMS key {keyId} is not configured for signing. Key usage: {publicKeyResponse.KeyUsage}");
        }

        // Convert AWS KMS key spec to TUF key format
        var (keyType, scheme) = publicKeyResponse.KeySpec switch
        {
            KeySpec.RSA_2048 => ("rsa", "rsa-pss-sha256"),
            KeySpec.RSA_3072 => ("rsa", "rsa-pss-sha256"),
            KeySpec.RSA_4096 => ("rsa", "rsa-pss-sha256"),
            _ => throw new InvalidOperationException($"Unsupported AWS KMS key spec: {publicKeyResponse.KeySpec}")
        };

        // Extract DER-encoded public key
        var publicKeyDer = publicKeyResponse.PublicKey.ToArray();
        
        // Convert DER to PEM format for TUF compatibility
        var publicKeyPem = ConvertDerToPem(publicKeyDer, "PUBLIC KEY");

        // Generate deterministic key ID from public key
        var keyIdBytes = SHA256.HashData(publicKeyDer);
        var keyIdHex = Convert.ToHexString(keyIdBytes).ToLowerInvariant();

        // Create TUF key object
        var tufKey = new Key(
            Id: new KeyId(keyIdHex),
            Type: keyType,
            Scheme: scheme,
            Value: publicKeyPem
        );

        return new AwsKmsSigner(kmsClient, keyId, tufKey);
    }

    /// <summary>
    /// The TUF public key information for this AWS KMS signer.
    /// </summary>
    public Key Key => _tufKey;

    /// <summary>
    /// Signs the provided data using AWS KMS and returns a TUF signature object.
    /// </summary>
    /// <param name="data">The data to sign</param>
    /// <returns>A signature object containing the key ID and signature value</returns>
    /// <exception cref="ObjectDisposedException">Thrown when signer has been disposed</exception>
    /// <exception cref="InvalidOperationException">Thrown when AWS KMS signing fails</exception>
    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();

        try
        {
            // AWS KMS Sign is inherently async, but TUF ISigner is sync
            // We use GetAwaiter().GetResult() for synchronous execution
            // In production, consider using async signing patterns where possible
            var task = SignBytesAsync(data.ToArray(), CancellationToken.None);
            return task.GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            throw new InvalidOperationException($"AWS KMS signing failed for key {_keyId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Asynchronously signs the provided data using AWS KMS.
    /// </summary>
    /// <param name="data">The data to sign</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A signature object containing the key ID and signature value</returns>
    public async Task<SignatureObject> SignBytesAsync(byte[] data, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        // For RSA-PSS, we need to hash the data first
        var hashedData = _tufKey.Scheme switch
        {
            "rsa-pss-sha256" => SHA256.HashData(data),
            _ => throw new InvalidOperationException($"Unsupported signing scheme: {_tufKey.Scheme}")
        };

        // Sign using AWS KMS
        var signRequest = new SignRequest
        {
            KeyId = _keyId,
            Message = new MemoryStream(hashedData),
            MessageType = MessageType.DIGEST, // We're providing pre-hashed data
            SigningAlgorithm = _tufKey.Scheme switch
            {
                "rsa-pss-sha256" => SigningAlgorithmSpec.RSASSA_PSS_SHA_256,
                _ => throw new InvalidOperationException($"Unsupported signing scheme: {_tufKey.Scheme}")
            }
        };

        var signResponse = await _kmsClient.SignAsync(signRequest, cancellationToken);
        
        // Convert signature to hex string for TUF compatibility
        var signatureHex = Convert.ToHexString(signResponse.Signature.ToArray()).ToLowerInvariant();

        return new SignatureObject(
            KeyId: _tufKey.Id,
            Signature: signatureHex
        );
    }

    /// <summary>
    /// Converts DER-encoded key material to PEM format.
    /// </summary>
    private static string ConvertDerToPem(byte[] derData, string label)
    {
        var base64 = Convert.ToBase64String(derData);
        var sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {label}-----");
        
        // Split base64 into 64-character lines
        for (int i = 0; i < base64.Length; i += 64)
        {
            var length = Math.Min(64, base64.Length - i);
            sb.AppendLine(base64.Substring(i, length));
        }
        
        sb.AppendLine($"-----END {label}-----");
        return sb.ToString();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AwsKmsSigner));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _kmsClient?.Dispose();
            _disposed = true;
        }
    }
}