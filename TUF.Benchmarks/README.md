# TUF .NET Performance Benchmarks

This project contains comprehensive performance benchmarks for the TUF .NET implementation using BenchmarkDotNet. These benchmarks help establish baseline performance metrics and enable comparison with other TUF implementations.

## Benchmark Categories

### CanonicalJsonBenchmarks
Measures performance of JSON serialization/deserialization operations:
- Root metadata serialization/deserialization
- Full metadata container serialization/deserialization  
- Signed bytes extraction
- Key ID calculation
- Comparison with System.Text.Json baseline

### SigningBenchmarks
Measures cryptographic operation performance:
- Key generation (Ed25519, RSA, ECDSA)
- Signing operations for all key types
- Signature verification for all key types
- Key ID calculation for all key types

### UpdaterBenchmarks
Measures core TUF workflow performance:
- Metadata validation operations
- Trusted metadata management
- Update operations (timestamp, snapshot, targets)
- Target file validation

## Running Benchmarks

### Run All Benchmarks
```bash
cd TUF.Benchmarks
dotnet run -c Release
```

### Run Specific Benchmark Class
```bash
dotnet run -c Release -- --filter "*CanonicalJsonBenchmarks*"
dotnet run -c Release -- --filter "*SigningBenchmarks*"
dotnet run -c Release -- --filter "*UpdaterBenchmarks*"
```

### Run Specific Methods
```bash
dotnet run -c Release -- --filter "*SerializeRoot*"
dotnet run -c Release -- --filter "*SignWithEd25519*"
```

### Generate Reports
```bash
# Generate markdown report
dotnet run -c Release -- --exporters md

# Generate HTML report
dotnet run -c Release -- --exporters html

# Generate CSV data
dotnet run -c Release -- --exporters csv
```

## Benchmark Configuration

- **Target Frameworks**: .NET 8.0 and .NET 10.0
- **Memory Diagnostics**: Enabled to track allocations
- **Job Configuration**: Simple jobs for consistent results
- **Export Formats**: Markdown by default

## Performance Baselines

The benchmarks establish baselines for:

1. **JSON Operations**: Critical for metadata processing
2. **Cryptographic Operations**: Essential for security verification
3. **TUF Workflows**: Core client update scenarios

## Interpreting Results

### Key Metrics
- **Mean**: Average execution time
- **Error**: Standard error of measurement
- **StdDev**: Standard deviation
- **Allocated**: Memory allocated per operation
- **Gen0**: Generation 0 garbage collections per operation

### Performance Targets
- JSON serialization should be &lt;1ms for typical metadata
- Ed25519 signing should be &lt;1ms 
- RSA operations may be slower but should be &lt;10ms
- Memory allocations should be minimized

## Comparison with Other Implementations

Use these benchmarks to compare performance with:
- **Python TUF**: Reference implementation
- **Go TUF**: High-performance implementation  
- **Rust Tough**: Systems-level performance
- **JavaScript TUF**: Web-focused implementation

## CI Integration

Benchmarks can be integrated into CI/CD pipelines to:
- Detect performance regressions
- Track performance improvements over time
- Compare performance across .NET versions
- Validate optimization efforts

## Performance Optimization Guidelines

Based on benchmark results:

1. **Memory Optimization**: Use `Span&lt;T&gt;` and `Memory&lt;T&gt;` for hot paths
2. **Allocation Reduction**: Pool objects for frequently used operations
3. **Async Patterns**: Use async/await for I/O bound operations
4. **Caching**: Cache computed values like key IDs and signatures
5. **Parallel Processing**: Parallelize independent operations

## AOT Compatibility

All benchmarks are AOT-compatible and can be run with:
```bash
dotnet publish -c Release --self-contained -r linux-x64 /p:PublishAot=true
./TUF.Benchmarks
```