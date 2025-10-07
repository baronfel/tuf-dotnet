# Cryptographic Implementation Architecture

This document details the cryptographic design and implementation within TUF .NET, covering algorithms, key management, signature operations, and security considerations.

## Overview

TUF .NET's cryptographic architecture provides:
- **Multiple signature algorithms**: Ed25519, RSA-PSS, ECDSA support
- **Secure key handling**: Safe key storage and manipulation
- **Performance optimization**: Efficient cryptographic operations
- **AOT compatibility**: Native compilation support
- **Future extensibility**: Plugin architecture for new algorithms

## Cryptographic Stack

```
┌─────────────────────────────────────────────────────────────┐
│                    TUF .NET Application                     │
├─────────────────────────────────────────────────────────────┤
│                      Signing Interface                      │
│                      (ISigner, IVerifier)                  │
├─────────────────────────────────────────────────────────────┤
│                    TUF Cryptographic Layer                  │
│                    (Signing.cs, Key models)                │
├─────────────────┬─────────────────┬─────────────────────────┤
│   .NET BCL      │ NSec.Cryptography │    Future Extensions   │
│  (RSA, ECDSA,   │   (Ed25519)       │   (HSM, Cloud KMS)    │
│   SHA-256/512)  │                   │                       │
└─────────────────┴─────────────────┴─────────────────────────┘
```

## Supported Algorithms

### Digital Signature Algorithms

#### Ed25519 (Preferred)
```csharp
// Ed25519 signature implementation
public static bool VerifyEd25519(Key key, ReadOnlyMemory<byte> data, Signature signature)
{
    try
    {
        var publicKeyBytes = Convert.FromHexString(key.KeyVal["public"]);
        var signatureBytes = Convert.FromHexString(signature.SignatureValue);
        
        var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);
        return SignatureAlgorithm.Ed25519.Verify(publicKey, data.Span, signatureBytes);
    }
    catch (Exception ex)
    {
        throw new SignatureVerificationException($"Ed25519 signature verification failed: {ex.Message}", ex);
    }
}
```

**Properties**:
- **Algorithm**: EdDSA with Curve25519
- **Key Size**: 32 bytes (256 bits)
- **Signature Size**: 64 bytes (512 bits)
- **Performance**: Fastest verification, moderate signing
- **Security**: High (equivalent to ~3072-bit RSA)
- **Library**: NSec.Cryptography

#### RSA-PSS-SHA256
```csharp
public static bool VerifyRsaPss(Key key, ReadOnlyMemory<byte> data, Signature signature)
{
    try
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(key.KeyVal["public"]);
        
        var signatureBytes = Convert.FromHexString(signature.SignatureValue);
        return rsa.VerifyData(data.Span, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
    }
    catch (Exception ex)
    {
        throw new SignatureVerificationException($"RSA-PSS signature verification failed: {ex.Message}", ex);
    }
}
```

**Properties**:
- **Algorithm**: RSASSA-PSS with SHA-256
- **Key Sizes**: 2048, 3072, 4096 bits
- **Padding**: PSS (Probabilistic Signature Scheme)
- **Performance**: Slower than Ed25519, widely supported
- **Security**: High (for appropriate key sizes)
- **Library**: .NET BCL (System.Security.Cryptography)

#### ECDSA-P256-SHA256
```csharp
public static bool VerifyEcdsa(Key key, ReadOnlyMemory<byte> data, Signature signature)
{
    try
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ecdsa.ImportFromPem(key.KeyVal["public"]);
        
        var signatureBytes = Convert.FromHexString(signature.SignatureValue);
        return ecdsa.VerifyData(data.Span, signatureBytes, HashAlgorithmName.SHA256);
    }
    catch (Exception ex)
    {
        throw new SignatureVerificationException($"ECDSA signature verification failed: {ex.Message}", ex);
    }
}
```

**Properties**:
- **Algorithm**: ECDSA with NIST P-256 curve
- **Key Size**: 256 bits
- **Hash Algorithm**: SHA-256
- **Performance**: Fast verification, moderate signing
- **Security**: High (equivalent to ~3072-bit RSA)
- **Library**: .NET BCL (System.Security.Cryptography)

### Hash Algorithms

#### SHA-256 (Primary)
```csharp
public static string ComputeSha256(ReadOnlySpan<byte> data)
{
    using var sha256 = SHA256.Create();
    var hash = sha256.ComputeHash(data.ToArray());
    return Convert.ToHexString(hash).ToLowerInvariant();
}

// Streaming version for large files
public static async Task<string> ComputeSha256Async(Stream stream)
{
    using var sha256 = SHA256.Create();
    var hash = await sha256.ComputeHashAsync(stream);
    return Convert.ToHexString(hash).ToLowerInvariant();
}
```

#### SHA-512 (Alternative)
```csharp
public static string ComputeSha512(ReadOnlySpan<byte> data)
{
    using var sha512 = SHA512.Create();
    var hash = sha512.ComputeHash(data.ToArray());
    return Convert.ToHexString(hash).ToLowerInvariant();
}
```

## Key Management Architecture

### Key Models

#### TUF Key Representation
```csharp
public record Key
{
    public string KeyType { get; init; }           // "ed25519", "rsa", "ecdsa"
    public string Scheme { get; init; }            // "ed25519", "rsassa-pss-sha256", etc.
    public Dictionary<string, string> KeyVal { get; init; }  // Algorithm-specific key data
    public Dictionary<string, object> UnrecognizedFields { get; init; } = new();
    
    public string KeyId => ComputeKeyId();
    
    private string ComputeKeyId()
    {
        var canonical = CanonicalJsonSerializer.Serialize(this);
        return ComputeSha256(Encoding.UTF8.GetBytes(canonical));
    }
}
```

#### Key Storage Formats

**Ed25519 Key Format**:
```json
{
  "keytype": "ed25519",
  "scheme": "ed25519", 
  "keyval": {
    "public": "01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b"
  }
}
```

**RSA Key Format**:
```json
{
  "keytype": "rsa",
  "scheme": "rsassa-pss-sha256",
  "keyval": {
    "public": "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEF...\n-----END PUBLIC KEY-----"
  }
}
```

### Signature Interface Design

#### ISigner Interface
```csharp
public interface ISigner
{
    Task<Signature> SignAsync(ReadOnlyMemory<byte> data);
    string KeyId { get; }
    Key PublicKey { get; }
}
```

#### Concrete Signer Implementations
```csharp
public class Ed25519Signer : ISigner
{
    private readonly NSec.Cryptography.Key privateKey;
    
    public Ed25519Signer(byte[] privateKeyBytes)
    {
        privateKey = NSec.Cryptography.Key.Import(
            SignatureAlgorithm.Ed25519, 
            privateKeyBytes, 
            KeyBlobFormat.RawPrivateKey);
    }
    
    public async Task<Signature> SignAsync(ReadOnlyMemory<byte> data)
    {
        var signatureBytes = SignatureAlgorithm.Ed25519.Sign(privateKey, data.Span);
        return new Signature
        {
            KeyId = KeyId,
            SignatureValue = Convert.ToHexString(signatureBytes).ToLowerInvariant()
        };
    }
}
```

### Key Security Considerations

#### Memory Protection
```csharp
public class SecureKeySigner : ISigner, IDisposable
{
    private readonly SafeBuffer secureKeyBuffer;
    
    public SecureKeySigner(ReadOnlySpan<byte> privateKeyBytes)
    {
        // Store private key in protected memory
        secureKeyBuffer = new SafeBuffer((ulong)privateKeyBytes.Length);
        secureKeyBuffer.AcquirePointer(ref keyPtr);
        privateKeyBytes.CopyTo(new Span<byte>((void*)keyPtr, privateKeyBytes.Length));
    }
    
    public void Dispose()
    {
        secureKeyBuffer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

#### Key Validation
```csharp
public static void ValidateKey(Key key)
{
    switch (key.KeyType)
    {
        case "ed25519":
            ValidateEd25519Key(key);
            break;
        case "rsa":
            ValidateRsaKey(key);
            break;
        case "ecdsa":
            ValidateEcdsaKey(key);
            break;
        default:
            throw new UnsupportedKeyTypeException($"Unsupported key type: {key.KeyType}");
    }
}

private static void ValidateEd25519Key(Key key)
{
    if (!key.KeyVal.TryGetValue("public", out var publicKeyHex))
        throw new InvalidKeyException("Ed25519 key missing public key value");
        
    try
    {
        var publicKeyBytes = Convert.FromHexString(publicKeyHex);
        if (publicKeyBytes.Length != 32)
            throw new InvalidKeyException($"Ed25519 public key must be 32 bytes, got {publicKeyBytes.Length}");
    }
    catch (FormatException)
    {
        throw new InvalidKeyException("Ed25519 public key must be valid hexadecimal");
    }
}
```

## Signature Verification Architecture

### Verification Process Flow

```
Input: Metadata + Signatures
         │
         ▼
┌─────────────────┐
│ Canonicalize    │ ◄── Convert metadata to canonical JSON bytes
│ Metadata        │
└─────────────────┘
         │
         ▼
┌─────────────────┐
│ For Each        │ ◄── Iterate through all signatures
│ Signature       │
└─────────────────┘
         │
         ▼
┌─────────────────┐
│ Lookup Key      │ ◄── Find trusted key by KeyId
│ by KeyId        │
└─────────────────┘
         │
         ▼
┌─────────────────┐
│ Algorithm       │ ◄── Choose verification algorithm
│ Selection       │
└─────────────────┘
         │
         ▼
┌─────────────────┐
│ Cryptographic   │ ◄── Perform signature verification
│ Verification    │
└─────────────────┘
         │
         ▼
┌─────────────────┐
│ Threshold       │ ◄── Check minimum signature count
│ Validation      │
└─────────────────┘
         │
         ▼
    Success/Failure
```

### Multi-Signature Verification
```csharp
public class MultiSignatureVerifier
{
    public VerificationResult VerifySignatures(
        ReadOnlyMemory<byte> canonicalData,
        Signature[] signatures, 
        Dictionary<string, Key> trustedKeys,
        int threshold)
    {
        var validSignatures = 0;
        var verificationErrors = new List<SignatureVerificationError>();
        
        foreach (var signature in signatures)
        {
            try
            {
                if (trustedKeys.TryGetValue(signature.KeyId, out var key))
                {
                    if (VerifySingleSignature(canonicalData, signature, key))
                    {
                        validSignatures++;
                    }
                    else
                    {
                        verificationErrors.Add(new SignatureVerificationError(
                            signature.KeyId, "Signature verification failed"));
                    }
                }
                else
                {
                    verificationErrors.Add(new SignatureVerificationError(
                        signature.KeyId, "Key not found in trusted key set"));
                }
            }
            catch (Exception ex)
            {
                verificationErrors.Add(new SignatureVerificationError(
                    signature.KeyId, $"Verification exception: {ex.Message}"));
            }
        }
        
        return new VerificationResult
        {
            ValidSignatureCount = validSignatures,
            RequiredThreshold = threshold,
            IsValid = validSignatures >= threshold,
            Errors = verificationErrors
        };
    }
}
```

### Concurrent Signature Verification
```csharp
public async Task<VerificationResult> VerifySignaturesParallel(
    ReadOnlyMemory<byte> canonicalData,
    Signature[] signatures,
    Dictionary<string, Key> trustedKeys,
    int threshold)
{
    var verificationTasks = signatures.Select(async signature =>
    {
        if (trustedKeys.TryGetValue(signature.KeyId, out var key))
        {
            // CPU-intensive verification can benefit from parallel execution
            return await Task.Run(() => new SignatureResult
            {
                KeyId = signature.KeyId,
                IsValid = VerifySingleSignature(canonicalData, signature, key),
                Error = null
            });
        }
        
        return new SignatureResult
        {
            KeyId = signature.KeyId,
            IsValid = false,
            Error = "Key not found in trusted key set"
        };
    });
    
    var results = await Task.WhenAll(verificationTasks);
    var validCount = results.Count(r => r.IsValid);
    
    return new VerificationResult
    {
        ValidSignatureCount = validCount,
        RequiredThreshold = threshold,
        IsValid = validCount >= threshold,
        Results = results
    };
}
```

## Performance Optimizations

### Hash Computation Optimization

#### SIMD-Accelerated Hashing (Future Enhancement)
```csharp
public static class OptimizedHashing
{
    public static string ComputeSha256Optimized(ReadOnlySpan<byte> data)
    {
        if (Vector.IsHardwareAccelerated && data.Length > 1024)
        {
            return ComputeSha256Simd(data);
        }
        
        return ComputeSha256Standard(data);
    }
    
    private static string ComputeSha256Simd(ReadOnlySpan<byte> data)
    {
        // SIMD-optimized implementation for large data
        // Using System.Runtime.Intrinsics when available
    }
}
```

#### Incremental Hash Computation
```csharp
public class IncrementalHasher : IDisposable
{
    private readonly IncrementalHash hasher;
    
    public IncrementalHasher(HashAlgorithmName algorithm)
    {
        hasher = IncrementalHash.CreateHash(algorithm);
    }
    
    public void AppendData(ReadOnlySpan<byte> data)
    {
        hasher.AppendData(data);
    }
    
    public string GetHashAndReset()
    {
        var hash = hasher.GetHashAndReset();
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    public void Dispose() => hasher?.Dispose();
}
```

### Signature Verification Caching

#### Result Caching
```csharp
public class SignatureVerificationCache
{
    private readonly LRUCache<SignatureCacheKey, bool> cache;
    
    public bool TryVerifyFromCache(Key key, ReadOnlyMemory<byte> data, Signature signature, out bool isValid)
    {
        var cacheKey = new SignatureCacheKey(key.KeyId, ComputeDataHash(data), signature.SignatureValue);
        return cache.TryGetValue(cacheKey, out isValid);
    }
    
    public void CacheResult(Key key, ReadOnlyMemory<byte> data, Signature signature, bool isValid)
    {
        var cacheKey = new SignatureCacheKey(key.KeyId, ComputeDataHash(data), signature.SignatureValue);
        cache.Add(cacheKey, isValid);
    }
}
```

#### Key Preprocessing
```csharp
public class PreprocessedKeyCache
{
    private readonly ConcurrentDictionary<string, object> preprocessedKeys = new();
    
    public T GetOrAddPreprocessedKey<T>(string keyId, Key key, Func<Key, T> preprocessor)
    {
        return (T)preprocessedKeys.GetOrAdd(keyId, _ => preprocessor(key));
    }
}

// Usage for RSA keys
public bool VerifyRsaPssWithCaching(Key key, ReadOnlyMemory<byte> data, Signature signature)
{
    var rsa = preprocessedKeyCache.GetOrAddPreprocessedKey(key.KeyId, key, k =>
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(k.KeyVal["public"]);
        return rsa;
    });
    
    var signatureBytes = Convert.FromHexString(signature.SignatureValue);
    return rsa.VerifyData(data.Span, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
}
```

## Security Considerations

### Side-Channel Attack Mitigation

#### Constant-Time Operations
```csharp
public static bool SecureCompare(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
{
    if (a.Length != b.Length)
        return false;
        
    var result = 0;
    for (int i = 0; i < a.Length; i++)
    {
        result |= a[i] ^ b[i];
    }
    
    return result == 0;
}
```

#### Memory Wiping
```csharp
public static void SecureWipeMemory(Span<byte> sensitiveData)
{
    System.Security.Cryptography.CryptographicOperations.ZeroMemory(sensitiveData);
}
```

### Cryptographic Agility

#### Algorithm Negotiation
```csharp
public class AlgorithmSelector
{
    private static readonly string[] PreferredSignatureAlgorithms = 
    {
        "ed25519",
        "ecdsa-sha2-nistp256", 
        "rsassa-pss-sha256"
    };
    
    public string SelectBestAlgorithm(string[] availableAlgorithms)
    {
        foreach (var preferred in PreferredSignatureAlgorithms)
        {
            if (availableAlgorithms.Contains(preferred))
                return preferred;
        }
        
        throw new UnsupportedAlgorithmException(
            $"No supported algorithms found in: {string.Join(", ", availableAlgorithms)}");
    }
}
```

#### Future Algorithm Support
```csharp
public interface ISignatureAlgorithm
{
    string Name { get; }
    bool IsSupported { get; }
    bool Verify(Key publicKey, ReadOnlyMemory<byte> data, Signature signature);
    Task<Signature> SignAsync(ISigner signer, ReadOnlyMemory<byte> data);
}

public class SignatureAlgorithmRegistry
{
    private readonly Dictionary<string, ISignatureAlgorithm> algorithms = new();
    
    public void RegisterAlgorithm(ISignatureAlgorithm algorithm)
    {
        algorithms[algorithm.Name] = algorithm;
    }
    
    public ISignatureAlgorithm GetAlgorithm(string name)
    {
        return algorithms.TryGetValue(name, out var algorithm) 
            ? algorithm 
            : throw new UnsupportedAlgorithmException($"Algorithm not registered: {name}");
    }
}
```

## Hardware Security Module (HSM) Support

### HSM Interface Design
```csharp
public interface IHsmSigner : ISigner
{
    string HsmSlotId { get; }
    Task<bool> IsAvailableAsync();
    Task<IEnumerable<string>> ListKeysAsync();
}

public class Pkcs11HsmSigner : IHsmSigner
{
    public async Task<Signature> SignAsync(ReadOnlyMemory<byte> data)
    {
        // PKCS#11 HSM integration
        using var session = await OpenHsmSessionAsync();
        var signature = await session.SignAsync(keyHandle, data.ToArray());
        
        return new Signature
        {
            KeyId = KeyId,
            SignatureValue = Convert.ToHexString(signature).ToLowerInvariant()
        };
    }
}
```

### Cloud KMS Integration
```csharp
public class AzureKeyVaultSigner : ISigner
{
    private readonly KeyClient keyClient;
    private readonly CryptographyClient cryptoClient;
    
    public async Task<Signature> SignAsync(ReadOnlyMemory<byte> data)
    {
        var signResult = await cryptoClient.SignDataAsync(SignatureAlgorithm.PS256, data.ToArray());
        
        return new Signature
        {
            KeyId = KeyId,
            SignatureValue = Convert.ToHexString(signResult.Signature).ToLowerInvariant()
        };
    }
}
```

## Testing and Validation

### Cryptographic Test Vectors
```csharp
[TestFixture]
public class CryptographicValidationTests
{
    [Test]
    [TestCase("ed25519", "test-vectors/ed25519-vectors.json")]
    [TestCase("rsa", "test-vectors/rsa-pss-vectors.json")]
    [TestCase("ecdsa", "test-vectors/ecdsa-vectors.json")]
    public void ValidateAgainstTestVectors(string algorithm, string vectorFile)
    {
        var vectors = LoadTestVectors(vectorFile);
        
        foreach (var vector in vectors)
        {
            var isValid = Signing.VerifySignature(vector.Key, vector.Data, vector.Signature);
            Assert.That(isValid, Is.EqualTo(vector.ExpectedResult));
        }
    }
}
```

### Cross-Implementation Compatibility Tests
```csharp
[TestFixture]
public class InteroperabilityTests
{
    [Test]
    public void VerifyPythonGeneratedEd25519Signature()
    {
        // Test signature generated by python-tuf
        var pythonSignature = LoadPythonSignature();
        var publicKey = LoadPublicKey();
        var data = LoadSignedData();
        
        var isValid = Signing.VerifySignature(publicKey, data, pythonSignature);
        Assert.That(isValid, Is.True);
    }
    
    [Test]
    public void VerifyGoGeneratedRsaSignature()
    {
        // Test signature generated by go-tuf
        var goSignature = LoadGoSignature();
        var publicKey = LoadPublicKey();
        var data = LoadSignedData();
        
        var isValid = Signing.VerifySignature(publicKey, data, goSignature);
        Assert.That(isValid, Is.True);
    }
}
```

## Future Enhancements

### Post-Quantum Cryptography Preparation
```csharp
// Future post-quantum signature algorithm interface
public interface IPostQuantumSignatureAlgorithm : ISignatureAlgorithm
{
    bool IsQuantumResistant { get; }
    int SecurityLevel { get; } // NIST security levels 1-5
}

public class DilithiumSignatureAlgorithm : IPostQuantumSignatureAlgorithm
{
    public string Name => "dilithium3";
    public bool IsQuantumResistant => true;
    public int SecurityLevel => 3;
    
    // Implementation when standards are finalized
}
```

### Zero-Knowledge Proof Integration
```csharp
// Future zero-knowledge proof interface for advanced use cases
public interface IZkProofSystem
{
    Task<ZkProof> GenerateProofAsync(Statement statement, Witness witness);
    Task<bool> VerifyProofAsync(Statement statement, ZkProof proof);
}
```

## Related Documentation

- [Architecture Overview](overview.md) - High-level system architecture
- [Metadata Processing](metadata-processing.md) - How metadata is processed and validated
- [Performance Considerations](performance.md) - Performance optimization strategies
- [Security Model](../security/security-model.md) - Overall security design
- [Signing APIs](../api/signing.md) - Signing API reference