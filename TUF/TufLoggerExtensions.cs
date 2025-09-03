using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TUF.Exceptions;

namespace TUF;

/// <summary>
/// High-performance logging extensions for TUF operations using LoggerMessage source generator
/// </summary>
internal static partial class TufLoggerExtensions
{
    // Configuration and Validation Events (EventId: 1000-1099)
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Initializing TUF updater with repository: {RepositoryUrl}")]
    public static partial void LogUpdaterInitialized(this ILogger logger, string repositoryUrl);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "TUF configuration validation failed for property {Property}: {ValidationError}")]
    public static partial void LogConfigurationValidationFailed(this ILogger logger, string? property, string validationError);

    // Metadata Processing Events (EventId: 1100-1199)
    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Debug,
        Message = "Loading {MetadataType} metadata from local cache")]
    public static partial void LogLoadingMetadataFromCache(this ILogger logger, string metadataType);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Debug,
        Message = "Fetching {MetadataType} metadata from repository")]
    public static partial void LogFetchingMetadataFromRepository(this ILogger logger, string metadataType);

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Information,
        Message = "Successfully loaded {MetadataType} metadata version {Version}")]
    public static partial void LogMetadataLoaded(this ILogger logger, string metadataType, int version);

    [LoggerMessage(
        EventId = 1104,
        Level = LogLevel.Error,
        Message = "Failed to deserialize {MetadataType} metadata: {Error}")]
    public static partial void LogMetadataDeserializationFailed(this ILogger logger, string? metadataType, string error, Exception exception);

    [LoggerMessage(
        EventId = 1105,
        Level = LogLevel.Error,
        Message = "Metadata validation failed for {MetadataType}: {ValidationError}")]
    public static partial void LogMetadataValidationFailed(this ILogger logger, string? metadataType, string validationError);

    [LoggerMessage(
        EventId = 1106,
        Level = LogLevel.Warning,
        Message = "{MetadataType} metadata expired at {ExpiryTime}")]
    public static partial void LogMetadataExpired(this ILogger logger, string? metadataType, DateTime? expiryTime);

    // Security Events (EventId: 1200-1299)
    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Critical,
        Message = "SECURITY ALERT: Rollback attack detected for {MetadataType} - expected version {ExpectedVersion}, got version {ActualVersion}")]
    public static partial void LogRollbackAttackDetected(this ILogger logger, string? metadataType, int expectedVersion, int actualVersion);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Error,
        Message = "Signature verification failed for {MetadataType} with key {KeyId}: {Error}")]
    public static partial void LogSignatureVerificationFailed(this ILogger logger, string? metadataType, string? keyId, string error);

    [LoggerMessage(
        EventId = 1203,
        Level = LogLevel.Warning,
        Message = "Insufficient signatures for {MetadataType} role {RoleName}: {ValidSignatures} valid of {RequiredSignatures} required")]
    public static partial void LogInsufficientSignatures(this ILogger logger, string metadataType, string? roleName, int validSignatures, int requiredSignatures);

    [LoggerMessage(
        EventId = 1204,
        Level = LogLevel.Critical,
        Message = "SECURITY ALERT: Target file integrity check failed for {TargetPath} - expected {ExpectedHash}, got {ActualHash}")]
    public static partial void LogTargetIntegrityFailure(this ILogger logger, string? targetPath, string? expectedHash, string? actualHash);

    [LoggerMessage(
        EventId = 1205,
        Level = LogLevel.Information,
        Message = "Successfully verified signature for {MetadataType} with key {KeyId}")]
    public static partial void LogSignatureVerificationSuccess(this ILogger logger, string? metadataType, string? keyId);

    // Network and Repository Events (EventId: 1300-1399)
    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Debug,
        Message = "HTTP request to {RepositoryUrl} - Method: {Method}, Path: {Path}")]
    public static partial void LogHttpRequest(this ILogger logger, string repositoryUrl, string method, string path);

    [LoggerMessage(
        EventId = 1302,
        Level = LogLevel.Error,
        Message = "Repository network error for {RepositoryUrl}: {StatusCode} {Error}")]
    public static partial void LogRepositoryNetworkError(this ILogger logger, Uri? repositoryUrl, int? statusCode, string error, Exception exception);

    [LoggerMessage(
        EventId = 1303,
        Level = LogLevel.Warning,
        Message = "Repository operation timed out for {RepositoryUrl} after {TimeoutMs}ms")]
    public static partial void LogRepositoryTimeout(this ILogger logger, Uri? repositoryUrl, int timeoutMs);

    // HTTP Resilience Events (EventId: 1350-1399)
    [LoggerMessage(
        EventId = 1351,
        Level = LogLevel.Debug,
        Message = "HTTP request attempt {AttemptNumber}/{MaxAttempts} for {Uri}")]
    public static partial void LogHttpRequestAttempt(this ILogger logger, Uri uri, int attemptNumber, int maxAttempts);

    [LoggerMessage(
        EventId = 1352,
        Level = LogLevel.Debug,
        Message = "HTTP request succeeded for {Uri} on attempt {AttemptNumber} ({ResponseSize} bytes)")]
    public static partial void LogHttpRequestSuccess(this ILogger logger, Uri uri, int attemptNumber, int responseSize);

    [LoggerMessage(
        EventId = 1353,
        Level = LogLevel.Warning,
        Message = "HTTP request failed for {Uri}, retrying in {Delay} (attempt {AttemptNumber})")]
    public static partial void LogHttpRetryDelay(this ILogger logger, Uri uri, int attemptNumber, TimeSpan delay);

    [LoggerMessage(
        EventId = 1354,
        Level = LogLevel.Error,
        Message = "HTTP request failed for {Uri} after {TotalAttempts} attempts")]
    public static partial void LogHttpRequestFailed(this ILogger logger, Uri uri, int totalAttempts);

    [LoggerMessage(
        EventId = 1355,
        Level = LogLevel.Warning,
        Message = "HTTP request to {Uri} timed out after {Timeout}")]
    public static partial void LogHttpRequestTimeout(this ILogger logger, Uri uri, TimeSpan timeout);

    [LoggerMessage(
        EventId = 1356,
        Level = LogLevel.Debug,
        Message = "HTTP request to {Uri} was cancelled")]
    public static partial void LogHttpRequestCancelled(this ILogger logger, Uri uri);

    [LoggerMessage(
        EventId = 1357,
        Level = LogLevel.Warning,
        Message = "HTTP request error for {Uri}")]
    public static partial void LogHttpRequestError(this ILogger logger, Uri uri, Exception exception);

    // Target and File Events (EventId: 1400-1499)
    [LoggerMessage(
        EventId = 1401,
        Level = LogLevel.Debug,
        Message = "Resolving target file: {TargetPath}")]
    public static partial void LogTargetResolution(this ILogger logger, string targetPath);

    [LoggerMessage(
        EventId = 1402,
        Level = LogLevel.Error,
        Message = "Target file not found: {TargetPath}")]
    public static partial void LogTargetNotFound(this ILogger logger, string? targetPath);

    [LoggerMessage(
        EventId = 1403,
        Level = LogLevel.Warning,
        Message = "File size limit exceeded for {FilePath}: {ActualSize} bytes > {MaxSize} bytes")]
    public static partial void LogFileSizeExceeded(this ILogger logger, string? filePath, long actualSize, long maxSize);

    [LoggerMessage(
        EventId = 1404,
        Level = LogLevel.Information,
        Message = "Successfully downloaded target {TargetPath} ({SizeBytes} bytes)")]
    public static partial void LogTargetDownloadSuccess(this ILogger logger, string targetPath, long sizeBytes);

    // Delegation Events (EventId: 1500-1599)
    [LoggerMessage(
        EventId = 1501,
        Level = LogLevel.Debug,
        Message = "Resolving delegation for role {RoleName} at depth {DelegationDepth}")]
    public static partial void LogDelegationResolution(this ILogger logger, string? roleName, int delegationDepth);

    [LoggerMessage(
        EventId = 1502,
        Level = LogLevel.Warning,
        Message = "Maximum delegation depth {MaxDepth} exceeded (current: {CurrentDepth}) for role {RoleName}")]
    public static partial void LogMaxDelegationDepthExceeded(this ILogger logger, int maxDepth, int currentDepth, string? roleName);

    [LoggerMessage(
        EventId = 1503,
        Level = LogLevel.Error,
        Message = "Delegation resolution failed for role {RoleName}: {Error}")]
    public static partial void LogDelegationResolutionFailed(this ILogger logger, string? roleName, string error);

    // Performance and Diagnostics Events (EventId: 1600-1699)
    [LoggerMessage(
        EventId = 1601,
        Level = LogLevel.Debug,
        Message = "TUF operation {OperationName} completed in {ElapsedMs}ms")]
    public static partial void LogOperationTiming(this ILogger logger, string operationName, long elapsedMs);

    [LoggerMessage(
        EventId = 1602,
        Level = LogLevel.Information,
        Message = "TUF update check completed: {UpdatesAvailable} updates available")]
    public static partial void LogUpdateCheckCompleted(this ILogger logger, int updatesAvailable);

    [LoggerMessage(
        EventId = 1603,
        Level = LogLevel.Debug,
        Message = "Cache statistics: {CacheHits} hits, {CacheMisses} misses, {CacheSize} items")]
    public static partial void LogCacheStatistics(this ILogger logger, int cacheHits, int cacheMisses, int cacheSize);
}

/// <summary>
/// Helper methods for logging TUF exceptions with proper context
/// </summary>
public static class TufExceptionLoggingExtensions
{
    /// <summary>
    /// Log TUF exceptions with appropriate severity and context
    /// </summary>
    public static void LogTufException(this ILogger logger, TufException exception)
    {
        switch (exception)
        {
            case RollbackAttackException rollbackEx:
                logger.LogRollbackAttackDetected(rollbackEx.MetadataType, rollbackEx.ExpectedVersion, rollbackEx.ActualVersion);
                break;

            case TargetIntegrityException integrityEx:
                logger.LogTargetIntegrityFailure(integrityEx.TargetPath, integrityEx.ExpectedHash, integrityEx.ActualHash);
                break;

            case SignatureVerificationException sigEx:
                logger.LogSignatureVerificationFailed(sigEx.MetadataType, sigEx.KeyId, sigEx.Message);
                break;

            case InsufficientSignaturesException signaturesEx:
                logger.LogInsufficientSignatures("metadata", signaturesEx.RoleName, signaturesEx.ValidSignatures, signaturesEx.RequiredSignatures);
                break;

            case MetadataDeserializationException deserializationEx:
                logger.LogMetadataDeserializationFailed(deserializationEx.MetadataType, deserializationEx.Message, deserializationEx);
                break;

            case MetadataValidationException validationEx:
                logger.LogMetadataValidationFailed(validationEx.MetadataType, validationEx.Message);
                break;

            case ExpiredMetadataException expiredEx:
                logger.LogMetadataExpired(expiredEx.MetadataType, expiredEx.ExpiryTime);
                break;

            case RepositoryNetworkException networkEx:
                logger.LogRepositoryNetworkError(networkEx.RepositoryUri, (int?)networkEx.StatusCode, networkEx.Message, networkEx);
                break;

            case TargetNotFoundException targetEx:
                logger.LogTargetNotFound(targetEx.TargetPath);
                break;

            case FileSizeException fileSizeEx:
                logger.LogFileSizeExceeded(fileSizeEx.FilePath, fileSizeEx.ActualSize, fileSizeEx.MaxAllowedSize);
                break;

            case MaxDelegationDepthExceededException maxDepthEx:
                logger.LogMaxDelegationDepthExceeded(maxDepthEx.MaxDepth, maxDepthEx.DelegationDepth, maxDepthEx.RoleName);
                break;

            case DelegationException delegationEx:
                logger.LogDelegationResolutionFailed(delegationEx.RoleName, delegationEx.Message);
                break;

            case TufConfigurationException configEx:
                logger.LogConfigurationValidationFailed(configEx.ConfigurationProperty, configEx.Message);
                break;

            default:
                logger.LogError(exception, "TUF operation failed: {ExceptionType}", exception.GetType().Name);
                break;
        }
    }
}

/// <summary>
/// Activity source for TUF operations to support System.Diagnostics tracing
/// </summary>
public static class TufActivitySource
{
    /// <summary>
    /// The activity source name for TUF operations
    /// </summary>
    public const string Name = "TUF.NET";

    /// <summary>
    /// The activity source version
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// The activity source instance for TUF operations
    /// </summary>
    public static readonly ActivitySource Instance = new(Name, Version);

    /// <summary>
    /// Start a TUF operation activity
    /// </summary>
    public static Activity? StartActivity(string operationName)
    {
        return Instance.StartActivity($"TUF.{operationName}");
    }
}