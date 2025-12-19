# TUF .NET Performance Benchmarks

This project provides comprehensive performance benchmarks for the TUF .NET implementation using [BenchmarkDotNet](https://benchmarkdotnet.org/). These benchmarks measure the performance of critical TUF operations to ensure optimal performance and identify potential bottlenecks.

## Overview

The benchmarks are organized into three main categories covering all performance-critical aspects of TUF operations:

### 1. **CryptographicBenchmarks** 
- **Key Generation**: Ed25519, RSA (2048/3072/4096), ECDSA
- **Digital Signing**: Small, medium, and large data signing across all supported algorithms
- **Performance Comparison**: Direct comparison between cryptographic algorithms
- **Real-world Scenarios**: Typical metadata signing (small) to large file signing

### 2. **SerializationBenchmarks**
- **Canonical JSON**: Serialization and deserialization performance
- **Metadata Complexity**: Small, medium, and large metadata structures
- **Round-trip Performance**: Combined serialize + deserialize operations
- **Memory Efficiency**: Memory allocation patterns during JSON operations

### 3. **ClientWorkflowBenchmarks**
- **Complete TUF Workflows**: End-to-end client refresh operations
- **Signature Verification**: Metadata signature validation performance
- **Target File Operations**: File lookup and filtering operations
- **Production Scenarios**: Realistic client update workflows

## Running Benchmarks

### Prerequisites

- .NET 8.0 or later
- BenchmarkDotNet automatically included via package reference

### Run All Benchmarks

```bash
# Run complete benchmark suite
dotnet run --project TUF.Benchmarks --configuration Release

# Run with specific runtime information
dotnet run --project TUF.Benchmarks --configuration Release -- --runtimes net8.0
```

### Run Specific Benchmark Categories

```bash
# Run only cryptographic benchmarks
dotnet run --project TUF.Benchmarks --configuration Release -- --filter "*CryptographicBenchmarks*"

# Run only serialization benchmarks  
dotnet run --project TUF.Benchmarks --configuration Release -- --filter "*SerializationBenchmarks*"

# Run only client workflow benchmarks
dotnet run --project TUF.Benchmarks --configuration Release -- --filter "*ClientWorkflowBenchmarks*"
```

### Run Individual Benchmarks

```bash
# Run specific benchmark methods
dotnet run --project TUF.Benchmarks --configuration Release -- --filter "*Ed25519_KeyGeneration*"
dotnet run --project TUF.Benchmarks --configuration Release -- --filter "*SerializeSmallRoot*"
```

## Benchmark Results

Results are automatically generated in the `TUF.Benchmarks/BenchmarkDotNet.Artifacts/` directory:

- **HTML Reports**: Detailed interactive reports with charts
- **CSV/JSON Data**: Raw benchmark data for analysis
- **Markdown Summary**: Quick overview of results

### Sample Results Format

```
|               Method |      Mean |     Error |    StdDev | Allocated |
|--------------------- |----------:|----------:|----------:|----------:|
| Ed25519_KeyGeneration |  45.67 μs |  0.234 μs |  0.219 μs |     312 B |
| Rsa2048_KeyGeneration | 234.12 ms |  1.456 ms |  1.362 ms |   1,024 B |
| Ed25519_SignSmallData |   2.34 μs |  0.012 μs |  0.011 μs |      96 B |
```

## Performance Targets

Based on research of other TUF implementations, target performance goals:

### Cryptographic Operations
- **Ed25519 Signing**: < 10 μs for small data (< 1KB)
- **RSA-2048 Signing**: < 500 μs for small data
- **Ed25519 Key Generation**: < 100 μs
- **RSA-2048 Key Generation**: < 500 ms

### JSON Operations
- **Small Metadata Serialization**: < 50 μs (typical root/timestamp)
- **Large Metadata Serialization**: < 500 μs (1000+ targets)
- **Memory Allocations**: Minimal allocations for frequent operations

### Client Workflows
- **Complete Refresh**: < 10 ms for typical repository
- **Signature Verification**: < 100 μs per signature
- **Target Lookups**: < 1 μs per target in large repositories

## Comparing with Other Implementations

These benchmarks enable performance comparison with other TUF implementations:

### Python TUF (Reference Implementation)
- Generally slower due to Python overhead
- Focus on correctness over raw performance
- Good baseline for functional comparison

### Go TUF
- Expected to be faster for CPU-intensive operations
- Direct performance comparison target
- Similar memory efficiency goals

### AWS Tough (Rust)
- Likely fastest implementation
- Memory efficiency reference
- High-performance production target

### JavaScript TUF
- V8 JIT compilation provides competitive performance
- Good comparison for JSON operations
- Similar to .NET in managed runtime characteristics

## Continuous Performance Monitoring

### CI Integration

Add to GitHub Actions workflow:

```yaml
- name: Run Performance Benchmarks
  run: |
    dotnet run --project TUF.Benchmarks --configuration Release -- --exporters json --filter "*" 
    
- name: Archive Benchmark Results
  uses: actions/upload-artifact@v4
  with:
    name: benchmark-results
    path: TUF.Benchmarks/BenchmarkDotNet.Artifacts/
```

### Performance Regression Detection

Monitor key metrics over time:
- Ed25519 signing performance
- JSON serialization speed
- Memory allocations per operation
- Complete client refresh time

Alert on >10% performance regressions in critical paths.

## Development Guidelines

### Adding New Benchmarks

1. **Follow BenchmarkDotNet patterns**: Use `[Benchmark]`, `[MemoryDiagnoser]`, etc.
2. **Realistic data**: Use representative data sizes and complexity
3. **Proper setup**: Initialize test data in constructor, not benchmark methods
4. **Clear naming**: Method names should indicate what's being measured
5. **Memory measurement**: Include memory allocation tracking for core operations

### Benchmark Design Principles

- **Measure real workflows**: Focus on operations users actually perform
- **Avoid artificial tests**: Don't micro-benchmark trivial operations
- **Consistent test data**: Use deterministic data generation for reproducible results
- **Multiple scales**: Test with small, medium, and large data sets
- **Cross-platform compatibility**: Ensure benchmarks work on all supported platforms

## Profiling Integration

For detailed performance analysis:

```bash
# Profile with dotnet-trace
dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime -- dotnet run --project TUF.Benchmarks --configuration Release

# Profile with PerfView on Windows
PerfView.exe run "dotnet run --project TUF.Benchmarks --configuration Release"
```

## Contributing

When adding new TUF features, please:

1. **Add corresponding benchmarks**: Ensure performance impact is measurable
2. **Update performance targets**: Document expected performance characteristics  
3. **Run before/after comparisons**: Measure performance impact of changes
4. **Consider alternatives**: If performance is critical, benchmark multiple approaches

---

These benchmarks help ensure that TUF .NET maintains excellent performance characteristics while providing comprehensive security features for software update frameworks.