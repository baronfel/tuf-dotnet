using System.Security.Cryptography;
using NSec.Cryptography;
using TUF.Models.Keys;
using TUF.Models.Keys.Types;
using TUF.Models.Keys.Values;
using TUF.Models.Primitives;

namespace TUF.Signing;

public interface ISigner
{
    public Models.Keys.KeyBase Key { get; }
    public Signature SignBytes(ReadOnlySpan<byte> data);
}

/// <summary>
/// Ed25519 signer implementation using NSec.Cryptography
/// </summary>
public sealed class Ed25519Signer : ISigner
{
    private readonly NSec.Cryptography.Key _privateKey;
    
    public Models.Keys.KeyBase Key { get; }

    public Ed25519Signer(NSec.Cryptography.Key privateKey)
    {
        if (privateKey.Algorithm != SignatureAlgorithm.Ed25519)
            throw new ArgumentException("Private key must be Ed25519", nameof(privateKey));
            
        _privateKey = privateKey;
        
        // Get the public key bytes and create the TUF Key
        var publicKeyBytes = privateKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var hexString = new HexString(Convert.ToHexString(publicKeyBytes).ToLowerInvariant());
        var keyValue = new Ed25519KeyValue(hexString);
        Key = new KeyBase.Ed25519(keyValue);
    }

    public static Ed25519Signer Generate()
    {
        var privateKey = NSec.Cryptography.Key.Create(SignatureAlgorithm.Ed25519);
        return new Ed25519Signer(privateKey);
    }

    public static Ed25519Signer FromPrivateKeyBytes(ReadOnlySpan<byte> privateKeyBytes)
    {
        var privateKey = NSec.Cryptography.Key.Import(SignatureAlgorithm.Ed25519, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
        return new Ed25519Signer(privateKey);
    }

    public Signature SignBytes(ReadOnlySpan<byte> data)
    {
        var signatureBytes = SignatureAlgorithm.Ed25519.Sign(_privateKey, data);
        return new Signature(signatureBytes);
    }
}

/// <summary>
/// RSA-PSS signer implementation using .NET cryptography
/// </summary>
public sealed class RsaSigner : ISigner
{
    private readonly RSA _rsa;
    
    public Models.Keys.KeyBase Key { get; }

    public RsaSigner(RSA rsa)
    {
        _rsa = rsa ?? throw new ArgumentNullException(nameof(rsa));
        
        // Export public key as PEM and create TUF Key
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();
        var pemString = new PEMString(publicKeyPem);
        var keyValue = new RsaKeyValue(pemString);
        Key = new KeyBase.Rsa(keyValue);
    }

    public static RsaSigner Generate(int keySize = 2048)
    {
        var rsa = RSA.Create(keySize);
        return new RsaSigner(rsa);
    }

    public static RsaSigner FromPem(string privatePem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privatePem);
        return new RsaSigner(rsa);
    }

    public Signature SignBytes(ReadOnlySpan<byte> data)
    {
        // Hash the data first - TUF expects the signature to be over the hash
        var hash = SHA256.HashData(data);
        var signatureBytes = _rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return new Signature(signatureBytes);
    }

    public void Dispose()
    {
        _rsa?.Dispose();
    }
}