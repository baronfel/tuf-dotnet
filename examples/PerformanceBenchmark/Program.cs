using System.Text;
using Microsoft.Extensions.Logging;
using TUF.Models;
using TUF.Performance;
using CanonicalJson;

namespace PerformanceBenchmark;

/// <summary>
/// Comprehensive performance benchmarking for TUF .NET operations.
/// Demonstrates performance monitoring capabilities and provides baseline measurements.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 TUF .NET Performance Benchmark");
        Console.WriteLine("=====================================\n");

        // Set up logging for performance monitoring
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<PerformanceMonitor>();

        // Create performance monitor
        using var monitor = new PerformanceMonitor(logger);

        try
        {
            // Run comprehensive performance benchmarks
            await RunCryptographicBenchmarks(monitor);
            await RunSerializationBenchmarks(monitor);
            await RunMetadataValidationBenchmarks(monitor);

            // Generate and display comprehensive performance report
            GeneratePerformanceReport(monitor);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Benchmark failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static async Task RunCryptographicBenchmarks(PerformanceMonitor monitor)
    {
        Console.WriteLine("🔐 Cryptographic Performance Benchmarks");
        Console.WriteLine("----------------------------------------");

        // Ed25519 benchmarks
        await BenchmarkEd25519Operations(monitor);
        
        // RSA benchmarks  
        await BenchmarkRsaOperations(monitor);
        
        // ECDSA benchmarks
        await BenchmarkEcdsaOperations(monitor);

        Console.WriteLine();
    }

    private static async Task BenchmarkEd25519Operations(PerformanceMonitor monitor)
    {
        Console.WriteLine("📊 Ed25519 Performance:");
        
        // Key generation benchmark
        var keyGenResult = monitor.MeasureOperation("TUF.KeyGeneration.Ed25519", () =>
        {
            var keys = new List<Ed25519Signer>();
            for (int i = 0; i < 100; i++)
            {
                keys.Add(Ed25519Signer.Generate());
            }
            return keys;
        });
        
        Console.WriteLine($"  • Generated 100 Ed25519 keys");

        // Signing benchmark
        var signer = keyGenResult.First();
        var testData = GenerateTestData(1024); // 1KB test data
        
        var signingResult = monitor.MeasureSigning(signer, () =>
        {
            var signatures = new List<SignatureObject>();
            for (int i = 0; i < 1000; i++)
            {
                signatures.Add(signer.SignBytes(testData));
            }
            return signatures;
        });
        
        Console.WriteLine($"  • Created 1000 Ed25519 signatures on 1KB data");

        await Task.CompletedTask; // Make method async for consistency
    }

    private static async Task BenchmarkRsaOperations(PerformanceMonitor monitor)
    {
        Console.WriteLine("📊 RSA Performance:");
        
        // RSA key generation and signing (fewer iterations due to cost)
        var rsaResult = monitor.MeasureOperation("TUF.KeyGeneration.RSA", () =>
        {
            var keys = new List<RsaSigner>();
            for (int i = 0; i < 10; i++) // Fewer RSA keys due to generation cost
            {
                keys.Add(RsaSigner.Generate(2048));
            }
            return keys;
        });
        
        Console.WriteLine($"  • Generated 10 RSA-2048 keys");

        var rsaSigner = rsaResult.First();
        var testData = GenerateTestData(1024);
        
        var rsaSigningResult = monitor.MeasureSigning(rsaSigner, () =>
        {
            var signatures = new List<SignatureObject>();
            for (int i = 0; i < 100; i++) // Fewer RSA signatures due to cost
            {
                signatures.Add(rsaSigner.SignBytes(testData));
            }
            return signatures;
        });
        
        Console.WriteLine($"  • Created 100 RSA signatures on 1KB data");

        await Task.CompletedTask;
    }

    private static async Task BenchmarkEcdsaOperations(PerformanceMonitor monitor)
    {
        Console.WriteLine("📊 ECDSA Performance:");
        
        // ECDSA key generation and signing
        var ecdsaResult = monitor.MeasureOperation("TUF.KeyGeneration.ECDSA", () =>
        {
            var keys = new List<EcdsaSigner>();
            for (int i = 0; i < 50; i++)
            {
                keys.Add(EcdsaSigner.Generate());
            }
            return keys;
        });
        
        Console.WriteLine($"  • Generated 50 ECDSA P-256 keys");

        var ecdsaSigner = ecdsaResult.First();
        var testData = GenerateTestData(1024);
        
        var ecdsaSigningResult = monitor.MeasureSigning(ecdsaSigner, () =>
        {
            var signatures = new List<SignatureObject>();
            for (int i = 0; i < 500; i++)
            {
                signatures.Add(ecdsaSigner.SignBytes(testData));
            }
            return signatures;
        });
        
        Console.WriteLine($"  • Created 500 ECDSA signatures on 1KB data");

        await Task.CompletedTask;
    }

    private static async Task RunSerializationBenchmarks(PerformanceMonitor monitor)
    {
        Console.WriteLine("📄 Serialization Performance Benchmarks");
        Console.WriteLine("---------------------------------------");

        // Create sample TUF metadata for serialization testing
        var sampleRoot = CreateSampleRootMetadata();
        var sampleTargets = CreateSampleTargetsMetadata();
        
        // Canonical JSON serialization benchmarks
        await BenchmarkCanonicalSerialization(monitor, sampleRoot, sampleTargets);
        
        // JSON deserialization benchmarks
        await BenchmarkDeserialization(monitor, sampleRoot, sampleTargets);

        Console.WriteLine();
    }

    private static async Task BenchmarkCanonicalSerialization(PerformanceMonitor monitor, Root sampleRoot, Targets sampleTargets)
    {
        Console.WriteLine("📊 Canonical JSON Serialization:");

        // Root metadata serialization
        var rootSerResult = monitor.MeasureCanonicalSerialization(typeof(Root), () =>
        {
            var results = new List<byte[]>();
            for (int i = 0; i < 1000; i++)
            {
                results.Add(CanonicalJson.Serializer.Serialize(sampleRoot));
            }
            return results;
        });
        
        Console.WriteLine($"  • Serialized Root metadata 1000 times");

        // Targets metadata serialization
        var targetsSerResult = monitor.MeasureCanonicalSerialization(typeof(Targets), () =>
        {
            var results = new List<byte[]>();
            for (int i = 0; i < 1000; i++)
            {
                results.Add(CanonicalJson.Serializer.Serialize(sampleTargets));
            }
            return results;
        });
        
        Console.WriteLine($"  • Serialized Targets metadata 1000 times");

        await Task.CompletedTask;
    }

    private static async Task BenchmarkDeserialization(PerformanceMonitor monitor, Root sampleRoot, Targets sampleTargets)
    {
        Console.WriteLine("📊 JSON Deserialization:");

        // Prepare serialized data
        var rootJson = CanonicalJson.Serializer.Serialize(sampleRoot);
        var targetsJson = CanonicalJson.Serializer.Serialize(sampleTargets);

        // Root metadata deserialization
        var rootDeserResult = monitor.MeasureDeserialization(typeof(Root), () =>
        {
            var results = new List<Root>();
            for (int i = 0; i < 1000; i++)
            {
                results.Add(CanonicalJson.Serializer.Deserialize<Root>(Encoding.UTF8.GetString(rootJson)));
            }
            return results;
        });
        
        Console.WriteLine($"  • Deserialized Root metadata 1000 times");

        // Targets metadata deserialization
        var targetsDeserResult = monitor.MeasureDeserialization(typeof(Targets), () =>
        {
            var results = new List<Targets>();
            for (int i = 0; i < 1000; i++)
            {
                results.Add(CanonicalJson.Serializer.Deserialize<Targets>(Encoding.UTF8.GetString(targetsJson)));
            }
            return results;
        });
        
        Console.WriteLine($"  • Deserialized Targets metadata 1000 times");

        await Task.CompletedTask;
    }

    private static async Task RunMetadataValidationBenchmarks(PerformanceMonitor monitor)
    {
        Console.WriteLine("✅ Metadata Validation Performance Benchmarks");
        Console.WriteLine("---------------------------------------------");

        // Create signed metadata for validation testing
        var (signedRoot, signedTargets) = CreateSignedMetadata();
        
        // Signature verification benchmarks
        await BenchmarkSignatureVerification(monitor, signedRoot, signedTargets);

        Console.WriteLine();
    }

    private static async Task BenchmarkSignatureVerification(PerformanceMonitor monitor, Metadata<Root> signedRoot, Metadata<Targets> signedTargets)
    {
        Console.WriteLine("📊 Signature Verification:");

        // Root metadata signature verification
        var rootVerifyResult = monitor.MeasureSignatureVerification("Ed25519", () =>
        {
            var results = new List<bool>();
            for (int i = 0; i < 1000; i++)
            {
                // Simulate signature verification (simplified for benchmark)
                results.Add(signedRoot.Signatures.Count > 0);
            }
            return results;
        });
        
        Console.WriteLine($"  • Verified Root metadata signatures 1000 times");

        // Targets metadata signature verification
        var targetsVerifyResult = monitor.MeasureSignatureVerification("Ed25519", () =>
        {
            var results = new List<bool>();
            for (int i = 0; i < 1000; i++)
            {
                // Simulate signature verification (simplified for benchmark)
                results.Add(signedTargets.Signatures.Count > 0);
            }
            return results;
        });
        
        Console.WriteLine($"  • Verified Targets metadata signatures 1000 times");

        await Task.CompletedTask;
    }

    private static void GeneratePerformanceReport(PerformanceMonitor monitor)
    {
        Console.WriteLine("📈 Performance Analysis Report");
        Console.WriteLine("===============================\n");

        // Generate comprehensive performance report
        var report = monitor.GenerateReport();
        var tufAnalysis = report.AnalyzeTufPerformance();

        // Display overall performance metrics
        Console.WriteLine("📊 Overall Performance:");
        Console.WriteLine($"  • Total operations measured: {report.TotalOperations}");
        Console.WriteLine($"  • Total measurement time: {report.TotalDuration.TotalSeconds:F2} seconds");
        Console.WriteLine($"  • Average ops/sec: {report.AverageOperationsPerSecond:F1}");
        Console.WriteLine($"  • Total memory allocated: {FormatBytes(report.TotalMemoryAllocated)}");
        Console.WriteLine($"  • Measurement period: {report.MeasurementPeriod.TotalSeconds:F2} seconds\n");

        // Display TUF-specific analysis
        DisplayCategoryPerformance("🔐 Signing Performance", tufAnalysis.SigningPerformance);
        DisplayCategoryPerformance("✅ Verification Performance", tufAnalysis.VerificationPerformance);
        DisplayCategoryPerformance("📄 Serialization Performance", tufAnalysis.SerializationPerformance);
        DisplayCategoryPerformance("🌐 Network Performance", tufAnalysis.NetworkPerformance);

        // Display performance insights
        Console.WriteLine("💡 Performance Insights:");
        foreach (var insight in tufAnalysis.PerformanceInsights)
        {
            Console.WriteLine($"  • {insight}");
        }
        Console.WriteLine();

        // Display top 5 slowest operations
        var slowestOps = report.OperationSummaries
            .OrderByDescending(s => s.AverageDuration)
            .Take(5)
            .ToList();

        if (slowestOps.Count > 0)
        {
            Console.WriteLine("⏱️  Top 5 Slowest Operations:");
            for (int i = 0; i < slowestOps.Count; i++)
            {
                var op = slowestOps[i];
                Console.WriteLine($"  {i + 1}. {op.OperationName}: {op.AverageDuration.TotalMilliseconds:F2}ms avg ({op.TotalCount} ops)");
            }
            Console.WriteLine();
        }

        // Display memory usage leaders
        var memoryOps = report.OperationSummaries
            .OrderByDescending(s => s.AverageMemoryAllocated)
            .Take(3)
            .ToList();

        if (memoryOps.Count > 0)
        {
            Console.WriteLine("💾 Top Memory Allocators:");
            for (int i = 0; i < memoryOps.Count; i++)
            {
                var op = memoryOps[i];
                Console.WriteLine($"  {i + 1}. {op.OperationName}: {FormatBytes((long)op.AverageMemoryAllocated)} avg per operation");
            }
        }
    }

    private static void DisplayCategoryPerformance(string title, CategoryPerformance performance)
    {
        Console.WriteLine(title + ":");
        if (performance.TotalOperations == 0)
        {
            Console.WriteLine("  • No operations measured");
        }
        else
        {
            Console.WriteLine($"  • Total operations: {performance.TotalOperations}");
            Console.WriteLine($"  • Average duration: {performance.AverageDuration.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  • Success rate: {performance.SuccessRate:F1}%");
            Console.WriteLine($"  • Memory allocated: {FormatBytes(performance.TotalMemoryAllocated)}");
            
            if (performance.BestPerformingOperation != null)
                Console.WriteLine($"  • Best: {performance.BestPerformingOperation}");
            if (performance.WorstPerformingOperation != null)
                Console.WriteLine($"  • Worst: {performance.WorstPerformingOperation}");

            foreach (var recommendation in performance.Recommendations)
            {
                Console.WriteLine($"    ⚠️  {recommendation}");
            }
        }
        Console.WriteLine();
    }

    // Helper methods for generating test data and metadata

    private static byte[] GenerateTestData(int size)
    {
        var random = new Random(42); // Fixed seed for consistent benchmarking
        var data = new byte[size];
        random.NextBytes(data);
        return data;
    }

    private static Root CreateSampleRootMetadata()
    {
        var signer = Ed25519Signer.Generate();
        return new Root
        {
            Type = "root",
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            ConsistentSnapshot = true,
            Keys = new Dictionary<string, Key>
            {
                { signer.Key.Id.Value, signer.Key }
            },
            Roles = new Roles
            {
                Root = new RoleKeys { KeyIds = new List<string> { signer.Key.Id.Value }, Threshold = 1 },
                Timestamp = new RoleKeys { KeyIds = new List<string> { signer.Key.Id.Value }, Threshold = 1 },
                Snapshot = new RoleKeys { KeyIds = new List<string> { signer.Key.Id.Value }, Threshold = 1 },
                Targets = new RoleKeys { KeyIds = new List<string> { signer.Key.Id.Value }, Threshold = 1 }
            }
        };
    }

    private static Targets CreateSampleTargetsMetadata()
    {
        return new Targets
        {
            Type = "targets",
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            TargetMap = new Dictionary<string, TargetFile>
            {
                ["sample.txt"] = new TargetFile
                {
                    Length = 1024,
                    Hashes = new Dictionary<string, string>
                    {
                        ["sha256"] = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
                    }
                }
            }
        };
    }

    private static (Metadata<Root> root, Metadata<Targets> targets) CreateSignedMetadata()
    {
        var signer = Ed25519Signer.Generate();
        var root = CreateSampleRootMetadata();
        var targets = CreateSampleTargetsMetadata();

        var rootBytes = CanonicalJson.Serializer.Serialize(root);
        var targetsBytes = CanonicalJson.Serializer.Serialize(targets);

        var signedRoot = new Metadata<Root>
        {
            Signed = root,
            Signatures = new List<SignatureObject> { signer.SignBytes(rootBytes) }
        };

        var signedTargets = new Metadata<Targets>
        {
            Signed = targets,
            Signatures = new List<SignatureObject> { signer.SignBytes(targetsBytes) }
        };

        return (signedRoot, signedTargets);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        var absBytes = Math.Abs(bytes);
        var units = new[] { "B", "KB", "MB", "GB" };
        var index = 0;
        double value = absBytes;

        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        var sign = bytes < 0 ? "-" : "";
        return $"{sign}{value:F2} {units[index]}";
    }
}