using System.Net;
using TUF.Exceptions;
using Microsoft.Extensions.Logging;

namespace TUF.Http;

/// <summary>
/// Configuration for resilient HTTP operations
/// </summary>
public class HttpResilienceConfig
{
    /// <summary>
    /// Maximum number of retry attempts for failed requests
    /// </summary>
    public int MaxRetries { get; init; } = 3;
    
    /// <summary>
    /// Base delay between retry attempts (exponential backoff will multiply this)
    /// </summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// Maximum delay between retry attempts
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Request timeout for individual HTTP requests
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// User agent string for HTTP requests
    /// </summary>
    public string UserAgent { get; init; } = "TUF-DotNet/1.0";
    
    /// <summary>
    /// HTTP status codes that should trigger a retry
    /// </summary>
    public HashSet<HttpStatusCode> RetryStatusCodes { get; init; } = new()
    {
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway, 
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests
    };
}

/// <summary>
/// A resilient HTTP client wrapper with retry policies, timeouts, and proper error handling
/// </summary>
public class ResilientHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly HttpResilienceConfig _config;
    private readonly ILogger<ResilientHttpClient>? _logger;

    public ResilientHttpClient(HttpClient httpClient, HttpResilienceConfig config, ILogger<ResilientHttpClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        
        // Configure user agent
        if (!string.IsNullOrEmpty(_config.UserAgent))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_config.UserAgent);
        }
        
        // Configure request timeout
        _httpClient.Timeout = _config.RequestTimeout;
    }

    /// <summary>
    /// Download file with resilient retry logic and proper error handling
    /// </summary>
    public async Task<byte[]> DownloadFileAsync(Uri uri, uint? maxLength, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= _config.MaxRetries)
        {
            try
            {
                _logger?.LogHttpRequestAttempt(uri, attempt + 1, _config.MaxRetries + 1);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_config.RequestTimeout);

                using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                // Check content length before downloading
                var contentLength = response.Content.Headers.ContentLength ?? 0;
                if (maxLength.HasValue && contentLength > maxLength.Value)
                {
                    throw new RepositoryNetworkException(
                        $"File {uri} length {contentLength} exceeds maximum allowed length {maxLength}",
                        uri,
                        response.StatusCode);
                }

                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsByteArrayAsync(cts.Token);
                
                _logger?.LogHttpRequestSuccess(uri, attempt + 1, content.Length);
                
                return content;
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                // User-requested cancellation, don't retry
                _logger?.LogHttpRequestCancelled(uri);
                throw;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                // Timeout from HttpClient, treat as retryable
                lastException = new RepositoryNetworkException(
                    $"Request to {uri} timed out",
                    uri,
                    innerException: ex);
                    
                _logger?.LogHttpRequestTimeout(uri, _config.RequestTimeout);
            }
            catch (OperationCanceledException)
            {
                // Other timeout scenarios, treat as retryable
                lastException = new RepositoryNetworkException(
                    $"Request to {uri} timed out after {_config.RequestTimeout.TotalSeconds} seconds",
                    uri);
                    
                _logger?.LogHttpRequestTimeout(uri, _config.RequestTimeout);
            }
            catch (HttpRequestException ex)
            {
                lastException = new RepositoryNetworkException(
                    $"HTTP request to {uri} failed: {ex.Message}",
                    uri,
                    innerException: ex);
                    
                _logger?.LogHttpRequestError(uri, ex);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogHttpRequestError(uri, ex);
            }

            attempt++;
            
            // Don't delay after the last attempt
            if (attempt <= _config.MaxRetries)
            {
                var delay = CalculateDelay(attempt);
                _logger?.LogHttpRetryDelay(uri, attempt, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // All retries exhausted
        _logger?.LogHttpRequestFailed(uri, _config.MaxRetries + 1);
        
        throw lastException ?? new RepositoryNetworkException(
            $"Request to {uri} failed after {_config.MaxRetries + 1} attempts",
            uri);
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        // Exponential backoff with jitter
        var exponentialDelay = TimeSpan.FromTicks(_config.BaseDelay.Ticks * (1L << (attempt - 1)));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
        var totalDelay = exponentialDelay + jitter;
        
        return totalDelay > _config.MaxDelay ? _config.MaxDelay : totalDelay;
    }
}