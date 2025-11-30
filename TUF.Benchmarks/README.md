# TUF .NET Performance Benchmarks

This project provides comprehensive performance benchmarks for the TUF .NET implementation using [BenchmarkDotNet](https://benchmarkdotnet.org/). The benchmarks measure the performance of critical TUF operations to help with optimization efforts and performance regression detection.

## Quick Start

```bash
# Run all benchmarks
dotnet run --project TUF.Benchmarks --configuration Release

# Run specific benchmark categories
dotnet run --project TUF.Benchmarks --configuration Release --filter "*Signature*"
dotnet run --project TUF.Benchmarks --configuration Release --filter "*Serialization*"
dotnet run --project TUF.Benchmarks --configuration Release --filter "*Hashing*"
dotnet run --project TUF.Benchmarks --configuration Release --filter "*Updater*"

# Export results for analysis
dotnet run --project TUF.Benchmarks --configuration Release --exporters json,html,csv
```

## Benchmark Categories

### 1. Signature Benchmarks (`SignatureBenchmarks.cs`)

Measures cryptographic signature performance across different algorithms:

- **Ed25519 Operations**: Sign/verify operations (recommended TUF algorithm)
- **RSA-PSS Operations**: Sign/verify with 2048-bit keys (compatibility)
- **ECDSA Operations**: Sign/verify with P-256 curve
- **Batch Operations**: Multiple signature verification scenarios
- **TUF Signer Interface**: Performance through TUF's `ISigner` abstraction

**Key Metrics:**
- Signature verification time (critical for metadata validation)
- Signature generation time (for repository operations)
- Memory allocation patterns
- Comparative algorithm performance

### 2. Serialization Benchmarks (`SerializationBenchmarks.cs`)

Measures canonical JSON serialization/deserialization performance:

- **Metadata Serialization**: Root, Targets, Timestamp, Snapshot metadata
- **Size Variations**: Small (2KB) to large (1MB+) metadata files
- **Round-trip Operations**: Serialize → Deserialize cycles
- **Memory Efficiency**: Allocation patterns during JSON operations

**Key Metrics:**
- Serialization throughput (MB/s)
- Memory allocations per operation
- Scalability with metadata size
- Round-trip accuracy validation

### 3. Hashing Benchmarks (`HashingBenchmarks.cs`)

Measures file integrity verification performance:

- **Hash Algorithms**: SHA256 (primary), SHA512 (secondary) 
- **File Sizes**: 1KB to 100MB test files
- **Processing Patterns**: 
  - Static methods (`SHA256.HashData`)
  - Instance methods (reusable hashers)
  - Incremental hashing (streaming)
  - Span-based operations (zero-allocation)
- **Multiple Hashes**: Computing SHA256 + SHA512 simultaneously
- **Parallel Processing**: Multi-file hashing scenarios
- **Verification Workflows**: Hash comparison operations

**Key Metrics:**
- Hashing throughput (MB/s) by algorithm and file size
- Memory allocation efficiency
- Streaming vs in-memory processing comparison
- Parallel vs sequential multi-file processing

### 4. Updater Benchmarks (`UpdaterBenchmarks.cs`)

Measures end-to-end TUF workflow performance:

- **Updater Creation**: Initialization with various root metadata sizes
- **Metadata Operations**: Refresh cycles, target enumeration
- **File Operations**: Target download, cache lookup, integrity verification
- **Multi-Target Scenarios**: Sequential vs parallel download patterns
- **Cache Performance**: Cold vs warm cache behavior
- **Stress Testing**: Multiple refresh cycles, many target lookups

**Key Metrics:**
- Complete refresh cycle time
- Target download throughput
- Cache hit/miss performance impact
- Memory usage patterns during operations
- Scaling behavior with repository size

## Performance Targets

### Baseline Performance Expectations

| Operation | Target Performance | Notes |
|-----------|-------------------|--------|
| **Ed25519 Verify** | < 100μs | Single signature verification |
| **RSA-PSS Verify** | < 3ms | 2048-bit key verification |
| **Metadata Refresh** | < 500ms | Complete repository refresh |
| **Target Download** | Network limited | With integrity verification |
| **JSON Serialize (1MB)** | < 10ms | Large targets.json |
| **SHA256 Hash (10MB)** | < 50ms | Target file verification |

### Memory Usage Targets

| Operation | Target Memory | Notes |
|-----------|---------------|--------|
| **Cold Start** | < 50MB | Native AOT deployment |
| **Metadata Parse** | < 5MB heap | Typical root.json processing |
| **Target Verify** | < 1MB | Per file verification |
| **Cache Storage** | Configurable | Based on repository size |

## Interpreting Results

### Understanding BenchmarkDotNet Output

```
| Method                    | Mean        | Error    | StdDev   | Allocated |
|---------------------------|-------------|----------|----------|-----------|
| VerifyEd25519Signature    |    66.23 μs |  0.89 μs |  0.79 μs |         - |
| VerifyRsaPssSignature     | 1,847.45 μs | 15.23 μs | 14.25 μs |     312 B |
| SerializeSmallRoot        |   123.67 μs |  1.45 μs |  1.29 μs |   1,024 B |
```

**Key Columns:**
- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of measurements
- **Allocated**: Memory allocated per operation

### Performance Analysis Guidelines

1. **Focus on Baseline Comparisons**: Ed25519 operations are marked as baseline
2. **Monitor Memory Allocations**: Zero allocations ("Allocated: -") indicate optimal performance
3. **Consider Error Margins**: Results within error margins are statistically equivalent
4. **Scaling Analysis**: Compare performance across different input sizes

### Identifying Performance Issues

**Red Flags:**
- Memory allocations growing with input size (indicates non-streaming operations)
- High standard deviation (indicates inconsistent performance)
- Performance significantly worse than baseline expectations
- Linear scaling where logarithmic expected (algorithmic inefficiency)

**Optimization Opportunities:**
- Replace allocation-heavy operations with Span<T>-based alternatives
- Use streaming operations for large data processing
- Enable hardware acceleration where available
- Implement caching for frequently accessed data

## Running Benchmarks in CI/CD

### GitHub Actions Integration

```yaml
- name: Run Performance Benchmarks
  run: |
    dotnet run --project TUF.Benchmarks --configuration Release --exporters json
    
- name: Upload Benchmark Results
  uses: actions/upload-artifact@v3
  with:
    name: benchmark-results
    path: BenchmarkDotNet.Artifacts/results/
```

### Performance Regression Detection

```csharp
// Example performance test
[Test]
public void SignatureVerification_Performance_NoRegression()
{
    var stopwatch = Stopwatch.StartNew();
    
    // Run critical operation
    var result = SignatureAlgorithm.Ed25519.Verify(publicKey, data, signature);
    
    stopwatch.Stop();
    
    // Assert performance doesn't regress beyond 2x baseline
    Assert.That(stopwatch.ElapsedMicroseconds, Is.LessThan(200), 
        "Ed25519 verification should complete within 200μs");
    
    Assert.That(result, Is.True, "Signature verification should succeed");
}
```

## Benchmark Configuration

### Custom Benchmark Jobs

The benchmarks are configured to run with both JIT and Native AOT compilation:

```csharp
var config = DefaultConfig.Instance
    .AddJob(Job.Default.WithId("JIT").AsBaseline())
    .AddJob(Job.Default.WithId("NativeAOT").WithToolchain(NativeAotToolchain.Net80));
```

### Environment Considerations

- **CPU**: Results vary significantly across processors (Intel vs AMD, different generations)
- **Memory**: Ensure sufficient RAM to avoid swapping during large file operations
- **Storage**: SSD vs HDD can impact file I/O benchmarks significantly
- **Virtualization**: VM/container environments may show different performance characteristics

## Extending Benchmarks

### Adding New Benchmarks

1. Create new benchmark class in `TUF.Benchmarks/`
2. Inherit from appropriate base class or implement standalone
3. Add `[Benchmark]` attribute to methods
4. Use `[GlobalSetup]` and `[GlobalCleanup]` for initialization
5. Add `[MemoryDiagnoser]` for memory allocation tracking

### Example Custom Benchmark

```csharp
[MemoryDiagnoser]
[SimpleJob]
public class CustomBenchmarks
{
    private byte[] _testData;
    
    [GlobalSetup]
    public void Setup()
    {
        _testData = new byte[1024];
        Random.Shared.NextBytes(_testData);
    }
    
    [Benchmark(Baseline = true)]
    public string MyOperation()
    {
        // Your operation here
        return Convert.ToHexString(_testData);
    }
}
```

## Results Analysis

### Automated Analysis Tools

The benchmark results can be processed using various tools:

- **BenchmarkDotNet Reports**: HTML, CSV, JSON exports
- **Performance Dashboards**: Integration with monitoring systems
- **Trend Analysis**: Track performance over time
- **Comparison Tools**: Compare across different configurations

### Manual Analysis Checklist

1. **Identify Hotspots**: Focus on operations taking >90% of total time
2. **Memory Patterns**: Look for unnecessary allocations in hot paths
3. **Scaling Behavior**: Ensure operations scale appropriately with input size
4. **Algorithm Efficiency**: Compare different approaches for same operation
5. **Hardware Utilization**: Check if operations can benefit from parallelization

## Integration with Development Workflow

### Local Development

```bash
# Quick performance check during development
dotnet run --project TUF.Benchmarks --filter "*YourNewFeature*"

# Compare performance before/after changes
git stash  # Stash your changes
dotnet run --project TUF.Benchmarks --filter "*Target*" --exporters json
git stash pop  # Restore your changes
dotnet run --project TUF.Benchmarks --filter "*Target*" --exporters json
# Compare the two JSON files
```

### Performance Review Process

1. **Before Major Changes**: Run full benchmark suite to establish baseline
2. **After Implementation**: Compare results against baseline
3. **Investigate Regressions**: Any >10% performance decrease requires investigation
4. **Document Improvements**: Significant improvements should be documented

## Troubleshooting

### Common Issues

**Issue**: Benchmarks fail with "OutOfMemoryException"
**Solution**: Reduce test data size or run benchmarks individually

**Issue**: Inconsistent results across runs
**Solution**: Ensure stable system state, close other applications, use longer warmup periods

**Issue**: Native AOT benchmarks fail to compile
**Solution**: Check for reflection usage, ensure all types are AOT-compatible

### Performance Debugging

For detailed performance analysis beyond benchmarking:

1. **Use dotnet-trace**: `dotnet-trace collect -p <pid> --format speedscope`
2. **Enable GC logging**: `DOTNET_gcServer=1 DOTNET_gcConcurrent=0`
3. **Use PerfView**: Windows-specific detailed profiling
4. **Memory profilers**: JetBrains dotMemory, ANTS Memory Profiler

## Contributing

When adding new benchmarks:

1. Follow existing naming conventions
2. Include comprehensive documentation
3. Add both micro and macro benchmarks for new features
4. Consider edge cases and error conditions
5. Update this README with new benchmark descriptions

For questions about benchmark interpretation or performance optimization strategies, see the [Performance Guide](../docs/guides/performance.md) or open a discussion in the repository.