using Microsoft.Extensions.Logging;
using TUF;
using System.Text.Json;

namespace AspNetCoreIntegration;

/// <summary>
/// ASP.NET Core middleware for validating package signatures using TUF.
/// Intercepts requests to package endpoints and validates TUF metadata.
/// </summary>
public class TufValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TufValidationMiddleware> _logger;
    private readonly TufValidationOptions _options;

    public TufValidationMiddleware(
        RequestDelegate next, 
        ILogger<TufValidationMiddleware> logger,
        TufValidationOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only validate requests to package endpoints
        if (ShouldValidateRequest(context.Request))
        {
            _logger.LogInformation("Validating TUF metadata for request: {Path}", context.Request.Path);
            
            try
            {
                var isValid = await ValidateTufMetadataAsync(context);
                if (!isValid)
                {
                    _logger.LogWarning("TUF validation failed for request: {Path}", context.Request.Path);
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("TUF validation failed");
                    return;
                }
                
                _logger.LogInformation("TUF validation successful for request: {Path}", context.Request.Path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during TUF validation for request: {Path}", context.Request.Path);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("TUF validation error");
                return;
            }
        }

        await _next(context);
    }

    private bool ShouldValidateRequest(HttpRequest request)
    {
        // Validate requests to configured package paths
        return _options.ValidatedPaths.Any(path => 
            request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> ValidateTufMetadataAsync(HttpContext context)
    {
        // In a real implementation, this would:
        // 1. Extract package information from the request
        // 2. Load TUF metadata from configured repository
        // 3. Validate package signatures against TUF metadata
        // 4. Check for revoked keys or expired metadata
        
        // For demonstration, we'll simulate validation
        await Task.Delay(10); // Simulate async work
        
        // Simple validation based on request headers or parameters
        if (context.Request.Headers.ContainsKey("X-TUF-Signature"))
        {
            var signature = context.Request.Headers["X-TUF-Signature"].FirstOrDefault();
            return !string.IsNullOrEmpty(signature);
        }
        
        // For demo purposes, allow requests with specific query parameter
        return context.Request.Query.ContainsKey("tuf-validated");
    }
}

/// <summary>
/// Configuration options for TUF validation middleware.
/// </summary>
public class TufValidationOptions
{
    /// <summary>
    /// Paths that should be validated using TUF metadata.
    /// </summary>
    public List<PathString> ValidatedPaths { get; set; } = new();
    
    /// <summary>
    /// Path to the TUF repository metadata.
    /// </summary>
    public string? MetadataPath { get; set; }
    
    /// <summary>
    /// Whether to fail open (allow requests) when TUF validation fails.
    /// </summary>
    public bool FailOpen { get; set; } = false;
    
    /// <summary>
    /// Timeout for TUF metadata validation operations.
    /// </summary>
    public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(10);
}