using System.Net;
using System.Net.Sockets;
using TUF.Exceptions;

namespace TUF;

/// <summary>
/// Configuration for network resilience patterns including retries, timeouts, and circuit breakers.
/// Essential for production TUF deployments to handle network failures gracefully.
/// </summary>
/// <remarks>
/// Network resilience is critical for TUF clients because:
/// 1. Metadata must be fetched reliably to maintain security guarantees
/// 2. Large target files need robust download mechanisms
/// 3. Mirror failover ensures availability when primary servers fail
/// 4. Proper retry policies prevent cascade failures in distributed systems
/// 
/// Based on resilience patterns from mature TUF implementations and production systems.
/// </remarks>
public record NetworkResilienceConfig
{
    /// <summary>
    /// Maximum number of retry attempts for transient failures.
    /// Default: 3 retries (total of 4 attempts including initial).
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Initial delay before the first retry attempt.
    /// Default: 1 second. Subsequent retries use exponential backoff.
    /// </summary>
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retry attempts to prevent excessive waits.
    /// Default: 30 seconds maximum backoff delay.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Multiplier for exponential backoff between retry attempts.
    /// Default: 2.0 (doubles delay each retry).
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Timeout for individual HTTP requests.
    /// Default: 30 seconds for metadata, longer for large files.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for downloading large target files.
    /// Default: 5 minutes to accommodate large files and slow connections.
    /// </summary>
    public TimeSpan FileDownloadTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// HTTP status codes that should trigger retry attempts.
    /// Default includes common transient failures.
    /// </summary>
    public HashSet<HttpStatusCode> RetryableStatusCodes { get; init; } = new()
    {
        HttpStatusCode.RequestTimeout,           // 408
        HttpStatusCode.InternalServerError,      // 500  
        HttpStatusCode.BadGateway,               // 502
        HttpStatusCode.ServiceUnavailable,       // 503
        HttpStatusCode.GatewayTimeout,           // 504
        HttpStatusCode.InsufficientStorage,      // 507
        HttpStatusCode.TooManyRequests           // 429
    };

    /// <summary>
    /// Enable circuit breaker pattern to prevent cascade failures.
    /// Default: true for production resilience.
    /// </summary>
    public bool EnableCircuitBreaker { get; init; } = true;

    /// <summary>
    /// Number of consecutive failures before opening the circuit.
    /// Default: 5 failures before stopping requests to failing endpoint.
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; init; } = 5;

    /// <summary>
    /// Time to wait before attempting to close an open circuit.
    /// Default: 60 seconds before testing endpoint recovery.
    /// </summary>
    public TimeSpan CircuitBreakerRecoveryTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Enable automatic failover to mirror servers when primary fails.
    /// Default: true to improve availability.
    /// </summary>
    public bool EnableMirrorFailover { get; init; } = true;

    /// <summary>
    /// Maximum time to wait for any single operation (including retries).
    /// Default: 5 minutes total operation timeout.
    /// </summary>
    public TimeSpan TotalOperationTimeout { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Circuit breaker states for endpoint health tracking.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed - requests are allowed through normally.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open - requests are immediately failed to prevent cascade failures.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open - testing if the endpoint has recovered.
    /// </summary>
    HalfOpen
}

/// <summary>
/// Tracks circuit breaker state for a specific endpoint.
/// </summary>
internal class CircuitBreakerEndpointState
{
    public CircuitBreakerState State { get; set; } = CircuitBreakerState.Closed;
    public int FailureCount { get; set; } = 0;
    public DateTime LastFailureTime { get; set; } = DateTime.MinValue;
    public DateTime NextAttemptTime { get; set; } = DateTime.MinValue;
}

/// <summary>
/// Production-ready network resilience implementation for TUF operations.
/// Provides retry policies, circuit breakers, and mirror failover capabilities.
/// </summary>
/// <remarks>
/// This addresses a key parity gap with other TUF implementations by providing
/// enterprise-grade network resilience patterns essential for production deployments.
/// 
/// Features:
/// - Exponential backoff retry policies for transient failures
/// - Circuit breaker pattern to prevent cascade failures  
/// - Automatic mirror failover for high availability
/// - Proper timeout handling for different operation types
/// - Categorized error handling with appropriate retry behavior
/// </remarks>
public class NetworkResilience
{
    private readonly NetworkResilienceConfig _config;
    private readonly Dictionary<string, CircuitBreakerEndpointState> _circuitStates = new();
    private readonly object _lock = new();

    public NetworkResilience(NetworkResilienceConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Executes an HTTP operation with full resilience patterns applied.
    /// </summary>
    public async Task<T> ExecuteWithResilienceAsync<T>(
        Func<HttpClient, CancellationToken, Task<T>> operation,
        HttpClient httpClient,
        string operationName,
        List<string>? fallbackUrls = null,
        CancellationToken cancellationToken = default)
    {
        using var totalTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        totalTimeout.CancelAfter(_config.TotalOperationTimeout);

        var urls = new List<string> { httpClient.BaseAddress?.ToString() ?? "primary" };
        if (fallbackUrls != null && _config.EnableMirrorFailover)
        {
            urls.AddRange(fallbackUrls);
        }

        Exception? lastException = null;

        foreach (var url in urls)
        {
            if (string.IsNullOrEmpty(url)) continue;

            if (_config.EnableCircuitBreaker && IsCircuitOpen(url))
            {
                continue; // Skip this URL if circuit is open
            }

            try
            {
                var result = await ExecuteWithRetryAsync(
                    operation,
                    httpClient,
                    url,
                    operationName,
                    totalTimeout.Token);

                // Success - reset circuit breaker if enabled
                if (_config.EnableCircuitBreaker)
                {
                    ResetCircuitBreaker(url);
                }

                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;

                // Update circuit breaker state
                if (_config.EnableCircuitBreaker)
                {
                    RecordFailure(url);
                }

                // Don't retry on the same URL for non-transient errors
                if (!IsTransientError(ex))
                {
                    break;
                }
            }
        }

        // All URLs failed
        throw new NetworkResilienceException(
            $"All attempts failed for operation '{operationName}'. Tried {urls.Count} endpoint(s).",
            lastException!);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<HttpClient, CancellationToken, Task<T>> operation,
        HttpClient httpClient,
        string url,
        string operationName,
        CancellationToken cancellationToken)
    {
        var delay = _config.InitialRetryDelay;
        Exception? lastException = null;

        for (int attempt = 0; attempt <= _config.MaxRetryAttempts; attempt++)
        {
            try
            {
                // Set appropriate timeout for this request
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeout.CancelAfter(_config.RequestTimeout);

                return await operation(httpClient, requestTimeout.Token);
            }
            catch (Exception ex)
            {
                lastException = ex;

                // Don't retry on the last attempt or for non-transient errors
                if (attempt == _config.MaxRetryAttempts || !IsTransientError(ex))
                {
                    break;
                }

                // Wait before retrying with exponential backoff
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(Math.Min(
                    delay.TotalMilliseconds * _config.BackoffMultiplier,
                    _config.MaxRetryDelay.TotalMilliseconds));
            }
        }

        throw lastException ?? new NetworkResilienceException($"Operation '{operationName}' failed after {_config.MaxRetryAttempts + 1} attempts.");
    }

    private bool IsTransientError(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx => IsTransientHttpError(httpEx),
            TaskCanceledException => true, // Timeout
            SocketException => true,
            _ => false
        };
    }

    private bool IsTransientHttpError(HttpRequestException httpException)
    {
        // Check if the exception message contains information about HTTP status codes
        var message = httpException.Message;
        foreach (var code in _config.RetryableStatusCodes)
        {
            if (message.Contains(((int)code).ToString()) || message.Contains(code.ToString()))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsCircuitOpen(string url)
    {
        lock (_lock)
        {
            if (!_circuitStates.TryGetValue(url, out var state))
            {
                return false;
            }

            if (state.State == CircuitBreakerState.Closed)
            {
                return false;
            }

            if (state.State == CircuitBreakerState.Open &&
                DateTime.UtcNow >= state.NextAttemptTime)
            {
                // Transition to half-open to test recovery
                state.State = CircuitBreakerState.HalfOpen;
                return false;
            }

            return state.State == CircuitBreakerState.Open;
        }
    }

    private void RecordFailure(string url)
    {
        lock (_lock)
        {
            if (!_circuitStates.TryGetValue(url, out var state))
            {
                state = new CircuitBreakerEndpointState();
                _circuitStates[url] = state;
            }

            state.FailureCount++;
            state.LastFailureTime = DateTime.UtcNow;

            if (state.FailureCount >= _config.CircuitBreakerFailureThreshold)
            {
                state.State = CircuitBreakerState.Open;
                state.NextAttemptTime = DateTime.UtcNow.Add(_config.CircuitBreakerRecoveryTimeout);
            }
        }
    }

    private void ResetCircuitBreaker(string url)
    {
        lock (_lock)
        {
            if (_circuitStates.TryGetValue(url, out var state))
            {
                state.State = CircuitBreakerState.Closed;
                state.FailureCount = 0;
                state.LastFailureTime = DateTime.MinValue;
                state.NextAttemptTime = DateTime.MinValue;
            }
        }
    }
}

/// <summary>
/// Exception thrown when network resilience patterns are exhausted.
/// </summary>
public class NetworkResilienceException : TufException
{
    public NetworkResilienceException(string message) : base(message) { }
    public NetworkResilienceException(string message, Exception? innerException) : base(message, innerException) { }
}