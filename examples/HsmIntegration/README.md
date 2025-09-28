# TUF .NET HSM Integration Example

This example demonstrates enterprise-grade Hardware Security Module (HSM) integration with TUF .NET for secure cryptographic signing operations. It shows how to integrate both cloud-based (Azure Key Vault) and on-premises (PKCS#11) HSM solutions for production TUF deployments.

## Overview

Hardware Security Modules provide the highest level of cryptographic security by ensuring private keys never leave tamper-resistant hardware. This example addresses a key parity gap by providing comprehensive HSM integration patterns that enterprises require for production TUF deployments.

### Key Features

- **Azure Key Vault HSM Integration**: Complete implementation using Azure's FIPS 140-2 Level 3 certified HSMs
- **PKCS#11 Standard Support**: Demonstration of industry-standard HSM interface integration  
- **Enterprise Security**: Hardware-backed private keys with comprehensive audit logging
- **Production Patterns**: Real-world deployment guidance and security best practices
- **TUF Compliance**: Full integration with existing TUF .NET signing infrastructure

## Supported HSM Types

### Azure Key Vault HSM
- **Premium Key Vault**: FIPS 140-2 Level 2 HSM-backed keys
- **Managed HSM**: Dedicated FIPS 140-2 Level 3 HSM instances
- **Supported Algorithms**: RSA-PSS (2048-4096 bit), ECDSA P-256
- **Authentication**: Azure AD integration with RBAC
- **Global Availability**: Multi-region deployment support

### PKCS#11 Compatible HSMs
- **Network HSMs**: SafeNet Luna SA, Thales ProtectServer, Entrust nShield
- **PCIe Cards**: SafeNet Luna PCIe, Thales nShield Solo
- **USB Tokens**: SafeNet eToken, YubiKey HSM
- **Cloud HSMs**: AWS CloudHSM, IBM Cloud HSM
- **Standards**: PKCS#11 v2.40, FIPS 140-2 Level 2/3

## Security Benefits

### Hardware Security
- ✅ **Private keys never leave HSM**: All signing operations performed in hardware
- ✅ **Tamper resistance**: Physical attacks trigger key zeroization
- ✅ **Hardware RNG**: Cryptographically secure random number generation
- ✅ **Side-channel protection**: Resistance to timing and power analysis attacks

### Access Control
- ✅ **Role-based authentication**: Multi-level access controls (Crypto Officer, User)
- ✅ **Multi-person authorization**: Quorum controls for critical operations
- ✅ **Session management**: Automatic timeout and secure session handling
- ✅ **Comprehensive auditing**: All operations logged for compliance

### Operational Security
- ✅ **High availability**: HSM clustering and failover support
- ✅ **Disaster recovery**: Secure key backup and restore procedures  
- ✅ **Key lifecycle**: Hardware-based key generation, rotation, and destruction
- ✅ **Performance**: Optimized cryptographic operations at hardware speeds

## Quick Start

### Prerequisites

**For Azure Key Vault HSM:**
- Azure subscription with Key Vault Premium or Managed HSM
- Azure CLI or PowerShell for setup
- Appropriate IAM permissions (Key Vault Crypto Officer/User)

**For PKCS#11 HSM:**
- PKCS#11 compatible HSM hardware
- Vendor-provided PKCS#11 library
- HSM initialization and token setup
- Authentication credentials (PIN/certificate)

### Installation

```bash
# Clone repository
git clone https://github.com/baronfel/tuf-dotnet.git
cd tuf-dotnet/examples/HsmIntegration

# Restore dependencies  
dotnet restore

# Build example
dotnet build
```

### Basic Usage

```bash
# Display help
dotnet run -- --help

# Azure Key Vault HSM examples
dotnet run -- azure info --vault-uri "https://myvault.vault.azure.net/" --key-name "tuf-root-key"
dotnet run -- azure sign --vault-uri "https://myvault.vault.azure.net/" --key-name "tuf-root-key" --data "Hello TUF"

# PKCS#11 HSM information  
dotnet run -- pkcs11 info
dotnet run -- pkcs11 guide

# TUF signing demonstration
dotnet run -- tuf-demo create-root

# Security and compliance information
dotnet run -- security
```

## Azure Key Vault Setup

### 1. Create Premium Key Vault

```bash
# Create resource group
az group create --name tuf-hsm-rg --location eastus

# Create Premium Key Vault (HSM-backed keys)
az keyvault create \
  --name "tuf-hsm-vault-$(date +%s)" \
  --resource-group tuf-hsm-rg \
  --location eastus \
  --sku premium \
  --enable-rbac-authorization

# Or create Managed HSM (dedicated HSM instance)  
az keyvault create \
  --hsm-name "tuf-managed-hsm-$(date +%s)" \
  --resource-group tuf-hsm-rg \
  --location eastus \
  --administrators user@domain.com
```

### 2. Create HSM-backed Signing Key

```bash
# RSA-PSS key for TUF signing
az keyvault key create \
  --vault-name "your-vault-name" \
  --name "tuf-root-key" \
  --kty RSA-HSM \
  --size 3072 \
  --ops sign verify

# Or ECDSA P-256 key
az keyvault key create \
  --vault-name "your-vault-name" \
  --name "tuf-timestamp-key" \
  --kty EC-HSM \
  --curve P-256 \
  --ops sign verify
```

### 3. Configure Access Permissions

```bash
# Assign Key Vault Crypto User role
az role assignment create \
  --role "Key Vault Crypto User" \
  --assignee user@domain.com \
  --scope "/subscriptions/subscription-id/resourceGroups/tuf-hsm-rg/providers/Microsoft.KeyVault/vaults/vault-name"
```

## PKCS#11 HSM Setup

### 1. Install HSM Software

```bash
# Example for SafeNet Luna SA (adjust for your HSM vendor)
# Install Luna Client software from Thales
# Configure network connectivity to HSM appliance

# Initialize HSM partition
lunacm
hsm init -label "TUF-HSM-Partition"
```

### 2. Create Signing Keys

```bash
# Connect to HSM partition
slot set -slot 1
role login -name po

# Generate RSA-PSS key for TUF root signing
keypair generate -keyType RSA -keySize 3072 -label "tuf-root-key"

# Generate ECDSA key for timestamp signing  
keypair generate -keyType EC -curve secp256r1 -label "tuf-timestamp-key"
```

### 3. Production Integration

Add PKCS#11 library integration to your project:

```xml
<!-- Add to .csproj for production PKCS#11 support -->
<PackageReference Include="Pkcs11Interop" Version="5.1.2" />
```

## TUF Integration Examples

### Root Metadata Signing with HSM

```csharp
// Azure Key Vault HSM signer
var hsmSigner = await AzureKeyVaultSigner.CreateAsync(
    new Uri("https://myvault.vault.azure.net/"), 
    "tuf-root-key", 
    logger);

// Create TUF root metadata
var rootMetadata = new Root
{
    Version = 1,
    SpecVersion = "1.0.0", 
    Expires = DateTime.UtcNow.AddYears(1),
    Keys = { [hsmSigner.Key.GetKeyId()] = hsmSigner.Key },
    Roles = new Dictionary<string, Role>
    {
        ["root"] = new Role { KeyIds = [hsmSigner.Key.GetKeyId()], Threshold = 1 }
    }
};

// Sign with HSM
var metadata = new Metadata<Root> { Signed = rootMetadata };
var canonicalBytes = CanonicalJson.Serialize(rootMetadata);
var signature = hsmSigner.SignBytes(canonicalBytes);
metadata.Signatures = [signature];

// Serialize signed metadata
var signedJson = CanonicalJson.Serialize(metadata);
await File.WriteAllTextAsync("root.json", signedJson);
```

### Multi-Role HSM Deployment

```csharp
// Offline root HSM (air-gapped)
var rootSigner = await CreateOfflineHsmSigner("offline-hsm", "root-key");

// Online HSMs for operational roles
var timestampSigner = await AzureKeyVaultSigner.CreateAsync(vaultUri, "timestamp-key");  
var snapshotSigner = await AzureKeyVaultSigner.CreateAsync(vaultUri, "snapshot-key");
var targetsSigner = await AzureKeyVaultSigner.CreateAsync(vaultUri, "targets-key");

// Create complete TUF repository with HSM-backed keys
var repository = new RepositoryBuilder()
    .WithRootSigner(rootSigner)      // Offline HSM
    .WithTimestampSigner(timestampSigner)  // Online HSM
    .WithSnapshotSigner(snapshotSigner)    // Online HSM  
    .WithTargetsSigner(targetsSigner)      // Online HSM
    .Build();
```

## Production Deployment Architecture

### High-Level Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Offline HSM   │    │  Online HSM      │    │   TUF Clients   │
│   (Root Keys)   │    │  Cluster         │    │  (Validation)   │
│                 │    │                  │    │                 │
│ • Air-gapped    │───▶│ • Timestamp      │───▶│ • Public keys   │
│ • FIPS 140-2 L3 │    │ • Snapshot       │    │ • Signature     │
│ • Multi-auth    │    │ • Targets        │    │   verification  │
│ • Audit logging │    │ • Load balanced  │    │ • Update checks │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### Security Zones

- **Air-Gapped Zone**: Root key HSM with no network connectivity
- **Controlled Zone**: Online HSM cluster with restricted network access  
- **Public Zone**: Repository distribution and client access
- **Management Zone**: HSM administration and monitoring systems

### Operational Procedures

#### Key Generation
1. Generate root keys in offline HSM (air-gapped environment)
2. Generate operational keys in online HSM cluster
3. Export public keys for TUF metadata distribution
4. Secure backup of HSM configuration and authentication data

#### Signing Workflow
1. Prepare metadata in controlled environment
2. Transfer to appropriate HSM for signing
3. Validate signature using public key verification
4. Distribute signed metadata through secure channels

#### Key Rotation
1. Generate new keys in HSM using hardware RNG  
2. Create new TUF metadata with updated key information
3. Sign metadata transition using existing valid keys
4. Distribute updated metadata to all clients
5. Monitor client adoption before decommissioning old keys

#### Incident Response
1. Detect and isolate compromised systems
2. Assess extent of key material exposure
3. Execute emergency key rotation procedures  
4. Revoke compromised keys and update metadata
5. Coordinate client notification and updates

## Compliance and Certifications

### FIPS 140-2 Validation

This example supports HSMs with various FIPS 140-2 validation levels:

- **Level 2**: Tamper-evident seals, role-based authentication (minimum for production)
- **Level 3**: Identity-based authentication, tamper resistance (recommended for critical systems)
- **Level 4**: Tamper-responsive with environmental failure protection (maximum security)

### Industry Standards

- **PKCS#11 v2.40**: Industry-standard cryptographic token interface
- **RFC 3447**: RSA-PSS signature algorithm specification  
- **RFC 6979**: Deterministic ECDSA (where supported by HSM)
- **FIPS 186-4**: Digital Signature Standard compliance
- **Common Criteria EAL4+**: International security evaluation standard

### Audit and Compliance

All HSM operations generate comprehensive audit logs including:

- Key generation and import/export operations
- All signing operations with data fingerprints
- Authentication and authorization events
- Administrative configuration changes
- System health and availability metrics

These logs support compliance with:
- **SOX**: Sarbanes-Oxley financial reporting requirements
- **PCI DSS**: Payment card industry data security standards  
- **FedRAMP**: Federal risk and authorization management program
- **ISO 27001**: Information security management systems
- **NIST Cybersecurity Framework**: Comprehensive security controls

## Troubleshooting

### Azure Key Vault Issues

**Authentication Failures:**
```bash
# Check Azure CLI authentication
az account show

# Verify Key Vault permissions
az role assignment list --scope /subscriptions/.../resourceGroups/.../providers/Microsoft.KeyVault/vaults/...
```

**Network Connectivity:**
```bash
# Test Key Vault connectivity
nslookup your-vault.vault.azure.net
```

**Key Access Issues:**
- Ensure key has `sign` and `verify` operations enabled
- Verify user has "Key Vault Crypto User" or "Key Vault Crypto Officer" role
- Check Key Vault firewall and virtual network settings

### PKCS#11 Issues

**Library Loading:**
- Verify PKCS#11 library path is correct for your platform
- Check library architecture matches .NET runtime (x64/ARM64)
- Ensure HSM vendor software is properly installed

**Token Access:**
- Verify HSM is initialized and partition is created
- Check authentication credentials (PIN/password) 
- Confirm network connectivity for network-attached HSMs

**Key Operations:**
- Ensure keys are generated with appropriate usage flags
- Verify mechanism support (CKM_RSA_PKCS_PSS, CKM_ECDSA_SHA256)
- Check HSM capacity and performance limitations

## Performance Considerations

### Azure Key Vault HSM
- **Throughput**: ~2,000 RSA operations/second, ~5,000 ECDSA operations/second
- **Latency**: ~50-100ms per operation (includes network overhead)
- **Scaling**: Automatic load balancing across HSM cluster
- **Caching**: Consider caching signatures for identical data

### PKCS#11 HSM
- **Throughput**: Varies by hardware (1,000-10,000+ operations/second)
- **Latency**: <10ms for local operations (network HSMs higher)  
- **Scaling**: HSM clustering and load balancing required
- **Session Management**: Optimize PKCS#11 session reuse

## Advanced Topics

### HSM Clustering and High Availability

```csharp
// Load balancing across multiple HSM instances
public class HsmClusterSigner : ISigner
{
    private readonly ISigner[] _signers;
    private readonly ILoadBalancer _loadBalancer;
    
    public SignatureObject SignBytes(ReadOnlySpan<byte> data)
    {
        var signer = _loadBalancer.SelectSigner(_signers);
        return signer.SignBytes(data);
    }
}
```

### Key Backup and Recovery

Azure Key Vault provides automatic backup and geo-replication. For PKCS#11 HSMs:

```bash
# Backup HSM partition (SafeNet Luna example)
partition backup -par tuf-partition -file tuf-backup.bak

# Restore from backup
partition restore -par tuf-partition -file tuf-backup.bak
```

### Monitoring and Alerting

```csharp
// HSM health monitoring
public class HsmMonitor
{
    public async Task<HealthStatus> CheckHsmHealth(ISigner signer)
    {
        try
        {
            // Test signature operation
            var testData = "health-check"u8;
            var signature = signer.SignBytes(testData);
            return HealthStatus.Healthy;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HSM health check failed");
            return HealthStatus.Unhealthy;
        }
    }
}
```

## Contributing

This HSM integration example demonstrates enterprise-grade security patterns for TUF .NET. Contributions are welcome:

- Additional HSM vendor integrations
- Production deployment guides  
- Performance optimization examples
- Compliance and audit tooling
- Integration with popular .NET security frameworks

## Resources

### Documentation
- [TUF Specification](https://theupdateframework.io/specification/)
- [Azure Key Vault HSM](https://docs.microsoft.com/azure/key-vault/managed-hsm/)
- [PKCS#11 Cryptographic Token Interface Standard](http://docs.oasis-open.org/pkcs11/pkcs11-base/v2.40/)

### HSM Vendors
- [Thales Luna SA](https://cpl.thalesgroup.com/encryption/hardware-security-modules/network-attached-hsms/luna-network-hsm)
- [Entrust nShield](https://www.entrust.com/hsm)
- [AWS CloudHSM](https://aws.amazon.com/cloudhsm/)
- [Utimaco CryptoServer](https://utimaco.com/products/categories/hardware-security-modules)

### Security Standards  
- [FIPS 140-2](https://csrc.nist.gov/publications/detail/fips/140/2/final)
- [Common Criteria](https://commoncriteriaportal.org/)
- [ISO 15408](https://www.iso.org/standard/50341.html)

---

This example represents a significant step toward enterprise parity for TUF .NET, providing the hardware-backed security capabilities that production deployments require.