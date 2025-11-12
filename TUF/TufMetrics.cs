using System.Diagnostics.Metrics;

namespace TUF;

/// <summary>
/// OpenTelemetry metrics for TUF operations providing production monitoring capabilities
/// </summary>
public static class TufMetrics
{
    /// <summary>
    /// The meter name for TUF operations
    /// </summary>
    public const string MeterName = "TUF.NET";

    /// <summary>
    /// The meter version
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// The meter instance for TUF operations
    /// </summary>
    public static readonly Meter Instance = new(MeterName, Version);

    // Repository and Client Metrics
    
    /// <summary>
    /// Counter for total TUF operations by operation type and status
    /// </summary>
    public static readonly Counter<long> OperationsTotal = Instance.CreateCounter<long>(
        "tuf_operations_total",
        "operation",
        "Total number of TUF operations");

    /// <summary>
    /// Histogram for TUF operation duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> OperationDuration = Instance.CreateHistogram<double>(
        "tuf_operation_duration_ms",
        "ms",
        "Duration of TUF operations in milliseconds");

    // Metadata Processing Metrics

    /// <summary>
    /// Counter for metadata operations (load, validation, update)
    /// </summary>
    public static readonly Counter<long> MetadataOperations = Instance.CreateCounter<long>(
        "tuf_metadata_operations_total",
        "operation",
        "Total metadata operations by type and status");

    /// <summary>
    /// Histogram for metadata processing time
    /// </summary>
    public static readonly Histogram<double> MetadataProcessingTime = Instance.CreateHistogram<double>(
        "tuf_metadata_processing_duration_ms",
        "ms",
        "Time spent processing metadata in milliseconds");

    /// <summary>
    /// Gauge for metadata cache size
    /// </summary>
    public static readonly UpDownCounter<int> MetadataCacheSize = Instance.CreateUpDownCounter<int>(
        "tuf_metadata_cache_size",
        "items",
        "Number of metadata items in cache");

    /// <summary>
    /// Counter for metadata cache hits and misses
    /// </summary>
    public static readonly Counter<long> MetadataCacheOperations = Instance.CreateCounter<long>(
        "tuf_metadata_cache_operations_total",
        "operation",
        "Total metadata cache operations");

    // Signature Verification Metrics

    /// <summary>
    /// Counter for signature verification attempts
    /// </summary>
    public static readonly Counter<long> SignatureVerifications = Instance.CreateCounter<long>(
        "tuf_signature_verifications_total",
        "verification",
        "Total signature verification attempts");

    /// <summary>
    /// Histogram for signature verification time
    /// </summary>
    public static readonly Histogram<double> SignatureVerificationTime = Instance.CreateHistogram<double>(
        "tuf_signature_verification_duration_ms",
        "ms",
        "Time spent verifying signatures in milliseconds");

    // Network and Repository Access Metrics

    /// <summary>
    /// Counter for network requests to TUF repositories
    /// </summary>
    public static readonly Counter<long> NetworkRequests = Instance.CreateCounter<long>(
        "tuf_network_requests_total",
        "request",
        "Total network requests to TUF repositories");

    /// <summary>
    /// Histogram for network request duration
    /// </summary>
    public static readonly Histogram<double> NetworkRequestDuration = Instance.CreateHistogram<double>(
        "tuf_network_request_duration_ms",
        "ms",
        "Duration of network requests in milliseconds");

    /// <summary>
    /// Counter for network request bytes
    /// </summary>
    public static readonly Counter<long> NetworkBytes = Instance.CreateCounter<long>(
        "tuf_network_bytes_total",
        "byte",
        "Total bytes transferred in network requests");

    // Security and Error Metrics

    /// <summary>
    /// Counter for security-related events (rollback attacks, integrity failures, etc.)
    /// </summary>
    public static readonly Counter<long> SecurityEvents = Instance.CreateCounter<long>(
        "tuf_security_events_total",
        "event",
        "Total security-related events");

    /// <summary>
    /// Counter for TUF errors by category
    /// </summary>
    public static readonly Counter<long> Errors = Instance.CreateCounter<long>(
        "tuf_errors_total",
        "error",
        "Total TUF errors by category");

    // Target and File Metrics

    /// <summary>
    /// Counter for target file operations (downloads, verifications)
    /// </summary>
    public static readonly Counter<long> TargetOperations = Instance.CreateCounter<long>(
        "tuf_target_operations_total",
        "operation",
        "Total target file operations");

    /// <summary>
    /// Histogram for target file download size
    /// </summary>
    public static readonly Histogram<long> TargetFileSize = Instance.CreateHistogram<long>(
        "tuf_target_file_size_bytes",
        "byte",
        "Size of target files in bytes");

    /// <summary>
    /// Counter for delegation operations
    /// </summary>
    public static readonly Counter<long> DelegationOperations = Instance.CreateCounter<long>(
        "tuf_delegation_operations_total",
        "operation",
        "Total delegation operations");

    /// <summary>
    /// Histogram for delegation resolution depth
    /// </summary>
    public static readonly Histogram<int> DelegationDepth = Instance.CreateHistogram<int>(
        "tuf_delegation_depth",
        "level",
        "Delegation resolution depth");

    // Update and Repository Health Metrics

    /// <summary>
    /// Counter for TUF update checks
    /// </summary>
    public static readonly Counter<long> UpdateChecks = Instance.CreateCounter<long>(
        "tuf_update_checks_total",
        "check",
        "Total TUF update checks performed");

    /// <summary>
    /// Gauge for available updates count
    /// </summary>
    public static readonly UpDownCounter<int> AvailableUpdates = Instance.CreateUpDownCounter<int>(
        "tuf_available_updates",
        "update",
        "Number of available updates");

    /// <summary>
    /// Histogram for repository health check duration
    /// </summary>
    public static readonly Histogram<double> RepositoryHealthCheckDuration = Instance.CreateHistogram<double>(
        "tuf_repository_health_check_duration_ms",
        "ms",
        "Duration of repository health checks in milliseconds");
}

/// <summary>
/// Helper class for recording TUF metrics with proper tags and context
/// </summary>
public static class TufMetricsRecorder
{
    /// <summary>
    /// Record a TUF operation with timing and status
    /// </summary>
    public static void RecordOperation(string operationType, string status, double durationMs, params KeyValuePair<string, object?>[] additionalTags)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("operation.type", operationType),
            new("operation.status", status)
        };
        
        if (additionalTags.Length > 0)
        {
            tags.AddRange(additionalTags);
        }

        TufMetrics.OperationsTotal.Add(1, tags.ToArray());
        TufMetrics.OperationDuration.Record(durationMs, tags.ToArray());
    }

    /// <summary>
    /// Record metadata processing metrics
    /// </summary>
    public static void RecordMetadataOperation(string metadataType, string operation, string status, double processingTimeMs)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("metadata.type", metadataType),
            new("metadata.operation", operation),
            new("metadata.status", status)
        };

        TufMetrics.MetadataOperations.Add(1, tags);
        TufMetrics.MetadataProcessingTime.Record(processingTimeMs, tags);
    }

    /// <summary>
    /// Record signature verification metrics
    /// </summary>
    public static void RecordSignatureVerification(string keyType, string algorithm, bool success, double verificationTimeMs)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("key.type", keyType),
            new("signature.algorithm", algorithm),
            new("verification.result", success ? "success" : "failure")
        };

        TufMetrics.SignatureVerifications.Add(1, tags);
        TufMetrics.SignatureVerificationTime.Record(verificationTimeMs, tags);
    }

    /// <summary>
    /// Record network request metrics
    /// </summary>
    public static void RecordNetworkRequest(string method, string endpoint, int statusCode, long responseBytes, double durationMs)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("http.method", method),
            new("http.endpoint", endpoint),
            new("http.status_code", statusCode),
            new("request.result", statusCode >= 200 && statusCode < 300 ? "success" : "failure")
        };

        TufMetrics.NetworkRequests.Add(1, tags);
        TufMetrics.NetworkRequestDuration.Record(durationMs, tags);
        TufMetrics.NetworkBytes.Add(responseBytes, new KeyValuePair<string, object?>[] 
        { 
            new("direction", "response") 
        });
    }

    /// <summary>
    /// Record security event
    /// </summary>
    public static void RecordSecurityEvent(string eventType, string severity, string metadataType = "unknown")
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("security.event_type", eventType),
            new("security.severity", severity),
            new("metadata.type", metadataType)
        };

        TufMetrics.SecurityEvents.Add(1, tags);
    }

    /// <summary>
    /// Record error metrics
    /// </summary>
    public static void RecordError(string errorType, string errorCategory, string operation)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("error.type", errorType),
            new("error.category", errorCategory),
            new("operation.type", operation)
        };

        TufMetrics.Errors.Add(1, tags);
    }

    /// <summary>
    /// Record target file operation
    /// </summary>
    public static void RecordTargetOperation(string operation, string status, long fileSizeBytes, string targetPath)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("target.operation", operation),
            new("target.status", status),
            new("target.path", targetPath)
        };

        TufMetrics.TargetOperations.Add(1, tags);
        if (fileSizeBytes > 0)
        {
            TufMetrics.TargetFileSize.Record(fileSizeBytes, tags);
        }
    }
}