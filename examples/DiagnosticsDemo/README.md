# TUF Diagnostics Demo

This example demonstrates the TUF .NET library's diagnostic and troubleshooting utilities, designed to help developers identify and resolve common TUF configuration and runtime issues.

## What This Example Shows

- **Configuration Validation**: Pre-flight checks to validate UpdaterConfig before use
- **Exception Diagnostics**: Detailed troubleshooting reports for TUF exceptions
- **Metadata Consistency Analysis**: Cross-metadata validation and issue detection
- **Actionable Guidance**: Specific steps and recommendations for resolving issues

## Key Features Demonstrated

### 1. Configuration Validation (`TufDiagnostics.ValidateConfigurationAsync`)

Validates TUF client configuration before initialization:
- Trusted root metadata format and validity
- Repository URL accessibility and security (HTTPS)
- Local directory permissions and existence  
- HTTP client configuration optimization
- Expiration date warnings

### 2. Exception Diagnostic Reports (`TufDiagnostics.GenerateDiagnosticReport`)

Generates comprehensive troubleshooting reports for TUF exceptions:
- **Signature Verification Issues**: Key management and cryptographic problems
- **Rollback Attack Detection**: Security incident response guidance
- **Target Integrity Failures**: File corruption and download issues
- **Network Problems**: Repository accessibility and connectivity
- **Configuration Errors**: Setup and parameter validation issues

### 3. Metadata Consistency Analysis (`TufDiagnostics.AnalyzeMetadataConsistency`)

Cross-validates TUF metadata files for consistency:
- Version number alignment across metadata types
- Expiration date ordering and validity
- Structural validation and format checks
- Reference integrity between metadata files

## Running the Example

```bash
# Build and run the diagnostics demo
cd examples/DiagnosticsDemo
dotnet run
```

## Example Output

```
🔧 TUF Diagnostics Demo - Troubleshooting Utilities
====================================================

📋 Demo 1: Configuration Validation
✅ Valid configuration:
   Is Valid: True
   Issues: 0
   Warnings: 1
   Warnings found:
     • Repository URI uses non-HTTPS scheme - consider security implications

⚠️  Problematic configuration:
   Is Valid: False
   Issues: 2
   Issues found:
     ❌ Trusted root metadata is null or empty
     ❌ Invalid metadata directory path: Access denied
   Recommendations:
     💡 Provide a valid trusted root metadata file from a secure source
     💡 Ensure directory exists or will be created with appropriate permissions

🚨 Demo 2: Exception Diagnostic Reports
=== TUF Diagnostic Report (2025-11-27 09:15:32 UTC) ===
Problem Area: Cryptographic Signature Verification
Exception: SignatureVerificationException
Message: Ed25519 signature verification failed for root metadata

🔍 Troubleshooting Steps:
  • Verify that the correct trusted root metadata is being used
  • Check that key ID 'ed25519-root-key-2024' exists in the trusted key set
  • Ensure metadata has not been corrupted during transport
  • Verify that the signature algorithm is supported (Ed25519, RSA-PSS, ECDSA)
  • Check system time - expired keys may cause verification failures

💡 Suggested Actions:
  → Review key management and rotation procedures
  → Validate metadata using external tools (e.g., TUF CLI)
  → Check repository infrastructure for signing issues
```

## When to Use TUF Diagnostics

### Development Phase
- Validate configuration before deploying TUF clients
- Debug signature and metadata issues during integration
- Ensure proper repository setup and accessibility

### Production Support  
- Generate detailed reports for TUF-related incidents
- Provide troubleshooting guidance to support teams
- Analyze metadata consistency in repository operations

### CI/CD Integration
- Pre-flight validation of TUF configurations
- Automated metadata consistency checking
- Error reporting with actionable guidance

## Integration Examples

### Basic Configuration Validation
```csharp
var config = new UpdaterConfig(trustedRoot, metadataUri)
{
    LocalMetadataDir = "./metadata",
    LocalTargetsDir = "./targets", 
    Client = httpClient
};

var validation = await TufDiagnostics.ValidateConfigurationAsync(config);
if (!validation.IsValid)
{
    foreach (var issue in validation.Issues)
    {
        logger.LogError("TUF Configuration Issue: {Issue}", issue);
    }
    throw new TufConfigurationException("Invalid TUF configuration");
}
```

### Exception Diagnostics in Error Handling
```csharp
try
{
    await updater.RefreshAsync();
}
catch (TufException ex)
{
    var report = TufDiagnostics.GenerateDiagnosticReport(ex);
    logger.LogError("TUF Error Report:\n{Report}", report.ToString());
    
    // Send detailed report to monitoring/support system
    await notificationService.SendTufErrorReportAsync(report);
}
```

### Repository Metadata Validation
```csharp
var metadataFiles = new Dictionary<string, byte[]>
{
    ["root"] = await File.ReadAllBytesAsync("root.json"),
    ["timestamp"] = await File.ReadAllBytesAsync("timestamp.json"),
    ["snapshot"] = await File.ReadAllBytesAsync("snapshot.json"),
    ["targets"] = await File.ReadAllBytesAsync("targets.json")
};

var consistency = TufDiagnostics.AnalyzeMetadataConsistency(metadataFiles);
if (!consistency.IsConsistent)
{
    logger.LogWarning("Metadata consistency issues detected");
    foreach (var issue in consistency.Issues)
    {
        logger.LogError("Consistency Issue: {Issue}", issue);
    }
}
```

## Benefits for TUF Adoption

1. **Reduced Development Time**: Catch configuration issues early
2. **Better Error Messages**: Actionable troubleshooting guidance
3. **Easier Debugging**: Comprehensive diagnostic information  
4. **Improved Support**: Detailed reports for production issues
5. **Enhanced Security**: Validation of security-critical configurations

## Next Steps

- Integrate diagnostic utilities into your TUF client applications
- Use configuration validation in CI/CD pipelines
- Implement comprehensive error handling with diagnostic reports
- Consider automated monitoring of metadata consistency
- Review other examples for complete TUF implementation patterns