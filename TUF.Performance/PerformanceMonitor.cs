using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TUF.Performance;

/// <summary>
/// High-performance monitoring for TUF operations with minimal overhead.
/// Provides detailed timing, memory usage, and operation metrics for TUF workflows.
/// </summary>
/// <remarks>
/// Designed for production use with AOT compatibility and minimal allocations.
/// Uses System.Diagnostics.Activity for distributed tracing integration.
/// </remarks>
public sealed class PerformanceMonitor : IDisposable
{
    private static readonly ActivitySource ActivitySource = new("TUF.Performance");
    
    private readonly ILogger? _logger;
    private readonly List<OperationMetrics> _metrics = new();
    private bool _disposed;

    /// <summary>
    /// Gets the collected performance metrics.
    /// </summary>
    public IReadOnlyList<OperationMetrics> Metrics => _metrics;

    /// <summary>
    /// Creates a new performance monitor.
    /// </summary>
    /// <param name="logger">Optional logger for detailed performance logging</param>
    public PerformanceMonitor(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Measures the performance of a synchronous operation.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operationName">Name of the operation being measured</param>
    /// <param name="operation">The operation to measure</param>
    /// <returns>The result of the operation</returns>
    public T MeasureOperation<T>(string operationName, Func<T> operation)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PerformanceMonitor));

        using var activity = ActivitySource.StartActivity(operationName);
        var stopwatch = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(false);
        var initialGen0 = GC.CollectionCount(0);
        var initialGen1 = GC.CollectionCount(1);
        var initialGen2 = GC.CollectionCount(2);

        Exception? exception = null;
        T result;

        try
        {
            result = operation();
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);
            var memoryDelta = finalMemory - initialMemory;
            
            var metrics = new OperationMetrics
            {
                OperationName = operationName,
                Duration = stopwatch.Elapsed,
                MemoryAllocated = memoryDelta,
                Gen0Collections = GC.CollectionCount(0) - initialGen0,
                Gen1Collections = GC.CollectionCount(1) - initialGen1,
                Gen2Collections = GC.CollectionCount(2) - initialGen2,
                Success = exception == null,
                Exception = exception?.GetType().Name,
                Timestamp = DateTimeOffset.UtcNow
            };

            _metrics.Add(metrics);

            // Log performance metrics
            if (_logger != null)
            {
                if (exception == null)
                {
                    _logger.LogInformation(
                        "TUF operation {OperationName} completed in {Duration}ms, Memory: {Memory}B, GC: {Gen0}/{Gen1}/{Gen2}",
                        operationName, metrics.Duration.TotalMilliseconds, memoryDelta, 
                        metrics.Gen0Collections, metrics.Gen1Collections, metrics.Gen2Collections);
                }
                else
                {
                    _logger.LogError(exception,
                        "TUF operation {OperationName} failed after {Duration}ms, Memory: {Memory}B",
                        operationName, metrics.Duration.TotalMilliseconds, memoryDelta);
                }
            }

            // Add telemetry tags to activity
            activity?.SetTag("tuf.operation", operationName);
            activity?.SetTag("tuf.duration_ms", metrics.Duration.TotalMilliseconds);
            activity?.SetTag("tuf.memory_bytes", memoryDelta);
            activity?.SetTag("tuf.success", exception == null);
        }

        return result;
    }

    /// <summary>
    /// Measures the performance of an asynchronous operation.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operationName">Name of the operation being measured</param>
    /// <param name="operation">The async operation to measure</param>
    /// <returns>The result of the operation</returns>
    public async Task<T> MeasureOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PerformanceMonitor));

        using var activity = ActivitySource.StartActivity(operationName);
        var stopwatch = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(false);
        var initialGen0 = GC.CollectionCount(0);
        var initialGen1 = GC.CollectionCount(1);
        var initialGen2 = GC.CollectionCount(2);

        Exception? exception = null;
        T result;

        try
        {
            result = await operation();
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);
            var memoryDelta = finalMemory - initialMemory;
            
            var metrics = new OperationMetrics
            {
                OperationName = operationName,
                Duration = stopwatch.Elapsed,
                MemoryAllocated = memoryDelta,
                Gen0Collections = GC.CollectionCount(0) - initialGen0,
                Gen1Collections = GC.CollectionCount(1) - initialGen1,
                Gen2Collections = GC.CollectionCount(2) - initialGen2,
                Success = exception == null,
                Exception = exception?.GetType().Name,
                Timestamp = DateTimeOffset.UtcNow
            };

            _metrics.Add(metrics);

            // Log performance metrics
            if (_logger != null)
            {
                if (exception == null)
                {
                    _logger.LogInformation(
                        "TUF async operation {OperationName} completed in {Duration}ms, Memory: {Memory}B, GC: {Gen0}/{Gen1}/{Gen2}",
                        operationName, metrics.Duration.TotalMilliseconds, memoryDelta,
                        metrics.Gen0Collections, metrics.Gen1Collections, metrics.Gen2Collections);
                }
                else
                {
                    _logger.LogError(exception,
                        "TUF async operation {OperationName} failed after {Duration}ms, Memory: {Memory}B",
                        operationName, metrics.Duration.TotalMilliseconds, memoryDelta);
                }
            }

            // Add telemetry tags to activity
            activity?.SetTag("tuf.operation", operationName);
            activity?.SetTag("tuf.duration_ms", metrics.Duration.TotalMilliseconds);
            activity?.SetTag("tuf.memory_bytes", memoryDelta);
            activity?.SetTag("tuf.success", exception == null);
        }

        return result;
    }

    /// <summary>
    /// Generates a comprehensive performance report from collected metrics.
    /// </summary>
    /// <returns>Performance analysis report</returns>
    public PerformanceReport GenerateReport()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PerformanceMonitor));

        if (_metrics.Count == 0)
            return new PerformanceReport { Summary = "No operations measured" };

        var operationGroups = _metrics.GroupBy(m => m.OperationName).ToList();
        var operationSummaries = new List<OperationSummary>();

        foreach (var group in operationGroups)
        {
            var operations = group.ToList();
            var successfulOps = operations.Where(o => o.Success).ToList();
            
            operationSummaries.Add(new OperationSummary
            {
                OperationName = group.Key,
                TotalCount = operations.Count,
                SuccessCount = successfulOps.Count,
                FailureCount = operations.Count - successfulOps.Count,
                AverageDuration = successfulOps.Count > 0 ? TimeSpan.FromTicks((long)successfulOps.Average(o => o.Duration.Ticks)) : TimeSpan.Zero,
                MinDuration = successfulOps.Count > 0 ? successfulOps.Min(o => o.Duration) : TimeSpan.Zero,
                MaxDuration = successfulOps.Count > 0 ? successfulOps.Max(o => o.Duration) : TimeSpan.Zero,
                TotalMemoryAllocated = operations.Sum(o => o.MemoryAllocated),
                AverageMemoryAllocated = operations.Count > 0 ? operations.Average(o => o.MemoryAllocated) : 0,
                TotalGCCollections = operations.Sum(o => o.Gen0Collections + o.Gen1Collections + o.Gen2Collections)
            });
        }

        var totalDuration = _metrics.Sum(m => m.Duration.Ticks);
        var totalMemory = _metrics.Sum(m => m.MemoryAllocated);
        
        return new PerformanceReport
        {
            TotalOperations = _metrics.Count,
            TotalDuration = TimeSpan.FromTicks(totalDuration),
            TotalMemoryAllocated = totalMemory,
            OperationSummaries = operationSummaries,
            MeasurementPeriod = _metrics.Count > 0 ? 
                _metrics.Max(m => m.Timestamp) - _metrics.Min(m => m.Timestamp) : TimeSpan.Zero,
            Summary = $"Measured {_metrics.Count} TUF operations over {operationGroups.Count} operation types"
        };
    }

    /// <summary>
    /// Clears all collected metrics.
    /// </summary>
    public void Clear()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PerformanceMonitor));
        
        _metrics.Clear();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _metrics.Clear();
            _disposed = true;
        }
    }
}