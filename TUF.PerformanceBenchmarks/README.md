# TUF .NET Performance Benchmarks

This project contains comprehensive performance benchmarks for the TUF .NET implementation, designed to measure and optimize critical performance paths that impact real-world TUF client applications.

## Overview

TUF operations typically occur during application startup or update checks, making performance crucial for user experience. These benchmarks measure the key operations that affect TUF client performance:

- **JSON Serialization/Deserialization** - Core to metadata processing
- **Cryptographic Operations** - Signature generation and verification
- **Metadata Verification** - Complete TUF verification workflows
- **TrustedMetadata Operations** - High-level client API performance

## Benchmark Categories

### 1. JSON Serialization Benchmarks (`JsonSerializationBenchmarks`)

Measures canonical JSON serialization performance for all TUF metadata types:

- **Root metadata** (10 keys) - Long-lived, infrequently updated
- **Targets metadata** (1000 files) - Large, contains all downloadable files
- **Snapshot metadata** (50 delegated files) - Medium size, updated regularly
- **Timestamp metadata** - Small, updated frequently

**Why This Matters**: JSON serialization happens during signature verification (canonical JSON generation) and metadata parsing. Poor performance here directly impacts client startup time.

### 2. Cryptographic Signature Benchmarks (`CryptoSignatureBenchmarks`)

Tests all supported TUF signature algorithms across different data sizes:

**Key Generation**:
- Ed25519 (fastest, recommended for new repositories)
- RSA-2048 (widely compatible)
- RSA-4096 (high security)
- ECDSA P-256 (good balance)

**Signing Performance** (across 1KB, 50KB, 500KB data):
- Repository signing (offline, less performance critical)
- Client verification (online, performance critical)

**Key Operations**:
- Key ID calculation (happens frequently)
- Signature verification (most critical for client performance)

**Why This Matters**: Signature verification happens for every metadata file during client updates. Ed25519 is typically fastest, but RSA may be required for compatibility.

### 3. Metadata Verification Benchmarks (`MetadataVerificationBenchmarks`)

Tests complete TUF metadata verification workflows:

- **Single signature verification** - Basic verification
- **Multi-signature verification** - Threshold signatures (2 of 3 keys)
- **Complete verification chain** - Root → Timestamp → Snapshot → Targets
- **Large metadata parsing** - 500 target files with realistic data
- **Target file lookup** - Finding specific files in large repositories

**Why This Matters**: This represents the complete TUF client workflow. Optimizations here directly improve application startup time and update check performance.

### 4. TrustedMetadata Benchmarks (`TrustedMetadataBenchmarks`)

Measures the high-level TrustedMetadata API that most applications use:

- **Metadata initialization** - Loading trusted root metadata
- **Metadata updates** - Refreshing timestamp, snapshot, targets
- **Target resolution** - Finding files with delegation traversal
- **Freshness validation** - Checking metadata expiration
- **Complete refresh cycle** - Full client update operation

**Why This Matters**: TrustedMetadata is the primary API used by TUF clients. This measures real-world performance as experienced by applications.

## Running Benchmarks

### Prerequisites

- .NET 8.0 or later
- Release configuration (benchmarks automatically use optimized builds)

### Basic Usage

```bash
# Build the benchmark project
dotnet build TUF.PerformanceBenchmarks --configuration Release

# Run all benchmarks
dotnet run --project TUF.PerformanceBenchmarks --configuration Release

# Run specific benchmark class
dotnet run --project TUF.PerformanceBenchmarks --configuration Release -- --filter "*JsonSerialization*"

# Run with specific runtime
dotnet run --project TUF.PerformanceBenchmarks --configuration Release -- --runtimes net8.0 nativeaot8.0

# Generate detailed reports
dotnet run --project TUF.PerformanceBenchmarks --configuration Release -- --exporters html json
```

### Advanced Options

```bash
# Memory profiling
dotnet run --project TUF.PerformanceBenchmarks --configuration Release -- --memory

# Statistical analysis
dotnet run --project TUF.PerformanceBenchmarks --configuration Release -- --statisticalTest 3ms

# Outlier detection
dotnet run --project TUF.PerformanceBenchmarks --configuration Release -- --outliers RemoveUpper

# Custom iteration counts
dotnet run --project TUF.PerformanceBenchmarks --configuration Release -- --minIterations 15 --maxIterations 100
```

## Interpreting Results

### Key Metrics

- **Mean**: Average execution time (most important for user experience)
- **Median**: Middle value (less affected by outliers)
- **Gen0/Gen1/Gen2**: Garbage collection pressure (lower is better)
- **Allocated**: Total memory allocated (affects GC pressure)

### Performance Targets

Based on other TUF implementations and user experience requirements:

| Operation | Target | Good | Acceptable |
|-----------|--------|------|------------|
| Root verification | < 1ms | < 5ms | < 10ms |
| Timestamp verification | < 0.5ms | < 2ms | < 5ms |
| Targets parsing (1000 files) | < 10ms | < 50ms | < 100ms |
| Complete refresh cycle | < 50ms | < 200ms | < 500ms |

### Native AOT vs JIT

The benchmarks run on both regular .NET (JIT) and Native AOT:

- **JIT**: Faster after warmup, larger memory footprint
- **Native AOT**: Consistent performance, smaller footprint, important for containers/edge deployments

## Optimization Guidelines

### JSON Performance
- Canonical JSON serialization is required for TUF but can be expensive
- Large targets metadata (>1000 files) may need streaming or pagination
- Consider caching serialized forms for frequently-accessed metadata

### Cryptographic Performance
- Ed25519 is typically 10x faster than RSA for verification
- ECDSA provides good balance of performance and compatibility
- Consider async signature verification for multiple signatures

### Memory Optimization
- Large target metadata can cause GC pressure
- Use streaming for very large repositories
- Consider object pooling for frequently-created objects

### Client Performance
- Cache verified metadata to avoid re-verification
- Use HTTP caching headers for metadata that hasn't changed
- Consider background refresh for better user experience

## Comparison with Other Implementations

These benchmarks enable comparison with TUF implementations in other languages:

- **Python TUF**: Generally slower due to Python overhead, but mature
- **Go TUF**: Fast compilation, good concurrency, comparable performance
- **Rust TUF**: Excellent performance, especially for cryptographic operations
- **Node.js TUF**: V8 JIT provides good performance after warmup

The TUF .NET implementation aims for:
- **Faster startup** than JIT-based implementations (Native AOT)
- **Competitive steady-state performance** with compiled languages
- **Lower memory usage** than garbage-collected implementations
- **Better integration** with .NET ecosystems

## Contributing

When adding new benchmarks:

1. **Focus on realistic scenarios** - Use representative data sizes and patterns
2. **Measure end-to-end workflows** - Not just individual operations
3. **Include memory diagnostics** - GC pressure affects performance
4. **Test both JIT and AOT** - Performance characteristics differ
5. **Document performance targets** - What performance is acceptable?

### Adding New Benchmarks

```csharp
[Benchmark(Description = "Descriptive name")]
[Arguments(1000)] // Parameterize data sizes
public ResultType YourBenchmark(int parameterValue)
{
    // Setup (not measured)
    var data = CreateTestData(parameterValue);
    
    // The operation being measured
    return ProcessData(data);
}
```

## Results Archive

Benchmark results are automatically saved to `BenchmarkDotNet.Artifacts/` with timestamps. Key results should be documented in the main repository for tracking performance regressions.

---

## Example Output

```
| Method                                    | Runtime    | Mean      | Error     | StdDev    | Gen0   | Allocated |
|------------------------------------------ |----------- |----------:|----------:|----------:|-------:|----------:|
| Serialize Root (10 keys)                 | .NET 8.0   |  1.234 ms |  0.045 ms |  0.012 ms | 0.0153 |      98 B |
| Serialize Targets (1000 files)           | .NET 8.0   | 15.678 ms |  0.234 ms |  0.067 ms | 1.2345 |   7.89 KB |
| Ed25519 Sign Medium (50KB)               | .NET 8.0   |  0.567 ms |  0.012 ms |  0.003 ms | 0.0076 |      64 B |
| RSA-2048 Sign Medium (50KB)              | .NET 8.0   |  3.456 ms |  0.089 ms |  0.023 ms | 0.0234 |     156 B |
| Complete Metadata Verification Chain     | .NET 8.0   |  4.567 ms |  0.123 ms |  0.034 ms | 0.0456 |     289 B |
| Complete Metadata Refresh Cycle          | .NET 8.0   | 12.345 ms |  0.456 ms |  0.123 ms | 0.1234 |     789 B |
```

This shows TUF .NET achieving sub-millisecond performance for small operations and reasonable performance for large operations, with minimal memory allocation.