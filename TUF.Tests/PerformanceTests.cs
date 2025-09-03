using TUF.Tests.TestFixtures;
using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Performance tests to measure and validate optimizations
/// </summary>
public class PerformanceTests
{
    [Test]
    public async Task SharedHttpClient_IsFasterThanIndividualInstances()
    {

        // Baseline: Create new HttpClient each time (old pattern)
        void CreateIndividualHttpClients()
        {
            for (int i = 0; i < 50; i++)
            {
                using var client = new HttpClient();
                // Simulate some lightweight operation
                _ = client.BaseAddress;
            }
        }

        // Optimized: Use shared HttpClient
        void UseSharedHttpClient()
        {
            for (int i = 0; i < 50; i++)
            {
                var client = SharedTestResources.HttpClient;
                // Simulate some lightweight operation  
                _ = client.BaseAddress;
            }
        }

        var comparison = PerformanceMeasurement.Compare(
            CreateIndividualHttpClients, 
            UseSharedHttpClient, 
            iterations: 10);

        // Shared HttpClient should be faster (improvement ratio > 1.0)
        await Assert.That(comparison.ImprovementRatio).IsGreaterThan(1.0);
        
        Console.WriteLine($"HttpClient Performance Comparison:");
        Console.WriteLine($"  Individual: {comparison.BaselineAverage.TotalMilliseconds:F2}ms");
        Console.WriteLine($"  Shared: {comparison.OptimizedAverage.TotalMilliseconds:F2}ms");
        Console.WriteLine($"  Improvement: {comparison.ImprovementRatio:F2}x faster");
    }

    [Test]
    public async Task TempDirectoryCreation_Performance()
    {
        const int directoryCount = 100;

        var time = PerformanceMeasurement.Measure(() =>
        {
            for (int i = 0; i < directoryCount; i++)
            {
                var tempDir = SharedTestResources.CreateTempDirectory();
                // Verify directory exists
                Directory.Exists(tempDir);
            }
        });

        // Should be reasonable fast (< 1 second for 100 directories)
        await Assert.That(time.TotalSeconds).IsLessThan(1.0);
        
        Console.WriteLine($"Created {directoryCount} temp directories in {time.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Average per directory: {time.TotalMilliseconds / directoryCount:F2}ms");
    }

    [Test]
    public async Task MemoryAllocation_Optimization()
    {
        // This test verifies we're not creating excessive object allocations
        
        long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
        
        // Perform operations that used to allocate heavily
        for (int i = 0; i < 1000; i++)
        {
            var tempDir = SharedTestResources.CreateTempDirectory();
            var httpClient = SharedTestResources.HttpClient;
            
            // Some lightweight operations
            _ = Path.GetDirectoryName(tempDir);
            _ = httpClient.BaseAddress;
        }
        
        long memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        long allocatedBytes = memoryAfter - memoryBefore;
        
        // Should not allocate excessive memory (< 1MB for 1000 operations)
        await Assert.That(allocatedBytes).IsLessThan(1024 * 1024); // 1MB
        
        Console.WriteLine($"Memory allocated for 1000 operations: {allocatedBytes:N0} bytes");
        Console.WriteLine($"Average per operation: {allocatedBytes / 1000.0:F2} bytes");
    }
}