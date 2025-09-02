# TUF .NET Error Handling and Telemetry Demo

This example demonstrates the enhanced error handling and telemetry features added to achieve parity with mature TUF implementations in other languages.

## Features Demonstrated

### 🎯 **Specific Exception Types**
Instead of generic `Exception` types, TUF .NET now provides specific exceptions for different error scenarios:

- `TufConfigurationException` - Invalid configuration parameters
- `MetadataDeserializationException` - Failed to parse TUF metadata
- `MetadataValidationException` - Metadata validation failures
- `SignatureVerificationException` - Cryptographic signature failures
- `InsufficientSignaturesException` - Not enough valid signatures for threshold
- `ExpiredMetadataException` - Metadata past expiration time
- `RollbackAttackException` - Version rollback attack detection
- `TargetNotFoundException` - Target file not found
- `TargetIntegrityException` - Target file integrity verification failures
- `DelegationException` - Delegation resolution failures
- `MaxDelegationDepthExceededException` - Delegation depth limit exceeded
- `RepositoryNetworkException` - Network communication failures
- `FileSizeException` - File size limit violations

### 📊 **Structured Logging**
- Rich contextual information with structured log data
- Integration with Microsoft.Extensions.Logging
- Configurable log levels (Debug, Information, Warning, Error)
- Production-ready telemetry support

### 🛡️ **Security Attack Detection**
- Automatic detection and reporting of TUF security attacks:
  - Rollback attacks (version downgrade attempts)
  - Insufficient signature attacks
  - Metadata expiration attacks
  - Target integrity attacks

### ⚙️ **Configuration Validation**
- Comprehensive validation of `UpdaterConfig` parameters
- Clear error messages for misconfigurations
- Prevention of common setup mistakes

## Running the Demo

```bash
cd examples/ErrorHandlingDemo
dotnet run
```

## Example Output

The demo will show:

1. **Configuration Validation**: How invalid configurations are caught early
2. **Metadata Errors**: Structured error reporting for malformed metadata
3. **Exception Hierarchy**: All available TUF-specific exception types
4. **Security Examples**: Detection of rollback attacks and signature failures
5. **Production Categories**: How errors are categorized for monitoring

## Integration with Your Application

To use the enhanced error handling in your TUF client:

```csharp
using Microsoft.Extensions.Logging;
using TUF;
using TUF.Exceptions;

// Create logger
var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("MyApp.TUF");

try 
{
    // Configure TUF updater with logging
    var config = new UpdaterConfig(rootData, repoUrl)
    {
        LocalMetadataDir = "./metadata",
        LocalTargetsDir = "./targets",
        Client = httpClient,
        Logger = logger  // Enable structured logging
    };
    
    var updater = new Updater(config);
    await updater.Refresh();
    
    // Get targets with enhanced error handling
    var targets = updater.GetTopLevelTargets();
}
catch (RollbackAttackException ex)
{
    logger.LogError("Security attack detected: {Attack} on {MetadataType}", 
        "Rollback", ex.MetadataType);
    // Handle security incident
}
catch (RepositoryNetworkException ex)
{
    logger.LogWarning("Repository unavailable: {Repository} ({StatusCode})", 
        ex.RepositoryUri, ex.StatusCode);
    // Handle network failure gracefully
}
catch (TufException ex)
{
    logger.LogError(ex, "TUF operation failed: {Message}", ex.Message);
    // Handle TUF-specific error
}
```

## Monitoring and Alerting

The structured logging enables production monitoring:

- **Error Rate Metrics**: Track `TufException` occurrences
- **Security Alerts**: Monitor for `RollbackAttackException`, `SignatureVerificationException`
- **Performance Metrics**: Track `FileSizeException` for capacity planning
- **Health Checks**: Use specific exception types for service health indicators

## Parity Achievement

This enhancement brings TUF .NET to parity with other mature TUF implementations:

| Feature | Python-TUF | Go-TUF | Rust-TUF | **TUF .NET** |
|---------|------------|--------|----------|--------------|
| Specific Exception Types | ✅ | ✅ | ✅ | ✅ |
| Structured Logging | ✅ | ✅ | ✅ | ✅ |
| Security Attack Detection | ✅ | ✅ | ✅ | ✅ |
| Configuration Validation | ✅ | ✅ | ✅ | ✅ |
| Production Telemetry | ✅ | ✅ | ✅ | ✅ |

The TUF .NET implementation now provides enterprise-grade error handling and observability capabilities comparable to other mature TUF ecosystems.