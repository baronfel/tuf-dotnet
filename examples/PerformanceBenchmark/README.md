# TUF .NET Performance Benchmark

This benchmark demonstrates the performance improvements achieved through memory optimization using `Span<T>` and `Memory<T>` patterns in the TUF .NET implementation.

## Overview

The benchmark compares:
- **Hex conversion**: Optimized `HexUtilities` vs `Convert.ToHexString()`
- **Signature generation**: Memory-efficient signing across Ed25519, RSA-PSS, and ECDSA
- **JSON serialization**: Canonical JSON with reduced allocations

## Key Optimizations

### 1. Hex String Conversion
- **Before**: `Convert.ToHexString(bytes).ToLowerInvariant()` - Creates 2 string allocations
- **After**: `HexUtilities.ToHexString(bytes)` - Uses `stackalloc` for small arrays, single allocation

### 2. Hash Operations
- **Before**: `SHA256.HashData(data)` - Allocates byte array for hash result
- **After**: `SHA256.TryHashData(data, stackalloc byte[32])` - Stack allocation for hash

### 3. JSON Raw Value Writing
- **Before**: `_utf8.GetBytes(value)` - Always allocates byte array
- **After**: `stackalloc` for small strings, fallback for large strings

## Performance Expectations

Based on .NET memory allocation patterns, expected improvements:
- **Hex conversion**: 50-70% less memory allocation, 10-20% faster
- **Signature generation**: 20-30% less allocation per signature
- **JSON serialization**: 15-25% reduction in allocation rate

## Running the Benchmark

```bash
cd examples/PerformanceBenchmark
dotnet run --configuration Release
```

## Interpreting Results

- **Time improvement**: How much faster the optimized version runs
- **Memory improvement**: Reduction factor in total bytes allocated
- **Bytes per operation**: Memory cost per individual operation

Lower numbers indicate better performance and efficiency.

## .NET Performance Features Used

- `Span<T>` and `ReadOnlySpan<T>` for zero-copy data access
- `stackalloc` for small, short-lived allocations
- `ArrayPool<T>` for reusable buffer management  
- Inlined helper methods for minimal overhead
- UTF-8 encoding optimizations for JSON generation

These optimizations align with .NET's high-performance patterns and are particularly effective for TUF operations that happen frequently during application startup and update checks.