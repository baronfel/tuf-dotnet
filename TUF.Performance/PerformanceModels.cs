namespace TUF.Performance;

/// <summary>
/// Represents performance metrics for a single TUF operation.
/// Captures timing, memory usage, and garbage collection impact.
/// </summary>
public sealed record OperationMetrics
{
    /// <summary>
    /// Name of the operation being measured.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Duration of the operation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Memory allocated during the operation in bytes.
    /// Can be negative if memory was freed during the operation.
    /// </summary>
    public long MemoryAllocated { get; init; }

    /// <summary>
    /// Number of Generation 0 garbage collections during the operation.
    /// </summary>
    public int Gen0Collections { get; init; }

    /// <summary>
    /// Number of Generation 1 garbage collections during the operation.
    /// </summary>
    public int Gen1Collections { get; init; }

    /// <summary>
    /// Number of Generation 2 garbage collections during the operation.
    /// </summary>
    public int Gen2Collections { get; init; }

    /// <summary>
    /// Whether the operation completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Name of the exception type if the operation failed.
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// Timestamp when the operation was measured.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the total number of garbage collections across all generations.
    /// </summary>
    public int TotalGCCollections => Gen0Collections + Gen1Collections + Gen2Collections;
}

/// <summary>
/// Summary of performance metrics for operations of the same type.
/// Provides statistical analysis across multiple operation instances.
/// </summary>
public sealed record OperationSummary
{
    /// <summary>
    /// Name of the operation type.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Total number of operations measured.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Number of successful operations.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Number of failed operations.
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// Average duration across successful operations.
    /// </summary>
    public TimeSpan AverageDuration { get; init; }

    /// <summary>
    /// Minimum duration among successful operations.
    /// </summary>
    public TimeSpan MinDuration { get; init; }

    /// <summary>
    /// Maximum duration among successful operations.
    /// </summary>
    public TimeSpan MaxDuration { get; init; }

    /// <summary>
    /// Total memory allocated across all operations in bytes.
    /// </summary>
    public long TotalMemoryAllocated { get; init; }

    /// <summary>
    /// Average memory allocated per operation in bytes.
    /// </summary>
    public double AverageMemoryAllocated { get; init; }

    /// <summary>
    /// Total garbage collections across all operations and generations.
    /// </summary>
    public int TotalGCCollections { get; init; }

    /// <summary>
    /// Success rate as a percentage (0.0 to 100.0).
    /// </summary>
    public double SuccessRate => TotalCount > 0 ? (SuccessCount * 100.0) / TotalCount : 0.0;

    /// <summary>
    /// Operations per second based on average duration.
    /// </summary>
    public double OperationsPerSecond => AverageDuration.TotalSeconds > 0 ? 1.0 / AverageDuration.TotalSeconds : 0.0;
}

/// <summary>
/// Comprehensive performance report aggregating all measured operations.
/// Provides insights for performance optimization and monitoring.
/// </summary>
public sealed record PerformanceReport
{
    /// <summary>
    /// Total number of operations measured.
    /// </summary>
    public int TotalOperations { get; init; }

    /// <summary>
    /// Total duration across all operations.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Total memory allocated across all operations in bytes.
    /// </summary>
    public long TotalMemoryAllocated { get; init; }

    /// <summary>
    /// Time period over which measurements were taken.
    /// </summary>
    public TimeSpan MeasurementPeriod { get; init; }

    /// <summary>
    /// Performance summaries grouped by operation type.
    /// </summary>
    public IReadOnlyList<OperationSummary> OperationSummaries { get; init; } = new List<OperationSummary>();

    /// <summary>
    /// Human-readable summary of the performance report.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the operation summary with the highest average duration.
    /// </summary>
    public OperationSummary? SlowestOperation => 
        OperationSummaries.OrderByDescending(s => s.AverageDuration).FirstOrDefault();

    /// <summary>
    /// Gets the operation summary with the highest memory allocation.
    /// </summary>
    public OperationSummary? HighestMemoryOperation => 
        OperationSummaries.OrderByDescending(s => s.AverageMemoryAllocated).FirstOrDefault();

    /// <summary>
    /// Gets the operation summary with the most garbage collections.
    /// </summary>
    public OperationSummary? HighestGCOperation => 
        OperationSummaries.OrderByDescending(s => s.TotalGCCollections).FirstOrDefault();

    /// <summary>
    /// Average operations per second across all operation types.
    /// </summary>
    public double AverageOperationsPerSecond => 
        TotalDuration.TotalSeconds > 0 ? TotalOperations / TotalDuration.TotalSeconds : 0.0;
}

/// <summary>
/// Configuration for performance monitoring behavior.
/// </summary>
public sealed record PerformanceConfig
{
    /// <summary>
    /// Whether to enable detailed memory tracking.
    /// </summary>
    public bool EnableMemoryTracking { get; init; } = true;

    /// <summary>
    /// Whether to enable garbage collection tracking.
    /// </summary>
    public bool EnableGCTracking { get; init; } = true;

    /// <summary>
    /// Whether to enable distributed tracing integration.
    /// </summary>
    public bool EnableTracing { get; init; } = true;

    /// <summary>
    /// Maximum number of metrics to keep in memory.
    /// Older metrics are discarded when limit is reached.
    /// </summary>
    public int MaxMetricsCount { get; init; } = 10000;

    /// <summary>
    /// Minimum operation duration to record metrics for.
    /// Operations faster than this threshold are not recorded.
    /// </summary>
    public TimeSpan MinimumDuration { get; init; } = TimeSpan.FromMicroseconds(10);
}