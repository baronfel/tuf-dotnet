using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using TUF.Exceptions;

namespace TUF.Resilience;

/// <summary>
/// Retry policy with exponential backoff and jitter for robust handling of transient failures
/// </summary>
public sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly ExponentialBackoffRetryOptions _options;
    private readonly ILogger<ExponentialBackoffRetryPolicy>? _logger;
    private readonly Random _random = new();

    public ExponentialBackoffRetryPolicy(ExponentialBackoffRetryOptions? options = null, ILogger<ExponentialBackoffRetryPolicy>? logger = null)
    {
        _options = options ?? new ExponentialBackoffRetryOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public int MaxAttempts => _options.MaxAttempts;

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                _logger?.LogDebug("Executing operation, attempt {Attempt}/{MaxAttempts}", attempt, _options.MaxAttempts);
                
                using var timeoutCts = new CancellationTokenSource(_options.OperationTimeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                
                var result = await operation(combinedCts.Token);
                
                if (attempt > 1)
                {
                    _logger?.LogInformation("Operation succeeded on attempt {Attempt}", attempt);
                }
                
                return result;
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt) && attempt < _options.MaxAttempts)
            {
                lastException = ex;
                var delay = GetRetryDelay(attempt);
                
                _logger?.LogWarning(ex, "Operation failed on attempt {Attempt}, retrying in {Delay}ms", attempt, delay.TotalMilliseconds);
                
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogDebug("Retry cancelled during delay");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Operation failed on attempt {Attempt} and will not be retried", attempt);
                throw;
            }
        }

        _logger?.LogError(lastException, "Operation failed after {MaxAttempts} attempts", _options.MaxAttempts);
        throw lastException ?? new InvalidOperationException("Operation failed after retries but no exception was captured");
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async ct =>
        {
            await operation(ct);
            return 0; // Return dummy value
        }, cancellationToken);
    }

    /// <inheritdoc />
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        if (attemptNumber >= _options.MaxAttempts)
        {
            return false;
        }

        return exception switch
        {
            // Network-related exceptions are generally retryable
            HttpRequestException => true,
            TaskCanceledException when exception.InnerException is not OperationCanceledException => true, // Timeout
            SocketException => true,
            
            // TUF-specific retryable exceptions
            RepositoryNetworkException repoEx => IsRetryableHttpStatusCode(repoEx.StatusCode),
            
            // Security-related exceptions should NOT be retried
            SignatureVerificationException => false,
            RollbackAttackException => false,
            TargetIntegrityException => false,
            
            // Configuration and validation errors should NOT be retried
            TufConfigurationException => false,
            MetadataValidationException => false,
            
            // Other TUF exceptions - be conservative and don't retry
            TufException => false,
            
            // Default: don't retry unknown exceptions
            _ => false
        };
    }

    /// <inheritdoc />
    public TimeSpan GetRetryDelay(int attemptNumber)
    {
        if (attemptNumber <= 1)
        {
            return TimeSpan.Zero;
        }

        // Exponential backoff: baseDelay * (2 ^ (attempt - 2))
        var exponentialDelay = _options.BaseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 2);
        
        // Cap at maximum delay
        exponentialDelay = Math.Min(exponentialDelay, _options.MaxDelay.TotalMilliseconds);
        
        // Add jitter to avoid thundering herd problem
        if (_options.UseJitter)
        {
            var jitterRange = exponentialDelay * _options.JitterFactor;
            var jitter = (_random.NextDouble() - 0.5) * 2 * jitterRange; // Random between -jitterRange and +jitterRange
            exponentialDelay += jitter;
        }
        
        // Ensure non-negative delay
        exponentialDelay = Math.Max(0, exponentialDelay);
        
        return TimeSpan.FromMilliseconds(exponentialDelay);
    }

    private static bool IsRetryableHttpStatusCode(HttpStatusCode? statusCode)
    {
        return statusCode switch
        {
            // Server errors are generally retryable
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            
            // Too many requests - retryable with backoff
            HttpStatusCode.TooManyRequests => true,
            
            // Request timeout - retryable
            HttpStatusCode.RequestTimeout => true,
            
            // Client errors are generally NOT retryable
            HttpStatusCode.BadRequest => false,
            HttpStatusCode.Unauthorized => false,
            HttpStatusCode.Forbidden => false,
            HttpStatusCode.NotFound => false,
            HttpStatusCode.MethodNotAllowed => false,
            
            // Default: retry server errors (5xx), don't retry client errors (4xx)
            _ => statusCode >= HttpStatusCode.InternalServerError
        };
    }
}

/// <summary>
/// Configuration options for exponential backoff retry policy
/// </summary>
public class ExponentialBackoffRetryOptions
{
    /// <summary>
    /// Maximum number of retry attempts (default: 3)
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Base delay for first retry attempt (default: 1 second)
    /// </summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retry attempts (default: 30 seconds)
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for individual operation attempts (default: 30 seconds)
    /// </summary>
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to add jitter to retry delays (default: true)
    /// </summary>
    public bool UseJitter { get; init; } = true;

    /// <summary>
    /// Jitter factor as percentage of delay (default: 0.1 = 10%)
    /// </summary>
    public double JitterFactor { get; init; } = 0.1;
}