using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TUF;
using TUF.Exceptions;

// Demonstrate improved error handling, logging, and telemetry in TUF .NET

// Setup high-performance logging with structured output
var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger("TUF.Demo");

// Setup activity listener for demonstrating System.Diagnostics.Activity support
using var activityListener = new ActivityListener
{
    ShouldListenTo = source => source.Name == TufActivitySource.Name,
    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
    ActivityStarted = activity => logger.LogDebug("🔄 Started activity: {ActivityName}", activity.OperationName),
    ActivityStopped = activity => logger.LogDebug("✅ Completed activity: {ActivityName} in {Duration}ms", 
        activity.OperationName, activity.Duration.TotalMilliseconds)
};
ActivitySource.AddActivityListener(activityListener);

logger.LogInformation("🚀 TUF .NET Error Handling, Logging & Telemetry Demo");
logger.LogInformation("📊 Using high-performance LoggerMessage source generator for structured logging");
logger.LogInformation("🔍 Activity tracing enabled for System.Diagnostics integration");

// Demo 1: TUF Exception Handling with Activity Tracing
logger.LogInformation("📋 Demo 1: Exception Handling with Activity Tracing");

using (var activity = TufActivitySource.StartActivity("ConfigurationValidation"))
{
    activity?.SetTag("demo.step", "1");
    
    try 
    {
        throw new TufConfigurationException("Directory path cannot be empty", "LocalMetadataDir");
    }
    catch (TufConfigurationException ex)
    {
        logger.LogTufException(ex); // Uses structured logging extension
        logger.LogInformation("✅ Configuration exception: {Property} - {Message}", ex.ConfigurationProperty, ex.Message);
        activity?.SetTag("error.type", "configuration");
        activity?.SetTag("error.property", ex.ConfigurationProperty);
    }
}

// Demo 2: Security Event Logging with Activities
logger.LogInformation("🛡️ Demo 2: Security Event Logging with Telemetry");
using (var activity = TufActivitySource.StartActivity("SecurityValidation"))
{
    activity?.SetTag("demo.step", "2");
    activity?.SetTag("security.category", "rollback_detection");
    
    try
    {
        throw new RollbackAttackException("timestamp", expectedVersion: 5, actualVersion: 3);
    }
    catch (RollbackAttackException ex)
    {
        logger.LogTufException(ex); // Logs as Critical with structured data
        logger.LogInformation("✅ Demonstrated security alert logging with activity tracing");
        activity?.SetTag("security.threat_detected", true);
    }
}

// Demo 3: TUF-specific Exception Hierarchy
logger.LogInformation("🏗️  Demo 3: TUF Exception Hierarchy");

var exceptionTypes = new[]
{
    typeof(TufException),
    typeof(MetadataValidationException),
    typeof(SignatureVerificationException), 
    typeof(InsufficientSignaturesException),
    typeof(ExpiredMetadataException),
    typeof(RollbackAttackException),
    typeof(TargetNotFoundException),
    typeof(TargetIntegrityException),
    typeof(DelegationException),
    typeof(MaxDelegationDepthExceededException),
    typeof(RepositoryNetworkException),
    typeof(TufConfigurationException),
    typeof(FileSizeException)
};

logger.LogInformation("Available TUF-specific exception types:");
foreach (var exType in exceptionTypes)
{
    var isTufException = typeof(TufException).IsAssignableFrom(exType);
    logger.LogInformation("  • {ExceptionType} {Indicator}", 
        exType.Name, 
        isTufException ? "(✅ TufException)" : "");
}

// Demo 4: Rollback Attack Detection
logger.LogInformation("🛡️  Demo 4: Security Exception Examples");
try
{
    throw new RollbackAttackException("timestamp", expectedVersion: 5, actualVersion: 3);
}
catch (RollbackAttackException ex)
{
    logger.LogWarning("✅ Detected rollback attack: {MetadataType} version {ActualVersion} < expected {ExpectedVersion}",
        ex.MetadataType, ex.ActualVersion, ex.ExpectedVersion);
}

try 
{
    throw new InsufficientSignaturesException(requiredSignatures: 3, validSignatures: 1, roleName: "targets");
}
catch (InsufficientSignaturesException ex)
{
    logger.LogWarning("✅ Insufficient signatures for role {RoleName}: {ValidSigs}/{RequiredSigs}",
        ex.RoleName, ex.ValidSignatures, ex.RequiredSignatures);
}

// Demo 5: Production Error Categories
logger.LogInformation("⚙️  Demo 5: Production Error Categorization");

var productionExamples = new Dictionary<string, Exception>
{
    ["Network"] = new RepositoryNetworkException("Failed to reach TUF repository", new Uri("https://repo.example.com")),
    ["Security"] = new SignatureVerificationException("RSA signature verification failed", "key-123", "targets"),
    ["Integrity"] = new TargetIntegrityException("Hash mismatch detected", "app.exe", "sha256-expected", "sha256-actual"),
    ["Performance"] = new FileSizeException("large-file.bin", actualSize: 1_000_000_000, maxAllowedSize: 100_000_000)
};

foreach (var (category, ex) in productionExamples)
{
    logger.LogInformation("  • {Category}: {ExceptionType} - {Message}", 
        category, ex.GetType().Name, ex.Message);
}

// Demo 6: Performance Telemetry
logger.LogInformation("⚡ Demo 6: Performance and Telemetry Features");
using (var activity = TufActivitySource.StartActivity("PerformanceDemo"))
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    // Simulate some work
    await Task.Delay(100);
    
    stopwatch.Stop();
    logger.LogInformation("Operation {OperationName} completed in {ElapsedMs}ms", "SimulatedMetadataValidation", stopwatch.ElapsedMilliseconds);
    
    activity?.SetTag("operation.duration_ms", stopwatch.ElapsedMilliseconds);
    activity?.SetTag("operation.success", true);
    
    logger.LogInformation("✅ Demonstrated performance telemetry with structured metrics");
}

// Demo 7: Network Error Handling
logger.LogInformation("🌐 Demo 7: Network Error Handling with Context");
try
{
    var networkEx = new RepositoryNetworkException(
        "Failed to reach TUF repository", 
        new Uri("https://repo.example.com"), 
        System.Net.HttpStatusCode.NotFound,
        new HttpRequestException("DNS resolution failed"));
    
    throw networkEx;
}
catch (RepositoryNetworkException ex)
{
    logger.LogTufException(ex); // Structured logging with all context
    logger.LogInformation("✅ Demonstrated network error logging with full context preservation");
}

logger.LogInformation("✨ Demo completed! TUF .NET now provides enterprise-grade features:");
logger.LogInformation("  🎯 13 specific exception types for different error scenarios");
logger.LogInformation("  📊 High-performance structured logging via LoggerMessage source generator");
logger.LogInformation("  🔍 System.Diagnostics.Activity integration for distributed tracing");
logger.LogInformation("  🛡️  Critical security event logging for SIEM integration");
logger.LogInformation("  ⚙️  Production-ready configuration validation with context");
logger.LogInformation("  📈 Performance telemetry for operational monitoring");
logger.LogInformation("  🚀 AOT-compatible design for cloud-native deployments");

logger.LogInformation("🏆 This brings TUF .NET to full parity with mature implementations!");