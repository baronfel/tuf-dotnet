# Production Resilience Demo

This example demonstrates production-ready network resilience patterns for TUF operations, addressing a key parity gap identified in the achieve-parity analysis.

## Overview

The .NET TUF implementation now includes enterprise-grade network resilience patterns comparable to mature TUF implementations in Python, Go, and Rust. This example showcases:

- **Retry Policies**: Exponential backoff for transient failures
- **Circuit Breakers**: Fault isolation to prevent cascade failures
- **Mirror Failover**: High availability through automatic fallback
- **Timeout Handling**: Appropriate timeouts for different operation types
- **Error Categorization**: Smart retry behavior based on error types

## Running the Demo

```bash
cd examples/ProductionResilienceDemo
dotnet run
```

## Key Features Demonstrated

### 1. Basic Resilience Configuration

```csharp
var resilience = new NetworkResilience(new NetworkResilienceConfig
{
    MaxRetryAttempts = 3,
    InitialRetryDelay = TimeSpan.FromSeconds(1),
    BackoffMultiplier = 2.0,
    RequestTimeout = TimeSpan.FromSeconds(30)
});
```

### 2. Mirror Failover for High Availability

```csharp
var updaterConfig = new UpdaterConfig(rootData, primaryUrl)
{
    NetworkResilience = productionConfig,
    MirrorUrls = new List<string>
    {
        "https://cdn1.example.com/metadata/",
        "https://cdn2.example.com/metadata/",
        "https://mirror.example.org/metadata/"
    }
};
```

### 3. Circuit Breaker Protection

```csharp
var resilience = new NetworkResilience(new NetworkResilienceConfig
{
    EnableCircuitBreaker = true,
    CircuitBreakerFailureThreshold = 5,
    CircuitBreakerRecoveryTimeout = TimeSpan.FromMinutes(2)
});
```

## Production Benefits

This resilience implementation provides:

- **Automatic Recovery**: Intelligent retry policies handle temporary network issues
- **Fault Isolation**: Circuit breakers prevent overwhelming failing services
- **High Availability**: Mirror failover ensures service continuity
- **Performance Protection**: Proper timeouts prevent resource exhaustion
- **Operational Insights**: Clear error categorization for monitoring

## Parity Achievement

This implementation brings the .NET TUF library to feature parity with:

- **Python TUF**: Reference implementation resilience patterns
- **Go TUF**: Production deployment best practices
- **Rust Tough**: Enterprise-grade error handling

## Configuration Examples

### Development Environment
```csharp
var devConfig = new NetworkResilienceConfig
{
    MaxRetryAttempts = 2,
    InitialRetryDelay = TimeSpan.FromSeconds(1),
    EnableCircuitBreaker = false // Simpler for development
};
```

### Production Environment
```csharp
var prodConfig = new NetworkResilienceConfig
{
    MaxRetryAttempts = 3,
    InitialRetryDelay = TimeSpan.FromSeconds(2),
    MaxRetryDelay = TimeSpan.FromMinutes(1),
    BackoffMultiplier = 1.5,
    RequestTimeout = TimeSpan.FromSeconds(45),
    FileDownloadTimeout = TimeSpan.FromMinutes(10),
    EnableCircuitBreaker = true,
    CircuitBreakerFailureThreshold = 5,
    EnableMirrorFailover = true
};
```

## Integration with TUF Operations

The resilience patterns integrate seamlessly with all TUF operations:

- **Metadata Downloads**: Automatic retry with exponential backoff
- **Target File Downloads**: Long timeout handling for large files
- **Mirror Discovery**: Automatic failover to backup repositories
- **Error Recovery**: Circuit breaker protection and smart retry logic

This makes the .NET TUF implementation suitable for mission-critical enterprise deployments where reliability and resilience are paramount.