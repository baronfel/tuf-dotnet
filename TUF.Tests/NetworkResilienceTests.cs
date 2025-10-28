using System.Net;
using TUF.Exceptions;

namespace TUF.Tests;

/// <summary>
/// Comprehensive tests for network resilience patterns including retry policies,
/// circuit breakers, and mirror failover mechanisms.
/// </summary>
/// <remarks>
/// These tests ensure the .NET TUF implementation has production-ready network
/// resilience comparable to mature TUF implementations in other languages.
/// </remarks>
public class NetworkResilienceTests
{
    [Test]
    public async Task NetworkResilienceConfig_DefaultValues_AreReasonableForProduction()
    {
        var config = new NetworkResilienceConfig();

        await Assert.That(config.MaxRetryAttempts).IsEqualTo(3);
        await Assert.That(config.InitialRetryDelay).IsEqualTo(TimeSpan.FromSeconds(1));
        await Assert.That(config.MaxRetryDelay).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(config.BackoffMultiplier).IsEqualTo(2.0);
        await Assert.That(config.RequestTimeout).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(config.FileDownloadTimeout).IsEqualTo(TimeSpan.FromMinutes(5));
        await Assert.That(config.EnableCircuitBreaker).IsTrue();
        await Assert.That(config.CircuitBreakerFailureThreshold).IsEqualTo(5);
        await Assert.That(config.CircuitBreakerRecoveryTimeout).IsEqualTo(TimeSpan.FromSeconds(60));
        await Assert.That(config.EnableMirrorFailover).IsTrue();
        await Assert.That(config.TotalOperationTimeout).IsEqualTo(TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task NetworkResilienceConfig_RetryableStatusCodes_IncludeCommonTransientFailures()
    {
        var config = new NetworkResilienceConfig();
        
        await Assert.That(config.RetryableStatusCodes).Contains(HttpStatusCode.RequestTimeout);
        await Assert.That(config.RetryableStatusCodes).Contains(HttpStatusCode.InternalServerError);
        await Assert.That(config.RetryableStatusCodes).Contains(HttpStatusCode.BadGateway);
        await Assert.That(config.RetryableStatusCodes).Contains(HttpStatusCode.ServiceUnavailable);
        await Assert.That(config.RetryableStatusCodes).Contains(HttpStatusCode.GatewayTimeout);
        await Assert.That(config.RetryableStatusCodes).Contains(HttpStatusCode.TooManyRequests);
        
        // Should NOT include non-transient errors
        await Assert.That(config.RetryableStatusCodes).DoesNotContain(HttpStatusCode.NotFound);
        await Assert.That(config.RetryableStatusCodes).DoesNotContain(HttpStatusCode.Unauthorized);
        await Assert.That(config.RetryableStatusCodes).DoesNotContain(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ExecuteWithResilienceAsync_SuccessfulOperation_ReturnsResultImmediately()
    {
        var config = new NetworkResilienceConfig { MaxRetryAttempts = 2 };
        var resilience = new NetworkResilience(config);
        var httpClient = new HttpClient();

        var result = await resilience.ExecuteWithResilienceAsync<string>(
            (client, cancellationToken) => Task.FromResult("success"),
            httpClient,
            "test-operation");

        await Assert.That(result).IsEqualTo("success");
    }

    [Test]
    public async Task ExecuteWithResilienceAsync_TransientFailureThenSuccess_RetriesAndSucceeds()
    {
        var config = new NetworkResilienceConfig 
        { 
            MaxRetryAttempts = 2,
            InitialRetryDelay = TimeSpan.FromMilliseconds(10),
            BackoffMultiplier = 1.0 // No exponential backoff for faster test
        };
        var resilience = new NetworkResilience(config);
        var httpClient = new HttpClient();
        int attemptCount = 0;

        var result = await resilience.ExecuteWithResilienceAsync<string>(
            (client, cancellationToken) => 
            {
                attemptCount++;
                if (attemptCount < 2)
                    throw new HttpRequestException("Transient failure (500 Internal Server Error)");
                return Task.FromResult("success");
            },
            httpClient,
            "test-operation");

        await Assert.That(result).IsEqualTo("success");
        await Assert.That(attemptCount).IsEqualTo(2);
    }

    [Test]
    public async Task ExecuteWithResilienceAsync_PersistentFailure_ThrowsNetworkResilienceException()
    {
        var config = new NetworkResilienceConfig 
        { 
            MaxRetryAttempts = 2,
            InitialRetryDelay = TimeSpan.FromMilliseconds(10)
        };
        var resilience = new NetworkResilience(config);
        var httpClient = new HttpClient();
        int attemptCount = 0;

        var exception = await Assert.ThrowsAsync<NetworkResilienceException>(() =>
            resilience.ExecuteWithResilienceAsync<string>(
                (client, cancellationToken) => 
                {
                    attemptCount++;
                    throw new HttpRequestException("Persistent failure (500 Internal Server Error)");
                },
                httpClient,
                "test-operation"));

        await Assert.That(attemptCount).IsEqualTo(3); // Initial attempt + 2 retries
        await Assert.That(exception.Message).Contains("test-operation");
        await Assert.That(typeof(HttpRequestException).IsAssignableFrom(exception.InnerException?.GetType())).IsTrue();
    }

    [Test]
    public async Task ExecuteWithResilienceAsync_NonTransientError_DoesNotRetry()
    {
        var config = new NetworkResilienceConfig { MaxRetryAttempts = 2 };
        var resilience = new NetworkResilience(config);
        var httpClient = new HttpClient();
        int attemptCount = 0;

        var exception = await Assert.ThrowsAsync<NetworkResilienceException>(() =>
            resilience.ExecuteWithResilienceAsync<string>(
                (client, cancellationToken) => 
                {
                    attemptCount++;
                    throw new ArgumentException("Non-transient error");
                },
                httpClient,
                "test-operation"));

        await Assert.That(attemptCount).IsEqualTo(1); // Only initial attempt, no retries
        await Assert.That(typeof(ArgumentException).IsAssignableFrom(exception.InnerException?.GetType())).IsTrue();
    }

    [Test]
    public async Task ExecuteWithResilienceAsync_WithMirrorFailover_TriesFallbackUrls()
    {
        var config = new NetworkResilienceConfig 
        { 
            MaxRetryAttempts = 1,
            InitialRetryDelay = TimeSpan.FromMilliseconds(10),
            EnableMirrorFailover = true
        };
        var resilience = new NetworkResilience(config);
        var httpClient = new HttpClient { BaseAddress = new Uri("https://primary.example.com/") };
        var fallbackUrls = new List<string> { "https://mirror1.example.com/", "https://mirror2.example.com/" };
        
        var urlsTried = new List<string>();

        var result = await resilience.ExecuteWithResilienceAsync<string>(
            (client, cancellationToken) => 
            {
                var currentUrl = client.BaseAddress?.ToString() ?? "unknown";
                urlsTried.Add(currentUrl);
                
                if (urlsTried.Count < 3) // Fail first two URLs
                    throw new HttpRequestException("Server error (503 Service Unavailable)");
                    
                return Task.FromResult("success from mirror");
            },
            httpClient,
            "test-operation",
            fallbackUrls);

        await Assert.That(result).IsEqualTo("success from mirror");
        await Assert.That(urlsTried.Count).IsEqualTo(3);
    }

    [Test] 
    public async Task ExecuteWithResilienceAsync_CircuitBreakerOpen_SkipsFailingEndpoint()
    {
        var config = new NetworkResilienceConfig 
        { 
            MaxRetryAttempts = 1,
            InitialRetryDelay = TimeSpan.FromMilliseconds(10),
            EnableCircuitBreaker = true,
            CircuitBreakerFailureThreshold = 2
        };
        var resilience = new NetworkResilience(config);
        var httpClient = new HttpClient { BaseAddress = new Uri("https://failing.example.com/") };
        
        // Cause enough failures to open the circuit
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await resilience.ExecuteWithResilienceAsync<string>(
                    (client, cancellationToken) => 
                        throw new HttpRequestException("Persistent failure (500 Internal Server Error)"),
                    httpClient,
                    "test-operation");
            }
            catch (NetworkResilienceException)
            {
                // Expected failures
            }
        }
        
        // Now the circuit should be open and skip the failing endpoint
        var startTime = DateTime.UtcNow;
        try
        {
            await resilience.ExecuteWithResilienceAsync<string>(
                (client, cancellationToken) => 
                    throw new HttpRequestException("This should not be called"),
                httpClient,
                "test-operation");
        }
        catch (NetworkResilienceException)
        {
            // Expected - all endpoints failed
        }
        var elapsed = DateTime.UtcNow - startTime;
        
        // Should fail quickly due to circuit breaker, not wait for retries
        await Assert.That(elapsed).IsLessThan(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task ExecuteWithResilienceAsync_OperationTimeout_ThrowsTaskCanceledException()
    {
        var config = new NetworkResilienceConfig 
        { 
            TotalOperationTimeout = TimeSpan.FromMilliseconds(100),
            MaxRetryAttempts = 5,
            InitialRetryDelay = TimeSpan.FromMilliseconds(50)
        };
        var resilience = new NetworkResilience(config);
        var httpClient = new HttpClient();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            resilience.ExecuteWithResilienceAsync<string>(
                async (client, cancellationToken) => 
                {
                    await Task.Delay(200, cancellationToken); // Longer than total timeout
                    return "should not complete";
                },
                httpClient,
                "test-operation"));
    }

    [Test]
    public async Task NetworkResilience_ExponentialBackoff_IncreasesDelayCorrectly()
    {
        var config = new NetworkResilienceConfig 
        { 
            MaxRetryAttempts = 3,
            InitialRetryDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0,
            MaxRetryDelay = TimeSpan.FromSeconds(1)
        };
        var resilience = new NetworkResilience(config);
        var httpClient = new HttpClient();
        
        var attemptTimes = new List<DateTime>();

        try
        {
            await resilience.ExecuteWithResilienceAsync<string>(
                (client, cancellationToken) => 
                {
                    attemptTimes.Add(DateTime.UtcNow);
                    throw new HttpRequestException("Failure (503 Service Unavailable)");
                },
                httpClient,
                "test-operation");
        }
        catch (NetworkResilienceException)
        {
            // Expected failure
        }

        await Assert.That(attemptTimes.Count).IsEqualTo(4); // Initial + 3 retries
        
        // Check that delays are approximately exponential
        var delay1 = attemptTimes[1] - attemptTimes[0];
        var delay2 = attemptTimes[2] - attemptTimes[1];
        var delay3 = attemptTimes[3] - attemptTimes[2];
        
        await Assert.That(delay1.TotalMilliseconds).IsGreaterThanOrEqualTo(90); // ~100ms
        await Assert.That(delay2.TotalMilliseconds).IsGreaterThanOrEqualTo(180); // ~200ms
        await Assert.That(delay3.TotalMilliseconds).IsGreaterThanOrEqualTo(380); // ~400ms
    }

    [Test]
    public async Task NetworkResilienceException_HasCorrectInheritance()
    {
        var exception = new NetworkResilienceException("Test message");
        
        await Assert.That(typeof(NetworkResilienceException).IsAssignableFrom(exception.GetType())).IsTrue();
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
        await Assert.That(typeof(Exception).IsAssignableFrom(exception.GetType())).IsTrue();
        await Assert.That(exception.Message).IsEqualTo("Test message");
    }

    [Test]
    public async Task NetworkResilienceException_WithInnerException_PreservesInnerException()
    {
        var innerException = new HttpRequestException("HTTP error");
        var exception = new NetworkResilienceException("Network resilience failed", innerException);
        
        await Assert.That(exception.Message).IsEqualTo("Network resilience failed");
        await Assert.That(exception.InnerException).IsEqualTo(innerException);
    }

    [Test]
    public async Task CircuitBreakerState_InitialState_IsClosed()
    {
        var config = new NetworkResilienceConfig();
        var resilience = new NetworkResilience(config);
        
        // Circuit should start closed and allow requests through
        var result = await resilience.ExecuteWithResilienceAsync<string>(
            (client, cancellationToken) => Task.FromResult("success"),
            new HttpClient(),
            "test-operation");
            
        await Assert.That(result).IsEqualTo("success");
    }

    [Test] 
    public async Task NetworkResilience_ConfigurationValidation_AcceptsValidConfigurations()
    {
        var validConfigs = new[]
        {
            new NetworkResilienceConfig(),
            new NetworkResilienceConfig { MaxRetryAttempts = 0 }, // No retries
            new NetworkResilienceConfig { EnableCircuitBreaker = false },
            new NetworkResilienceConfig { EnableMirrorFailover = false }
        };

        foreach (var config in validConfigs)
        {
            var resilience = new NetworkResilience(config);
            
            // Should be able to create resilience instance
            await Assert.That(resilience).IsNotNull();
            
            // Should be able to execute simple operations
            var result = await resilience.ExecuteWithResilienceAsync<string>(
                (client, cancellationToken) => Task.FromResult("test"),
                new HttpClient(),
                "validation-test");
                
            await Assert.That(result).IsEqualTo("test");
        }
    }
}