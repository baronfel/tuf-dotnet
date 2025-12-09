# TUF .NET HSM (Hardware Security Module) Integration Demo

This example demonstrates how to integrate Hardware Security Modules (HSMs) with TUF .NET for enhanced security in cryptographic signing operations. HSM integration ensures that private keys never exist in software memory, providing the highest level of security for TUF metadata signing.

## 🔐 What is HSM Integration?

Hardware Security Modules are dedicated cryptographic devices designed to securely store and manage digital keys and perform cryptographic operations. When integrated with TUF:

- **Private keys remain in hardware**: Keys are generated and stored within tamper-resistant hardware
- **Hardware-enforced authentication**: Access to keys requires proper authentication (PIN/password)
- **Audit trails**: All cryptographic operations are logged by the HSM
- **Compliance support**: Meets requirements for FIPS 140-2, Common Criteria, and other standards
- **Attack resistance**: Protection against physical and logical attacks

## 🎯 Use Cases

### Production Repository Management
- **Root key protection**: Store TUF root private keys in HSMs for maximum security
- **Key rotation**: Safely manage key lifecycle with hardware controls
- **Multi-person authorization**: Some HSMs support M-of-N authentication schemes
- **Compliance**: Meet regulatory requirements for key management

### Enterprise Deployment
- **CI/CD integration**: Secure automated signing in build pipelines
- **Cloud HSM**: Use cloud-based HSMs (AWS CloudHSM, Azure Dedicated HSM)
- **High availability**: Clustered HSM deployments for continuous operation
- **Disaster recovery**: HSM backup and recovery procedures

### Air-Gapped Environments
- **Offline signing**: Use HSMs in completely isolated networks
- **Physical security**: HSM devices can be stored in secure facilities
- **Manual processes**: Operator-driven signing workflows with HSM protection

## 🚀 Quick Start

### Prerequisites

1. **HSM or PKCS#11 Device**:
   - Physical HSM (SafeNet Luna, Thales, Utimaco)
   - Smart card (YubiKey PIV, CAC card)
   - SoftHSM for testing (software-based PKCS#11 implementation)

2. **PKCS#11 Library**:
   - Provided by HSM vendor
   - For SoftHSM: `libsofthsm2.so` (Linux) or `libsofthsm2.dylib` (macOS)

3. **.NET 8.0 or later**

### Setting Up SoftHSM (For Testing)

SoftHSM provides a software implementation of PKCS#11 for development and testing:

```bash
# Install SoftHSM
## Ubuntu/Debian
sudo apt-get install softhsm2

## macOS
brew install softhsm

## Windows (via vcpkg)
vcpkg install softhsm2

# Initialize a token
softhsm2-util --init-token --slot 0 --label "TUF_TOKEN" --pin 1234 --so-pin 5678

# Generate a key pair in the token
softhsm2-util --keypairgen --slot 0 --label "TUF_ROOT_KEY" --id 01 --pin 1234

# Verify token setup
softhsm2-util --show-slots
```

### Running the Demo

```bash
# Build the demo
dotnet build examples/HsmSigningDemo

# Get token serial number
TOKEN_SERIAL=$(softhsm2-util --show-slots | grep "Serial number" | head -1 | cut -d: -f2 | tr -d ' ')

# Run with SoftHSM
dotnet run --project examples/HsmSigningDemo -- \
  /usr/lib/softhsm/libsofthsm2.so \
  $TOKEN_SERIAL \
  TUF_ROOT_KEY \
  1234 \
  RsaPssSha256

# Run with different signature algorithm
dotnet run --project examples/HsmSigningDemo -- \
  /usr/lib/softhsm/libsofthsm2.so \
  $TOKEN_SERIAL \
  TUF_ROOT_KEY \
  1234 \
  EcdsaSha2NistP256
```

## 🏗️ Architecture

### HSM Signer Implementation

The `TUF.Hsm` library provides:

```csharp
// HSM signer interface
public interface IHsmSigner : ISigner, IDisposable
{
    string KeyLabel { get; }
    string TokenSerial { get; }
    bool TestConnection();
}

// PKCS#11 implementation
public class Pkcs11Signer : IHsmSigner
{
    public enum SignatureAlgorithm
    {
        RsaPssSha256,        // Most widely supported
        EcdsaSha2NistP256,   // Smaller signatures
        Ed25519              // Modern, fast (HSM support varies)
    }
}
```

### Integration with TUF Repository

HSM signers work seamlessly with existing TUF infrastructure:

```csharp
// Create HSM signer
using var hsmSigner = new Pkcs11Signer(
    "/usr/lib/softhsm/libsofthsm2.so",  // PKCS#11 library
    "123456789",                         // Token serial
    "TUF_ROOT_KEY",                     // Key label
    "1234",                             // PIN
    Pkcs11Signer.SignatureAlgorithm.RsaPssSha256);

// Use with repository builder
var repository = new RepositoryBuilder();
repository.AddSigner("root", hsmSigner);

// HSM will be used for root metadata signing
var rootMetadata = repository.CreateRootMetadata();
```

## 🔧 Configuration

### HSM Library Paths

Common PKCS#11 library locations:

```bash
# SoftHSM
/usr/lib/softhsm/libsofthsm2.so         # Linux
/usr/local/lib/softhsm/libsofthsm2.so   # Linux (custom install)
/usr/local/lib/softhsm/libsofthsm2.dylib # macOS

# SafeNet Luna
/usr/safenet/lunaclient/lib/libCryptoki2_64.so    # Linux 64-bit
/usr/safenet/lunaclient/lib/libCryptoki2.so       # Linux 32-bit

# Thales (Gemalto) 
/opt/gemalto/lib64/libpkcs11.so                   # Linux 64-bit

# AWS CloudHSM
/opt/cloudhsm/lib/libcloudhsm_pkcs11.so          # Linux
```

### Environment Variables

Set environment variables for easier configuration:

```bash
export TUF_HSM_LIBRARY="/usr/lib/softhsm/libsofthsm2.so"
export TUF_HSM_TOKEN_SERIAL="123456789"
export TUF_HSM_KEY_LABEL="TUF_ROOT_KEY"
export TUF_HSM_PIN="1234"
```

### Configuration Files

For production use, store HSM configuration in secure configuration files:

```json
{
  "hsm": {
    "pkcs11Library": "/usr/safenet/lunaclient/lib/libCryptoki2_64.so",
    "tokenSerial": "ABC123DEF456",
    "keys": {
      "root": {
        "label": "TUF_ROOT_2024",
        "algorithm": "RsaPssSha256"
      },
      "timestamp": {
        "label": "TUF_TIMESTAMP_2024", 
        "algorithm": "EcdsaSha2NistP256"
      }
    }
  }
}
```

## 🛡️ Security Best Practices

### Key Management
- **Separate keys by role**: Use different keys for root, timestamp, snapshot, targets
- **Key rotation**: Regularly rotate keys according to your security policy
- **Backup and recovery**: Ensure HSM keys can be recovered in disaster scenarios
- **Access control**: Limit HSM access to authorized personnel only

### Authentication
- **Strong PINs**: Use complex, non-predictable PINs for token access
- **Multi-factor authentication**: Enable additional authentication factors where available
- **PIN policies**: Configure HSM PIN policies (length, complexity, retry limits)
- **Segregation**: Use separate tokens/partitions for different environments

### Operational Security
- **Logging**: Monitor and log all HSM access and operations
- **Network security**: Secure network connections to network-attached HSMs
- **Physical security**: Protect HSM devices with appropriate physical controls
- **Incident response**: Have procedures for handling HSM failures or compromises

## 🔍 Troubleshooting

### Common Issues

#### PKCS#11 Library Not Found
```
Error: PKCS#11 library could not be loaded
```
**Solution**: Verify library path and ensure HSM software is installed

#### Token Not Found
```
Error: HSM token with serial number 'XXX' not found
```
**Solution**: Check token is inserted and accessible:
```bash
softhsm2-util --show-slots
pkcs11-tool --list-slots
```

#### Authentication Failed
```
Error: HSM authentication failed
```
**Solution**: Verify PIN and check if token is locked:
```bash
pkcs11-tool --list-slots  # Check token status
softhsm2-util --show-slots  # For SoftHSM
```

#### Key Not Found
```
Error: Private key with label 'XXX' not found
```
**Solution**: List available keys:
```bash
pkcs11-tool --list-objects --pin 1234
softhsm2-util --show-slots --pin 1234
```

### Debugging Tips

#### Enable Verbose Logging
```csharp
// Add detailed logging to your application
var logger = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
    .CreateLogger<Pkcs11Signer>();
```

#### Test HSM Connection
```csharp
// Test HSM connectivity before using
using var signer = new Pkcs11Signer(/* ... */);
if (!signer.TestConnection())
{
    Console.WriteLine("HSM connection failed");
    return;
}
```

#### Validate PKCS#11 Setup
```bash
# Test PKCS#11 library directly
pkcs11-tool --library /path/to/library --list-slots
pkcs11-tool --library /path/to/library --list-objects --pin YOUR_PIN
```

## 📚 Additional Resources

### Documentation
- [PKCS #11 Specification](https://www.oasis-open.org/committees/pkcs11/)
- [Pkcs11Interop Documentation](https://pkcs11interop.net/doc/)
- [TUF Specification](https://theupdateframework.github.io/specification/latest/)

### HSM Vendors
- [SafeNet Luna HSMs](https://www.thalesesecurity.com/products/data-encryption/hardware-security-modules)
- [Utimaco HSMs](https://www.utimaco.com/products/hsm/)
- [AWS CloudHSM](https://aws.amazon.com/cloudhsm/)
- [Azure Dedicated HSM](https://azure.microsoft.com/services/azure-dedicated-hsm/)

### Testing Tools
- [SoftHSM](https://github.com/opendnssec/SoftHSMv2) - Software PKCS#11 implementation
- [OpenSC pkcs11-tool](https://github.com/OpenSC/OpenSC) - PKCS#11 testing utility

## 🤝 Contributing

This HSM integration addresses a critical parity gap where other TUF implementations (Python-TUF with PyKCS11, AWS Tough with KMS) provided hardware security integration while TUF .NET was limited to software-only signing.

The implementation provides:
- ✅ **Complete PKCS#11 integration** for standards-based HSM support
- ✅ **Multiple signature algorithms** (RSA-PSS, ECDSA, Ed25519 where supported)
- ✅ **Production-ready patterns** with proper error handling and resource management
- ✅ **Comprehensive documentation** and examples for enterprise adoption

This brings TUF .NET to full parity with other implementations for enterprise-grade security requirements.

---

> This HSM integration was developed as part of the [achieve-parity initiative](https://github.com/baronfel/tuf-dotnet/issues/112) to bring TUF .NET capabilities to parity with established implementations in other languages.