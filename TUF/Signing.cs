using System.Security.Cryptography;
using NSec.Cryptography;
using Serde;

namespace TUF.Models;

/// <summary>
/// Interface for TUF signers that can create cryptographic signatures over data.
/// All signers must provide their public key information and signing capability.
/// </summary>
/// <remarks>
/// Signers implement the cryptographic signing portion of the TUF specification.
/// Each signer is associated with a specific key type and signing algorithm,
/// ensuring type-safe signing operations.
/// </remarks>
public interface ISigner
{
    /// <summary>
    /// The TUF public key information for this signer.
    /// Contains the key type, scheme, and public key material.
    /// </summary>
    Key Key { get; }
    
    /// <summary>
    /// Signs the provided data and returns a TUF signature object.
    /// </summary>
    /// <param name="data">The data to sign</param>
    /// <returns>A signature object containing the key ID and signature value</returns>
    SignatureObject SignBytes(ReadOnlySpan<byte> data);
}

/// <summary>
/// Ed25519 signer implementation using NSec.Cryptography.
/// Provides high-performance, secure digital signatures using the Ed25519 elliptic curve.
/// </summary>
/// <remarks>
/// Ed25519 is the recommended signature scheme for TUF due to:
/// - Strong security guarantees (equivalent to ~128-bit symmetric security)
/// - Excellent performance for both signing and verification
/// - Small signature and key sizes (64-byte signatures, 32-byte public keys)
/// - Resistance to side-channel attacks
/// - Deterministic signatures (same message always produces same signature)
/// 
/// Type/Scheme Pinning:
/// - KeyType: "ed25519" 
/// - Scheme: "ed25519"
/// - Hash: Not applicable (Ed25519 uses internal hash function)
/// </remarks>
public sealed class Ed25519Signer : ISigner
{
    private readonly NSec.Cryptography.Key _privateKey;
    
    /// <summary>
    /// The TUF Key information for this Ed25519 signer.
    /// Guaranteed to have KeyType="ed25519" and Scheme="ed25519".
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// Creates an Ed25519 signer from an NSec private key.
    /// </summary>
    /// <param name="privateKey">NSec Ed25519 private key</param>
    /// <exception cref="ArgumentException">Thrown if private key is not Ed25519</exception>
    public Ed25519Signer(NSec.Cryptography.Key privateKey)
    {
        if (privateKey.Algorithm != SignatureAlgorithm.Ed25519)
            throw new ArgumentException("Private key must be Ed25519", nameof(privateKey));
            
        _privateKey = privateKey;
        
        // Get the public key bytes and create the TUF Key with pinned types
        var publicKeyBytes = privateKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var hexString = Convert.ToHexString(publicKeyBytes).ToLowerInvariant();
        
        // Create Key with strictly pinned type and scheme for Ed25519
        Key = new Key
        {
            KeyType = "ed25519",    // Pinned: Ed25519 uses "ed25519" key type
            Scheme = "ed25519",     // Pinned: Ed25519 uses "ed25519" scheme
            KeyVal = new KeyValue { Public = hexString }
        };
    }

    /// <summary>
    /// Generates a new Ed25519 signer with a random private key.
    /// </summary>
    /// <returns>A new Ed25519 signer ready for use</returns>
    public static Ed25519Signer Generate()
    {
        var privateKey = NSec.Cryptography.Key.Create(SignatureAlgorithm.Ed25519);
        return new Ed25519Signer(privateKey);
    }

    /// <summary>
    /// Creates an Ed25519 signer from raw private key bytes.
    /// </summary>
    /// <param name="privateKeyBytes">32-byte Ed25519 private key</param>
    /// <returns>Ed25519 signer using the provided private key</returns>
    /// <exception cref="ArgumentException">Thrown if private key bytes are invalid</exception>
    public static Ed25519Signer FromPrivateKeyBytes(ReadOnlySpan<byte> privateKeyBytes)
    {
        var privateKey = NSec.Cryptography.Key.Import(SignatureAlgorithm.Ed25519, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
        return new Ed25519Signer(privateKey);
    }

    /// <summary>
    /// Signs data using Ed25519 and returns a TUF signature object.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>TUF signature object with key ID and hex-encoded signature</returns>
    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        var signatureBytes = SignatureAlgorithm.Ed25519.Sign(_privateKey, data);
        var signatureHex = Convert.ToHexString(signatureBytes).ToLowerInvariant();
        
        return new SignatureObject
        {
            KeyId = Key.GetKeyId(),
            Sig = signatureHex
        };
    }
}

/// <summary>
/// RSA-PSS signer implementation using .NET cryptography.
/// Provides RSA signatures with PSS padding and SHA-256 hashing for TUF compliance.
/// </summary>
/// <remarks>
/// RSA with PSS padding provides:
/// - Compatibility with existing PKI infrastructure  
/// - Provable security guarantees when using sufficient key sizes
/// - Wide support across cryptographic libraries and standards
/// 
/// Type/Scheme Pinning:
/// - KeyType: "rsa" 
/// - Scheme: "rsassa-pss-sha256" (RSA-PSS with SHA-256)
/// - Hash: SHA-256 (built into the signature process)
/// - Minimum key size: 2048 bits (3072+ recommended for new deployments)
/// </remarks>
public sealed class RsaSigner : ISigner, IDisposable
{
    private readonly RSA _rsa;
    
    /// <summary>
    /// The TUF Key information for this RSA signer.
    /// Guaranteed to have KeyType="rsa" and Scheme="rsassa-pss-sha256".
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// Creates an RSA signer from a .NET RSA instance.
    /// </summary>
    /// <param name="rsa">.NET RSA instance with private key</param>
    /// <exception cref="ArgumentNullException">Thrown if RSA instance is null</exception>
    public RsaSigner(RSA rsa)
    {
        _rsa = rsa ?? throw new ArgumentNullException(nameof(rsa));
        
        // Validate minimum key size for security
        if (rsa.KeySize < 2048)
            throw new ArgumentException("RSA key size must be at least 2048 bits", nameof(rsa));
        
        // Export public key as PEM and create TUF Key with pinned types
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();
        
        // Create Key with strictly pinned type and scheme for RSA-PSS
        Key = new Key
        {
            KeyType = "rsa",                    // Pinned: RSA uses "rsa" key type
            Scheme = "rsassa-pss-sha256",       // Pinned: RSA-PSS with SHA-256
            KeyVal = new KeyValue { Public = publicKeyPem }
        };
    }

    /// <summary>
    /// Generates a new RSA signer with the specified key size.
    /// </summary>
    /// <param name="keySize">RSA key size in bits (minimum 2048, recommended 3072+)</param>
    /// <returns>A new RSA signer ready for use</returns>
    /// <exception cref="ArgumentException">Thrown if key size is less than 2048</exception>
    public static RsaSigner Generate(int keySize = 3072)
    {
        if (keySize < 2048)
            throw new ArgumentException("RSA key size must be at least 2048 bits", nameof(keySize));
            
        var rsa = RSA.Create(keySize);
        return new RsaSigner(rsa);
    }

    /// <summary>
    /// Creates an RSA signer from a PEM-encoded private key.
    /// </summary>
    /// <param name="privatePem">PEM-encoded RSA private key</param>
    /// <returns>RSA signer using the provided private key</returns>
    /// <exception cref="ArgumentException">Thrown if PEM is invalid or key size insufficient</exception>
    public static RsaSigner FromPem(string privatePem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privatePem);
        return new RsaSigner(rsa);
    }

    /// <summary>
    /// Signs data using RSA-PSS with SHA-256 and returns a TUF signature object.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>TUF signature object with key ID and hex-encoded signature</returns>
    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        // Hash the data with SHA-256 as required by rsassa-pss-sha256 scheme
        var hash = SHA256.HashData(data);
        
        // Sign the hash using RSA-PSS padding with SHA-256
        var signatureBytes = _rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var signatureHex = Convert.ToHexString(signatureBytes).ToLowerInvariant();
        
        return new SignatureObject
        {
            KeyId = Key.GetKeyId(),
            Sig = signatureHex
        };
    }

    /// <summary>
    /// Disposes the underlying RSA instance.
    /// </summary>
    public void Dispose()
    {
        _rsa?.Dispose();
    }
}

/// <summary>
/// ECDSA signer implementation using .NET cryptography with NIST P-256 curve.
/// Provides ECDSA signatures with SHA-256 hashing for TUF compliance.
/// </summary>
/// <remarks>
/// ECDSA with P-256 provides:
/// - Good balance of security and performance
/// - Smaller key sizes compared to RSA for equivalent security (256-bit keys)
/// - Wide industry adoption and FIPS 186-4 compliance
/// - Support across major cryptographic libraries
/// 
/// Type/Scheme Pinning:
/// - KeyType: "ecdsa"
/// - Scheme: "ecdsa-sha2-nistp256" (ECDSA with SHA-256 on P-256 curve)
/// - Hash: SHA-256 (built into the signature process)
/// - Curve: NIST P-256 (secp256r1)
/// 
/// Note: Unlike Ed25519, ECDSA signatures are non-deterministic,
/// meaning the same message will produce different signatures each time.
/// </remarks>
public sealed class EcdsaSigner : ISigner, IDisposable
{
    private readonly ECDsa _ecdsa;
    
    /// <summary>
    /// The TUF Key information for this ECDSA signer.
    /// Guaranteed to have KeyType="ecdsa" and Scheme="ecdsa-sha2-nistp256".
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// Creates an ECDSA signer from a .NET ECDsa instance.
    /// Must use NIST P-256 curve for TUF compliance.
    /// </summary>
    /// <param name="ecdsa">.NET ECDsa instance with private key on P-256 curve</param>
    /// <exception cref="ArgumentNullException">Thrown if ECDsa instance is null</exception>
    /// <exception cref="ArgumentException">Thrown if not using P-256 curve</exception>
    public EcdsaSigner(ECDsa ecdsa)
    {
        _ecdsa = ecdsa ?? throw new ArgumentNullException(nameof(ecdsa));
        
        // Validate that we're using the P-256 curve for TUF compliance
        var keySize = ecdsa.KeySize;
        if (keySize != 256)
            throw new ArgumentException("ECDSA key must use NIST P-256 curve (256-bit key size)", nameof(ecdsa));
        
        // Export public key as PEM and create TUF Key with pinned types
        var publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();
        
        // Create Key with strictly pinned type and scheme for ECDSA P-256
        Key = new Key
        {
            KeyType = "ecdsa",                      // Pinned: ECDSA uses "ecdsa" key type
            Scheme = "ecdsa-sha2-nistp256",         // Pinned: ECDSA-SHA256 with P-256 curve
            KeyVal = new KeyValue { Public = publicKeyPem }
        };
    }

    /// <summary>
    /// Generates a new ECDSA signer using NIST P-256 curve.
    /// </summary>
    /// <returns>A new ECDSA signer ready for use</returns>
    public static EcdsaSigner Generate()
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new EcdsaSigner(ecdsa);
    }

    /// <summary>
    /// Creates an ECDSA signer from a PEM-encoded private key.
    /// </summary>
    /// <param name="privatePem">PEM-encoded ECDSA private key</param>
    /// <returns>ECDSA signer using the provided private key</returns>
    /// <exception cref="ArgumentException">Thrown if PEM is invalid or not P-256</exception>
    public static EcdsaSigner FromPem(string privatePem)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privatePem);
        return new EcdsaSigner(ecdsa);
    }

    /// <summary>
    /// Signs data using ECDSA-SHA256 on P-256 curve and returns a TUF signature object.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>TUF signature object with key ID and hex-encoded signature</returns>
    /// <remarks>
    /// ECDSA signatures are non-deterministic, so the same data will produce
    /// different signature values on each call. This is normal and expected.
    /// </remarks>
    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        // Hash the data with SHA-256 as required by ecdsa-sha2-nistp256 scheme
        var hash = SHA256.HashData(data);
        
        // Sign the hash using ECDSA
        var signatureBytes = _ecdsa.SignHash(hash);
        var signatureHex = Convert.ToHexString(signatureBytes).ToLowerInvariant();
        
        return new SignatureObject
        {
            KeyId = Key.GetKeyId(),
            Sig = signatureHex
        };
    }

    /// <summary>
    /// Disposes the underlying ECDsa instance.
    /// </summary>
    public void Dispose()
    {
        _ecdsa?.Dispose();
    }
}