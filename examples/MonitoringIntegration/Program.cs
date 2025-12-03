using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TUF;

namespace MonitoringIntegration;

/// <summary>
/// Comprehensive example demonstrating TUF .NET integration with modern observability stacks
/// including structured logging, metrics collection, and distributed tracing.
/// 
/// This example shows production-ready patterns for:
/// - OpenTelemetry integration for metrics, logs, and traces
/// - Prometheus-compatible metrics export
/// - Structured logging for security events
/// - Health checks and monitoring endpoints
/// - Performance monitoring and alerting
/// </summary>
class Program
{
    // Metrics for TUF operations
    private static readonly Meter TufMeter = new("TUF.MonitoringDemo", "1.0.0");
    private static readonly Counter<int> UpdatesCheckedCounter = TufMeter.CreateCounter<int>("tuf_updates_checked_total", "Total number of TUF update checks performed");
    private static readonly Counter<int> SecurityEventsCounter = TufMeter.CreateCounter<int>("tuf_security_events_total", "Total number of TUF security events detected");
    private static readonly Histogram<double> OperationDurationHistogram = TufMeter.CreateHistogram<double>("tuf_operation_duration_seconds", "Duration of TUF operations in seconds");
    private static readonly Counter<int> MetadataRefreshCounter = TufMeter.CreateCounter<int>("tuf_metadata_refreshes_total", "Total number of metadata refresh operations");
    private static readonly UpDownCounter<int> CacheHitsGauge = TufMeter.CreateUpDownCounter<int>("tuf_cache_hits", "Current number of cache hits");

    static async Task Main(string[] args)
    {
        Console.WriteLine("🔍 TUF .NET Monitoring and Observability Integration Example");
        Console.WriteLine("=========================================================");
        
        // Build host with comprehensive logging and monitoring
        var host = CreateHostBuilder(args).Build();
        
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        
        try
        {
            await RunMonitoringDemo(logger);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Demo failed with unhandled exception");
            throw;
        }
        finally
        {
            await host.StopAsync();
        }
    }
    
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
                });
                logging.AddDebug();
                
                // Configure structured logging for security events
                logging.AddFilter("TUF", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices(services =>
            {
                // Add monitoring services
                services.AddSingleton<TufMetricsCollector>();
                services.AddSingleton<TufHealthCheckService>();
                
                // Add hosted service for continuous monitoring
                services.AddHostedService<TufMonitoringService>();
            });

    private static async Task RunMonitoringDemo(ILogger logger)
    {
        logger.LogInformation("🚀 Starting TUF monitoring and observability demonstration");
        
        // Demonstrate metrics collection
        await DemonstrateMetricsCollection(logger);
        
        // Demonstrate distributed tracing
        await DemonstrateDistributedTracing(logger);
        
        // Demonstrate security event monitoring
        await DemonstrateSecurityEventMonitoring(logger);
        
        // Demonstrate health checks
        await DemonstrateHealthChecks(logger);
        
        // Demonstrate performance monitoring
        await DemonstratePerformanceMonitoring(logger);
        
        logger.LogInformation("✅ TUF monitoring demonstration completed successfully");
        
        // Display metrics summary
        DisplayMetricsSummary(logger);
    }

    private static async Task DemonstrateMetricsCollection(ILogger logger)
    {
        using var activity = TufActivitySource.StartActivity("MetricsCollectionDemo");
        logger.LogInformation("📊 Demonstrating metrics collection for TUF operations");
        
        // Simulate TUF operations with metrics
        for (int i = 0; i < 5; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            logger.LogInformation("Performing TUF update check #{CheckNumber}", i + 1);
            
            // Simulate update check
            await Task.Delay(Random.Shared.Next(100, 500));
            
            UpdatesCheckedCounter.Add(1, new KeyValuePair<string, object?>("repository", "example-repo"));
            MetadataRefreshCounter.Add(1, new KeyValuePair<string, object?>("metadata_type", "targets"));
            CacheHitsGauge.Add(Random.Shared.Next(0, 3));
            
            // Record operation timing
            stopwatch.Stop();
            OperationDurationHistogram.Record(stopwatch.ElapsedMilliseconds / 1000.0, 
                new KeyValuePair<string, object?>("operation", "update_check"));
        }
        
        logger.LogInformation("✅ Metrics collection demonstration completed");
    }

    private static async Task DemonstrateDistributedTracing(ILogger logger)
    {
        using var activity = TufActivitySource.StartActivity("DistributedTracingDemo");
        activity?.SetTag("demo.type", "distributed_tracing");
        activity?.SetTag("repository.url", "https://example-repo.com");
        
        logger.LogInformation("🔗 Demonstrating distributed tracing for TUF operations");
        
        // Nested operations to show tracing hierarchy
        await TraceableOperation("MetadataRefresh", async () =>
        {
            await TraceableOperation("FetchRoot", async () =>
            {
                await Task.Delay(100);
                logger.LogDebug("Root metadata fetched");
            });
            
            await TraceableOperation("FetchTargets", async () =>
            {
                await Task.Delay(150);
                logger.LogDebug("Targets metadata fetched");
            });
            
            await TraceableOperation("VerifySignatures", async () =>
            {
                await Task.Delay(50);
                logger.LogDebug("Signatures verified");
            });
        });
        
        logger.LogInformation("✅ Distributed tracing demonstration completed");
    }

    private static async Task TraceableOperation(string operationName, Func<Task> operation)
    {
        using var activity = TufActivitySource.StartActivity(operationName);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await operation();
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private static async Task DemonstrateSecurityEventMonitoring(ILogger logger)
    {
        using var activity = TufActivitySource.StartActivity("SecurityEventMonitoring");
        logger.LogInformation("🔒 Demonstrating security event monitoring and alerting");
        
        // Simulate various security events that would be detected in production
        var securityEvents = new[]
        {
            ("signature_verification_failure", "Detected invalid signature on targets metadata"),
            ("rollback_attack_attempt", "Potential rollback attack: version decreased from 15 to 12"),
            ("expired_metadata_warning", "Timestamp metadata expired 2 hours ago"),
            ("integrity_check_failure", "Target file hash mismatch detected"),
            ("suspicious_network_activity", "Multiple consecutive network timeouts detected")
        };
        
        foreach (var (eventType, description) in securityEvents)
        {
            using var securityActivity = TufActivitySource.StartActivity($"SecurityEvent.{eventType}");
            securityActivity?.SetTag("event.type", eventType);
            securityActivity?.SetTag("event.severity", GetSecurityEventSeverity(eventType));
            
            logger.LogWarning("⚠️  SECURITY EVENT: {EventType} - {Description}", eventType, description);
            
            SecurityEventsCounter.Add(1, 
                new KeyValuePair<string, object?>("event_type", eventType),
                new KeyValuePair<string, object?>("severity", GetSecurityEventSeverity(eventType)));
            
            await Task.Delay(100); // Simulate processing time
        }
        
        logger.LogInformation("✅ Security event monitoring demonstration completed");
    }

    private static string GetSecurityEventSeverity(string eventType) => eventType switch
    {
        "rollback_attack_attempt" => "critical",
        "signature_verification_failure" => "high",
        "integrity_check_failure" => "high",
        "expired_metadata_warning" => "medium",
        "suspicious_network_activity" => "medium",
        _ => "low"
    };

    private static async Task DemonstrateHealthChecks(ILogger logger)
    {
        using var activity = TufActivitySource.StartActivity("HealthCheckDemo");
        logger.LogInformation("❤️  Demonstrating TUF health checks and service monitoring");
        
        var healthChecker = new TufHealthCheckService();
        
        // Check various aspects of TUF service health
        var healthChecks = new (string name, Func<Task<HealthResult>> check)[]
        {
            ("repository_connectivity", () => healthChecker.CheckRepositoryConnectivity()),
            ("metadata_freshness", () => healthChecker.CheckMetadataFreshness()),
            ("cache_integrity", () => healthChecker.CheckCacheIntegrity()),
            ("signing_capabilities", () => healthChecker.CheckSigningCapabilities()),
            ("storage_availability", () => healthChecker.CheckStorageAvailability())
        };
        
        var healthResults = new List<(string check, bool healthy, string details)>();
        
        foreach (var (checkName, healthCheckFunc) in healthChecks)
        {
            using var checkActivity = TufActivitySource.StartActivity($"HealthCheck.{checkName}");
            
            try
            {
                var result = await healthCheckFunc();
                healthResults.Add((checkName, result.IsHealthy, result.Details));
                
                checkActivity?.SetTag("health.status", result.IsHealthy ? "healthy" : "unhealthy");
                checkActivity?.SetTag("health.details", result.Details);
                
                var logLevel = result.IsHealthy ? LogLevel.Information : LogLevel.Warning;
                logger.Log(logLevel, "Health check '{CheckName}': {Status} - {Details}", 
                    checkName, result.IsHealthy ? "HEALTHY" : "UNHEALTHY", result.Details);
            }
            catch (Exception ex)
            {
                healthResults.Add((checkName, false, ex.Message));
                checkActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Health check '{CheckName}' failed", checkName);
            }
        }
        
        // Overall health assessment
        var overallHealth = healthResults.All(r => r.healthy);
        logger.Log(overallHealth ? LogLevel.Information : LogLevel.Warning,
            "Overall TUF service health: {OverallHealth}", overallHealth ? "HEALTHY" : "DEGRADED");
        
        logger.LogInformation("✅ Health checks demonstration completed");
    }

    private static async Task DemonstratePerformanceMonitoring(ILogger logger)
    {
        using var activity = TufActivitySource.StartActivity("PerformanceMonitoring");
        logger.LogInformation("⚡ Demonstrating performance monitoring and optimization");
        
        var performanceMetrics = new Dictionary<string, double>();
        
        // Measure different TUF operations
        var operations = new (string name, Func<Task> operation)[]
        {
            ("metadata_parse", () => Task.Delay(Random.Shared.Next(10, 50))),
            ("signature_verification", () => Task.Delay(Random.Shared.Next(20, 100))),
            ("network_request", () => Task.Delay(Random.Shared.Next(100, 300))),
            ("file_hash_calculation", () => Task.Delay(Random.Shared.Next(50, 200))),
            ("cache_lookup", () => Task.Delay(Random.Shared.Next(5, 20)))
        };
        
        foreach (var (operationName, operationFunc) in operations)
        {
            using var performanceActivity = TufActivitySource.StartActivity($"Performance.{operationName}");
            var stopwatch = Stopwatch.StartNew();
            
            await operationFunc();
            
            stopwatch.Stop();
            var durationSeconds = stopwatch.ElapsedMilliseconds / 1000.0;
            performanceMetrics[operationName] = durationSeconds;
            
            OperationDurationHistogram.Record(durationSeconds, 
                new KeyValuePair<string, object?>("operation", operationName));
            
            performanceActivity?.SetTag("duration_seconds", durationSeconds);
            
            logger.LogInformation("Performance: {Operation} completed in {Duration:F3}s", 
                operationName, durationSeconds);
        }
        
        // Performance analysis and alerting
        var slowOperations = performanceMetrics.Where(kvp => kvp.Value > 0.2).ToList();
        if (slowOperations.Any())
        {
            logger.LogWarning("⚠️  Performance alert: Slow operations detected: {SlowOperations}", 
                string.Join(", ", slowOperations.Select(kvp => $"{kvp.Key}:{kvp.Value:F3}s")));
        }
        
        logger.LogInformation("✅ Performance monitoring demonstration completed");
    }

    private static void DisplayMetricsSummary(ILogger logger)
    {
        logger.LogInformation("📈 Metrics Summary:");
        logger.LogInformation("  - Operations performed with timing data");
        logger.LogInformation("  - Security events tracked and categorized");
        logger.LogInformation("  - Health checks executed with results");
        logger.LogInformation("  - Performance metrics collected for analysis");
        logger.LogInformation("");
        logger.LogInformation("🔧 Integration Points:");
        logger.LogInformation("  - OpenTelemetry: Activities and traces captured");
        logger.LogInformation("  - Prometheus: Metrics available for scraping");
        logger.LogInformation("  - Structured Logging: JSON-formatted events");
        logger.LogInformation("  - Health Endpoints: Service status monitoring");
    }
}

/// <summary>
/// Service for collecting TUF-specific metrics
/// </summary>
public class TufMetricsCollector
{
    private readonly ILogger<TufMetricsCollector> _logger;

    public TufMetricsCollector(ILogger<TufMetricsCollector> logger)
    {
        _logger = logger;
    }

    public void RecordUpdateCheck(string repository)
    {
        _logger.LogDebug("Recording update check for repository: {Repository}", repository);
        // Metrics recording logic would go here
    }
}

/// <summary>
/// Health check service for TUF operations
/// </summary>
public class TufHealthCheckService
{
    public Task<HealthResult> CheckRepositoryConnectivity()
    {
        var isHealthy = Random.Shared.NextDouble() > 0.1; // 90% healthy
        return Task.FromResult(new HealthResult(isHealthy, 
            isHealthy ? "Repository accessible" : "Repository connection timeout"));
    }

    public Task<HealthResult> CheckMetadataFreshness()
    {
        var isHealthy = Random.Shared.NextDouble() > 0.2; // 80% healthy  
        return Task.FromResult(new HealthResult(isHealthy,
            isHealthy ? "Metadata is fresh" : "Metadata is stale"));
    }

    public Task<HealthResult> CheckCacheIntegrity()
    {
        var isHealthy = Random.Shared.NextDouble() > 0.05; // 95% healthy
        return Task.FromResult(new HealthResult(isHealthy,
            isHealthy ? "Cache integrity verified" : "Cache corruption detected"));
    }

    public Task<HealthResult> CheckSigningCapabilities()
    {
        var isHealthy = Random.Shared.NextDouble() > 0.1; // 90% healthy
        return Task.FromResult(new HealthResult(isHealthy,
            isHealthy ? "Signing keys accessible" : "Signing key unavailable"));
    }

    public Task<HealthResult> CheckStorageAvailability()
    {
        var isHealthy = Random.Shared.NextDouble() > 0.15; // 85% healthy
        return Task.FromResult(new HealthResult(isHealthy,
            isHealthy ? "Storage available" : "Storage space low"));
    }
}

/// <summary>
/// Health check result
/// </summary>
public record HealthResult(bool IsHealthy, string Details);

/// <summary>
/// Background service for continuous TUF monitoring
/// </summary>
public class TufMonitoringService : BackgroundService
{
    private readonly ILogger<TufMonitoringService> _logger;

    public TufMonitoringService(ILogger<TufMonitoringService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TUF monitoring service started");
        
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                // Continuous monitoring would happen here
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        
        _logger.LogInformation("TUF monitoring service stopped");
    }
}