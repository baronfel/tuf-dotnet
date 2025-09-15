using System;
using System.Diagnostics;
using TUF.Models;
using TUF.Performance;

namespace PerformanceBenchmark;

/// <summary>
/// Performance benchmarks demonstrating memory optimization improvements in TUF .NET implementation.
/// Compares optimized Span&lt;T&gt;/Memory&lt;T&gt; implementations against baseline allocation patterns.
/// </summary>
class Program
{
    private const int IterationCount = 10_000;
    private static readonly byte[] TestData = new byte[1024]; // 1KB test payload
    
    static void Main(string[] args)
    {
        Console.WriteLine("TUF .NET Performance Benchmarks");
        Console.WriteLine("==================================");
        Console.WriteLine($"Running {IterationCount:N0} iterations with {TestData.Length:N0} byte payloads");
        Console.WriteLine();

        // Initialize test data
        Random.Shared.NextBytes(TestData);

        // Benchmark hex conversion
        BenchmarkHexConversion();
        
        // Benchmark signing performance  
        BenchmarkSigningPerformance();

        Console.WriteLine("Performance benchmarks completed!");
        Console.WriteLine("Key improvements:");
        Console.WriteLine("- Hex conversion: Uses stackalloc for small arrays");
        Console.WriteLine("- Hash operations: Stack-allocated buffers for SHA-256");
        Console.WriteLine("- JSON serialization: Optimized UTF-8 encoding for small strings");
        Console.WriteLine();
        Console.WriteLine("These optimizations reduce GC pressure and improve startup performance,");
        Console.WriteLine("which is critical for TUF operations during application initialization.");
    }

    static void BenchmarkHexConversion()
    {
        Console.WriteLine("Hex Conversion Benchmarks:");
        Console.WriteLine("--------------------------");

        var testBytes = TestData.AsSpan(0, 64); // 64 bytes = 128 hex chars

        // Baseline: Convert.ToHexString()
        var sw = Stopwatch.StartNew();
        long baselineAllocations = GC.GetTotalAllocatedBytes(false);
        
        for (int i = 0; i < IterationCount; i++)
        {
            var hex = Convert.ToHexString(testBytes).ToLowerInvariant();
        }
        
        sw.Stop();
        var baselineTime = sw.ElapsedMilliseconds;
        var baselineAllocated = GC.GetTotalAllocatedBytes(false) - baselineAllocations;

        // Optimized: HexUtilities with Span<T>
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        sw.Restart();
        long optimizedAllocations = GC.GetTotalAllocatedBytes(false);
        
        for (int i = 0; i < IterationCount; i++)
        {
            var hex = HexUtilities.ToHexString(testBytes);
        }
        
        sw.Stop();
        var optimizedTime = sw.ElapsedMilliseconds;
        var optimizedAllocated = GC.GetTotalAllocatedBytes(false) - optimizedAllocations;

        Console.WriteLine($"  Baseline:  {baselineTime:N0}ms, {baselineAllocated:N0} bytes allocated");
        Console.WriteLine($"  Optimized: {optimizedTime:N0}ms, {optimizedAllocated:N0} bytes allocated");
        
        if (optimizedTime > 0 && baselineTime > 0)
        {
            Console.WriteLine($"  Time improvement: {(double)baselineTime / optimizedTime:F1}x faster");
        }
        if (optimizedAllocated > 0 && baselineAllocated > optimizedAllocated)
        {
            Console.WriteLine($"  Memory improvement: {(double)baselineAllocated / optimizedAllocated:F1}x less allocation");
        }
        Console.WriteLine();
    }

    static void BenchmarkSigningPerformance()
    {
        Console.WriteLine("Signature Generation Benchmarks:");
        Console.WriteLine("--------------------------------");

        var ed25519Signer = Ed25519Signer.Generate();
        using var rsaSigner = RsaSigner.Generate(2048);
        using var ecdsaSigner = EcdsaSigner.Generate();

        // Benchmark Ed25519
        BenchmarkSigner("Ed25519", ed25519Signer, TestData.AsSpan(0, 512));
        
        // Benchmark RSA-PSS  
        BenchmarkSigner("RSA-PSS", rsaSigner, TestData.AsSpan(0, 512));
        
        // Benchmark ECDSA
        BenchmarkSigner("ECDSA", ecdsaSigner, TestData.AsSpan(0, 512));
        
        Console.WriteLine();
    }

    static void BenchmarkSigner(string name, ISigner signer, ReadOnlySpan<byte> data)
    {
        const int sigIterations = 1000; // Fewer iterations for crypto operations
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var sw = Stopwatch.StartNew();
        long allocations = GC.GetTotalAllocatedBytes(false);
        
        for (int i = 0; i < sigIterations; i++)
        {
            var signature = signer.SignBytes(data);
        }
        
        sw.Stop();
        var allocated = GC.GetTotalAllocatedBytes(false) - allocations;

        Console.WriteLine($"  {name}: {sw.ElapsedMilliseconds:N0}ms, {allocated / sigIterations:N0} bytes per signature");
    }
}