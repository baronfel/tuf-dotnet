using System;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;

namespace TUF.Benchmarks;

/// <summary>
/// Simple benchmarks for core TUF operations.
/// Provides baseline performance measurements for critical paths.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class SimpleBenchmarks
{
    private byte[] _testData = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create 1KB test data - typical metadata size
        _testData = new byte[1024];
        Random.Shared.NextBytes(_testData);
    }

    [Benchmark(Baseline = true)]
    public string HashSha256()
    {
        return Convert.ToHexString(SHA256.HashData(_testData)).ToLowerInvariant();
    }

    [Benchmark]
    public string HashSha512()
    {
        return Convert.ToHexString(SHA512.HashData(_testData)).ToLowerInvariant();
    }

    [Benchmark]
    public string SerializeTestData()
    {
        // Simple serialization benchmark using base64 encoding as proxy
        return Convert.ToBase64String(_testData);
    }
}