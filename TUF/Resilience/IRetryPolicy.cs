namespace TUF.Resilience;

/// <summary>
/// Interface for retry policies that handle transient failures in TUF operations
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Execute an operation with retry logic
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">Operation to retry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the operation</returns>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute an operation with retry logic (no return value)
    /// </summary>
    /// <param name="operation">Operation to retry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determine if an exception should trigger a retry
    /// </summary>
    /// <param name="exception">Exception that occurred</param>
    /// <param name="attemptNumber">Current attempt number (1-based)</param>
    /// <returns>True if the operation should be retried, false otherwise</returns>
    bool ShouldRetry(Exception exception, int attemptNumber);

    /// <summary>
    /// Calculate delay before next retry attempt
    /// </summary>
    /// <param name="attemptNumber">Current attempt number (1-based)</param>
    /// <returns>Delay before next retry</returns>
    TimeSpan GetRetryDelay(int attemptNumber);

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    int MaxAttempts { get; }
}