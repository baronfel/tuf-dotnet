# TUF .NET HTTP Resilience Demo

This example demonstrates the new HTTP resilience features in TUF .NET, which bring the implementation to parity with other mature TUF implementations like python-tuf and go-tuf.

## Features Demonstrated

### 🔄 Retry Policies
- Configurable maximum retry attempts
- Exponential backoff with jitter
- Specific HTTP status codes that trigger retries
- Graceful handling of transient failures

### ⏱️ Timeout Handling
- Per-request timeouts
- Configurable timeout values
- Proper cancellation of timed-out requests
- Distinction between user cancellation and timeouts

### 🛡️ Production-Ready Error Handling
- Specific exception types for different failure modes
- Structured logging with rich context
- Network error categorization
- Security-focused error messages

### 📊 Observability
- Comprehensive logging with Microsoft.Extensions.Logging
- HTTP request attempt tracking
- Retry delay logging
- Success/failure metrics

## Usage

```bash
dotnet run --project examples/HttpResilienceDemo
```

## Configuration

The demo shows how to configure HTTP resilience settings:

```csharp
var resilienceConfig = new HttpResilienceConfig
{
    MaxRetries = 3,                           // Number of retry attempts
    BaseDelay = TimeSpan.FromSeconds(1),      // Initial delay between retries
    MaxDelay = TimeSpan.FromSeconds(10),      // Maximum delay cap
    RequestTimeout = TimeSpan.FromSeconds(30), // Individual request timeout
    UserAgent = "TUF-HttpResilienceDemo/1.0"  // Custom user agent
};
```

## Integration with TUF

The resilient HTTP client integrates seamlessly with the TUF `Updater`:

```csharp
var updaterConfig = new UpdaterConfig(initialRootBytes, metadataUrl)
{
    HttpResilienceConfig = resilienceConfig,  // ✅ New resilience settings
    Logger = logger,                          // ✅ Structured logging support
    // ... other settings
};

var updater = new Updater(updaterConfig);

// ✅ All TUF operations now support cancellation tokens
await updater.RefreshAsync(cancellationToken);
await updater.DownloadTarget(targetFile, path, cancellationToken: cancellationToken);
```

## Parity Achievement

This implementation addresses **Phase 3** of the TUF .NET parity plan:

- ✅ **Retry policies and timeout handling**: Comprehensive retry logic with exponential backoff
- ✅ **Configuration validation and defaults**: Sensible defaults with full customization
- ✅ **Comprehensive error handling**: Specific exception types for different scenarios  
- ✅ **Telemetry integration**: Native .NET logging with structured data
- ✅ **Cancellation support**: CancellationToken support throughout the API
- ✅ **Production readiness**: Enterprise-grade HTTP client behavior

This brings TUF .NET HTTP handling to parity with python-tuf, go-tuf, and other mature TUF implementations.