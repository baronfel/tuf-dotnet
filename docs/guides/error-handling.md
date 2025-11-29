# TUF .NET Error Handling Guide

This guide provides comprehensive information on handling errors in TUF .NET applications, including exception types, best practices, and actionable troubleshooting steps.

## Table of Contents

- [Exception Hierarchy](#exception-hierarchy)
- [Exception Types and Handling](#exception-types-and-handling)
- [Error Handling Best Practices](#error-handling-best-practices)
- [Logging and Diagnostics](#logging-and-diagnostics)
- [Recovery Strategies](#recovery-strategies)
- [Production Considerations](#production-considerations)

## Exception Hierarchy

All TUF .NET exceptions inherit from the base `TufException` class, which provides a clear separation from other application exceptions:

```csharp
try
{
    await updater.RefreshAsync();
}
catch (TufException tufEx)
{
    // Handle all TUF-specific errors
    await HandleTufError(tufEx);
}
catch (Exception generalEx)
{
    // Handle other errors (network, I/O, etc.)
    await HandleGeneralError(generalEx);
}
```

## Exception Types and Handling

### Configuration and Setup Errors

#### TufConfigurationException
**Cause**: Invalid TUF client configuration
**When it occurs**: During `Updater` initialization
**Properties**: `ConfigurationProperty`

```csharp
try
{
    var updater = new Updater(config);
}
catch (TufConfigurationException ex)
{
    Console.WriteLine($"Configuration error in {ex.ConfigurationProperty}: {ex.Message}");
    
    // Common fixes:
    if (ex.ConfigurationProperty == "LocalTrustedRoot")
    {
        Console.WriteLine("Ensure trusted root metadata file exists and is valid JSON");
    }
    else if (ex.ConfigurationProperty == "RemoteMetadataUrl")
    {
        Console.WriteLine("Verify the metadata URL is accessible and uses HTTPS");
    }
}
```

#### RepositoryNetworkException
**Cause**: Network connectivity issues with TUF repository
**When it occurs**: During metadata refresh or target downloads
**Properties**: `RepositoryUri`, `StatusCode`

```csharp
try
{
    await updater.RefreshAsync();
}
catch (RepositoryNetworkException ex)
{
    var action = ex.StatusCode switch
    {
        HttpStatusCode.NotFound => "Repository URL may be incorrect",
        HttpStatusCode.Unauthorized => "Check authentication credentials",
        HttpStatusCode.Forbidden => "Verify repository access permissions", 
        HttpStatusCode.ServiceUnavailable => "Repository temporarily unavailable, implement retry logic",
        null => "Network connectivity issue - check internet connection",
        _ => $"HTTP {(int)ex.StatusCode} error - check repository status"
    };
    
    Console.WriteLine($"Repository error: {action}");
    Console.WriteLine($"Repository: {ex.RepositoryUri}");
    
    // Implement retry with exponential backoff
    if (ShouldRetry(ex.StatusCode))
    {
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        // Retry logic here
    }
}
```

### Metadata Processing Errors

#### MetadataDeserializationException
**Cause**: Corrupted or invalid JSON metadata
**When it occurs**: During metadata parsing
**Properties**: `MetadataType`, `RawMetadata`

```csharp
try
{
    await updater.RefreshAsync();
}
catch (MetadataDeserializationException ex)
{
    Console.WriteLine($"Failed to parse {ex.MetadataType} metadata: {ex.Message}");
    
    // Diagnostic information
    if (ex.RawMetadata != null)
    {
        File.WriteAllText($"corrupted-{ex.MetadataType}-{DateTime.Now:yyyyMMdd-HHmmss}.json", ex.RawMetadata);
        Console.WriteLine("Corrupted metadata saved for analysis");
    }
    
    // Recovery: Clear local metadata cache and retry
    ClearMetadataCache();
    Console.WriteLine("Cleared metadata cache - retrying with fresh download");
}
```

#### MetadataValidationException  
**Cause**: Metadata structure violates TUF specification
**When it occurs**: After successful deserialization, during validation
**Properties**: `MetadataType`, `ValidationRule`

```csharp
try
{
    await updater.RefreshAsync();
}
catch (MetadataValidationException ex)
{
    Console.WriteLine($"Metadata validation failed: {ex.ValidationRule}");
    Console.WriteLine($"Metadata type: {ex.MetadataType}");
    
    // Common validation failures and solutions
    var guidance = ex.ValidationRule switch
    {
        "version_mismatch" => "Repository metadata may be out of sync - contact repository administrator",
        "missing_required_field" => "Metadata is incomplete or corrupted",
        "invalid_expiry_date" => "Metadata expiry date is malformed",
        _ => "Contact repository administrator for assistance"
    };
    
    Console.WriteLine($"Suggested action: {guidance}");
}
```

#### ExpiredMetadataException
**Cause**: TUF metadata has exceeded its validity period
**When it occurs**: During metadata validation
**Properties**: `MetadataType`, `ExpiryTime`

```csharp
try
{
    await updater.RefreshAsync();
}
catch (ExpiredMetadataException ex)
{
    var timeExpired = DateTime.UtcNow - ex.ExpiryTime;
    Console.WriteLine($"{ex.MetadataType} metadata expired {timeExpired?.TotalHours:F1} hours ago");
    
    if (timeExpired?.TotalDays < 1)
    {
        Console.WriteLine("Recently expired - repository may be updating soon");
        // Implement short retry delay
        await Task.Delay(TimeSpan.FromMinutes(5));
    }
    else
    {
        Console.WriteLine("Metadata significantly expired - contact repository administrator");
        // Don't proceed - this could indicate a security issue
        throw;
    }
}
```

### Security and Cryptographic Errors

#### SignatureVerificationException
**Cause**: Cryptographic signature validation failed
**When it occurs**: During metadata signature verification
**Properties**: `KeyId`, `MetadataType`
**Security Impact**: HIGH - indicates potential security compromise

```csharp
try
{
    await updater.RefreshAsync();
}
catch (SignatureVerificationException ex)
{
    // SECURITY ALERT - Do not ignore these exceptions
    Console.WriteLine("⚠️  SECURITY ALERT: Signature verification failed");
    Console.WriteLine($"Metadata type: {ex.MetadataType}");
    Console.WriteLine($"Key ID: {ex.KeyId}");
    
    // Log security event for analysis
    securityLogger.LogCritical("TUF signature verification failure", new 
    {
        MetadataType = ex.MetadataType,
        KeyId = ex.KeyId,
        Message = ex.Message,
        Timestamp = DateTime.UtcNow
    });
    
    // DO NOT CONTINUE - this could indicate:
    // 1. Compromised repository
    // 2. Man-in-the-middle attack
    // 3. Corrupted metadata transmission
    
    throw; // Re-throw - do not suppress security exceptions
}
```

#### InsufficientSignaturesException
**Cause**: Not enough valid signatures to meet threshold requirements
**When it occurs**: During metadata signature verification
**Properties**: `RequiredSignatures`, `ValidSignatures`, `RoleName`
**Security Impact**: HIGH

```csharp
try
{
    await updater.RefreshAsync();
}
catch (InsufficientSignaturesException ex)
{
    Console.WriteLine("⚠️  SECURITY ALERT: Insufficient signatures");
    Console.WriteLine($"Role: {ex.RoleName}");
    Console.WriteLine($"Required: {ex.RequiredSignatures}, Valid: {ex.ValidSignatures}");
    
    // This indicates:
    // 1. Some keys may have been compromised
    // 2. Repository signing process is incomplete
    // 3. Threshold configuration mismatch
    
    securityLogger.LogCritical("Insufficient TUF signatures", new 
    {
        Role = ex.RoleName,
        RequiredSignatures = ex.RequiredSignatures,
        ValidSignatures = ex.ValidSignatures
    });
    
    throw; // Do not proceed without sufficient signatures
}
```

#### RollbackAttackException
**Cause**: Metadata version numbers indicate a rollback attack
**When it occurs**: During metadata validation
**Properties**: `MetadataType`, `ExpectedVersion`, `ActualVersion`
**Security Impact**: CRITICAL - indicates active attack

```csharp
try
{
    await updater.RefreshAsync();
}
catch (RollbackAttackException ex)
{
    // CRITICAL SECURITY ALERT - Potential active attack
    Console.WriteLine("🚨 CRITICAL SECURITY ALERT: Rollback attack detected");
    Console.WriteLine($"Metadata: {ex.MetadataType}");
    Console.WriteLine($"Expected version: {ex.ExpectedVersion}");
    Console.WriteLine($"Received version: {ex.ActualVersion}");
    
    // Log incident immediately
    securityLogger.LogCritical("TUF rollback attack detected", new 
    {
        MetadataType = ex.MetadataType,
        ExpectedVersion = ex.ExpectedVersion,
        ActualVersion = ex.ActualVersion,
        ClientIP = GetClientIP(),
        RepositoryUrl = config.RemoteMetadataUrl,
        Timestamp = DateTime.UtcNow
    });
    
    // Alert security team
    await NotifySecurityTeam(ex);
    
    // STOP ALL TUF OPERATIONS - do not continue
    throw;
}
```

### Target File Errors

#### TargetNotFoundException
**Cause**: Requested target file not found in metadata
**When it occurs**: During target lookup
**Properties**: `TargetPath`

```csharp
try
{
    var targetInfo = await updater.GetTargetInfo("my-app.exe");
}
catch (TargetNotFoundException ex)
{
    Console.WriteLine($"Target not found: {ex.TargetPath}");
    
    // Suggest alternatives
    var availableTargets = await updater.GetAvailableTargets();
    if (availableTargets.Any())
    {
        Console.WriteLine("Available targets:");
        foreach (var target in availableTargets.Take(10))
        {
            Console.WriteLine($"  - {target}");
        }
        
        // Suggest similar names
        var similar = availableTargets
            .Where(t => LevenshteinDistance(t, ex.TargetPath!) < 3)
            .Take(5);
        if (similar.Any())
        {
            Console.WriteLine("Did you mean:");
            foreach (var suggestion in similar)
            {
                Console.WriteLine($"  - {suggestion}");
            }
        }
    }
}
```

#### TargetIntegrityException
**Cause**: Downloaded file hash doesn't match expected value
**When it occurs**: During target file verification
**Properties**: `TargetPath`, `ExpectedHash`, `ActualHash`
**Security Impact**: HIGH - indicates file corruption or tampering

```csharp
try
{
    var (localPath, data) = await updater.DownloadTarget(targetFile, "my-app.exe");
}
catch (TargetIntegrityException ex)
{
    Console.WriteLine("⚠️  SECURITY ALERT: File integrity verification failed");
    Console.WriteLine($"File: {ex.TargetPath}");
    Console.WriteLine($"Expected hash: {ex.ExpectedHash}");
    Console.WriteLine($"Actual hash: {ex.ActualHash}");
    
    // Security logging
    securityLogger.LogWarning("TUF target integrity failure", new 
    {
        TargetPath = ex.TargetPath,
        ExpectedHash = ex.ExpectedHash,
        ActualHash = ex.ActualHash
    });
    
    // Clean up corrupted file
    if (File.Exists(localPath))
    {
        File.Delete(localPath);
        Console.WriteLine("Corrupted file removed");
    }
    
    // Retry download once
    if (!isRetry)
    {
        Console.WriteLine("Retrying download...");
        return await RetryDownloadTarget(targetFile, targetName, isRetry: true);
    }
    
    throw; // Multiple failures indicate serious issue
}
```

#### FileSizeException
**Cause**: File exceeds configured size limits
**When it occurs**: During download or processing
**Properties**: `FilePath`, `ActualSize`, `MaxAllowedSize`

```csharp
try
{
    var (localPath, data) = await updater.DownloadTarget(targetFile, "my-app.exe");
}
catch (FileSizeException ex)
{
    var actualSizeMB = ex.ActualSize / (1024.0 * 1024.0);
    var maxSizeMB = ex.MaxAllowedSize / (1024.0 * 1024.0);
    
    Console.WriteLine($"File too large: {actualSizeMB:F1} MB exceeds limit of {maxSizeMB:F1} MB");
    
    // Offer user choice
    Console.WriteLine("Options:");
    Console.WriteLine("1. Increase size limit (if appropriate)");
    Console.WriteLine("2. Download a different version");
    Console.WriteLine("3. Stream processing instead of loading into memory");
    
    // Allow administrator override
    if (userSettings.AllowLargeFiles)
    {
        config.MaxFileSize = ex.ActualSize + (10 * 1024 * 1024); // Add 10MB buffer
        Console.WriteLine("Size limit temporarily increased");
    }
}
```

### Delegation Errors

#### DelegationException & MaxDelegationDepthExceededException
**Cause**: Issues with TUF delegation resolution
**When it occurs**: During delegation traversal
**Properties**: `RoleName`, `DelegationDepth`, `MaxDepth`

```csharp
try
{
    var targetInfo = await updater.GetTargetInfo("my-app.exe");
}
catch (MaxDelegationDepthExceededException ex)
{
    Console.WriteLine($"Maximum delegation depth {ex.MaxDepth} exceeded");
    Console.WriteLine($"Current depth: {ex.DelegationDepth}");
    
    // This might indicate:
    // 1. Circular delegation chains
    // 2. Misconfigured repository delegations
    // 3. Need to increase max depth limit
    
    // Diagnostic: show delegation chain
    await LogDelegationChain(ex.RoleName);
}
catch (DelegationException ex)
{
    Console.WriteLine($"Delegation error in role '{ex.RoleName}': {ex.Message}");
    
    // Recovery: try direct target lookup if delegation fails
    try
    {
        var directTarget = await updater.GetTargetInfoDirect("my-app.exe");
        if (directTarget.HasValue)
        {
            Console.WriteLine("Found target through direct lookup");
            return directTarget.Value;
        }
    }
    catch
    {
        Console.WriteLine("Direct lookup also failed - target may not exist");
    }
}
```

## Error Handling Best Practices

### 1. Layered Exception Handling

```csharp
public async Task<UpdateResult> SafeUpdateAsync(string targetName)
{
    try
    {
        return await PerformUpdate(targetName);
    }
    catch (RollbackAttackException ex)
    {
        // Security exceptions - never suppress
        await HandleSecurityIncident(ex);
        throw;
    }
    catch (SignatureVerificationException ex)
    {
        await HandleSecurityIncident(ex);
        throw;
    }
    catch (InsufficientSignaturesException ex)
    {
        await HandleSecurityIncident(ex);
        throw;
    }
    catch (TargetIntegrityException ex)
    {
        // File integrity - retry once, then fail
        if (!isRetry)
        {
            return await SafeUpdateAsync(targetName, isRetry: true);
        }
        await HandleSecurityIncident(ex);
        throw;
    }
    catch (ExpiredMetadataException ex)
    {
        // Recoverable - retry with backoff
        return await RetryWithBackoff(() => PerformUpdate(targetName), ex);
    }
    catch (RepositoryNetworkException ex)
    {
        // Network issues - retry with backoff
        return await RetryWithBackoff(() => PerformUpdate(targetName), ex);
    }
    catch (TufException ex)
    {
        // Other TUF exceptions - log and fail gracefully
        logger.LogError(ex, "TUF operation failed");
        return UpdateResult.Failed(ex.Message);
    }
}
```

### 2. Retry Logic with Exponential Backoff

```csharp
public async Task<T> RetryWithBackoff<T>(
    Func<Task<T>> operation, 
    Exception originalException,
    int maxRetries = 3)
{
    for (int retry = 1; retry <= maxRetries; retry++)
    {
        try
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, retry));
            await Task.Delay(delay);
            return await operation();
        }
        catch (Exception ex) when (retry < maxRetries && IsRetryableException(ex))
        {
            logger.LogWarning($"Retry {retry}/{maxRetries} failed: {ex.Message}");
        }
    }
    
    throw originalException; // Give up and throw original exception
}

private bool IsRetryableException(Exception ex)
{
    return ex is RepositoryNetworkException ||
           ex is ExpiredMetadataException ||
           ex is MetadataDeserializationException;
}
```

### 3. Security Exception Handling

```csharp
private async Task HandleSecurityIncident(TufException ex)
{
    // Log security incident
    var incident = new SecurityIncident
    {
        Type = ex.GetType().Name,
        Message = ex.Message,
        Timestamp = DateTime.UtcNow,
        Details = ExtractExceptionDetails(ex),
        Severity = GetSecuritySeverity(ex)
    };
    
    await securityLogger.LogIncidentAsync(incident);
    
    // Notify security team for critical issues
    if (incident.Severity == SecuritySeverity.Critical)
    {
        await notificationService.NotifySecurityTeamAsync(incident);
    }
    
    // Clear caches to prevent using potentially compromised data
    await ClearAllCaches();
}
```

## Logging and Diagnostics

### Structured Logging

```csharp
// Configure logging in Program.cs or Startup.cs
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddJsonConsole(); // For structured logs
    logging.SetMinimumLevel(LogLevel.Information);
});

// In your TUF client code
public class TufClientService
{
    private readonly ILogger<TufClientService> logger;
    
    public TufClientService(ILogger<TufClientService> logger)
    {
        this.logger = logger;
    }
    
    public async Task UpdateAsync(string targetName)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "TufUpdate",
            ["Target"] = targetName,
            ["CorrelationId"] = Guid.NewGuid()
        });
        
        try
        {
            logger.LogInformation("Starting TUF update for {Target}", targetName);
            
            await updater.RefreshAsync();
            logger.LogInformation("Metadata refreshed successfully");
            
            var (localPath, data) = await updater.DownloadTarget(targetFile, targetName);
            logger.LogInformation("Target downloaded and verified: {LocalPath}", localPath);
        }
        catch (TufException ex)
        {
            logger.LogError(ex, "TUF update failed for {Target}", targetName);
            throw;
        }
    }
}
```

### Custom Diagnostic Information

```csharp
public static class TufDiagnostics
{
    public static async Task<DiagnosticReport> GenerateReportAsync(Updater updater)
    {
        var report = new DiagnosticReport();
        
        try
        {
            // Configuration validation
            report.Configuration = ValidateConfiguration(updater.Config);
            
            // Metadata status
            report.MetadataStatus = await CheckMetadataStatus(updater);
            
            // Network connectivity
            report.NetworkStatus = await CheckNetworkConnectivity(updater.Config);
            
            // Local cache status
            report.CacheStatus = CheckLocalCache(updater.Config);
            
            return report;
        }
        catch (Exception ex)
        {
            report.Errors.Add($"Diagnostic generation failed: {ex.Message}");
            return report;
        }
    }
}
```

## Recovery Strategies

### Metadata Cache Management

```csharp
public static class CacheManager
{
    public static async Task ClearMetadataCache(UpdaterConfig config)
    {
        if (Directory.Exists(config.LocalMetadataDir))
        {
            Directory.Delete(config.LocalMetadataDir, recursive: true);
            Directory.CreateDirectory(config.LocalMetadataDir);
        }
    }
    
    public static async Task<bool> ValidateMetadataCache(UpdaterConfig config)
    {
        try
        {
            var updater = new Updater(config);
            await updater.RefreshAsync();
            return true;
        }
        catch (TufException)
        {
            return false;
        }
    }
}
```

### Configuration Validation

```csharp
public static class ConfigValidator
{
    public static ValidationResult ValidateConfiguration(UpdaterConfig config)
    {
        var result = new ValidationResult();
        
        if (config.LocalTrustedRoot == null || config.LocalTrustedRoot.Length == 0)
        {
            result.Errors.Add("LocalTrustedRoot is required");
        }
        
        if (config.RemoteMetadataUrl == null)
        {
            result.Errors.Add("RemoteMetadataUrl is required");
        }
        else if (config.RemoteMetadataUrl.Scheme != "https")
        {
            result.Warnings.Add("RemoteMetadataUrl should use HTTPS for security");
        }
        
        if (string.IsNullOrEmpty(config.LocalMetadataDir))
        {
            result.Errors.Add("LocalMetadataDir is required");
        }
        
        return result;
    }
}
```

## Production Considerations

### 1. Security Monitoring

```csharp
// Set up security alerts for critical TUF exceptions
services.Configure<AlertingOptions>(options =>
{
    options.CriticalExceptions = new[]
    {
        typeof(RollbackAttackException),
        typeof(SignatureVerificationException),
        typeof(InsufficientSignaturesException)
    };
    options.AlertChannels = new[] { "email", "slack", "pagerduty" };
});
```

### 2. Health Checks

```csharp
// Add TUF health checks
builder.Services.AddHealthChecks()
    .AddCheck<TufHealthCheck>("tuf")
    .AddCheck<TufRepositoryHealthCheck>("tuf-repository");

public class TufHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var updater = serviceProvider.GetRequiredService<Updater>();
            await updater.RefreshAsync();
            return HealthCheckResult.Healthy("TUF metadata refresh successful");
        }
        catch (TufException ex)
        {
            return HealthCheckResult.Unhealthy($"TUF health check failed: {ex.Message}", ex);
        }
    }
}
```

### 3. Metrics and Telemetry

```csharp
// Custom metrics for TUF operations
public class TufMetrics
{
    private readonly IMetricsLogger metricsLogger;
    
    public void RecordRefreshSuccess(TimeSpan duration)
    {
        metricsLogger.LogMetric("tuf.refresh.success", 1);
        metricsLogger.LogMetric("tuf.refresh.duration_ms", duration.TotalMilliseconds);
    }
    
    public void RecordSecurityException(string exceptionType)
    {
        metricsLogger.LogMetric("tuf.security.exception", 1, new Dictionary<string, string>
        {
            ["exception_type"] = exceptionType
        });
    }
}
```

## Related Documentation

- [Troubleshooting Guide](./troubleshooting.md) - Common problems and solutions
- [Core Concepts](./core-concepts.md) - Understanding TUF fundamentals
- [Security Model](../security/security-model.md) - TUF security guarantees
- [API Reference](../api/README.md) - Complete API documentation

This error handling guide should be used in conjunction with comprehensive logging, monitoring, and incident response procedures appropriate for your security requirements.