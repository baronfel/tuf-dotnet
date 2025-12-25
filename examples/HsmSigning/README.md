# TUF HSM Signing Examples

This example demonstrates how to use Hardware Security Modules (HSMs) for TUF signing operations, providing enhanced security through hardware-backed key protection.

## Overview

The TUF .NET implementation now supports three types of HSM integration:

1. **YubiKey PIV** - Popular USB security keys with PIV slots
2. **PKCS#11 Devices** - Universal HSM interface (smart cards, network HSMs)
3. **Windows CNG** - Windows-integrated HSMs and smart cards

## Quick Start

### YubiKey PIV

```bash
# Sign with YubiKey PIV slot 9C (Digital Signature - requires touch)
dotnet run yubikey --slot 9C --pin 123456 --data "Hello TUF!"

# Sign with slot 9A (Authentication - no touch required)
dotnet run yubikey --slot 9A --pin 123456 --data "Hello TUF!"
```

### PKCS#11 Device

```bash
# Sign with generic PKCS#11 device
dotnet run pkcs11 \
  --library "/usr/local/lib/libykcs11.so" \
  --slot-id 0 \
  --key-label "TUF Key" \
  --key-type "rsa" \
  --pin 123456 \
  --data "Hello TUF!"
```

### Windows CNG Certificate

```bash
# Sign with Windows certificate (by thumbprint)
dotnet run windows-cng --thumbprint ABC123... --data "Hello TUF!"

# Sign with Windows certificate (by subject)
dotnet run windows-cng --subject "CN=My TUF Key" --data "Hello TUF!"
```

### Compare Signers

```bash
# Compare software vs HSM signing capabilities
dotnet run compare
```

## HSM Signer Types

### YubiKey PIV (`YubiKeySigner`)

**Best for:** Root key signing with user presence verification

- ✅ Hardware key protection (keys never leave the device)
- ✅ PIN authentication
- ✅ User presence verification (slot 9C requires touch)
- ✅ Air-gapped signing capability
- ✅ FIPS 140-2 Level 2 certified (FIPS models)
- 📋 Supports: RSA 1024/2048, ECDSA P-256/P-384

**PIV Slot Recommendations:**
- **Slot 9C (Digital Signature)**: Root keys - requires user touch for each signature
- **Slot 9A (Authentication)**: Automated signing - targets, timestamp, snapshot
- **Slot 9E (Card Authentication)**: Alternative for automated signing

### PKCS#11 (`Pkcs11Signer`)

**Best for:** Enterprise HSMs and network-attached security devices

- ✅ Universal HSM compatibility
- ✅ Network-attached HSM support
- ✅ Enterprise-grade security and audit logging
- ✅ High-performance cryptographic operations
- 📋 Device-dependent supported algorithms

**Compatible devices:**
- Thales nShield
- SafeNet Luna
- AWS CloudHSM (via client)
- YubiKey (via YKCS11)
- Smart cards

### Windows CNG (`WindowsCngSigner`)

**Best for:** Windows enterprise environments with existing PKI

- ✅ Native Windows integration
- ✅ Smart card and TPM support
- ✅ Certificate-based authentication
- ✅ Enterprise PKI integration
- 📋 Supports: RSA, ECDSA

## Security Recommendations

### Root Key Protection

For maximum security, root keys should use YubiKey PIV slot 9C:

```csharp
// Root key signing - requires user presence
var rootSigner = await YubiKeySigner.CreateAsync(PivSlot.DigitalSignature, pin: "123456");

// Each signature requires touching the YubiKey
var rootSignature = await rootSigner.SignBytesAsync(rootMetadataBytes);
```

### Automated Role Keys

For automated roles (targets, timestamp, snapshot), use:

```csharp
// Option 1: YubiKey slot 9A (no touch required)
var targetsSigner = await YubiKeySigner.CreateAsync(PivSlot.Authentication, pin: "123456");

// Option 2: Windows CNG certificate
var timestampSigner = await WindowsCngSigner.CreateFromSubjectAsync("CN=TUF Timestamp Key");

// Option 3: Network HSM via PKCS#11
var snapshotSigner = await Pkcs11Signer.CreateAsync(
    "/path/to/hsm/library.so", 
    slotId: 0, 
    keyLabel: "TUF Snapshot Key", 
    keyType: "rsa"
);
```

### Air-Gapped Workflows

HSM signers enable secure air-gapped signing:

1. Transfer unsigned metadata to air-gapped machine with HSM
2. Sign using HSM signer
3. Transfer signed metadata back to online systems
4. HSM private keys never leave the secure environment

## Installation Requirements

### YubiKey PIV

**Windows:**
- Download [YubiKey PIV Tool](https://developers.yubico.com/yubico-piv-tool/Releases/)
- YKCS11 library will be in `C:\Program Files\Yubico\Yubico PIV Tool\bin\`

**macOS:**
```bash
brew install yubico-piv-tool
# Library: /usr/local/lib/libykcs11.dylib or /opt/homebrew/lib/libykcs11.dylib
```

**Linux:**
```bash
sudo apt install yubico-piv-tool
# Library: /usr/local/lib/libykcs11.so or /usr/lib/libykcs11.so
```

### PKCS#11 Devices

Install device-specific PKCS#11 libraries:
- **Thales nShield**: Install Security World Software
- **SafeNet Luna**: Install Luna Client
- **AWS CloudHSM**: Install CloudHSM client
- **Smart Cards**: Install card reader drivers and middleware

### Windows CNG

No additional installation required on Windows. Works with:
- Smart card readers and certificates
- TPM-backed certificates
- USB security tokens with Windows drivers

## Error Troubleshooting

### YubiKey Errors

**"YKCS11 library not found"**
```bash
# Verify YubiKey PIV Tool installation
# Windows: Check C:\Program Files\Yubico\Yubico PIV Tool\bin\libykcs11.dll
# macOS: Check /usr/local/lib/libykcs11.dylib
# Linux: Check /usr/local/lib/libykcs11.so
```

**"PIN authentication failed"**
```bash
# Check PIN - default is "123456" for new YubiKeys
# PIN may be changed during initialization
ykman piv info  # Shows PIN retry counter
```

**"Key pair not found"**
```bash
# Generate or import keys to PIV slots first
ykman piv keys generate 9c key.pem  # Generate in slot 9C
ykman piv certificates import 9c cert.pem  # Import certificate
```

### PKCS#11 Errors

**"Library not found"**
```bash
# Verify PKCS#11 library path
ldd /path/to/pkcs11.so  # Check dependencies on Linux
```

**"Slot not found or no token present"**
```bash
# List available slots
pkcs11-tool --list-slots
```

### Windows CNG Errors

**"Certificate not found"**
```bash
# List certificates in Windows store
certlm.msc  # Local machine certificates
certmgr.msc  # Current user certificates
```

**"Certificate must have private key"**
- Certificate must be installed with private key
- Private key must be accessible to current user/process

## Integration with TUF Workflows

### Repository Creation

```csharp
// Create repository with HSM-backed root key
var rootSigner = await YubiKeySigner.CreateAsync(PivSlot.DigitalSignature, "123456");

var repository = new RepositoryBuilder()
    .WithRootKey(rootSigner.Key)
    .WithTargetsKey(targetsSigner.Key)
    .WithTimestampKey(timestampSigner.Key)
    .WithSnapshotKey(snapshotSigner.Key)
    .Build();

// Sign root metadata with HSM
var rootMetadata = repository.CreateRootMetadata();
var rootSignature = rootSigner.SignBytes(CanonicalJson.Serialize(rootMetadata));
rootMetadata.Signatures.Add(rootSignature);
```

### Metadata Signing

```csharp
// Sign targets metadata
var targetsMetadata = repository.CreateTargetsMetadata(targetFiles);
var targetsBytes = CanonicalJson.Serialize(targetsMetadata);
var targetsSignature = await targetsSigner.SignBytesAsync(targetsBytes);
targetsMetadata.Signatures.Add(targetsSignature);
```

## Performance Considerations

Software signers typically provide higher throughput:
- Ed25519: ~10,000 signatures/second
- RSA-2048: ~500 signatures/second  
- ECDSA-P256: ~2,000 signatures/second

HSM signers prioritize security over performance:
- YubiKey PIV: ~50-100 signatures/second
- Network HSM: ~100-1000 signatures/second (depends on network latency)
- Smart Cards: ~10-50 signatures/second

**Recommendation:** Use HSM signers for high-value keys (root, targets) and software signers for high-frequency operations (timestamp) when appropriate for your security model.

## Next Steps

- Review [TUF specification](https://theupdateframework.io/) for role-based key management
- See [examples/BasicClient/](../BasicClient/) for complete TUF client workflow
- Check [examples/SigningDemo/](../SigningDemo/) for software signing examples
- Read [Security Best Practices](../../docs/security.md) for production deployments