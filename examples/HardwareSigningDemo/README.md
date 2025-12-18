# TUF Hardware Signing Demo

This example demonstrates secure TUF operations using hardware-based signing with PIV smart cards, YubiKeys, and other Hardware Security Modules (HSMs). Hardware signing provides enhanced security for TUF deployments by keeping private keys in tamper-resistant hardware.

## Overview

The TUF .NET implementation now supports hardware-based signing through a pluggable provider architecture. This enables:

- **Enhanced Security**: Private keys never leave secure hardware
- **Air-Gapped Workflows**: Critical signing operations can be performed offline
- **Compliance**: Meet strict security requirements for production deployments
- **Flexibility**: Support for multiple hardware types through provider interface

## Supported Hardware

### PIV (Personal Identity Verification) Cards

- **YubiKeys**: Models 4, 5, and newer with PIV functionality
- **Smart Cards**: Any PIV-compatible smart card
- **Supported Algorithms**: 
  - RSA 2048/3072/4096 with PSS padding
  - ECDSA P-256 with SHA-256
  - Ed25519 (on newer YubiKeys with firmware 5.7+)

### Future Support

The architecture supports extending to:
- PKCS#11 HSMs
- Windows CNG providers  
- Cloud HSMs (Azure Key Vault, AWS CloudHSM)
- TPM (Trusted Platform Module) chips

## Prerequisites

### YubiKey Setup

1. **Install YubiKey Manager**: Download from [Yubico's website](https://developers.yubico.com/yubikey-manager/)

2. **Generate PIV Certificate**:
   ```bash
   # Generate RSA 3072 key in PIV slot 9c (Digital Signature)
   ykman piv keys generate 9c public_key.pem --algorithm RSA3072
   
   # Create self-signed certificate
   ykman piv certificates generate 9c public_key.pem --subject "CN=TUF Root Key"
   
   # Import certificate
   ykman piv certificates import 9c certificate.pem
   
   # Set PIN (default is 123456, change it!)
   ykman piv access change-pin
   ```

3. **Install Certificate in Windows**: Export the certificate and install it in the Windows certificate store so .NET can access it.

### Smart Card Setup

For PIV-compatible smart cards:

1. Use card management tools to generate keys and certificates
2. Ensure certificates are installed in Windows certificate store
3. Verify private key is accessible to current user

## Running the Demo

### Basic Demo

```bash
cd examples/HardwareSigningDemo
dotnet run
```

The demo will:
1. Discover available hardware signing providers
2. List compatible keys for TUF operations
3. Demonstrate signing operations with hardware keys
4. Show TUF metadata signing workflow
5. Explain air-gapped signing procedures

### Expected Output

```
TUF Hardware Signing Demo
Demonstrating secure TUF operations with PIV cards and YubiKeys

=== Step 1: Initialize Hardware Signing Manager ===
Initialized hardware signing manager with 1 providers:
  - PIV Smart Card: ✓ Available

=== Step 2: Discover Available Hardware Keys ===
Found 1 hardware signing keys:
  [1] PIV: CN=TUF Root Key
      Provider: PIV Smart Card
      Algorithm: RSA 3072
      Certificate: Present
      Valid: 2024-12-18 to 2025-12-18

=== Step 3: Filter for TUF-Compatible Keys ===
Found 1 TUF-compatible keys:
  - PIV: CN=TUF Root Key → TUF Scheme: rsassa-pss-sha256

=== Step 4: Demonstrate Hardware Signing ===
Using key: PIV: CN=TUF Root Key
Created hardware signer with Key ID: a1b2c3d4e5f6...
Performing hardware signing operation...
(If prompted, please enter your PIV PIN)
✓ Hardware signing successful!
  Key ID: a1b2c3d4e5f6...
  Signature: 8f7e6d5c4b3a... (384 bytes)
✓ Hardware resources cleaned up
```

## Air-Gapped Signing Workflow

For maximum security, especially for TUF root keys, use air-gapped signing:

### 1. Preparation (Online System)

```csharp
// Create unsigned root metadata
var rootMetadata = new Root
{
    SpecVersion = "1.0.0",
    Version = 1,
    Expires = DateTimeOffset.UtcNow.AddYears(1),
    // ... other metadata fields
};

// Serialize for transfer to air-gapped system
var canonicalBytes = CanonicalJson.CanonicalJson.Serialize(rootMetadata);
File.WriteAllBytes("unsigned-root.json", canonicalBytes);
```

### 2. Signing (Air-Gapped System)

```csharp
// On air-gapped system with YubiKey
var manager = new HardwareSigningManager();
var tufKeys = manager.GetTufCompatibleKeys().ToList();

if (tufKeys.Any())
{
    var selectedKey = tufKeys.First();
    using var signer = manager.CreateSigner(selectedKey);
    
    // Load unsigned metadata
    var canonicalBytes = File.ReadAllBytes("unsigned-root.json");
    
    // Sign with hardware key (will prompt for PIN)
    var signature = signer.SignBytes(canonicalBytes);
    
    // Save signature for transfer back
    var signatureJson = CanonicalJson.CanonicalJson.Serialize(signature);
    File.WriteAllBytes("root-signature.json", signatureJson);
}
```

### 3. Assembly (Online System)

```csharp
// Load unsigned metadata and signature
var canonicalBytes = File.ReadAllBytes("unsigned-root.json");
var signatureBytes = File.ReadAllBytes("root-signature.json");

var rootMetadata = CanonicalJson.CanonicalJson.Deserialize<Root>(canonicalBytes);
var signature = CanonicalJson.CanonicalJson.Deserialize<SignatureObject>(signatureBytes);

// Create complete signed metadata
var signedMetadata = new Metadata<Root>
{
    Signatures = [signature],
    Signed = rootMetadata
};

// Deploy to TUF repository
var finalJson = CanonicalJson.CanonicalJson.Serialize(signedMetadata);
File.WriteAllBytes("root.json", finalJson);
```

## Security Considerations

### Hardware Key Benefits

- **Private Key Protection**: Keys cannot be extracted from hardware
- **Tamper Resistance**: Physical attacks are detected and cause key destruction
- **Authentication Required**: PIN/biometric required for each signing operation
- **Audit Logging**: Hardware tracks all key usage operations
- **Non-Repudiation**: Strong evidence of who performed signing operations

### Air-Gapped Benefits

- **Network Isolation**: Signing system completely isolated from network
- **Malware Protection**: Reduced attack surface on offline system
- **Physical Security**: Control over signing environment and procedures
- **Compliance**: Meets strict regulatory requirements for critical keys

### Best Practices

1. **Key Hierarchy**: Use hardware keys for root and critical intermediate keys
2. **PIN Management**: Use strong PINs, change defaults, protect PIN entry
3. **Physical Security**: Secure storage and handling of hardware tokens
4. **Backup Procedures**: Multiple hardware tokens with same keys for redundancy
5. **Regular Rotation**: Periodic key rotation even with hardware protection
6. **Access Control**: Limit who can access hardware signing devices

## Production Deployment

### Root Key Management

```csharp
// Example: Production root key signing with YubiKey
public class ProductionRootSigner
{
    private readonly HardwareSigningManager _manager;
    private readonly ILogger _logger;
    
    public async Task<Metadata<Root>> SignRootMetadata(Root rootMetadata)
    {
        _logger.LogInformation("Starting root key signing with hardware token");
        
        // Require hardware key for root signing
        var tufKeys = _manager.GetTufCompatibleKeys().ToList();
        if (!tufKeys.Any())
            throw new InvalidOperationException("No hardware keys available for root signing");
        
        // Use first available hardware key (in production, you'd have key selection logic)
        var (provider, key) = tufKeys.First();
        _logger.LogInformation($"Using hardware key: {key.Identifier}");
        
        using var signer = _manager.CreateSigner((provider, key));
        
        // Sign metadata
        var canonicalBytes = CanonicalJson.CanonicalJson.Serialize(rootMetadata);
        var signature = signer.SignBytes(canonicalBytes);
        
        _logger.LogInformation($"Root metadata signed with key ID: {signature.KeyId}");
        
        return new Metadata<Root>
        {
            Signatures = [signature],
            Signed = rootMetadata
        };
    }
}
```

### Key Rotation with Hardware

```csharp
public class HardwareKeyRotation
{
    public async Task RotateRootKey(
        Metadata<Root> currentRoot,
        HardwareSigningKey newHardwareKey,
        HardwareSigningKey oldHardwareKey)
    {
        var manager = new HardwareSigningManager();
        
        // Create signers for both keys
        var oldSigner = manager.CreateSigner((provider, oldHardwareKey));
        var newSigner = manager.CreateSigner((provider, newHardwareKey));
        
        try
        {
            // Add new key to root metadata
            var newRoot = currentRoot.Signed with
            {
                Version = currentRoot.Signed.Version + 1,
                Keys = new Dictionary<string, Key>(currentRoot.Signed.Keys)
                {
                    [newSigner.Key.GetKeyId()] = newSigner.Key
                }
            };
            
            // Sign with both old and new keys for transition
            var canonicalBytes = CanonicalJson.CanonicalJson.Serialize(newRoot);
            
            var oldSignature = oldSigner.SignBytes(canonicalBytes);
            var newSignature = newSigner.SignBytes(canonicalBytes);
            
            var rotatedMetadata = new Metadata<Root>
            {
                Signatures = [oldSignature, newSignature],
                Signed = newRoot
            };
            
            // Deploy rotated root metadata
            // In next version, remove old key and sign only with new key
        }
        finally
        {
            oldSigner.Dispose();
            newSigner.Dispose();
        }
    }
}
```

## Troubleshooting

### Common Issues

1. **"No hardware signing keys found"**
   - Ensure PIV certificate is installed in Windows certificate store
   - Verify certificate has accessible private key
   - Check YubiKey/smart card is connected

2. **"PIV signing failed"**
   - Enter correct PIN when prompted
   - Ensure card is not blocked (too many wrong PIN attempts)
   - Try removing and reinserting hardware token

3. **"Private key not accessible"**
   - Certificate may not have private key associated
   - Check certificate was properly imported with private key
   - Verify current user has access to private key

4. **PIN Prompts Not Appearing**
   - Windows may cache PIN for short period
   - Try unplugging and reconnecting hardware token
   - Check Windows smart card service is running

### Debugging

Enable detailed logging:

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

var logger = loggerFactory.CreateLogger<Program>();
```

Check certificate store programmatically:

```csharp
using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
store.Open(OpenFlags.ReadOnly);

foreach (var cert in store.Certificates)
{
    Console.WriteLine($"Subject: {cert.Subject}");
    Console.WriteLine($"Has Private Key: {cert.HasPrivateKey}");
    Console.WriteLine($"Thumbprint: {cert.Thumbprint}");
    Console.WriteLine();
}
```

## Integration Examples

### ASP.NET Core Integration

```csharp
// Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<HardwareSigningManager>();
    services.AddScoped<ITufSigner, HardwareTufSigner>();
}

public class HardwareTufSigner : ITufSigner
{
    private readonly HardwareSigningManager _manager;
    
    public async Task<Metadata<T>> SignMetadata<T>(T metadata) where T : class
    {
        var keys = _manager.GetTufCompatibleKeys().ToList();
        if (!keys.Any())
            throw new InvalidOperationException("No hardware keys available");
            
        using var signer = _manager.CreateSigner(keys.First());
        
        var canonicalBytes = CanonicalJson.CanonicalJson.Serialize(metadata);
        var signature = signer.SignBytes(canonicalBytes);
        
        return new Metadata<T>
        {
            Signatures = [signature],
            Signed = metadata
        };
    }
}
```

### Dependency Injection

```csharp
services.Configure<HardwareSigningOptions>(options =>
{
    options.PreferredProvider = "PIV Smart Card";
    options.RequireHardwareKeys = true;
    options.PinCacheTimeout = TimeSpan.FromMinutes(5);
});

services.AddScoped<IHardwareSigningProvider, PivSigningProvider>();
services.AddScoped<HardwareSigningManager>();
```

## References

- [TUF Specification](https://theupdateframework.io/docs/)
- [YubiKey PIV Documentation](https://developers.yubico.com/PIV/)
- [NIST PIV Standards](https://csrc.nist.gov/projects/piv)
- [Windows Certificate Store](https://docs.microsoft.com/en-us/windows/win32/seccrypto/certificate-stores)
- [Air-Gapped Security Best Practices](https://www.cisa.gov/air-gapped-security)

---

This example demonstrates the enhanced security capabilities of TUF .NET with hardware-based signing. For production deployments requiring the highest level of security, hardware signing with air-gapped workflows provides excellent protection for critical TUF operations.