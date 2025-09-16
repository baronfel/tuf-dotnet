# Azure Key Vault Integration

TUF .NET provides enterprise-grade Azure Key Vault integration for secure key management and signing operations. This integration enables organizations to leverage Azure's Hardware Security Module (HSM) backed key storage while maintaining full TUF specification compliance.

## Features

- **Hardware Security Module (HSM) Support**: Keys stored in Azure Key Vault Premium tier are backed by FIPS 140-2 Level 2 validated HSMs
- **Multiple Authentication Methods**: Support for managed identity, Azure CLI, Visual Studio, and service principal authentication
- **Enterprise RBAC Integration**: Fine-grained access control using Azure Role-Based Access Control
- **Audit Logging**: Complete audit trail of all cryptographic operations through Azure Monitor
- **High Availability**: Built-in redundancy and disaster recovery capabilities
- **TUF Compliance**: Full compliance with TUF specification requirements

## Supported Key Types

### RSA Keys
- **Minimum Size**: 2048 bits (TUF requirement)
- **Recommended Size**: 3072+ bits for new deployments
- **Scheme**: `rsassa-pss-sha256` (RSA-PSS with SHA-256)
- **Azure Key Vault Types**: RSA-2048, RSA-3072, RSA-4096
- **Protection**: Software or HSM-backed

### ECDSA Keys
- **Curve**: NIST P-256 (secp256r1) only
- **Scheme**: `ecdsa-sha2-nistp256` (ECDSA with SHA-256)
- **Azure Key Vault Type**: EC-P256
- **Protection**: Software or HSM-backed

## Quick Start

### 1. Prerequisites

#### Azure Setup
- Azure subscription with Key Vault service access
- Key Vault created with appropriate access policies
- RSA or ECDSA keys created in Key Vault
- Proper RBAC permissions assigned

#### Authentication
Choose one authentication method:
- **Azure CLI**: `az login` (development)
- **Managed Identity**: Enabled on Azure resources (production)
- **Visual Studio**: Signed in with Azure account (development)
- **Service Principal**: Environment variables set (CI/CD)

### 2. Installation

The Azure Key Vault integration is included in the main TUF package:

```xml
<PackageReference Include="TUF" Version="1.0.0" />
```

Required dependencies are automatically included:
- `Azure.Identity` - Azure authentication
- `Azure.Security.KeyVault.Keys` - Key Vault operations

### 3. Basic Usage

```csharp
using TUF.Models;

// Create signer with default authentication (tries multiple methods)
var keyVaultUrl = new Uri("https://myvault.vault.azure.net/");
var keyName = "tuf-signing-key";

await using var signer = await AzureKeyVaultSigner.CreateAsync(keyVaultUrl, keyName);

// Use signer like any other TUF signer
var message = "TUF metadata to sign"u8.ToArray();
var signature = signer.SignBytes(message);

// Verify signature
bool isValid = signer.Key.VerifySignature(signature.Sig, message);
Console.WriteLine($"Signature valid: {isValid}");
```

### 4. Authentication Methods

#### Default Credential Chain
```csharp
var signer = await AzureKeyVaultSignerFactory.CreateWithDefaultCredentialAsync(
    keyVaultUrl, keyName);
```

#### Managed Identity (Production)
```csharp
var signer = await AzureKeyVaultSignerFactory.CreateWithManagedIdentityAsync(
    keyVaultUrl, keyName);
```

#### Azure CLI (Development)
```csharp
var signer = await AzureKeyVaultSignerFactory.CreateWithCliCredentialAsync(
    keyVaultUrl, keyName);
```

#### Visual Studio (Development)
```csharp
var signer = await AzureKeyVaultSignerFactory.CreateWithVisualStudioCredentialAsync(
    keyVaultUrl, keyName);
```

### 5. Performance Optimization

#### Async Operations
```csharp
// Preferred for performance-critical scenarios
var signature = await signer.SignBytesAsync(data, cancellationToken);
```

#### Configuration
```csharp
var options = new AzureKeyVaultSignerOptions
{
    KeyVaultUrl = keyVaultUrl,
    KeyName = keyName,
    Credential = new ManagedIdentityCredential(),
    Timeout = TimeSpan.FromSeconds(30)
};
```

## Enterprise Integration Patterns

### ASP.NET Core Integration

```csharp
// In Program.cs or Startup.cs
builder.Services.AddSingleton<ISigner>(async serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var keyVaultUrl = new Uri(config["AzureKeyVault:VaultUrl"]!);
    var keyName = config["AzureKeyVault:KeyName"]!;
    
    return await AzureKeyVaultSignerFactory
        .CreateWithDefaultCredentialAsync(keyVaultUrl, keyName);
});

// Use in controllers
[ApiController]
public class SigningController : ControllerBase
{
    private readonly ISigner _signer;
    
    public SigningController(ISigner signer) => _signer = signer;
    
    [HttpPost("sign")]
    public async Task<IActionResult> SignData([FromBody] SignRequest request)
    {
        var signature = _signer.SignBytes(request.Data);
        return Ok(new { signature.KeyId, signature.Sig });
    }
}
```

### Azure Functions Integration

```csharp
[FunctionName("SignMetadata")]
public static async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
    ILogger log)
{
    var keyVaultUrl = new Uri(Environment.GetEnvironmentVariable("KEY_VAULT_URL")!);
    var keyName = Environment.GetEnvironmentVariable("KEY_NAME")!;
    
    await using var signer = await AzureKeyVaultSignerFactory
        .CreateWithManagedIdentityAsync(keyVaultUrl, keyName);
        
    // Process TUF signing operation
    var data = await req.GetRawBodyBytesAsync();
    var signature = await signer.SignBytesAsync(data);
    
    return new OkObjectResult(signature);
}
```

### Configuration Management

#### appsettings.json
```json
{
  "AzureKeyVault": {
    "VaultUrl": "https://prod-vault.vault.azure.net/",
    "KeyName": "tuf-root-key",
    "AuthMethod": "managed-identity",
    "Timeout": "00:00:30"
  }
}
```

#### Environment Variables
```bash
# For service principal authentication
export AZURE_CLIENT_ID="your-client-id"
export AZURE_CLIENT_SECRET="your-client-secret"  
export AZURE_TENANT_ID="your-tenant-id"

# Application settings
export KEY_VAULT_URL="https://vault.vault.azure.net/"
export KEY_NAME="signing-key"
```

## Security Best Practices

### Key Management
- Use **HSM-backed keys** for production (`--protection hsm`)
- Implement **key rotation policies** (e.g., annual rotation)
- Use **separate keys** for different environments
- Consider **separate Key Vaults** per environment for isolation

### Access Control
- Use **Azure RBAC** instead of access policies where possible
- Grant **minimum required permissions** (Key Vault Crypto User for signing)
- Use **managed identity** in production environments
- **Never store credentials** in application code or configuration

### Network Security
- Use **private endpoints** for Key Vault access in production
- Implement **firewall rules** to restrict network access
- Consider **VNet integration** for enhanced security
- Enable **diagnostic logging** for security monitoring

### Monitoring and Compliance
- Enable **Azure Monitor** integration for Key Vault
- Set up **alerts** for unusual key usage patterns
- Implement **automated key rotation** where possible
- Regular **access reviews** and audit log analysis

## Performance Considerations

### Network Latency
- Azure Key Vault operations involve **network round-trips**
- Consider **caching public keys** locally to reduce API calls
- Use **async methods** (`SignBytesAsync`) when possible
- Implement **retry policies** for transient network failures

### Rate Limits
Azure Key Vault has service limits per tier:

| Tier | Limit | Burst Capacity |
|------|-------|----------------|
| Standard | 2,000 requests/10s/vault/region | 5,000 requests |
| Premium | 4,000 requests/10s/vault/region | 10,000 requests |

### Cost Optimization
- **HSM operations** cost more than software operations
- **Batch operations** when possible to reduce API calls
- Consider **key caching strategies** to minimize vault access
- Monitor **billing** for cost optimization opportunities

## Troubleshooting

### Authentication Issues

#### `AuthenticationFailedException`
**Cause**: Authentication credentials not configured or invalid

**Solutions**:
```bash
# Verify Azure CLI login
az account show

# Test Key Vault access
az keyvault key show --vault-name "your-vault" --name "your-key"

# Check managed identity assignment
az role assignment list --assignee <identity-object-id>
```

#### `Forbidden` (403) Error
**Cause**: Insufficient permissions

**Solutions**:
- Verify RBAC role assignment: `Key Vault Crypto User` minimum
- Check access policy if using legacy access policies
- Ensure correct scope assignment (Key Vault level)

### Key Type Issues

#### `Unsupported key type`
**Cause**: Key type not supported by TUF

**Solutions**:
- Use **RSA keys** (2048+ bits) or **ECDSA P-256** keys only
- Recreate key with correct type and size
- Verify key specifications in Azure portal

#### `Key size below minimum`
**Cause**: RSA key smaller than 2048 bits

**Solutions**:
```bash
# Create new RSA key with correct size
az keyvault key create \
  --vault-name "your-vault" \
  --name "new-tuf-key" \
  --kty RSA \
  --size 3072 \
  --protection hsm
```

### Network Connectivity

#### Connection Timeout
**Cause**: Network connectivity issues or firewall blocking

**Solutions**:
```bash
# Test connectivity
nslookup your-vault.vault.azure.net

# Check firewall rules in Azure portal
# Verify VNet configuration if using private endpoints
```

## Migration Guide

### From Local Keys to Azure Key Vault

1. **Create matching keys** in Azure Key Vault:
```bash
# For existing RSA keys
az keyvault key import \
  --vault-name "your-vault" \
  --name "imported-tuf-key" \
  --pem-file "existing-key.pem" \
  --protection hsm
```

2. **Update application code**:
```csharp
// Before: Local RSA signer
var localSigner = RsaSigner.FromPem(pemContent);

// After: Azure Key Vault signer
var azureSigner = await AzureKeyVaultSignerFactory
    .CreateWithDefaultCredentialAsync(vaultUrl, keyName);
```

3. **Verify signature compatibility**:
```csharp
// Both signers should produce verifiable signatures
var testData = "test message"u8.ToArray();
var localSig = localSigner.SignBytes(testData);
var azureSig = await azureSigner.SignBytesAsync(testData);

// Cross-verification (should work if same key material)
var localValid = localSigner.Key.VerifySignature(azureSig.Sig, testData);
```

### From Other Cloud Providers

The Azure Key Vault integration follows the same `ISigner` interface as other signers, making migration straightforward:

```csharp
// Replace existing cloud provider signer
ISigner signer = await AzureKeyVaultSignerFactory
    .CreateWithDefaultCredentialAsync(vaultUrl, keyName);
    
// All existing TUF code continues to work unchanged
var signature = signer.SignBytes(data);
```

## Examples

See the [AzureKeyVaultDemo](examples/AzureKeyVaultDemo/README.md) project for a complete working example including:

- Multiple authentication methods
- Error handling and retry logic
- Performance optimization techniques
- Integration patterns for different scenarios

## API Reference

### AzureKeyVaultSigner

Primary class for Azure Key Vault signing operations.

#### Methods

```csharp
public static async Task<AzureKeyVaultSigner> CreateAsync(
    Uri keyVaultUrl, 
    string keyName, 
    TokenCredential? credential = null)

public SignatureObject SignBytes(ReadOnlySpan<byte> data)

public async Task<SignatureObject> SignBytesAsync(
    ReadOnlyMemory<byte> data, 
    CancellationToken cancellationToken = default)
```

### AzureKeyVaultSignerFactory

Factory methods for common authentication scenarios.

#### Methods

```csharp
public static async Task<AzureKeyVaultSigner> CreateWithDefaultCredentialAsync(
    Uri keyVaultUrl, string keyName)

public static async Task<AzureKeyVaultSigner> CreateWithManagedIdentityAsync(
    Uri keyVaultUrl, string keyName, string? clientId = null)

public static async Task<AzureKeyVaultSigner> CreateWithCliCredentialAsync(
    Uri keyVaultUrl, string keyName)

public static async Task<AzureKeyVaultSigner> CreateWithVisualStudioCredentialAsync(
    Uri keyVaultUrl, string keyName)
```

### AzureKeyVaultSignerOptions

Configuration options for Azure Key Vault signers.

#### Properties

```csharp
public required Uri KeyVaultUrl { get; init; }
public required string KeyName { get; init; }
public TokenCredential? Credential { get; init; }
public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
```

## Related Documentation

- [TUF Specification](https://theupdateframework.io/specification/)
- [Azure Key Vault Documentation](https://docs.microsoft.com/azure/key-vault/)
- [Azure Identity Library](https://docs.microsoft.com/dotnet/api/azure.identity)
- [Azure RBAC Documentation](https://docs.microsoft.com/azure/role-based-access-control/)
- [SigningDemo Example](examples/SigningDemo/README.md) - Local signing comparison