# Signing API Reference

The TUF .NET signing API provides cryptographic signing capabilities for TUF metadata and repository management. This document covers the signing interfaces, supported algorithms, and best practices for secure signing operations.

## Overview

The signing API is built around the `ISigner` interface and provides implementations for Ed25519, RSA-PSS, and ECDSA signatures. All signing operations are designed to be secure by default with support for both online and offline signing scenarios.

## Core Interfaces

### ISigner Interface

The primary interface for all signing operations:

```csharp
public interface ISigner
{
    /// <summary>
    /// The unique identifier for this signer
    /// </summary>
    string KeyId { get; }
    
    /// <summary>
    /// The cryptographic algorithm used by this signer
    /// </summary>
    string Algorithm { get; }
    
    /// <summary>
    /// The public key in TUF format
    /// </summary>
    PublicKey PublicKey { get; }
    
    /// <summary>
    /// Sign the provided data
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>Digital signature</returns>
    Signature Sign(ReadOnlySpan<byte> data);
    
    /// <summary>
    /// Verify a signature against the provided data
    /// </summary>
    /// <param name="data">Original data</param>
    /// <param name="signature">Signature to verify</param>
    /// <returns>True if signature is valid</returns>
    bool Verify(ReadOnlySpan<byte> data, Signature signature);
}
```

### Signature Structure

Signatures in TUF .NET are strongly-typed:

```csharp
public record Signature
{
    /// <summary>
    /// Key identifier that created this signature
    /// </summary>
    public required string KeyId { get; init; }
    
    /// <summary>
    /// The signature bytes
    /// </summary>
    public required byte[] SignatureBytes { get; init; }
}

public record PublicKey
{
    /// <summary>
    /// Key type (e.g., "ed25519", "rsa", "ecdsa")
    /// </summary>
    public required string KeyType { get; init; }
    
    /// <summary>
    /// Key scheme (e.g., "ed25519", "rsa-pss-sha256", "ecdsa-sha2-nistp256")
    /// </summary>
    public required string Scheme { get; init; }
    
    /// <summary>
    /// Algorithm-specific key data
    /// </summary>
    public required IReadOnlyDictionary<string, object> KeyVal { get; init; }
}
```

## Signer Implementations

### Ed25519Signer (Recommended)

Ed25519 provides the best security and performance characteristics:

```csharp
public static class Ed25519Signer
{
    /// <summary>
    /// Generate a new Ed25519 signer with a random key pair
    /// </summary>
    public static ISigner Generate()
    {
        return Generate(keyId: Guid.NewGuid().ToString());
    }
    
    /// <summary>
    /// Generate a new Ed25519 signer with a specific key ID
    /// </summary>
    public static ISigner Generate(string keyId)
    {
        var key = Key.Create(SignatureAlgorithm.Ed25519);
        return new Ed25519SignerImpl(keyId, key);
    }
    
    /// <summary>
    /// Create an Ed25519 signer from existing key material
    /// </summary>
    public static ISigner FromKey(string keyId, Key privateKey)
    {
        if (privateKey.Algorithm != SignatureAlgorithm.Ed25519)
            throw new ArgumentException("Key must be Ed25519", nameof(privateKey));
            
        return new Ed25519SignerImpl(keyId, privateKey);
    }
    
    /// <summary>
    /// Import an Ed25519 signer from PEM format
    /// </summary>
    public static ISigner ImportFromPem(string keyId, string pemData)
    {
        var key = Key.Import(SignatureAlgorithm.Ed25519, pemData, KeyBlobFormat.PkixPrivateKey);
        return new Ed25519SignerImpl(keyId, key);
    }
}

// Usage example
var signer = Ed25519Signer.Generate("my-signing-key");

// Sign some data
var data = "Hello, TUF!"u8.ToArray();
var signature = signer.Sign(data);

// Verify the signature
var isValid = signer.Verify(data, signature);
Console.WriteLine($"Signature valid: {isValid}");
```

**Ed25519 Advantages:**
- High security (128-bit security level)
- Fast signing and verification
- Small key and signature sizes (32-byte keys, 64-byte signatures)
- No parameter choices (eliminates misconfiguration)
- Deterministic signatures (same input always produces same signature)

### RSA-PSS Signer

RSA-PSS provides compatibility with systems requiring RSA signatures:

```csharp
public static class RsaSigner
{
    /// <summary>
    /// Generate a new RSA-PSS signer (minimum 3072 bits for security)
    /// </summary>
    public static ISigner Generate(int keySize = 3072)
    {
        if (keySize < 3072)
            throw new ArgumentException("RSA keys must be at least 3072 bits for security");
            
        return Generate(Guid.NewGuid().ToString(), keySize);
    }
    
    /// <summary>
    /// Generate a new RSA-PSS signer with specific key ID
    /// </summary>
    public static ISigner Generate(string keyId, int keySize = 3072)
    {
        var keyCreationParameters = new RSAKeyCreationParameters
        {
            ModulusLength = keySize,
            PublicExponent = RSAKeyCreationParameters.DefaultPublicExponent
        };
        
        var key = Key.Create(SignatureAlgorithm.RsaPkcs1, keyCreationParameters);
        return new RsaSignerImpl(keyId, key, useRsaPss: true);
    }
    
    /// <summary>
    /// Import RSA signer from existing RSA instance
    /// </summary>
    public static ISigner FromRsa(string keyId, RSA rsa, bool useRsaPss = true)
    {
        var key = Key.Import(
            useRsaPss ? SignatureAlgorithm.RsaPss : SignatureAlgorithm.RsaPkcs1,
            rsa.ExportRSAPrivateKey(),
            KeyBlobFormat.RsaPrivateKey);
            
        return new RsaSignerImpl(keyId, key, useRsaPss);
    }
}

// Usage example
var rsaSigner = RsaSigner.Generate("rsa-signing-key", keySize: 4096);

// RSA-PSS provides better security than PKCS#1 v1.5
var data = "Important security update"u8.ToArray();
var signature = rsaSigner.Sign(data);
```

**RSA-PSS Advantages:**
- Widely supported and understood
- Stronger security than PKCS#1 v1.5
- Configurable key sizes
- Good for systems requiring RSA compatibility

**RSA-PSS Considerations:**
- Larger keys and signatures than Ed25519
- Slower than Ed25519
- Requires careful parameter selection

### ECDSA Signer

ECDSA provides a good balance of security and performance:

```csharp
public static class EcdsaSigner
{
    /// <summary>
    /// Generate a new ECDSA signer using P-256 curve (recommended)
    /// </summary>
    public static ISigner Generate()
    {
        return Generate(Guid.NewGuid().ToString(), ECCurve.NamedCurves.nistP256);
    }
    
    /// <summary>
    /// Generate ECDSA signer with specific curve
    /// </summary>
    public static ISigner Generate(string keyId, ECCurve curve)
    {
        ValidateCurve(curve);
        
        var ecdsa = ECDsa.Create(curve);
        var key = Key.Import(SignatureAlgorithm.ECDsa, 
            ecdsa.ExportECPrivateKey(), 
            KeyBlobFormat.EcPrivateKey);
            
        return new EcdsaSignerImpl(keyId, key, curve);
    }
    
    /// <summary>
    /// Import ECDSA signer from existing ECDsa instance
    /// </summary>
    public static ISigner FromEcdsa(string keyId, ECDsa ecdsa)
    {
        var curve = GetCurveFromEcdsa(ecdsa);
        ValidateCurve(curve);
        
        var key = Key.Import(SignatureAlgorithm.ECDsa,
            ecdsa.ExportECPrivateKey(),
            KeyBlobFormat.EcPrivateKey);
            
        return new EcdsaSignerImpl(keyId, key, curve);
    }
    
    private static void ValidateCurve(ECCurve curve)
    {
        // Only allow secure curves
        var allowedCurves = new[]
        {
            ECCurve.NamedCurves.nistP256,
            ECCurve.NamedCurves.nistP384,
            ECCurve.NamedCurves.nistP521
        };
        
        if (!allowedCurves.Any(allowed => curve.Oid?.Value == allowed.Oid?.Value))
            throw new ArgumentException("Only NIST P-256, P-384, and P-521 curves are supported");
    }
}

// Usage example
var ecdsaSigner = EcdsaSigner.Generate("ecdsa-key", ECCurve.NamedCurves.nistP256);

// ECDSA signatures are not deterministic (include randomness)
var data = "Secure update content"u8.ToArray();
var signature1 = ecdsaSigner.Sign(data);
var signature2 = ecdsaSigner.Sign(data); // Different signature, same validity

Debug.Assert(ecdsaSigner.Verify(data, signature1));
Debug.Assert(ecdsaSigner.Verify(data, signature2));
```

## Algorithm Selection Guide

| Algorithm | Security Level | Performance | Signature Size | Key Size | Deterministic | Recommendation |
|-----------|---------------|-------------|---------------|----------|---------------|----------------|
| Ed25519   | 128-bit       | Fastest     | 64 bytes      | 32 bytes | Yes           | **Preferred** |
| ECDSA P-256 | 128-bit     | Fast        | ~72 bytes     | ~32 bytes | No           | Good alternative |
| ECDSA P-384 | 192-bit     | Medium      | ~104 bytes    | ~48 bytes | No           | High security needs |
| RSA-PSS 3072 | 128-bit    | Slow        | 384 bytes     | 384 bytes | No          | Legacy compatibility |
| RSA-PSS 4096 | 150-bit    | Slower      | 512 bytes     | 512 bytes | No          | High security + legacy |

### Algorithm Selection Recommendations

**For new implementations:** Use Ed25519
```csharp
var signer = Ed25519Signer.Generate("primary-signing-key");
```

**For high-security requirements:** Use Ed25519 or ECDSA P-384
```csharp
var highSecSigner = EcdsaSigner.Generate("high-sec-key", ECCurve.NamedCurves.nistP384);
```

**For legacy system compatibility:** Use RSA-PSS with 3072+ bit keys
```csharp
var legacySigner = RsaSigner.Generate("legacy-key", keySize: 4096);
```

## Repository Signing

### Single Signer Repository

Create a repository with single signers for each role:

```csharp
public async Task<Repository> CreateSingleSignerRepositoryAsync()
{
    // Generate signers for each role
    var rootSigner = Ed25519Signer.Generate("root-key");
    var timestampSigner = Ed25519Signer.Generate("timestamp-key");  
    var snapshotSigner = Ed25519Signer.Generate("snapshot-key");
    var targetsSigner = Ed25519Signer.Generate("targets-key");
    
    var builder = new RepositoryBuilder()
        .AddSigner("root", rootSigner)
        .AddSigner("timestamp", timestampSigner)
        .AddSigner("snapshot", snapshotSigner)
        .AddSigner("targets", targetsSigner);
        
    // Add target files
    builder.AddTarget("app.exe", await File.ReadAllBytesAsync("app.exe"));
    builder.AddTarget("config.json", await File.ReadAllBytesAsync("config.json"));
    
    return builder.Build();
}
```

### Multi-Signer Repository (Threshold Signatures)

Create a repository with multiple signers and threshold requirements:

```csharp
public async Task<Repository> CreateMultiSignerRepositoryAsync()
{
    // Create multiple signers for critical roles
    var rootSigners = new[]
    {
        Ed25519Signer.Generate("root-key-1"),
        Ed25519Signer.Generate("root-key-2"), 
        Ed25519Signer.Generate("root-key-3")
    };
    
    var targetsSigners = new[]
    {
        Ed25519Signer.Generate("targets-key-1"),
        Ed25519Signer.Generate("targets-key-2")
    };
    
    var builder = new RepositoryBuilder();
    
    // Root role: require 2 of 3 signatures
    foreach (var signer in rootSigners)
        builder.AddSigner("root", signer);
    builder.SetThreshold("root", threshold: 2);
    
    // Targets role: require 2 of 2 signatures  
    foreach (var signer in targetsSigners)
        builder.AddSigner("targets", signer);
    builder.SetThreshold("targets", threshold: 2);
    
    // Online roles can use single signers
    builder.AddSigner("timestamp", Ed25519Signer.Generate("timestamp-key"));
    builder.AddSigner("snapshot", Ed25519Signer.Generate("snapshot-key"));
    
    // Add targets
    builder.AddTarget("critical-app.exe", await File.ReadAllBytesAsync("critical-app.exe"));
    
    return builder.Build();
}
```

### Offline Signing Workflow

For production environments, implement offline signing for critical roles:

```csharp
public class OfflineSigningWorkflow
{
    public async Task<Repository> CreateRepositoryWithOfflineSigningAsync()
    {
        // 1. Prepare metadata on online system (without signatures)
        var unsignedMetadata = await PrepareUnsignedMetadataAsync();
        
        // 2. Transfer to offline system for signing
        var metadataToSign = SerializeForOfflineSigning(unsignedMetadata);
        await WriteToSecureTransferAsync("metadata-to-sign.json", metadataToSign);
        
        Console.WriteLine("Transfer metadata-to-sign.json to offline system");
        Console.WriteLine("Run offline signing process");
        Console.WriteLine("Transfer signed metadata back");
        Console.ReadKey();
        
        // 3. Load signed metadata from offline system
        var signedMetadata = await LoadSignedMetadataAsync("signed-metadata.json");
        
        // 4. Combine with online signatures and finalize
        return await FinalizeRepositoryAsync(signedMetadata);
    }
    
    private async Task<UnsignedMetadata> PrepareUnsignedMetadataAsync()
    {
        // Create all metadata without signatures
        // This runs on the online system with target files
        var targets = await LoadTargetFilesAsync();
        return new UnsignedMetadata
        {
            Targets = CreateTargetsMetadata(targets),
            Snapshot = CreateSnapshotMetadata(),
            Timestamp = CreateTimestampMetadata(),
            Root = CreateRootMetadata()
        };
    }
}
```

### Automated Online Signing

For frequently updated metadata (timestamp, snapshot), implement automated signing:

```csharp
public class AutomatedSigningService : BackgroundService  
{
    private readonly ISigner _timestampSigner;
    private readonly ISigner _snapshotSigner;
    private readonly IRepositoryManager _repository;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Update timestamp metadata every 5 minutes
                await UpdateTimestampMetadataAsync();
                
                // Update snapshot metadata if targets changed
                await UpdateSnapshotIfNeededAsync();
                
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in automated signing");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
    
    private async Task UpdateTimestampMetadataAsync()
    {
        var currentSnapshot = await _repository.GetSnapshotMetadataAsync();
        
        var timestampMetadata = new Timestamp
        {
            Version = await _repository.GetNextTimestampVersionAsync(),
            Expires = DateTime.UtcNow.AddHours(1), // Short expiration
            Meta = new Dictionary<string, MetaFile>
            {
                ["snapshot.json"] = new MetaFile
                {
                    Version = currentSnapshot.Version,
                    Length = CalculateLength(currentSnapshot),
                    Hashes = CalculateHashes(currentSnapshot)
                }
            }
        };
        
        var signedTimestamp = await SignMetadataAsync(timestampMetadata, _timestampSigner);
        await _repository.PublishTimestampAsync(signedTimestamp);
    }
}
```

## Key Management Integration

### Secure Key Storage

Integrate with secure key storage systems:

```csharp
public interface IKeyStore
{
    Task StoreKeyAsync(string keyId, ISigner signer);
    Task<ISigner> LoadKeyAsync(string keyId);
    Task DeleteKeyAsync(string keyId);
    Task<bool> KeyExistsAsync(string keyId);
}

public class HardwareSecurityModuleKeyStore : IKeyStore
{
    private readonly IHsmClient _hsmClient;
    
    public async Task StoreKeyAsync(string keyId, ISigner signer)
    {
        // Store private key in HSM
        await _hsmClient.ImportKeyAsync(keyId, signer.PrivateKey);
    }
    
    public async Task<ISigner> LoadKeyAsync(string keyId)
    {
        // Create signer that uses HSM for private key operations
        var publicKey = await _hsmClient.GetPublicKeyAsync(keyId);
        return new HsmSigner(keyId, publicKey, _hsmClient);
    }
}

// HSM-backed signer
public class HsmSigner : ISigner
{
    private readonly IHsmClient _hsmClient;
    
    public Signature Sign(ReadOnlySpan<byte> data)
    {
        // Sign using HSM (private key never leaves HSM)
        var signatureBytes = _hsmClient.SignAsync(KeyId, data.ToArray()).Result;
        
        return new Signature
        {
            KeyId = KeyId,
            SignatureBytes = signatureBytes
        };
    }
}
```

### Key Rotation

Implement key rotation with signing continuity:

```csharp
public class KeyRotationManager
{
    private readonly IKeyStore _keyStore;
    private readonly IRepositoryManager _repository;
    
    public async Task RotateKeyAsync(string role, string currentKeyId)
    {
        // 1. Generate new key
        var newSigner = Ed25519Signer.Generate($"{role}-{DateTime.UtcNow:yyyyMMdd}");
        await _keyStore.StoreKeyAsync(newSigner.KeyId, newSigner);
        
        // 2. Update root metadata with new key
        var rootMetadata = await _repository.GetRootMetadataAsync();
        var updatedRoot = rootMetadata with
        {
            Keys = rootMetadata.Keys.SetItem(newSigner.KeyId, newSigner.PublicKey),
            Roles = rootMetadata.Roles.SetItem(role, new Role
            {
                KeyIds = new[] { newSigner.KeyId },
                Threshold = 1
            }),
            Version = rootMetadata.Version + 1
        };
        
        // 3. Sign updated root with both old and new keys (for transition)
        var oldSigner = await _keyStore.LoadKeyAsync(currentKeyId);
        var signedRoot = await SignWithMultipleKeys(updatedRoot, oldSigner, newSigner);
        
        // 4. Publish updated root
        await _repository.PublishRootAsync(signedRoot);
        
        // 5. Schedule old key cleanup (after grace period)
        await ScheduleKeyCleanupAsync(currentKeyId, TimeSpan.FromDays(30));
    }
}
```

## Security Best Practices

### 1. Key Generation
```csharp
// Always use cryptographically secure random number generation
var signer = Ed25519Signer.Generate(); // Uses secure RNG internally

// For HSM or secure enclaves
var hsm = new HardwareSecurityModule();
var hsmKey = await hsm.GenerateKeyAsync(KeyType.Ed25519);
```

### 2. Signature Verification
```csharp
// Always verify signatures before trusting metadata
public async Task<bool> VerifyMetadataAsync<T>(Metadata<T> metadata) where T : class
{
    // 1. Check signature count meets threshold
    var role = GetRoleForMetadataType<T>();
    var requiredThreshold = await GetThresholdAsync(role);
    
    if (metadata.Signatures.Count < requiredThreshold)
        return false;
    
    // 2. Verify each signature
    var validSignatures = 0;
    foreach (var signature in metadata.Signatures)
    {
        var publicKey = await GetPublicKeyAsync(signature.KeyId);
        if (publicKey == null) continue;
        
        var signer = CreateVerificationSigner(publicKey);
        var canonicalBytes = CanonicalJsonSerializer.Serialize(metadata.Signed);
        
        if (signer.Verify(canonicalBytes, signature))
            validSignatures++;
    }
    
    return validSignatures >= requiredThreshold;
}
```

### 3. Canonical JSON for Signatures
```csharp
// Always use canonical JSON for signature generation and verification
public Signature SignMetadata<T>(T metadata, ISigner signer) where T : class
{
    // Serialize to canonical JSON (deterministic)
    var canonicalBytes = CanonicalJsonSerializer.Serialize(metadata);
    
    // Sign the canonical bytes
    return signer.Sign(canonicalBytes);
}
```

### 4. Key Algorithm Validation
```csharp
public void ValidateSignerSecurity(ISigner signer)
{
    switch (signer.Algorithm.ToLowerInvariant())
    {
        case "ed25519":
            // Ed25519 is always secure
            break;
            
        case "rsa-pss-sha256":
        case "rsa-pss-sha384":
        case "rsa-pss-sha512":
            if (signer is RsaSigner rsa && rsa.KeySize < 3072)
                throw new SecurityException("RSA keys must be at least 3072 bits");
            break;
            
        case "ecdsa-sha2-nistp256":
        case "ecdsa-sha2-nistp384": 
        case "ecdsa-sha2-nistp521":
            // NIST curves are acceptable
            break;
            
        default:
            throw new SecurityException($"Unsupported or insecure algorithm: {signer.Algorithm}");
    }
}
```

## Performance Considerations

### Signature Verification Performance

```csharp
// For high-throughput scenarios, consider signature verification caching
public class CachingSignatureVerifier
{
    private readonly ConcurrentDictionary<string, bool> _verificationCache = new();
    
    public bool VerifyWithCaching(ReadOnlySpan<byte> data, Signature signature, ISigner signer)
    {
        var cacheKey = ComputeCacheKey(data, signature);
        
        return _verificationCache.GetOrAdd(cacheKey, key =>
        {
            return signer.Verify(data, signature);
        });
    }
    
    private string ComputeCacheKey(ReadOnlySpan<byte> data, Signature signature)
    {
        // Create cache key from data hash and signature
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        
        return $"{Convert.ToHexString(hash)}:{signature.KeyId}:{Convert.ToHexString(signature.SignatureBytes)}";
    }
}
```

### Batch Signature Operations

```csharp
// For signing multiple pieces of data with the same key
public class BatchSigner
{
    private readonly ISigner _signer;
    
    public async Task<Signature[]> SignBatchAsync(IEnumerable<byte[]> dataItems)
    {
        // Process in parallel for better performance
        return await Task.WhenAll(
            dataItems.Select(async data => await Task.Run(() => _signer.Sign(data)))
        );
    }
}
```

## Error Handling

```csharp
public enum SigningErrorType
{
    KeyNotFound,
    InvalidKey,
    SigningFailed,
    VerificationFailed,
    UnsupportedAlgorithm,
    InsufficientPermissions
}

public class SigningException : TufException
{
    public SigningErrorType ErrorType { get; }
    
    public SigningException(SigningErrorType errorType, string message) 
        : base(message)
    {
        ErrorType = errorType;
    }
}

// Usage in signing operations
try
{
    var signature = signer.Sign(data);
}
catch (CryptographicException ex)
{
    throw new SigningException(SigningErrorType.SigningFailed, 
        $"Cryptographic signing failed: {ex.Message}", ex);
}
```

## Testing

```csharp
[TestFixture]
public class SigningApiTests
{
    [Test]
    public void Ed25519Signer_GenerateAndVerify_ShouldWork()
    {
        // Arrange
        var signer = Ed25519Signer.Generate("test-key");
        var testData = "Hello, TUF!"u8.ToArray();
        
        // Act
        var signature = signer.Sign(testData);
        var isValid = signer.Verify(testData, signature);
        
        // Assert
        Assert.That(isValid, Is.True);
        Assert.That(signature.KeyId, Is.EqualTo("test-key"));
        Assert.That(signature.SignatureBytes.Length, Is.EqualTo(64)); // Ed25519 signature size
    }
    
    [Test]
    public void AllSigners_WithTamperedData_ShouldFailVerification()
    {
        // Test all supported algorithms
        var signers = new ISigner[]
        {
            Ed25519Signer.Generate("ed25519-test"),
            RsaSigner.Generate("rsa-test"),
            EcdsaSigner.Generate("ecdsa-test")
        };
        
        foreach (var signer in signers)
        {
            var originalData = "Original data"u8.ToArray();
            var tamperedData = "Tampered data"u8.ToArray();
            
            var signature = signer.Sign(originalData);
            var isValid = signer.Verify(tamperedData, signature);
            
            Assert.That(isValid, Is.False, 
                $"Signature verification should fail for tampered data with {signer.Algorithm}");
        }
    }
}
```

## Related Documentation

- **[Repository Builder](./repository-builder.md)** - Using signers with repository creation
- **[Security Model](../security/security-model.md)** - Understanding TUF's cryptographic security
- **[Implementation Practices](../security/implementation-practices.md)** - Security best practices for production

> AI-generated content by [Update Docs](https://github.com/baronfel/tuf-dotnet/actions/runs/17420730471) may contain mistakes.