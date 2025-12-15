using Microsoft.Extensions.Diagnostics.HealthChecks;
using TUF;
using TUF.Exceptions;

/// <summary>
/// Health check implementation for TUF repository connectivity and status.
/// Integrates with ASP.NET Core health checks to provide operational monitoring
/// of TUF update system health.
/// </summary>
public class TufHealthCheck : IHealthCheck
{
    private readonly ITufClient _tufClient;
    private readonly ILogger<TufHealthCheck> _logger;
    private readonly IConfiguration _configuration;

    public TufHealthCheck(
        ITufClient tufClient,
        ILogger<TufHealthCheck> logger,
        IConfiguration configuration)
    {
        _tufClient = tufClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = TufActivitySource.StartActivity("TufHealthCheck");
        var healthCheckData = new Dictionary<string, object>();
        
        try
        {
            _logger.LogDebug("Starting TUF health check");
            
            // Get TUF status
            var status = await _tufClient.GetStatusAsync();
            
            // Populate health check data
            healthCheckData["repository_url"] = status.RepositoryUrl;
            healthCheckData["last_refresh"] = status.LastRefresh;
            healthCheckData["target_count"] = status.TargetCount;
            healthCheckData["next_scheduled_refresh"] = status.NextScheduledRefresh;
            
            // Check if metadata is recent enough
            var maxAge = TimeSpan.FromHours(_configuration.GetValue("Tuf:MaxMetadataAgeHours", 24));
            var metadataAge = DateTime.UtcNow - status.LastRefresh;
            healthCheckData["metadata_age_minutes"] = metadataAge.TotalMinutes;
            healthCheckData["max_metadata_age_hours"] = maxAge.TotalHours;
            
            activity?.SetTag("health.repository_healthy", status.IsHealthy);
            activity?.SetTag("health.metadata_age_minutes", metadataAge.TotalMinutes);
            activity?.SetTag("health.target_count", status.TargetCount);
            
            // Determine health status
            if (!status.IsHealthy)
            {
                _logger.LogWarning("TUF health check failed: repository unhealthy");
                return HealthCheckResult.Unhealthy(
                    "TUF repository is not healthy",
                    data: healthCheckData);
            }
            
            if (metadataAge > maxAge)
            {
                _logger.LogWarning("TUF health check degraded: metadata age {MetadataAge} exceeds maximum {MaxAge}",
                    metadataAge, maxAge);
                return HealthCheckResult.Degraded(
                    $"TUF metadata is {metadataAge.TotalHours:F1} hours old (max: {maxAge.TotalHours})",
                    data: healthCheckData);
            }
            
            if (status.TargetCount == 0)
            {
                _logger.LogWarning("TUF health check degraded: no targets available");
                return HealthCheckResult.Degraded(
                    "No target files available in TUF repository",
                    data: healthCheckData);
            }
            
            _logger.LogDebug("TUF health check passed");
            return HealthCheckResult.Healthy(
                $"TUF repository healthy with {status.TargetCount} targets",
                data: healthCheckData);
        }
        catch (TufException ex)
        {
            _logger.LogError(ex, "TUF health check failed with TUF exception");
            
            healthCheckData["error_type"] = ex.GetType().Name;
            healthCheckData["error_message"] = ex.Message;
            
            activity?.SetTag("health.error", true);
            activity?.SetTag("health.error_type", ex.GetType().Name);
            
            return HealthCheckResult.Unhealthy(
                $"TUF error: {ex.Message}",
                exception: ex,
                data: healthCheckData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TUF health check failed with unexpected exception");
            
            healthCheckData["error_type"] = "unexpected";
            healthCheckData["error_message"] = ex.Message;
            
            activity?.SetTag("health.error", true);
            activity?.SetTag("health.error_type", "unexpected");
            
            return HealthCheckResult.Unhealthy(
                $"Unexpected error during TUF health check: {ex.Message}",
                exception: ex,
                data: healthCheckData);
        }
    }
}

/// <summary>
/// Custom health check response writer that provides detailed TUF status information.
/// </summary>
public static class HealthCheckResponseWriter
{
    public static async Task WriteResponse(HttpContext context, HealthReport healthReport)
    {
        var result = new
        {
            status = healthReport.Status.ToString(),
            totalDuration = healthReport.TotalDuration.TotalMilliseconds,
            checks = healthReport.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                duration = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description,
                data = entry.Value.Data,
                exception = entry.Value.Exception?.Message
            })
        };
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}