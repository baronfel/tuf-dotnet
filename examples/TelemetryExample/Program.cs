using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using System.Diagnostics;
using TUF;

namespace TelemetryExample;

/// <summary>
/// Demonstrates TUF telemetry and monitoring capabilities using OpenTelemetry
/// 
/// This example shows how to:
/// 1. Configure OpenTelemetry metrics collection for TUF operations
/// 2. Enable distributed tracing with ActivitySource
/// 3. Monitor TUF repository operations and performance
/// 4. Export metrics to various monitoring systems
/// </summary>
internal class Program
{
    private static async Task Main(string[] args)
    {
        // Configure logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var logger = loggerFactory.CreateLogger<Program>();

        // Configure OpenTelemetry metrics
        using var tracerProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(TufMetrics.MeterName)
            .AddConsoleExporter()  // In production, use Prometheus, OTLP, or other exporters
            .Build();

        logger.LogInformation("🚀 TUF Telemetry Example Starting");

        // Example: Simulate TUF operations with metrics and tracing
        await SimulateTufOperations(logger);

        logger.LogInformation("✅ TUF Telemetry Example Complete");

        // Give time for metrics to be exported
        await Task.Delay(1000);
    }

    /// <summary>
    /// Simulate various TUF operations to demonstrate telemetry collection
    /// </summary>
    private static async Task SimulateTufOperations(ILogger logger)
    {
        logger.LogInformation("📊 Simulating TUF operations with telemetry...");

        // 1. Simulate repository initialization
        using (var initActivity = TufActivitySource.StartActivity("Repository.Initialize"))
        {
            initActivity?.SetTag("repository.url", "https://example.com/tuf");
            initActivity?.SetTag("operation.type", "initialize");

            var stopwatch = Stopwatch.StartNew();
            
            // Simulate some work
            await Task.Delay(100);
            
            stopwatch.Stop();
            
            TufMetricsRecorder.RecordOperation("repository_init", "success", stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("repository.url", "https://example.com/tuf"));
                
            logger.LogInformation("Repository initialized in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }

        // 2. Simulate metadata operations
        await SimulateMetadataOperations(logger);

        // 3. Simulate signature verification
        await SimulateSignatureVerifications(logger);

        // 4. Simulate network operations
        await SimulateNetworkOperations(logger);

        // 5. Simulate security events
        SimulateSecurityEvents(logger);

        // 6. Demonstrate cache metrics
        SimulateCacheOperations(logger);

        logger.LogInformation("🔍 Current metrics state recorded");
    }

    /// <summary>
    /// Simulate metadata processing operations
    /// </summary>
    private static async Task SimulateMetadataOperations(ILogger logger)
    {
        logger.LogInformation("📄 Simulating metadata operations...");

        var metadataTypes = new[] { "root", "timestamp", "snapshot", "targets" };
        var operations = new[] { "load", "validate", "cache", "fetch" };

        foreach (var metadataType in metadataTypes)
        {
            foreach (var operation in operations)
            {
                using var activity = TufActivitySource.StartActivity($"Metadata.{operation}");
                activity?.SetTag("metadata.type", metadataType);
                activity?.SetTag("operation.type", operation);

                var stopwatch = Stopwatch.StartNew();
                
                // Simulate processing time (vary by operation complexity)
                var delay = operation switch
                {
                    "load" => Random.Shared.Next(10, 50),
                    "validate" => Random.Shared.Next(20, 100),
                    "cache" => Random.Shared.Next(5, 20),
                    "fetch" => Random.Shared.Next(50, 200),
                    _ => 30
                };
                
                await Task.Delay(delay);
                stopwatch.Stop();

                // Simulate occasional failures
                var success = Random.Shared.NextDouble() > 0.05; // 95% success rate

                TufMetricsRecorder.RecordMetadataOperation(metadataType, operation, success ? "success" : "failure", stopwatch.Elapsed.TotalMilliseconds);
                logger.LogInformation("Metadata operation {Operation} for {MetadataType}: {Status} in {DurationMs}ms", 
                    operation, metadataType, success ? "success" : "failure", stopwatch.Elapsed.TotalMilliseconds);

                if (!success)
                {
                    TufMetricsRecorder.RecordError($"Metadata{operation}Exception", "metadata", $"metadata_{operation}");
                }
            }
        }
    }

    /// <summary>
    /// Simulate signature verification operations
    /// </summary>
    private static async Task SimulateSignatureVerifications(ILogger logger)
    {
        logger.LogInformation("🔐 Simulating signature verifications...");

        var keyTypes = new[] { "ed25519", "ecdsa", "rsa" };
        var algorithms = new[] { "ed25519", "ecdsa-sha2-nistp256", "rsassa-pss-sha256" };

        for (int i = 0; i < 20; i++)
        {
            var keyType = keyTypes[i % keyTypes.Length];
            var algorithm = algorithms[i % algorithms.Length];

            using var activity = TufActivitySource.StartActivity("Signature.Verify");
            activity?.SetTag("key.type", keyType);
            activity?.SetTag("signature.algorithm", algorithm);

            var stopwatch = Stopwatch.StartNew();
            
            // Simulate verification time (varies by algorithm)
            var delay = keyType switch
            {
                "ed25519" => Random.Shared.Next(5, 20),
                "ecdsa" => Random.Shared.Next(10, 30),
                "rsa" => Random.Shared.Next(15, 50),
                _ => 20
            };
            
            await Task.Delay(delay);
            stopwatch.Stop();

            // Simulate occasional verification failures
            var success = Random.Shared.NextDouble() > 0.02; // 98% success rate

            TufMetricsRecorder.RecordSignatureVerification(keyType, algorithm, success, stopwatch.Elapsed.TotalMilliseconds);
            logger.LogInformation("Signature verification for {KeyType} {Algorithm}: {Status} in {DurationMs}ms", 
                keyType, algorithm, success ? "success" : "failure", stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Simulate network operations
    /// </summary>
    private static async Task SimulateNetworkOperations(ILogger logger)
    {
        logger.LogInformation("🌐 Simulating network operations...");

        var endpoints = new[] { "/1.root.json", "/timestamp.json", "/snapshot.json", "/targets.json", "/targets/file1.tar.gz" };
        var methods = new[] { "GET", "HEAD" };

        for (int i = 0; i < 15; i++)
        {
            var method = methods[Random.Shared.Next(methods.Length)];
            var endpoint = endpoints[Random.Shared.Next(endpoints.Length)];

            using var activity = TufActivitySource.StartActivity("Network.Request");
            activity?.SetTag("http.method", method);
            activity?.SetTag("http.endpoint", endpoint);

            var stopwatch = Stopwatch.StartNew();
            
            // Simulate network latency
            await Task.Delay(Random.Shared.Next(50, 300));
            stopwatch.Stop();

            // Simulate response characteristics
            var statusCode = Random.Shared.NextDouble() switch
            {
                < 0.90 => 200, // 90% success
                < 0.95 => 404, // 5% not found
                < 0.98 => 500, // 3% server error
                _ => 429       // 2% rate limited
            };

            var responseBytes = statusCode == 200 ? Random.Shared.Next(1024, 1024 * 1024) : 0;

            TufMetricsRecorder.RecordNetworkRequest(method, endpoint, statusCode, responseBytes, stopwatch.Elapsed.TotalMilliseconds);
            logger.LogInformation("Network request {Method} {Endpoint}: {StatusCode} ({ResponseBytes} bytes) in {DurationMs}ms", 
                method, endpoint, statusCode, responseBytes, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Simulate security events
    /// </summary>
    private static void SimulateSecurityEvents(ILogger logger)
    {
        logger.LogInformation("🔒 Simulating security events...");

        // Simulate occasional security events (should be rare in production)
        if (Random.Shared.NextDouble() < 0.1) // 10% chance
        {
            TufMetricsRecorder.RecordSecurityEvent("rollback_attack", "critical", "root");
            logger.LogError("SECURITY ALERT: Rollback attack detected for root metadata");
        }

        if (Random.Shared.NextDouble() < 0.05) // 5% chance
        {
            TufMetricsRecorder.RecordSecurityEvent("integrity_failure", "critical", "target");
            logger.LogError("SECURITY ALERT: Target file integrity check failed");
        }

        if (Random.Shared.NextDouble() < 0.15) // 15% chance
        {
            TufMetricsRecorder.RecordSecurityEvent("signature_failure", "warning", "snapshot");
            logger.LogWarning("Signature verification failed for snapshot metadata");
        }
    }

    /// <summary>
    /// Simulate cache operations and statistics
    /// </summary>
    private static void SimulateCacheOperations(ILogger logger)
    {
        logger.LogInformation("💾 Simulating cache operations...");

        var cacheSize = Random.Shared.Next(10, 100);
        var cacheHits = Random.Shared.Next(cacheSize * 2, cacheSize * 10);
        var cacheMisses = Random.Shared.Next(cacheHits / 10, cacheHits / 2);

        // Update cache size metric
        TufMetrics.MetadataCacheSize.Add(cacheSize);

        // Record cache operations
        for (int i = 0; i < cacheHits; i++)
        {
            TufMetrics.MetadataCacheOperations.Add(1, 
                new KeyValuePair<string, object?>("cache.result", "hit"));
        }

        for (int i = 0; i < cacheMisses; i++)
        {
            TufMetrics.MetadataCacheOperations.Add(1,
                new KeyValuePair<string, object?>("cache.result", "miss"));
        }

        logger.LogInformation("Cache statistics: {CacheHits} hits, {CacheMisses} misses, {CacheSize} items", 
            cacheHits, cacheMisses, cacheSize);
    }
}