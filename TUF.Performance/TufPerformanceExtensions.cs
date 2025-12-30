using TUF.Models;
using Microsoft.Extensions.Logging;

namespace TUF.Performance;

/// <summary>
/// Performance monitoring extensions specifically designed for TUF operations.
/// Provides instrumentation for common TUF workflows and operations.
/// </summary>
public static class TufPerformanceExtensions
{
    /// <summary>
    /// Measures the performance of TUF metadata refresh operations.
    /// </summary>
    public static Task<T> MeasureRefreshAsync<T>(this PerformanceMonitor monitor, Func<Task<T>> refreshOperation)
    {
        return monitor.MeasureOperationAsync("TUF.Refresh", refreshOperation);
    }

    /// <summary>
    /// Measures the performance of TUF target download operations.
    /// </summary>
    public static Task<T> MeasureDownloadAsync<T>(this PerformanceMonitor monitor, string targetName, Func<Task<T>> downloadOperation)
    {
        return monitor.MeasureOperationAsync($"TUF.Download.{targetName}", downloadOperation);
    }

    /// <summary>
    /// Measures the performance of TUF signature verification operations.
    /// </summary>
    public static T MeasureSignatureVerification<T>(this PerformanceMonitor monitor, string algorithm, Func<T> verificationOperation)
    {
        return monitor.MeasureOperation($"TUF.SignatureVerification.{algorithm}", verificationOperation);
    }

    /// <summary>
    /// Measures the performance of TUF signing operations.
    /// </summary>
    public static T MeasureSigning<T>(this PerformanceMonitor monitor, ISigner signer, Func<T> signingOperation)
    {
        var keyType = signer.Key.KeyType ?? "unknown";
        var scheme = signer.Key.Scheme ?? "unknown";
        return monitor.MeasureOperation($"TUF.Signing.{keyType}.{scheme}", signingOperation);
    }

    /// <summary>
    /// Measures the performance of canonical JSON serialization.
    /// </summary>
    public static T MeasureCanonicalSerialization<T>(this PerformanceMonitor monitor, Type objectType, Func<T> serializationOperation)
    {
        return monitor.MeasureOperation($"TUF.CanonicalJson.Serialize.{objectType.Name}", serializationOperation);
    }

    /// <summary>
    /// Measures the performance of JSON deserialization.
    /// </summary>
    public static T MeasureDeserialization<T>(this PerformanceMonitor monitor, Type objectType, Func<T> deserializationOperation)
    {
        return monitor.MeasureOperation($"TUF.Json.Deserialize.{objectType.Name}", deserializationOperation);
    }

    /// <summary>
    /// Measures the performance of metadata validation operations.
    /// </summary>
    public static T MeasureMetadataValidation<T>(this PerformanceMonitor monitor, string metadataType, Func<T> validationOperation)
    {
        return monitor.MeasureOperation($"TUF.Validation.{metadataType}", validationOperation);
    }

    /// <summary>
    /// Measures the performance of TUF updater initialization.
    /// </summary>
    public static T MeasureUpdaterInit<T>(this PerformanceMonitor monitor, Func<T> initOperation)
    {
        return monitor.MeasureOperation("TUF.Updater.Initialize", initOperation);
    }

    /// <summary>
    /// Measures the performance of hash computation operations.
    /// </summary>
    public static T MeasureHashComputation<T>(this PerformanceMonitor monitor, string algorithm, long dataSize, Func<T> hashOperation)
    {
        return monitor.MeasureOperation($"TUF.Hash.{algorithm}.{FormatDataSize(dataSize)}", hashOperation);
    }

    /// <summary>
    /// Creates a comprehensive performance analysis specifically for TUF workloads.
    /// </summary>
    public static TufPerformanceAnalysis AnalyzeTufPerformance(this PerformanceReport report)
    {
        var tufOperations = report.OperationSummaries
            .Where(s => s.OperationName.StartsWith("TUF.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var signingOperations = tufOperations
            .Where(s => s.OperationName.Contains("Signing", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var verificationOperations = tufOperations
            .Where(s => s.OperationName.Contains("SignatureVerification", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var serializationOperations = tufOperations
            .Where(s => s.OperationName.Contains("Serialize", StringComparison.OrdinalIgnoreCase) || 
                       s.OperationName.Contains("Deserialize", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var networkOperations = tufOperations
            .Where(s => s.OperationName.Contains("Refresh", StringComparison.OrdinalIgnoreCase) || 
                       s.OperationName.Contains("Download", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new TufPerformanceAnalysis
        {
            BaseReport = report,
            TotalTufOperations = tufOperations.Sum(s => s.TotalCount),
            SigningPerformance = AnalyzeCategory(signingOperations, "Signing"),
            VerificationPerformance = AnalyzeCategory(verificationOperations, "Verification"), 
            SerializationPerformance = AnalyzeCategory(serializationOperations, "Serialization"),
            NetworkPerformance = AnalyzeCategory(networkOperations, "Network"),
            PerformanceInsights = GeneratePerformanceInsights(tufOperations)
        };
    }

    private static string FormatDataSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes}B",
            < 1024 * 1024 => $"{bytes / 1024}KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024 * 1024)}MB",
            _ => $"{bytes / (1024 * 1024 * 1024)}GB"
        };
    }

    private static CategoryPerformance AnalyzeCategory(List<OperationSummary> operations, string categoryName)
    {
        if (operations.Count == 0)
        {
            return new CategoryPerformance
            {
                CategoryName = categoryName,
                TotalOperations = 0,
                AverageDuration = TimeSpan.Zero,
                TotalMemoryAllocated = 0,
                SuccessRate = 100.0,
                Recommendations = new[] { $"No {categoryName.ToLower()} operations measured" }
            };
        }

        var totalCount = operations.Sum(s => s.TotalCount);
        var successCount = operations.Sum(s => s.SuccessCount);
        var avgDuration = TimeSpan.FromTicks((long)operations.Average(s => s.AverageDuration.Ticks));
        var totalMemory = operations.Sum(s => s.TotalMemoryAllocated);
        var successRate = totalCount > 0 ? (successCount * 100.0) / totalCount : 100.0;

        var recommendations = new List<string>();
        
        // Generate performance recommendations
        if (avgDuration > TimeSpan.FromMilliseconds(100))
        {
            recommendations.Add($"{categoryName} operations are slower than expected (>{avgDuration.TotalMilliseconds:F1}ms average). Consider optimization.");
        }
        
        if (totalMemory > 1024 * 1024) // > 1MB
        {
            recommendations.Add($"{categoryName} operations allocate significant memory ({FormatDataSize(totalMemory)} total). Consider memory optimization.");
        }
        
        if (successRate < 95.0)
        {
            recommendations.Add($"{categoryName} success rate is low ({successRate:F1}%). Investigate failure causes.");
        }

        var fastestOperation = operations.MinBy(s => s.AverageDuration);
        var slowestOperation = operations.MaxBy(s => s.AverageDuration);

        if (fastestOperation != null && slowestOperation != null && operations.Count > 1)
        {
            var performanceSpread = slowestOperation.AverageDuration - fastestOperation.AverageDuration;
            if (performanceSpread > TimeSpan.FromMilliseconds(50))
            {
                recommendations.Add($"Significant performance variation in {categoryName} operations. Fastest: {fastestOperation.OperationName} ({fastestOperation.AverageDuration.TotalMilliseconds:F1}ms), Slowest: {slowestOperation.OperationName} ({slowestOperation.AverageDuration.TotalMilliseconds:F1}ms)");
            }
        }

        return new CategoryPerformance
        {
            CategoryName = categoryName,
            TotalOperations = totalCount,
            AverageDuration = avgDuration,
            TotalMemoryAllocated = totalMemory,
            SuccessRate = successRate,
            Recommendations = recommendations,
            BestPerformingOperation = fastestOperation?.OperationName,
            WorstPerformingOperation = slowestOperation?.OperationName
        };
    }

    private static List<string> GeneratePerformanceInsights(List<OperationSummary> operations)
    {
        var insights = new List<string>();

        if (operations.Count == 0)
        {
            insights.Add("No TUF operations measured - enable performance monitoring to gain insights");
            return insights;
        }

        // Overall performance insights
        var totalOps = operations.Sum(s => s.TotalCount);
        var avgDuration = TimeSpan.FromTicks((long)operations.Average(s => s.AverageDuration.Ticks));
        var totalMemory = operations.Sum(s => s.TotalMemoryAllocated);

        insights.Add($"Measured {totalOps} TUF operations across {operations.Count} operation types");
        insights.Add($"Average operation duration: {avgDuration.TotalMilliseconds:F2}ms");
        insights.Add($"Total memory allocated: {FormatDataSize(totalMemory)}");

        // Performance bottleneck detection
        var slowestOp = operations.MaxBy(s => s.AverageDuration);
        if (slowestOp != null && slowestOp.AverageDuration > TimeSpan.FromMilliseconds(10))
        {
            insights.Add($"Performance bottleneck detected: {slowestOp.OperationName} ({slowestOp.AverageDuration.TotalMilliseconds:F1}ms average)");
        }

        // Memory usage analysis
        var highMemoryOp = operations.MaxBy(s => s.AverageMemoryAllocated);
        if (highMemoryOp != null && highMemoryOp.AverageMemoryAllocated > 1024 * 100) // > 100KB
        {
            insights.Add($"High memory usage detected: {highMemoryOp.OperationName} (avg {FormatDataSize((long)highMemoryOp.AverageMemoryAllocated)} per operation)");
        }

        // Success rate analysis
        var problemOps = operations.Where(s => s.SuccessRate < 100.0).ToList();
        if (problemOps.Count > 0)
        {
            insights.Add($"Reliability concerns: {problemOps.Count} operation types have failures");
            foreach (var op in problemOps.Take(3)) // Show top 3 problematic operations
            {
                insights.Add($"  - {op.OperationName}: {op.SuccessRate:F1}% success rate ({op.FailureCount}/{op.TotalCount} failures)");
            }
        }

        return insights;
    }
}

/// <summary>
/// Comprehensive performance analysis specifically for TUF operations.
/// Provides insights into different categories of TUF performance.
/// </summary>
public sealed record TufPerformanceAnalysis
{
    /// <summary>
    /// The base performance report.
    /// </summary>
    public required PerformanceReport BaseReport { get; init; }

    /// <summary>
    /// Total number of TUF-specific operations measured.
    /// </summary>
    public int TotalTufOperations { get; init; }

    /// <summary>
    /// Performance analysis for signing operations.
    /// </summary>
    public CategoryPerformance SigningPerformance { get; init; } = new();

    /// <summary>
    /// Performance analysis for signature verification operations.
    /// </summary>
    public CategoryPerformance VerificationPerformance { get; init; } = new();

    /// <summary>
    /// Performance analysis for serialization/deserialization operations.
    /// </summary>
    public CategoryPerformance SerializationPerformance { get; init; } = new();

    /// <summary>
    /// Performance analysis for network operations (refresh, download).
    /// </summary>
    public CategoryPerformance NetworkPerformance { get; init; } = new();

    /// <summary>
    /// High-level performance insights and recommendations.
    /// </summary>
    public IReadOnlyList<string> PerformanceInsights { get; init; } = new List<string>();
}

/// <summary>
/// Performance analysis for a specific category of TUF operations.
/// </summary>
public sealed record CategoryPerformance
{
    /// <summary>
    /// Name of the performance category.
    /// </summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>
    /// Total number of operations in this category.
    /// </summary>
    public int TotalOperations { get; init; }

    /// <summary>
    /// Average duration across all operations in this category.
    /// </summary>
    public TimeSpan AverageDuration { get; init; }

    /// <summary>
    /// Total memory allocated by operations in this category.
    /// </summary>
    public long TotalMemoryAllocated { get; init; }

    /// <summary>
    /// Success rate across all operations in this category.
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Performance recommendations for this category.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = new List<string>();

    /// <summary>
    /// Name of the best performing operation in this category.
    /// </summary>
    public string? BestPerformingOperation { get; init; }

    /// <summary>
    /// Name of the worst performing operation in this category.
    /// </summary>
    public string? WorstPerformingOperation { get; init; }
}