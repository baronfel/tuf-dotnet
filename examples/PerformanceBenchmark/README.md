# TUF .NET Performance Benchmark

This example demonstrates comprehensive performance monitoring and benchmarking capabilities for TUF .NET operations. It provides baseline performance measurements and identifies optimization opportunities.

## Features

### 🚀 Comprehensive Benchmarking
- **Cryptographic Operations**: Ed25519, RSA, and ECDSA key generation and signing
- **Serialization**: Canonical JSON serialization and deserialization performance
- **Validation**: Metadata signature verification performance
- **Memory Analysis**: Detailed memory allocation and garbage collection tracking

### 📊 Performance Monitoring
- **Real-time Metrics**: Live performance data collection with minimal overhead
- **Distributed Tracing**: Integration with System.Diagnostics.Activity for observability
- **Statistical Analysis**: Comprehensive performance statistics and insights
- **Categorized Reporting**: Performance analysis by operation category

### 💡 Optimization Insights
- **Bottleneck Detection**: Automatically identifies performance bottlenecks
- **Memory Usage Analysis**: Highlights high-memory operations
- **Success Rate Monitoring**: Tracks operation reliability
- **Performance Recommendations**: Actionable optimization suggestions

## Quick Start

### Prerequisites
- .NET 8.0 or later
- TUF .NET library and dependencies

### Running the Benchmark

```bash
# Navigate to the benchmark directory
cd examples/PerformanceBenchmark

# Run the comprehensive benchmark suite
dotnet run

# Build and run in release mode for accurate measurements
dotnet run --configuration Release
```

## Sample Output

```
🚀 TUF .NET Performance Benchmark
=====================================

🔐 Cryptographic Performance Benchmarks
----------------------------------------
📊 Ed25519 Performance:
  • Generated 100 Ed25519 keys
  • Created 1000 Ed25519 signatures on 1KB data

📊 RSA Performance:
  • Generated 10 RSA-2048 keys
  • Created 100 RSA signatures on 1KB data

📊 ECDSA Performance:
  • Generated 50 ECDSA P-256 keys
  • Created 500 ECDSA signatures on 1KB data

📄 Serialization Performance Benchmarks
---------------------------------------
📊 Canonical JSON Serialization:
  • Serialized Root metadata 1000 times
  • Serialized Targets metadata 1000 times

📊 JSON Deserialization:
  • Deserialized Root metadata 1000 times
  • Deserialized Targets metadata 1000 times

📈 Performance Analysis Report
===============================

📊 Overall Performance:
  • Total operations measured: 2,150
  • Total measurement time: 4.32 seconds
  • Average ops/sec: 497.7
  • Total memory allocated: 2.45 MB
  • Measurement period: 12.34 seconds

🔐 Signing Performance:
  • Total operations: 1,600
  • Average duration: 0.89ms
  • Success rate: 100.0%
  • Memory allocated: 1.23 MB
  • Best: TUF.Signing.ed25519.ed25519
  • Worst: TUF.Signing.rsa.rsassa-pss-sha256

💡 Performance Insights:
  • Measured 2,150 TUF operations across 12 operation types
  • Average operation duration: 2.01ms
  • Total memory allocated: 2.45 MB
  • Ed25519 operations are significantly faster than RSA
  • Canonical JSON serialization performs well with minimal allocations

⏱️  Top 5 Slowest Operations:
  1. TUF.KeyGeneration.RSA: 45.67ms avg (10 ops)
  2. TUF.Signing.rsa.rsassa-pss-sha256: 12.34ms avg (100 ops)
  3. TUF.KeyGeneration.ECDSA: 3.45ms avg (50 ops)
  4. TUF.Signing.ecdsa.ecdsa-sha2-nistp256: 1.89ms avg (500 ops)
  5. TUF.CanonicalJson.Serialize.Root: 0.23ms avg (1000 ops)

💾 Top Memory Allocators:
  1. TUF.KeyGeneration.RSA: 89.45 KB avg per operation
  2. TUF.KeyGeneration.ECDSA: 12.34 KB avg per operation
  3. TUF.CanonicalJson.Serialize.Targets: 2.34 KB avg per operation
```

## Performance Monitoring Integration

### Basic Usage

```csharp
using TUF.Performance;
using Microsoft.Extensions.Logging;

// Create performance monitor with logging
var logger = loggerFactory.CreateLogger<PerformanceMonitor>();
using var monitor = new PerformanceMonitor(logger);

// Measure TUF operations
var signer = Ed25519Signer.Generate();
var signature = monitor.MeasureSigning(signer, () => 
    signer.SignBytes(data));

// Measure async operations
var refreshResult = await monitor.MeasureRefreshAsync(async () => 
    await updater.RefreshAsync());

// Generate performance report
var report = monitor.GenerateReport();
var tufAnalysis = report.AnalyzeTufPerformance();
```

### TUF-Specific Extensions

The performance monitoring library provides specialized extensions for TUF operations:

```csharp
// Cryptographic operations
monitor.MeasureSigning(signer, signingOperation);
monitor.MeasureSignatureVerification("ed25519", verificationOperation);

// Serialization operations  
monitor.MeasureCanonicalSerialization(typeof(Root), serializationOperation);
monitor.MeasureDeserialization(typeof(Targets), deserializationOperation);

// TUF workflow operations
await monitor.MeasureRefreshAsync(refreshOperation);
await monitor.MeasureDownloadAsync("target.txt", downloadOperation);
monitor.MeasureMetadataValidation("root", validationOperation);
```

## Performance Analysis Categories

### 🔐 Signing Performance
- Key generation performance across algorithms
- Signing operation throughput and latency
- Memory allocation patterns for cryptographic operations

### ✅ Verification Performance  
- Signature verification speed
- Key validation performance
- Certificate chain verification (if applicable)

### 📄 Serialization Performance
- Canonical JSON serialization efficiency
- Deserialization speed and memory usage
- Large metadata handling performance

### 🌐 Network Performance
- Metadata refresh operations
- Target download performance
- Network timeout and retry behavior

## Optimization Recommendations

Based on benchmark results, the library provides automatic recommendations:

### Performance Optimizations
- **Algorithm Selection**: Prefer Ed25519 for best signing performance
- **Batch Operations**: Group multiple operations to amortize setup costs
- **Memory Management**: Monitor GC pressure in high-throughput scenarios

### Production Considerations
- **Key Generation**: Pre-generate keys for better startup performance
- **Caching**: Cache parsed metadata to avoid repeated deserialization
- **Monitoring**: Use performance monitoring in production for regression detection

## Integration with Observability

The performance monitor integrates with modern .NET observability:

### Distributed Tracing
```csharp
// Automatic Activity creation for distributed tracing
using var activity = Activity.StartActivity("TUF.Operation");
var result = monitor.MeasureOperation("operation", () => DoWork());
```

### Structured Logging
```csharp
// Rich structured logs for performance metrics
logger.LogInformation(
    "TUF operation {OperationName} completed in {Duration}ms", 
    operationName, duration.TotalMilliseconds);
```

### Metrics Export
```csharp
// Export metrics to monitoring systems
var report = monitor.GenerateReport();
metricsCollector.RecordTufPerformance(report);
```

## Production Usage

### CI/CD Integration
```yaml
- name: Run Performance Benchmarks
  run: |
    dotnet run --project examples/PerformanceBenchmark --configuration Release
    # Parse results and fail if performance regression detected
```

### Continuous Performance Monitoring
```csharp
// Production performance monitoring
var monitor = new PerformanceMonitor(logger);
services.AddSingleton(monitor);

// Measure critical TUF operations
public async Task RefreshMetadata()
{
    await monitor.MeasureRefreshAsync(async () => 
    {
        // Your TUF refresh logic
    });
}
```

## Technical Implementation

### High-Performance Design
- **Minimal Overhead**: Designed for production use with minimal performance impact
- **Memory Efficient**: Careful memory management to avoid affecting GC
- **AOT Compatible**: Full support for ahead-of-time compilation
- **Thread Safe**: Safe for concurrent use across multiple threads

### Extensibility
- **Custom Metrics**: Easy to add application-specific performance metrics
- **Pluggable Analysis**: Customize performance analysis and recommendations
- **Export Formats**: Support for various monitoring and analysis tools

This performance monitoring framework enables data-driven optimization of TUF .NET applications and provides production-ready observability for critical security operations.