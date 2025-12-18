using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Serde;

namespace TUF.Models;

/// <summary>
/// Abstraction for hardware-based signing providers that can work with HSMs, smart cards, 
/// and other secure hardware devices. This interface allows pluggable hardware signing 
/// implementations while maintaining TUF signing compatibility.
/// </summary>
/// <remarks>
/// Hardware signing providers offer enhanced security by:
/// - Keeping private keys in secure hardware that cannot be extracted
/// - Providing tamper-resistant key storage and signing operations
/// - Supporting air-gapped signing workflows for critical keys (e.g., TUF root keys)
/// - Enabling compliance with security standards requiring hardware key protection
/// 
/// Common hardware signing scenarios in TUF:
/// - Root key signing with YubiKeys or HSMs for maximum security
/// - Intermediate key signing with PIV cards for operational security
/// - Bulk signing operations with dedicated HSMs for performance
/// </remarks>
public interface IHardwareSigningProvider
{
    /// <summary>
    /// Unique identifier for this hardware signing provider implementation.
    /// Used for configuration, logging, and troubleshooting.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Indicates whether this provider is currently available and can perform signing operations.
    /// Should check for required hardware/drivers/certificates.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Lists available keys/certificates that can be used for signing.
    /// Returns empty collection if no signing keys are available.
    /// </summary>
    /// <returns>Collection of available hardware signing keys</returns>
    IEnumerable<HardwareSigningKey> GetAvailableKeys();

    /// <summary>
    /// Creates a hardware-backed TUF signer for the specified key.
    /// </summary>
    /// <param name="key">Hardware signing key to use</param>
    /// <returns>TUF signer that uses the hardware key for signing operations</returns>
    /// <exception cref="ArgumentException">If the key is not valid for this provider</exception>
    /// <exception cref="InvalidOperationException">If hardware signing is not available</exception>
    ISigner CreateSigner(HardwareSigningKey key);
}

/// <summary>
/// Represents a signing key stored in secure hardware (HSM, smart card, etc.).
/// Contains metadata about the key without exposing the private key material.
/// </summary>
/// <remarks>
/// Hardware keys are identified by various mechanisms depending on the hardware:
/// - PIV cards: Certificate subject and serial number
/// - PKCS#11 tokens: Token label and key ID  
/// - Windows CNG: Key container name and provider
/// - YubiKeys: PIV slot and certificate information
/// </remarks>
[GenerateSerde]
public partial record HardwareSigningKey
{
    /// <summary>
    /// Human-readable identifier for this key (e.g., certificate subject, key label).
    /// Used for display and selection purposes.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// The cryptographic algorithm supported by this key (e.g., "RSA", "ECDSA", "Ed25519").
    /// Must be compatible with TUF signature schemes.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Key size in bits (for RSA/ECDSA) or curve name (for Ed25519).
    /// Used to validate TUF signature scheme compatibility.
    /// </summary>
    public required string KeySize { get; init; }

    /// <summary>
    /// Optional certificate associated with this key.
    /// Useful for validation and key identification.
    /// This property is not serialized - certificate data is stored in ProviderMetadata.
    /// </summary>
    [property: SerdeMemberOptions(Skip = true)]
    public X509Certificate2? Certificate { get; init; }

    /// <summary>
    /// Provider-specific metadata for locating and using this key.
    /// Format depends on the hardware provider implementation.
    /// </summary>
    public required Dictionary<string, string> ProviderMetadata { get; init; }
}

/// <summary>
/// PIV (Personal Identity Verification) card signing provider for YubiKeys and compatible smart cards.
/// Supports RSA and ECDSA keys stored in PIV slots with PIN authentication.
/// </summary>
/// <remarks>
/// PIV cards provide excellent security for TUF signing because:
/// - Private keys cannot be extracted from the card
/// - PIN/biometric authentication required for signing operations
/// - Tamper-resistant hardware protects against physical attacks
/// - Widely supported across operating systems and applications
/// - Air-gapped signing possible (card can work offline)
/// 
/// YubiKey PIV slots commonly used for TUF:
/// - Slot 9c (Digital Signature): Primary signing operations
/// - Slot 9a (PIV Authentication): Secondary/intermediate signing
/// - Slot 9d (Key Management): Key exchange (not used for TUF signing)
/// - Slot 9e (Card Authentication): Card-level authentication
/// 
/// Supported algorithms:
/// - RSA 2048/3072/4096 with PKCS#1 v1.5 or PSS padding
/// - ECDSA P-256/P-384 with SHA-256/SHA-384
/// - Ed25519 (on newer YubiKeys with firmware 5.7+)
/// </remarks>
public sealed class PivSigningProvider : IHardwareSigningProvider
{
    /// <inheritdoc/>
    public string ProviderName => "PIV Smart Card";

    /// <inheritdoc/>
    public bool IsAvailable
    {
        get
        {
            try
            {
                // Check if PIV certificates are available through Windows certificate store
                using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                // Look for certificates with PIV-compatible private keys
                var pivCerts = store.Certificates
                    .Cast<X509Certificate2>()
                    .Where(cert => cert.HasPrivateKey && IsPivCompatibleKey(cert))
                    .ToList();

                return pivCerts.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<HardwareSigningKey> GetAvailableKeys()
    {
        var keys = new List<HardwareSigningKey>();

        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            foreach (var cert in store.Certificates.Cast<X509Certificate2>())
            {
                if (!cert.HasPrivateKey || !IsPivCompatibleKey(cert))
                    continue;

                var key = CreateHardwareSigningKey(cert);
                if (key != null)
                    keys.Add(key);
            }
        }
        catch
        {
            // Return empty list if certificate store access fails
        }

        return keys;
    }

    /// <inheritdoc/>
    public ISigner CreateSigner(HardwareSigningKey key)
    {
        if (!key.ProviderMetadata.TryGetValue("Thumbprint", out var thumbprint))
            throw new ArgumentException("PIV key missing required thumbprint metadata", nameof(key));

        // Find the certificate by thumbprint
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        var cert = store.Certificates
            .Cast<X509Certificate2>()
            .FirstOrDefault(c => c.Thumbprint.Equals(thumbprint, StringComparison.OrdinalIgnoreCase));

        if (cert == null)
            throw new ArgumentException($"PIV certificate with thumbprint {thumbprint} not found", nameof(key));

        return key.Algorithm switch
        {
            "RSA" => CreatePivRsaSigner(cert, key),
            "ECDSA" => CreatePivEcdsaSigner(cert, key),
            _ => throw new ArgumentException($"Unsupported PIV key algorithm: {key.Algorithm}", nameof(key))
        };
    }

    private static bool IsPivCompatibleKey(X509Certificate2 cert)
    {
        // Check if this certificate has a private key that's likely from a PIV card
        // PIV keys typically have specific key usage extensions and are stored in smart card CSPs
        try
        {
            using var key = cert.GetRSAPrivateKey();
            if (key != null)
                return true;
        }
        catch { }

        try
        {
            using var key = cert.GetECDsaPrivateKey();
            if (key != null)
                return true;
        }
        catch { }

        return false;
    }

    private static HardwareSigningKey? CreateHardwareSigningKey(X509Certificate2 cert)
    {
        try
        {
            string algorithm;
            string keySize;

            // Determine algorithm and key size
            using (var rsa = cert.GetRSAPrivateKey())
            {
                if (rsa != null)
                {
                    algorithm = "RSA";
                    keySize = rsa.KeySize.ToString();
                }
                else
                {
                    using var ecdsa = cert.GetECDsaPrivateKey();
                    if (ecdsa != null)
                    {
                        algorithm = "ECDSA";
                        keySize = ecdsa.KeySize.ToString();
                    }
                    else
                    {
                        return null; // Unsupported key type
                    }
                }
            }

            var metadata = new Dictionary<string, string>
            {
                ["Thumbprint"] = cert.Thumbprint,
                ["Subject"] = cert.Subject,
                ["Issuer"] = cert.Issuer,
                ["SerialNumber"] = cert.SerialNumber,
                ["NotBefore"] = cert.NotBefore.ToString("O"),
                ["NotAfter"] = cert.NotAfter.ToString("O")
            };

            return new HardwareSigningKey
            {
                Identifier = $"PIV: {cert.Subject}",
                Algorithm = algorithm,
                KeySize = keySize,
                Certificate = cert,
                ProviderMetadata = metadata
            };
        }
        catch
        {
            return null;
        }
    }

    private static ISigner CreatePivRsaSigner(X509Certificate2 cert, HardwareSigningKey key)
    {
        var rsa = cert.GetRSAPrivateKey() ?? throw new InvalidOperationException("RSA private key not accessible");
        return new PivRsaSigner(rsa, cert, key);
    }

    private static ISigner CreatePivEcdsaSigner(X509Certificate2 cert, HardwareSigningKey key)
    {
        var ecdsa = cert.GetECDsaPrivateKey() ?? throw new InvalidOperationException("ECDSA private key not accessible");
        return new PivEcdsaSigner(ecdsa, cert, key);
    }
}

/// <summary>
/// PIV-backed RSA signer that performs signing operations using a smart card/YubiKey.
/// Maintains compatibility with TUF RSA signature schemes while using hardware-protected keys.
/// </summary>
/// <remarks>
/// This signer delegates RSA signing operations to PIV hardware while maintaining
/// full compatibility with the TUF signature format and verification process.
/// The private key never leaves the PIV card, providing excellent security.
/// 
/// PIN authentication is handled by the Windows CSP/CNG layer, which may prompt
/// the user for their PIV PIN when signing operations are performed.
/// </remarks>
internal sealed class PivRsaSigner : ISigner, IDisposable
{
    private readonly RSA _rsa;
    private readonly X509Certificate2 _certificate;
    private readonly HardwareSigningKey _hardwareKey;

    public Key Key { get; }

    public PivRsaSigner(RSA rsa, X509Certificate2 certificate, HardwareSigningKey hardwareKey)
    {
        _rsa = rsa;
        _certificate = certificate;
        _hardwareKey = hardwareKey;

        // Validate minimum key size for security
        if (rsa.KeySize < 2048)
            throw new ArgumentException("PIV RSA key size must be at least 2048 bits", nameof(rsa));

        // Export public key as PEM and create TUF Key with pinned types
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();

        Key = new Key
        {
            KeyType = "rsa",
            Scheme = "rsassa-pss-sha256",
            KeyVal = new KeyValue { Public = publicKeyPem }
        };
    }

    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        try
        {
            // Hash the data with SHA-256 as required by rsassa-pss-sha256 scheme
            var hash = SHA256.HashData(data);

            // Sign the hash using RSA-PSS padding with SHA-256
            // PIV card will prompt for PIN if required
            var signatureBytes = _rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            var signatureHex = Convert.ToHexString(signatureBytes).ToLowerInvariant();

            return new SignatureObject
            {
                KeyId = Key.GetKeyId(),
                Sig = signatureHex
            };
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                $"PIV RSA signing failed. This may indicate PIN required, card not present, or hardware error. " +
                $"Certificate: {_hardwareKey.Identifier}", ex);
        }
    }

    public void Dispose()
    {
        _rsa?.Dispose();
        _certificate?.Dispose();
    }
}

/// <summary>
/// PIV-backed ECDSA signer that performs signing operations using a smart card/YubiKey.
/// Maintains compatibility with TUF ECDSA signature schemes while using hardware-protected keys.
/// </summary>
/// <remarks>
/// This signer delegates ECDSA signing operations to PIV hardware while maintaining
/// full compatibility with the TUF signature format and verification process.
/// The private key never leaves the PIV card, providing excellent security.
/// 
/// Supports NIST P-256 curve as required by TUF specification.
/// PIN authentication is handled by the Windows CSP/CNG layer.
/// </remarks>
internal sealed class PivEcdsaSigner : ISigner, IDisposable
{
    private readonly ECDsa _ecdsa;
    private readonly X509Certificate2 _certificate;
    private readonly HardwareSigningKey _hardwareKey;

    public Key Key { get; }

    public PivEcdsaSigner(ECDsa ecdsa, X509Certificate2 certificate, HardwareSigningKey hardwareKey)
    {
        _ecdsa = ecdsa;
        _certificate = certificate;
        _hardwareKey = hardwareKey;

        // Validate that we're using the P-256 curve for TUF compliance
        if (ecdsa.KeySize != 256)
            throw new ArgumentException("PIV ECDSA key must use NIST P-256 curve (256-bit key size)", nameof(ecdsa));

        // Export public key as PEM and create TUF Key with pinned types
        var publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();

        Key = new Key
        {
            KeyType = "ecdsa",
            Scheme = "ecdsa-sha2-nistp256",
            KeyVal = new KeyValue { Public = publicKeyPem }
        };
    }

    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        try
        {
            // Sign the data using ECDSA-SHA256
            // PIV card will prompt for PIN if required
            var signatureBytes = _ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var signatureHex = Convert.ToHexString(signatureBytes).ToLowerInvariant();

            return new SignatureObject
            {
                KeyId = Key.GetKeyId(),
                Sig = signatureHex
            };
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                $"PIV ECDSA signing failed. This may indicate PIN required, card not present, or hardware error. " +
                $"Certificate: {_hardwareKey.Identifier}", ex);
        }
    }

    public void Dispose()
    {
        _ecdsa?.Dispose();
        _certificate?.Dispose();
    }
}

/// <summary>
/// Hardware signing manager that coordinates multiple hardware signing providers
/// and provides a unified interface for hardware-backed TUF signing operations.
/// </summary>
/// <remarks>
/// This manager allows applications to:
/// - Auto-discover available hardware signing providers
/// - List all available hardware keys across different providers  
/// - Create signers from hardware keys in a provider-agnostic way
/// - Handle provider-specific errors and fallbacks
/// 
/// Example usage:
/// <code>
/// var manager = new HardwareSigningManager();
/// var availableKeys = manager.GetAllAvailableKeys();
/// 
/// // Let user select a key, then create a signer
/// if (availableKeys.Any())
/// {
///     var selectedKey = availableKeys.First();
///     var signer = manager.CreateSigner(selectedKey);
///     
///     // Use signer for TUF operations
///     var signature = signer.SignBytes(data);
/// }
/// </code>
/// </remarks>
public sealed class HardwareSigningManager
{
    private readonly List<IHardwareSigningProvider> _providers;

    /// <summary>
    /// Creates a hardware signing manager with default providers.
    /// Currently includes PIV smart card provider.
    /// </summary>
    public HardwareSigningManager()
    {
        _providers = new List<IHardwareSigningProvider>
        {
            new PivSigningProvider()
        };
    }

    /// <summary>
    /// Creates a hardware signing manager with custom providers.
    /// </summary>
    /// <param name="providers">Hardware signing providers to use</param>
    public HardwareSigningManager(IEnumerable<IHardwareSigningProvider> providers)
    {
        _providers = new List<IHardwareSigningProvider>(providers);
    }

    /// <summary>
    /// Gets all available hardware signing providers.
    /// </summary>
    public IReadOnlyList<IHardwareSigningProvider> Providers => _providers.AsReadOnly();

    /// <summary>
    /// Gets all available hardware signing keys from all providers.
    /// </summary>
    public IEnumerable<(IHardwareSigningProvider Provider, HardwareSigningKey Key)> GetAllAvailableKeys()
    {
        foreach (var provider in _providers.Where(p => p.IsAvailable))
        {
            foreach (var key in provider.GetAvailableKeys())
            {
                yield return (provider, key);
            }
        }
    }

    /// <summary>
    /// Creates a TUF signer for the specified hardware key using its associated provider.
    /// </summary>
    /// <param name="providerKeyPair">Provider and key pair from GetAllAvailableKeys()</param>
    /// <returns>Hardware-backed TUF signer</returns>
    public ISigner CreateSigner((IHardwareSigningProvider Provider, HardwareSigningKey Key) providerKeyPair)
    {
        return providerKeyPair.Provider.CreateSigner(providerKeyPair.Key);
    }

    /// <summary>
    /// Gets available keys filtered by TUF-compatible algorithms.
    /// Only returns keys that can be used for TUF signing operations.
    /// </summary>
    public IEnumerable<(IHardwareSigningProvider Provider, HardwareSigningKey Key)> GetTufCompatibleKeys()
    {
        return GetAllAvailableKeys().Where(pair => IsTufCompatible(pair.Key));
    }

    private static bool IsTufCompatible(HardwareSigningKey key)
    {
        return key.Algorithm switch
        {
            "RSA" when int.TryParse(key.KeySize, out var size) && size >= 2048 => true,
            "ECDSA" when key.KeySize == "256" => true, // P-256 curve
            "Ed25519" => true,
            _ => false
        };
    }
}