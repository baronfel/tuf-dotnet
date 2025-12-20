# TUF Hardware Signing Example

This example demonstrates how to use TUF .NET with hardware security modules (HSMs) and hardware tokens like YubiKey PIV for secure signing operations. Hardware signing is especially important for root key signing in production TUF deployments.

## 🔒 Why Hardware Signing?

Hardware signing provides enhanced security for TUF deployments by:

- **Private Key Protection**: Private keys never leave the tamper-resistant hardware boundary
- **PIN-based Authentication**: Requires physical possession and knowledge of PIN
- **User Presence**: Optional touch/presence requirements for signing operations
- **Audit Trail**: Hardware devices provide audit logs for all cryptographic operations
- **Compliance**: Meets enterprise and government security requirements (FIPS 140-2, Common Criteria)

## 🏗️ Architecture Overview

TUF .NET hardware signing is built on a flexible architecture supporting multiple hardware providers:

```
┌─────────────────┐    ┌────────────────────┐    ┌─────────────────────┐
│   TUF Client    │────│  ISigner Interface │────│ HardwareSigner Base │
│  Applications   │    │                    │    │                     │
└─────────────────┘    └────────────────────┘    └─────────────────────┘
                                                              │
                       ┌──────────────────────────────────────┼──────────────────────────────────────┐
                       │                                      │                                      │
           ┌─────────────────────┐               ┌─────────────────────┐               ┌─────────────────────┐
           │   Pkcs11Signer      │               │   WindowsCngSigner  │               │  AzureKeyVaultSigner│
           │  (Cross-platform)   │               │   (Windows only)    │               │   (Cloud service)   │
           └─────────────────────┘               └─────────────────────┘               └─────────────────────┘
                       │                                      │                                      │
           ┌─────────────────────┐               ┌─────────────────────┐               ┌─────────────────────┐
           │  YubiKey PIV        │               │  Windows Smart Card │               │   Azure Key Vault   │
           │  SafeNet HSM        │               │  TPM 2.0 providers  │               │   Hardware HSM      │
           │  Thales HSM         │               │  3rd party HSMs     │               │                     │
           │  SoftHSM            │               │                     │               │                     │
           └─────────────────────┘               └─────────────────────┘               └─────────────────────┘
```

## 🔧 Prerequisites

### Hardware Requirements

**YubiKey PIV (Recommended)**:
- YubiKey 4/5 series with PIV application
- Private key and certificate loaded in PIV slot (typically 9c for Digital Signature)
- YubiKey PIV Tool installed for YKCS11 library

**Other PKCS#11 Devices**:
- SafeNet eToken or Luna HSMs
- Thales ProtectServer HSMs  
- SoftHSM for testing and development
- Any PKCS#11 v2.20+ compliant device

**Windows CNG Devices**:
- Windows Smart Cards
- TPM 2.0 with appropriate providers
- Third-party HSMs with CNG Key Storage Providers

### Software Requirements

- .NET 8.0 or later
- Pkcs11Interop NuGet package (for PKCS#11 support)
- Platform-specific PKCS#11 libraries:
  - **Linux**: `libykcs11.so` (YubiKey), `libsofthsm2.so` (SoftHSM)
  - **Windows**: `ykcs11.dll` (YubiKey, requires PATH configuration)
  - **macOS**: `libykcs11.dylib` (YubiKey)

## 🚀 Quick Start

### 1. Install Prerequisites

**YubiKey Setup**:
```bash
# Install YubiKey PIV Tool (provides YKCS11 library)
# Linux/macOS: Use package manager (apt, brew, etc.)
# Windows: Download from Yubico website

# Generate a key and certificate on YubiKey PIV slot 9c
ykman piv keys generate 9c public.pem
ykman piv certificates generate 9c public.pem --subject "CN=TUF Test Key"
```

**SoftHSM Setup** (for testing):
```bash
# Install SoftHSM
sudo apt install softhsm2  # Linux
brew install softhsm       # macOS

# Initialize token
softhsm2-util --init-token --slot 0 --label "TUF Test Token" --pin 1234 --so-pin 0000
```

### 2. List Available Providers

```bash
dotnet run -- list-providers
```

This displays all hardware signing providers available on your platform, including their capabilities and requirements.

### 3. Test Connection

**YubiKey**:
```bash
dotnet run -- test-connection --key-id "9c" --pin YOUR_PIN
```

**SoftHSM**:
```bash
dotnet run -- test-connection --key-id "testkey" --pin 1234 --library /usr/lib/softhsm/libsofthsm2.so
```

### 4. Perform Test Signing

```bash
dotnet run -- sign --key-id "9c" --pin YOUR_PIN --data "Hello, TUF!"
```

For YubiKey with touch requirement, the device will blink and require physical touch to complete signing.

### 5. Get Device Information

```bash
dotnet run -- device-info --key-id "9c" --pin YOUR_PIN
```

## 📋 Command Reference

### `list-providers`

Lists all available hardware signing providers on the current platform.

```bash
dotnet run -- list-providers
```

**Output includes**:
- Provider availability status
- Supported TUF signature algorithms
- Required NuGet packages
- Platform compatibility information

### `test-connection`

Tests connectivity and authentication to a hardware device.

```bash
dotnet run -- test-connection [options]
```

**Options**:
- `--key-id <id>` (required): Key ID or label on hardware device
- `--pin <pin>`: PIN for device authentication (will prompt if not provided)
- `--library <path>`: Path to PKCS#11 library (auto-detected if not specified)
- `--slot <number>`: PKCS#11 slot ID (auto-detected if not specified)

**Examples**:
```bash
# YubiKey PIV slot 9c
dotnet run -- test-connection --key-id "9c"

# SoftHSM with custom library path
dotnet run -- test-connection --key-id "testkey" --pin 1234 --library /usr/lib/softhsm/libsofthsm2.so

# Specific PKCS#11 slot
dotnet run -- test-connection --key-id "mykey" --slot 1
```

### `sign`

Performs a test signing operation with the hardware device.

```bash
dotnet run -- sign [options]
```

**Options**:
- `--key-id <id>` (required): Key ID or label on hardware device
- `--pin <pin>`: PIN for device authentication (will prompt if not provided)
- `--library <path>`: Path to PKCS#11 library
- `--slot <number>`: PKCS#11 slot ID
- `--data <text>`: Data to sign (default: "Hello, TUF Hardware Signing!")

**Examples**:
```bash
# Basic signing with YubiKey
dotnet run -- sign --key-id "9c" --data "My TUF data"

# Signing with custom PKCS#11 library
dotnet run -- sign --key-id "testkey" --pin 1234 --library /usr/local/lib/libpkcs11.so
```

### `device-info`

Retrieves detailed information about the hardware device and key.

```bash
dotnet run -- device-info [options]
```

**Options**: Same as `test-connection`

**Output includes**:
- Device manufacturer, model, and serial number
- Firmware and hardware versions
- Security certifications (FIPS, Common Criteria)
- Supported cryptographic algorithms
- User presence requirements

## 🔐 Security Considerations

### PIN Management

**⚠️ CRITICAL SECURITY PRACTICES**:

1. **Never hardcode PINs** in source code or configuration files
2. **Use environment variables** or secure configuration management:
   ```bash
   export YUBIKEY_PIN="123456"
   dotnet run -- sign --key-id "9c" --pin $YUBIKEY_PIN
   ```
3. **Enable interactive PIN prompts** for air-gapped signing scenarios
4. **Rotate PINs regularly** according to organizational policy
5. **Use strong PINs** with sufficient entropy

### Air-Gapped Signing Workflow

For maximum security, especially for TUF root keys:

```bash
# 1. Prepare signing data on networked machine
echo "TUF metadata to sign" > metadata.json

# 2. Transfer to air-gapped machine with hardware device
# (USB, optical media, etc.)

# 3. Perform signing on air-gapped machine
dotnet run -- sign --key-id "9c" --data "$(cat metadata.json)"

# 4. Transfer signature back to networked machine
# 5. Incorporate signature into TUF metadata
```

### Device Selection Guidelines

**YubiKey PIV** (Recommended for most use cases):
- ✅ Easy to use and deploy
- ✅ FIPS 140-2 Level 2 certified
- ✅ Touch-based user presence confirmation
- ✅ Cross-platform support
- ❌ Limited to 4 PIV key slots

**Enterprise HSMs** (For high-volume production):
- ✅ High-performance signing operations
- ✅ Large number of key slots
- ✅ Network-based access
- ✅ Enterprise management features
- ❌ Higher cost and complexity
- ❌ Requires specialized knowledge

**SoftHSM** (For testing only):
- ✅ Easy to set up and test with
- ✅ No hardware requirements
- ❌ **NOT SECURE** - software-based key storage
- ❌ Should never be used in production

## 🧪 Integration with TUF Workflows

### Repository Builder Integration

```csharp
// Example: Using hardware signer with TUF Repository Builder
var config = new HardwareSigningConfig
{
    Provider = HardwareSigningProvider.Pkcs11,
    KeyId = "9c", // YubiKey PIV Digital Signature slot
    Pin = Environment.GetEnvironmentVariable("YUBIKEY_PIN"),
    RequireUserPresence = true // Require touch for signing
};

using var hardwareSigner = await Pkcs11Signer.CreateAsync(config);

var repository = new RepositoryBuilder()
    .SetRootSigner(hardwareSigner) // Use hardware signer for root metadata
    .SetTargetsDirectory("./targets")
    .SetMetadataDirectory("./metadata")
    .Build();
```

### Multi-Role Signing

```csharp
// Different keys for different TUF roles
var rootConfig = new HardwareSigningConfig { KeyId = "9c" }; // Digital Signature slot
var timestampConfig = new HardwareSigningConfig { KeyId = "9d" }; // Key Management slot

using var rootSigner = await Pkcs11Signer.CreateAsync(rootConfig);
using var timestampSigner = await Pkcs11Signer.CreateAsync(timestampConfig);

// Root metadata signed with maximum security (air-gapped)
var rootMetadata = repository.CreateRootMetadata(rootSigner);

// Timestamp metadata signed frequently (online)
var timestampMetadata = repository.CreateTimestampMetadata(timestampSigner);
```

### Threshold Signatures

```csharp
// Multiple hardware devices for threshold signatures
var signers = new List<ISigner>();

for (int i = 0; i < 3; i++)
{
    var config = new HardwareSigningConfig { KeyId = $"key{i}" };
    signers.Add(await Pkcs11Signer.CreateAsync(config));
}

// Sign with threshold of 2-of-3 hardware devices
var metadata = repository.CreateRootMetadata(signers, threshold: 2);
```

## 🛠️ Troubleshooting

### Common Issues

**"No PKCS#11 slots with tokens found"**:
- Ensure hardware device is connected and recognized
- Check that PKCS#11 library is installed and accessible
- Verify library path is correct for your platform
- Try listing available slots: `pkcs11-tool --list-slots`

**"Failed to authenticate with PIN"**:
- Verify PIN is correct for your device
- Check if device is locked due to too many incorrect attempts
- For YubiKey: Use `ykman piv info` to check PIN retry counter

**"Key not found with identifier"**:
- List available keys: `pkcs11-tool --list-objects --type privkey`
- For YubiKey PIV: Use hex slot IDs ("9c", "9d", "9e", "9a")
- Check that key exists and has signing capability

**Library loading errors**:
- **Linux**: Ensure library permissions allow reading
- **Windows**: Add Yubico PIV Tool\\bin to system PATH
- **macOS**: Check library is not quarantined by Gatekeeper

### Debug Mode

Enable detailed logging for troubleshooting:

```bash
export DOTNET_LOGGING__LOGLEVEL__DEFAULT=Debug
dotnet run -- test-connection --key-id "9c"
```

### PKCS#11 Library Paths

**YubiKey default paths**:
- Linux: `/usr/local/lib/libykcs11.so`
- Windows: `ykcs11.dll` (requires PATH configuration)
- macOS: `/usr/local/lib/libykcs11.dylib`

**SoftHSM default paths**:
- Linux: `/usr/lib/softhsm/libsofthsm2.so`
- Windows: `C:\\SoftHSM2\\lib\\softhsm2.dll`
- macOS: `/usr/local/lib/softhsm/libsofthsm2.so`

## 📚 Additional Resources

### YubiKey PIV Resources
- [YubiKey PIV Tool Documentation](https://developers.yubico.com/yubico-piv-tool/)
- [PIV Application Guide](https://developers.yubico.com/PIV/)
- [YKCS11 PKCS#11 Module](https://developers.yubico.com/yubico-piv-tool/YKCS11/)

### PKCS#11 Resources
- [PKCS#11 Cryptographic Token Interface Standard](https://docs.oasis-open.org/pkcs11/pkcs11-base/v2.40/)
- [Pkcs11Interop Library Documentation](https://github.com/Pkcs11Interop/Pkcs11Interop)
- [PKCS#11 Best Practices](https://github.com/Pkcs11Interop/Pkcs11Interop/blob/master/doc/PKCS11-INTEROP.md)

### TUF Security Resources
- [TUF Specification](https://theupdateframework.io/docs/)
- [TUF Security Best Practices](https://theupdateframework.io/docs/1.0.0/security/)
- [Hardware Security Module Best Practices](https://www.nist.gov/publications/security-requirements-cryptographic-modules)

## 🎯 Production Deployment Checklist

Before deploying hardware signing in production:

- [ ] **Security Review**: Conduct thorough security review of key management procedures
- [ ] **PIN Policy**: Establish strong PIN policies and rotation procedures
- [ ] **Backup Strategy**: Implement secure key backup and recovery procedures
- [ ] **Access Control**: Limit physical and logical access to hardware devices
- [ ] **Audit Logging**: Enable and monitor all signing operations
- [ ] **Incident Response**: Prepare procedures for device loss or compromise
- [ ] **Testing**: Thoroughly test signing workflows under load
- [ ] **Documentation**: Document all procedures and emergency contacts
- [ ] **Compliance**: Verify compliance with organizational and regulatory requirements
- [ ] **Monitoring**: Implement monitoring for device health and availability

---

This example demonstrates the foundation for enterprise-grade TUF deployments using hardware security modules. The flexible architecture allows organizations to choose the hardware platform that best meets their security, performance, and operational requirements.