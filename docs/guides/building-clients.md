# Building Production TUF Clients

This guide covers best practices and patterns for building production-ready TUF clients in .NET applications. It focuses on real-world scenarios, error handling, performance optimization, and integration with existing .NET applications.

## Production Client Architecture

### Basic Client Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TUF;

public class ProductionTufClient
{
    private readonly IUpdater _updater;
    private readonly ILogger<ProductionTufClient> _logger;
    private readonly SemaphoreSlim _updateSemaphore;

    public ProductionTufClient(IUpdater updater, ILogger<ProductionTufClient> logger)
    {
        _updater = updater;
        _logger = logger;
        _updateSemaphore = new SemaphoreSlim(1, 1); // Prevent concurrent updates
    }

    public async Task<UpdateResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        await _updateSemaphore.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Starting TUF metadata refresh");
            
            await _updater.RefreshAsync();
            
            var availableTargets = await GetAvailableUpdates();
            return new UpdateResult
            {
                UpdatesAvailable = availableTargets.Any(),
                AvailableTargets = availableTargets,
                LastChecked = DateTimeOffset.UtcNow
            };
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }
}
```

### Dependency Injection Configuration

```csharp
// Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Configure HTTP client with appropriate timeouts and retry policies
    services.AddHttpClient<IUpdater>("TUFClient", client =>
    {
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0 TUF-Client");
    })
    .AddPolicyHandler(GetRetryPolicy());

    // Configure TUF updater
    services.AddScoped<IUpdater>(provider =>
    {
        var httpClient = provider.GetRequiredService<HttpClient>("TUFClient");
        var config = provider.GetRequiredService<IConfiguration>();
        var logger = provider.GetRequiredService<ILogger<Updater>>();

        var tufConfig = new UpdaterConfig(
            trustedRoot: LoadTrustedRoot(config),
            remoteMetadataUrl: new Uri(config["TUF:MetadataUrl"])
        )
        {
            LocalMetadataDir = config["TUF:MetadataDir"],
            LocalTargetsDir = config["TUF:TargetsDir"],
            RemoteTargetsUrl = new Uri(config["TUF:TargetsUrl"]),
            Client = httpClient
        };

        return new Updater(tufConfig);
    });

    services.AddScoped<ProductionTufClient>();
}

private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return Policy
        .Handle<HttpRequestException>()
        .Or<TimeoutRejectedException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                var logger = context.GetLogger();
                logger?.LogWarning("TUF HTTP request attempt {RetryCount} failed, retrying in {Delay}s", 
                    retryCount, timespan.TotalSeconds);
            });
}
```

## Error Handling Strategies

### Comprehensive Exception Handling

```csharp
public async Task<DownloadResult> SafeDownloadAsync(string targetPath, string localPath)
{
    try
    {
        var targetInfo = await _updater.GetTargetInfo(targetPath);
        if (!targetInfo.HasValue)
        {
            return DownloadResult.NotFound(targetPath);
        }

        var (downloadedPath, data) = await _updater.DownloadTarget(
            targetInfo.Value.File, localPath);

        _logger.LogInformation("Successfully downloaded {TargetPath} to {LocalPath} ({Size} bytes)",
            targetPath, downloadedPath, data.Length);

        return DownloadResult.Success(targetPath, downloadedPath, data.Length);
    }
    catch (TufMetadataException ex) when (ex.ErrorCode == TufErrorCode.SignatureVerificationFailed)
    {
        _logger.LogError("Signature verification failed for {TargetPath}: {Message}", targetPath, ex.Message);
        return DownloadResult.SecurityError(targetPath, "Signature verification failed");
    }
    catch (TufMetadataException ex) when (ex.ErrorCode == TufErrorCode.MetadataExpired)
    {
        _logger.LogWarning("Metadata expired for {TargetPath}, attempting refresh", targetPath);
        
        // Attempt to refresh metadata and retry once
        try
        {
            await _updater.RefreshAsync();
            return await SafeDownloadAsync(targetPath, localPath); // Single retry
        }
        catch
        {
            return DownloadResult.MetadataError(targetPath, "Metadata expired and refresh failed");
        }
    }
    catch (TufTargetNotFoundException ex)
    {
        _logger.LogInformation("Target {TargetPath} not found in repository", targetPath);
        return DownloadResult.NotFound(targetPath);
    }
    catch (TufNetworkException ex)
    {
        _logger.LogError(ex, "Network error downloading {TargetPath}", targetPath);
        return DownloadResult.NetworkError(targetPath, ex.Message);
    }
    catch (UnauthorizedAccessException ex)
    {
        _logger.LogError(ex, "Access denied writing to {LocalPath}", localPath);
        return DownloadResult.FileSystemError(targetPath, "Access denied");
    }
    catch (IOException ex)
    {
        _logger.LogError(ex, "IO error downloading {TargetPath}", targetPath);
        return DownloadResult.FileSystemError(targetPath, ex.Message);
    }
}
```

### Circuit Breaker Pattern

```csharp
public class TufCircuitBreakerClient
{
    private readonly IUpdater _updater;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly ILogger<TufCircuitBreakerClient> _logger;

    public TufCircuitBreakerClient(IUpdater updater, ILogger<TufCircuitBreakerClient> logger)
    {
        _updater = updater;
        _logger = logger;
        
        // Configure circuit breaker for TUF operations
        _circuitBreaker = Policy
            .Handle<TufNetworkException>()
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (exception, timespan) =>
                {
                    _logger.LogWarning("TUF circuit breaker opened for {Duration}s due to: {Exception}",
                        timespan.TotalSeconds, exception.Message);
                },
                onReset: () =>
                {
                    _logger.LogInformation("TUF circuit breaker reset - operations resumed");
                });
    }

    public async Task<T> ExecuteWithCircuitBreaker<T>(Func<Task<T>> operation)
    {
        try
        {
            return await _circuitBreaker.ExecuteAsync(operation);
        }
        catch (CircuitBreakerOpenException)
        {
            _logger.LogWarning("TUF operation blocked - circuit breaker is open");
            throw new TufServiceUnavailableException("TUF service temporarily unavailable");
        }
    }
}
```

## Performance Optimization

### Metadata Caching Strategy

```csharp
public class CachingTufClient
{
    private readonly IUpdater _updater;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _refreshSemaphore;
    private DateTimeOffset _lastRefresh;

    public CachingTufClient(IUpdater updater, IMemoryCache cache)
    {
        _updater = updater;
        _cache = cache;
        _refreshSemaphore = new SemaphoreSlim(1, 1);
    }

    public async Task<TargetInfo?> GetCachedTargetInfo(string targetPath, TimeSpan maxAge)
    {
        // Check if we have cached target info
        var cacheKey = $"target_info_{targetPath}";
        if (_cache.TryGetValue(cacheKey, out TargetInfo? cachedInfo))
        {
            return cachedInfo;
        }

        // Check if metadata needs refreshing
        if (DateTimeOffset.UtcNow - _lastRefresh > maxAge)
        {
            await RefreshIfNeeded(maxAge);
        }

        // Get target info and cache it
        var targetInfo = await _updater.GetTargetInfo(targetPath);
        if (targetInfo.HasValue)
        {
            var info = targetInfo.Value;
            _cache.Set(cacheKey, info, TimeSpan.FromMinutes(30));
            return info;
        }

        return null;
    }

    private async Task RefreshIfNeeded(TimeSpan maxAge)
    {
        if (!_refreshSemaphore.Wait(100)) // Don't wait long for refresh lock
            return;

        try
        {
            if (DateTimeOffset.UtcNow - _lastRefresh > maxAge)
            {
                await _updater.RefreshAsync();
                _lastRefresh = DateTimeOffset.UtcNow;
                _cache.Remove("refresh_timestamp"); // Clear any cached refresh indicators
            }
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }
}
```

### Parallel Downloads

```csharp
public async Task<List<DownloadResult>> DownloadMultipleTargets(
    IEnumerable<string> targetPaths, 
    string destinationDir,
    int maxConcurrency = 3)
{
    var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    var downloadTasks = targetPaths.Select(async targetPath =>
    {
        await semaphore.WaitAsync();
        try
        {
            var localPath = Path.Combine(destinationDir, Path.GetFileName(targetPath));
            return await SafeDownloadAsync(targetPath, localPath);
        }
        finally
        {
            semaphore.Release();
        }
    });

    return (await Task.WhenAll(downloadTasks)).ToList();
}
```

## Background Update Service

### Hosted Service Implementation

```csharp
public class TufUpdateService : BackgroundService
{
    private readonly ProductionTufClient _tufClient;
    private readonly ILogger<TufUpdateService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TufUpdateOptions _options;

    public TufUpdateService(
        ProductionTufClient tufClient,
        ILogger<TufUpdateService> logger,
        IServiceProvider serviceProvider,
        IOptions<TufUpdateOptions> options)
    {
        _tufClient = tufClient;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TUF Update Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformUpdateCheck(stoppingToken);
                await Task.Delay(_options.CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during update check cycle");
                await Task.Delay(_options.ErrorRetryInterval, stoppingToken);
            }
        }

        _logger.LogInformation("TUF Update Service stopped");
    }

    private async Task PerformUpdateCheck(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Performing TUF update check");

        var result = await _tufClient.CheckForUpdatesAsync(cancellationToken);
        
        if (result.UpdatesAvailable)
        {
            _logger.LogInformation("Found {Count} available updates", result.AvailableTargets.Count);

            if (_options.AutoDownload)
            {
                await DownloadAvailableUpdates(result.AvailableTargets, cancellationToken);
            }

            // Notify other services about available updates
            await NotifyUpdateAvailable(result, cancellationToken);
        }
    }

    private async Task DownloadAvailableUpdates(List<string> availableTargets, CancellationToken cancellationToken)
    {
        var downloadResults = await _tufClient.DownloadMultipleTargets(
            availableTargets,
            _options.DownloadDirectory,
            _options.MaxConcurrentDownloads);

        var successful = downloadResults.Count(r => r.IsSuccess);
        var failed = downloadResults.Count(r => !r.IsSuccess);

        _logger.LogInformation("Downloaded {Successful} targets successfully, {Failed} failed", successful, failed);

        if (failed > 0)
        {
            var failures = downloadResults.Where(r => !r.IsSuccess);
            foreach (var failure in failures)
            {
                _logger.LogWarning("Failed to download {TargetPath}: {Error}", failure.TargetPath, failure.ErrorMessage);
            }
        }
    }

    private async Task NotifyUpdateAvailable(UpdateResult result, CancellationToken cancellationToken)
    {
        // Notify through dependency injection
        using var scope = _serviceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetService<IUpdateNotificationService>();
        
        if (notificationService != null)
        {
            await notificationService.NotifyUpdatesAvailable(result, cancellationToken);
        }
    }
}

public class TufUpdateOptions
{
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan ErrorRetryInterval { get; set; } = TimeSpan.FromMinutes(15);
    public bool AutoDownload { get; set; } = false;
    public string DownloadDirectory { get; set; } = "./downloads";
    public int MaxConcurrentDownloads { get; set; } = 3;
}
```

## Integration with Existing Applications

### ASP.NET Core Integration

```csharp
// Controllers/UpdateController.cs
[ApiController]
[Route("api/[controller]")]
public class UpdateController : ControllerBase
{
    private readonly ProductionTufClient _tufClient;
    private readonly ILogger<UpdateController> _logger;

    public UpdateController(ProductionTufClient tufClient, ILogger<UpdateController> logger)
    {
        _tufClient = tufClient;
        _logger = logger;
    }

    [HttpGet("check")]
    public async Task<ActionResult<UpdateCheckResponse>> CheckForUpdates()
    {
        try
        {
            var result = await _tufClient.CheckForUpdatesAsync(HttpContext.RequestAborted);
            
            return Ok(new UpdateCheckResponse
            {
                UpdatesAvailable = result.UpdatesAvailable,
                AvailableTargets = result.AvailableTargets,
                LastChecked = result.LastChecked
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            return StatusCode(500, "Update check failed");
        }
    }

    [HttpPost("download/{targetPath}")]
    public async Task<ActionResult<DownloadResponse>> DownloadTarget(string targetPath)
    {
        try
        {
            var tempPath = Path.GetTempFileName();
            var result = await _tufClient.SafeDownloadAsync(targetPath, tempPath);

            if (result.IsSuccess)
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(tempPath);
                System.IO.File.Delete(tempPath);
                
                return File(fileBytes, "application/octet-stream", Path.GetFileName(targetPath));
            }

            return BadRequest(new { Error = result.ErrorMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download target {TargetPath}", targetPath);
            return StatusCode(500, "Download failed");
        }
    }
}
```

### Windows Service Integration

```csharp
public class TufWindowsService : WindowsService
{
    private readonly IHost _host;

    public TufWindowsService(IHost host)
    {
        _host = host;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _host.RunAsync(stoppingToken);
    }

    public static void Main(string[] args)
    {
        CreateHostBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "TUF Update Service";
            })
            .Build()
            .Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.Configure<TufUpdateOptions>(context.Configuration.GetSection("TufUpdate"));
                services.AddTufClient(context.Configuration);
                services.AddHostedService<TufUpdateService>();
            });
}
```

## Security Considerations

### Trusted Root Management

```csharp
public class SecureTrustedRootProvider
{
    private readonly ILogger<SecureTrustedRootProvider> _logger;

    public async Task<byte[]> LoadTrustedRoot(IConfiguration config)
    {
        var trustedRootPath = config["TUF:TrustedRootPath"];
        
        // Verify the trusted root file hasn't been tampered with
        var expectedHash = config["TUF:TrustedRootHash"];
        if (!string.IsNullOrEmpty(expectedHash))
        {
            var rootBytes = await File.ReadAllBytesAsync(trustedRootPath);
            var actualHash = Convert.ToHexString(SHA256.HashData(rootBytes)).ToLowerInvariant();
            
            if (actualHash != expectedHash.ToLowerInvariant())
            {
                throw new SecurityException($"Trusted root file hash mismatch. Expected: {expectedHash}, Actual: {actualHash}");
            }
        }

        return await File.ReadAllBytesAsync(trustedRootPath);
    }
}
```

### Secure Configuration

```csharp
// appsettings.json
{
  "TUF": {
    "MetadataUrl": "https://secure-updates.example.com/metadata/",
    "TargetsUrl": "https://secure-updates.example.com/targets/",
    "TrustedRootPath": "C:\\ProgramData\\MyApp\\trusted-root.json",
    "TrustedRootHash": "a1b2c3d4e5f6...", // SHA256 of trusted root
    "MetadataDir": "C:\\ProgramData\\MyApp\\metadata",
    "TargetsDir": "C:\\ProgramData\\MyApp\\downloads",
    "RequireHttps": true,
    "CertificatePinning": {
      "Enabled": true,
      "Pins": ["sha256/ABCD1234..."]
    }
  },
  "TufUpdate": {
    "CheckInterval": "01:00:00",
    "ErrorRetryInterval": "00:15:00",
    "AutoDownload": false,
    "MaxConcurrentDownloads": 3
  }
}
```

## Testing Strategies

### Unit Testing TUF Clients

```csharp
[TestClass]
public class ProductionTufClientTests
{
    private Mock<IUpdater> _mockUpdater;
    private Mock<ILogger<ProductionTufClient>> _mockLogger;
    private ProductionTufClient _client;

    [TestInitialize]
    public void Setup()
    {
        _mockUpdater = new Mock<IUpdater>();
        _mockLogger = new Mock<ILogger<ProductionTufClient>>();
        _client = new ProductionTufClient(_mockUpdater.Object, _mockLogger.Object);
    }

    [TestMethod]
    public async Task CheckForUpdatesAsync_ReturnsAvailableUpdates()
    {
        // Arrange
        var targetInfo = new TargetInfo("app.exe", new TargetFile
        {
            Length = 1024,
            Hashes = new Dictionary<string, string> { ["sha256"] = "abc123" }
        });
        
        _mockUpdater
            .Setup(u => u.RefreshAsync())
            .Returns(Task.CompletedTask);
            
        _mockUpdater
            .Setup(u => u.GetTargetInfo("app.exe"))
            .ReturnsAsync(targetInfo);

        // Act
        var result = await _client.CheckForUpdatesAsync();

        // Assert
        Assert.IsTrue(result.UpdatesAvailable);
        Assert.AreEqual(1, result.AvailableTargets.Count);
        _mockUpdater.Verify(u => u.RefreshAsync(), Times.Once);
    }
}
```

### Integration Testing

```csharp
[TestClass]
public class TufIntegrationTests
{
    private TestServer _server;
    private HttpClient _httpClient;
    private string _testRepositoryPath;

    [TestInitialize]
    public async Task Setup()
    {
        // Create a test TUF repository
        _testRepositoryPath = await CreateTestRepository();
        
        // Start test server serving the repository
        _server = new TestServer(new WebHostBuilder()
            .UseStartup<TestTufServerStartup>()
            .ConfigureServices(services =>
            {
                services.AddSingleton(new TestTufRepositoryOptions
                {
                    RepositoryPath = _testRepositoryPath
                });
            }));
            
        _httpClient = _server.CreateClient();
    }

    [TestMethod]
    public async Task EndToEnd_DownloadTarget_Success()
    {
        // Arrange
        var config = new UpdaterConfig(
            trustedRoot: await LoadTestTrustedRoot(),
            remoteMetadataUrl: new Uri(_server.BaseAddress, "/metadata/")
        )
        {
            RemoteTargetsUrl = new Uri(_server.BaseAddress, "/targets/"),
            LocalMetadataDir = Path.GetTempPath(),
            LocalTargetsDir = Path.GetTempPath(),
            Client = _httpClient
        };

        var updater = new Updater(config);
        var client = new ProductionTufClient(updater, Mock.Of<ILogger<ProductionTufClient>>());

        // Act
        await updater.RefreshAsync();
        var result = await client.SafeDownloadAsync("test-file.txt", Path.GetTempFileName());

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(File.Exists(result.LocalPath));
    }
}
```

## Monitoring and Observability

### Metrics Collection

```csharp
public class TufMetricsCollector
{
    private readonly Counter<long> _refreshCounter;
    private readonly Counter<long> _downloadCounter;
    private readonly Histogram<double> _downloadDuration;
    private readonly Gauge<long> _metadataAge;

    public TufMetricsCollector(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("TUF.Client");
        
        _refreshCounter = meter.CreateCounter<long>("tuf_refresh_total", description: "Total TUF metadata refresh operations");
        _downloadCounter = meter.CreateCounter<long>("tuf_download_total", description: "Total TUF target downloads");
        _downloadDuration = meter.CreateHistogram<double>("tuf_download_duration_seconds", description: "TUF download duration");
        _metadataAge = meter.CreateGauge<long>("tuf_metadata_age_seconds", description: "Age of TUF metadata");
    }

    public void RecordRefresh(bool success, string? errorType = null)
    {
        _refreshCounter.Add(1, new KeyValuePair<string, object?>[]
        {
            new("success", success),
            new("error_type", errorType ?? "none")
        });
    }

    public void RecordDownload(string targetPath, bool success, double durationSeconds, long sizeBytes = 0)
    {
        _downloadCounter.Add(1, new KeyValuePair<string, object?>[]
        {
            new("success", success),
            new("target_path", targetPath)
        });

        _downloadDuration.Record(durationSeconds, new KeyValuePair<string, object?>[]
        {
            new("target_path", targetPath),
            new("success", success)
        });
    }
}
```

### Health Checks

```csharp
public class TufHealthCheck : IHealthCheck
{
    private readonly IUpdater _updater;
    private readonly ILogger<TufHealthCheck> _logger;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple connectivity check
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync(_updater.Config.RemoteMetadataUrl + "timestamp.json", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("TUF repository is accessible");
            }
            
            return HealthCheckResult.Degraded($"TUF repository returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TUF health check failed");
            return HealthCheckResult.Unhealthy("TUF repository is not accessible", ex);
        }
    }
}

// Registration in Startup
services.AddHealthChecks()
    .AddCheck<TufHealthCheck>("tuf");
```

## Best Practices Summary

### Configuration
- ✅ Use strongly-typed configuration classes
- ✅ Validate configuration at startup
- ✅ Store trusted root securely with hash validation
- ✅ Use HTTPS with certificate pinning where possible

### Error Handling
- ✅ Handle TUF-specific exceptions appropriately
- ✅ Implement retry logic with exponential backoff
- ✅ Use circuit breaker pattern for network operations
- ✅ Log errors with appropriate levels and context

### Performance
- ✅ Cache metadata appropriately
- ✅ Use parallel downloads for multiple targets
- ✅ Implement proper semaphores for concurrency control
- ✅ Consider background update services

### Security
- ✅ Validate trusted root file integrity
- ✅ Never bypass signature verification
- ✅ Use secure temporary file handling
- ✅ Implement proper key rotation procedures

### Monitoring
- ✅ Collect metrics on operations
- ✅ Implement health checks
- ✅ Log all security-relevant events
- ✅ Monitor for unusual patterns or errors

## Next Steps

- **[Repository Builder Guide](creating-repositories.md)** - Creating TUF repositories
- **[Multi-Repository Client](../api/multi-repository-client.md)** - Advanced consensus patterns
- **[Security Model](../security/security-model.md)** - Understanding TUF security guarantees
