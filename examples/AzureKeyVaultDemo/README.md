# Azure Key Vault TUF Signing Demo

This example demonstrates how to integrate Azure Key Vault with TUF for enterprise-grade secure signing operations. Azure Key Vault provides hardware security module (HSM) backed key storage, centralized key management, and comprehensive audit logging.

## Features Demonstrated

- **Secure Key Storage**: Keys never leave Azure Key Vault HSM environment
- **Multiple Authentication Methods**: Support for various Azure authentication scenarios
- **Enterprise Integration**: RBAC, audit logging, and compliance features
- **TUF Compliance**: Full TUF specification compliance with Azure-backed keys
- **Performance Optimization**: Async operations and efficient key usage

## Prerequisites

### Azure Requirements

1. **Azure Subscription**: Active Azure subscription with Key Vault access
2. **Azure Key Vault**: Created Key Vault with appropriate access policies
3. **Keys**: RSA (2048+ bits) or ECDSA (P-256) keys created in Key Vault
4. **Permissions**: Key Vault Crypto User role (minimum) for signing operations

### Authentication Setup

Choose one of the following authentication methods:

#### Option 1: Azure CLI (Development)
```bash
az login
az account set --subscription "your-subscription-id"
```

#### Option 2: Managed Identity (Production)
- Enable managed identity on your Azure resource (VM, App Service, etc.)
- Assign Key Vault Crypto User role to the managed identity
- No additional setup required in application

#### Option 3: Visual Studio (Development)
- Sign in to Visual Studio with Azure account
- Ensure account has Key Vault access

#### Option 4: Service Principal (CI/CD)
Set environment variables:
```bash
export AZURE_CLIENT_ID="your-client-id"
export AZURE_CLIENT_SECRET="your-client-secret"  
export AZURE_TENANT_ID="your-tenant-id"
```

## Key Vault Setup

### 1. Create Key Vault
```bash
az keyvault create \
  --name "your-keyvault-name" \
  --resource-group "your-resource-group" \
  --location "East US" \
  --sku premium  # Premium required for HSM-backed keys
```

### 2. Create Signing Keys

#### RSA Key (Recommended for compatibility)
```bash
az keyvault key create \
  --vault-name "your-keyvault-name" \
  --name "tuf-signing-key" \
  --kty RSA \
  --size 3072 \
  --protection hsm  # HSM-backed for highest security
```

#### ECDSA Key (Smaller signatures, modern)
```bash
az keyvault key create \
  --vault-name "your-keyvault-name" \
  --name "tuf-signing-key" \
  --kty EC \
  --curve P-256 \
  --protection hsm
```

### 3. Set Access Policies
```bash
# For user/service principal
az keyvault set-policy \
  --name "your-keyvault-name" \
  --upn "user@domain.com" \
  --key-permissions sign verify

# For managed identity
az keyvault set-policy \
  --name "your-keyvault-name" \
  --object-id "managed-identity-object-id" \
  --key-permissions sign verify
```

## Running the Demo

### Basic Example (Azure CLI Authentication)
```bash
cd examples/AzureKeyVaultDemo
dotnet run -- \
  --key-vault-url "https://your-keyvault.vault.azure.net/" \
  --key-name "tuf-signing-key" \
  --auth-method cli
```

### Production Example (Managed Identity)
```bash
dotnet run -- \
  --key-vault-url "https://prod-keyvault.vault.azure.net/" \
  --key-name "tuf-prod-root-key" \
  --auth-method managed-identity
```

### User-Assigned Managed Identity
```bash
dotnet run -- \
  --key-vault-url "https://keyvault.vault.azure.net/" \
  --key-name "tuf-key" \
  --auth-method managed-identity \
  --client-id "12345678-1234-1234-1234-123456789012"
```

### Custom Message Signing
```bash
dotnet run -- \
  --key-vault-url "https://keyvault.vault.azure.net/" \
  --key-name "tuf-key" \
  --message "Custom message to sign and verify"
```

## Sample Output

```
Azure Key Vault TUF Signing Demo
=====================================
Key Vault URL: https://example-keyvault.vault.azure.net/
Key Name: tuf-signing-key
Authentication Method: cli

Creating Azure Key Vault signer...
Key Information:
  Key Type: rsa
  Scheme: rsassa-pss-sha256
  Key ID: 1234567890abcdef
  Public Key Preview: -----BEGIN RSA PUBLIC KEY-----\nMIIBCgKCAQEA...

Signing message: Sample TUF metadata for signing demonstration
Signature created successfully!
  Key ID: 1234567890abcdef
  Signature: a1b2c3d4e5f6...

Verifying signature...
Signature verification: ✅ VALID

Demonstrating TUF metadata signing...
Signing root metadata...
Root metadata signed successfully!
  Metadata version: 1
  Expires: 2025-09-16T10:30:00Z
  Signatures count: 1
  First signature key ID: 1234567890abcdef

Verifying metadata signature...
Metadata signature verification: ✅ VALID

Demo completed successfully! 🎉
```

## Integration Patterns

### ASP.NET Core Integration

```csharp
// In Startup.cs or Program.cs
services.AddSingleton<ISigner>(async provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var keyVaultUrl = new Uri(config["AzureKeyVault:VaultUrl"]);
    var keyName = config["AzureKeyVault:KeyName"];
    
    return await AzureKeyVaultSignerFactory
        .CreateWithDefaultCredentialAsync(keyVaultUrl, keyName);
});
```

### Azure Functions Integration

```csharp
[FunctionName("SignMetadata")]
public static async Task<IActionResult> Run(
    [HttpTrigger] HttpRequest req,
    ILogger log)
{
    var keyVaultUrl = new Uri(Environment.GetEnvironmentVariable("KEY_VAULT_URL"));
    var keyName = Environment.GetEnvironmentVariable("KEY_NAME");
    
    await using var signer = await AzureKeyVaultSignerFactory
        .CreateWithManagedIdentityAsync(keyVaultUrl, keyName);
        
    // Use signer for TUF operations...
}
```

### Configuration Management

```json
{
  "AzureKeyVault": {
    "VaultUrl": "https://your-keyvault.vault.azure.net/",
    "KeyName": "tuf-signing-key",
    "AuthMethod": "managed-identity",
    "ClientId": null  // For user-assigned managed identity
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Azure": "Warning"
    }
  }
}
```

## Security Best Practices

### Key Management
- Use HSM-backed keys for production (`--protection hsm`)
- Implement key rotation policies
- Use separate keys for different environments (dev/staging/prod)
- Consider using separate Key Vaults per environment

### Access Control
- Use Azure RBAC instead of access policies where possible
- Grant minimum required permissions (Key Vault Crypto User for signing)
- Use managed identity in production environments
- Avoid storing credentials in application code

### Monitoring and Auditing
- Enable Key Vault logging and monitoring
- Set up alerts for key usage anomalies  
- Review access logs regularly
- Implement automated key rotation

### Network Security
- Use private endpoints for Key Vault access
- Restrict network access with firewall rules
- Consider using VNet integration

## Performance Considerations

### Network Latency
- Azure Key Vault operations involve network calls
- Consider caching key metadata (public keys) locally
- Use async methods (`SignBytesAsync`) when possible
- Implement retry policies for transient failures

### Rate Limiting
- Key Vault has service limits per tier
- Standard tier: 2000 requests per 10 seconds per vault per region
- Premium tier: 4000 requests per 10 seconds per vault per region
- Plan signing operations accordingly

### Cost Optimization
- Key operations are charged per request
- HSM-backed keys cost more but provide higher security
- Consider key caching strategies to minimize API calls

## Troubleshooting

### Common Authentication Issues

**Error**: `Azure.Identity.AuthenticationFailedException`
**Solution**: Verify authentication setup:
```bash
# Test Azure CLI login
az account show

# Test Key Vault access
az keyvault key show --vault-name "your-keyvault" --name "your-key"
```

**Error**: `Azure.RequestFailedException: Forbidden`  
**Solution**: Check RBAC permissions:
```bash
az role assignment list --scope "/subscriptions/{subscription-id}/resourceGroups/{rg}/providers/Microsoft.KeyVault/vaults/{vault}"
```

### Key Type Issues

**Error**: `Unsupported key type`
**Solution**: Ensure key type is RSA (2048+ bits) or ECDSA (P-256 curve)

**Error**: `Key size below minimum`
**Solution**: RSA keys must be at least 2048 bits for TUF compliance

### Network Connectivity

**Error**: `Connection timeout`
**Solution**: Check network connectivity and firewall rules:
```bash
# Test connectivity
nslookup your-keyvault.vault.azure.net
```

## Additional Resources

- [Azure Key Vault Documentation](https://docs.microsoft.com/azure/key-vault/)
- [Azure Identity Library](https://docs.microsoft.com/dotnet/api/azure.identity)
- [TUF Specification](https://theupdateframework.io/specification/)
- [Azure Key Vault Security Best Practices](https://docs.microsoft.com/azure/key-vault/general/best-practices)

## Related Examples

- [SigningDemo](../SigningDemo/README.md) - Basic TUF signing without Azure
- [BasicClient](../BasicClient/README.md) - TUF client implementation
- [RepositoryManager](../RepositoryManager/README.md) - Repository management tools