using TUF;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add TUF service for dependency injection
builder.Services.AddSingleton<Updater>(provider =>
{
    // In production, configure with real repository URLs and trusted root
    // For demo, we'll use placeholder values
    byte[] trustedRoot = GetPlaceholderTrustedRoot();
    
    var config = new UpdaterConfig(trustedRoot, new Uri("https://your-tuf-repository.com/metadata"))
    {
        LocalMetadataDir = Path.Combine(Directory.GetCurrentDirectory(), "tuf-metadata"),
        LocalTargetsDir = Path.Combine(Directory.GetCurrentDirectory(), "tuf-targets"),
        Client = new HttpClient()
    };
    
    return new Updater(config);
});

// Add health checks for TUF repository connectivity
builder.Services.AddHealthChecks()
    .AddCheck<TufHealthCheck>("tuf-repository");

var app = builder.Build();

// Configure request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapHealthChecks("/health");

// TUF-enabled file serving endpoint
app.MapGet("/secure-download/{fileName}", async (string fileName, Updater updater, ILogger<Program> logger) =>
{
    try
    {
        // Refresh metadata to get latest state
        await updater.RefreshAsync();
        
        // Get target info and verify it exists
        var targetInfo = await updater.GetTargetInfo(fileName);
        if (targetInfo == null)
        {
            logger.LogWarning("Requested file not found in TUF metadata: {FileName}", fileName);
            return Results.NotFound($"File '{fileName}' not available");
        }
        
        // Download the file securely
        var (filePath, fileBytes) = await updater.DownloadTarget(targetInfo.Value.File, targetInfo.Value.RemotePath);
        
        logger.LogInformation("Successfully downloaded secure file: {FileName} ({FileSize} bytes)", 
            fileName, fileBytes.Length);
        
        // Return file with appropriate content type
        var contentType = fileName.EndsWith(".exe") ? "application/octet-stream" : 
                         fileName.EndsWith(".json") ? "application/json" : 
                         "application/octet-stream";
        
        return Results.File(fileBytes, contentType, fileName);
    }
    catch (TUF.Exceptions.TargetNotFoundException)
    {
        logger.LogWarning("Target file not found: {FileName}", fileName);
        return Results.NotFound($"File '{fileName}' not found");
    }
    catch (TUF.Exceptions.TargetIntegrityException ex)
    {
        logger.LogError("File integrity check failed for {FileName}: {Error}", fileName, ex.Message);
        return Results.Problem("File integrity verification failed", statusCode: 409);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to download secure file: {FileName}", fileName);
        return Results.Problem("Internal server error");
    }
});

// API endpoint to list available secure files
app.MapGet("/secure-files", async (Updater updater, ILogger<Program> logger) =>
{
    try
    {
        await updater.RefreshAsync();
        var targets = updater.GetTopLevelTargets();
        
        var targetSummary = targets.Select(t => new
        {
            FileName = t.Key,
            Length = t.Value.Length,
            Hashes = t.Value.Hashes
        }).ToArray();
        
        logger.LogInformation("Listed {Count} available targets", targetSummary.Length);
        return Results.Ok(targetSummary);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to list available targets");
        return Results.Problem("Failed to retrieve file list");
    }
});

// Repository status endpoint
app.MapGet("/tuf-status", async (Updater updater, ILogger<Program> logger) =>
{
    try
    {
        await updater.RefreshAsync();
        var trustedMetadata = updater.GetTrustedMetadataSet();
        
        var status = new
        {
            RootVersion = trustedMetadata.Root?.Signed.Version,
            LastRefresh = DateTime.UtcNow,
            TargetsCount = updater.GetTopLevelTargets().Count,
            MetadataType = trustedMetadata.GetType().Name
        };
        
        logger.LogInformation("TUF repository status retrieved successfully");
        return Results.Ok(status);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to get TUF repository status");
        return Results.Problem("Failed to retrieve repository status");
    }
});

app.MapGet("/", () => """
<!DOCTYPE html>
<html>
<head>
    <title>TUF ASP.NET Core Integration Demo</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        .endpoint { background: #f5f5f5; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .method { font-weight: bold; color: #0066cc; }
        code { background: #e8e8e8; padding: 2px 5px; border-radius: 3px; }
    </style>
</head>
<body>
    <h1>🛡️ TUF ASP.NET Core Integration Demo</h1>
    <p>This example shows how to integrate The Update Framework (TUF) into an ASP.NET Core web application for secure file distribution.</p>
    
    <h2>Available Endpoints:</h2>
    
    <div class="endpoint">
        <div class="method">GET /secure-files</div>
        <p>List all files available for secure download from the TUF repository</p>
        <p><a href="/secure-files">Try it</a></p>
    </div>
    
    <div class="endpoint">
        <div class="method">GET /secure-download/{fileName}</div>
        <p>Download a specific file with TUF integrity verification</p>
        <p>Example: <code>/secure-download/app.exe</code></p>
    </div>
    
    <div class="endpoint">
        <div class="method">GET /tuf-status</div>
        <p>Check TUF repository metadata status and versions</p>
        <p><a href="/tuf-status">Try it</a></p>
    </div>
    
    <div class="endpoint">
        <div class="method">GET /health</div>
        <p>Health check endpoint including TUF repository connectivity</p>
        <p><a href="/health">Try it</a></p>
    </div>
    
    <h2>Security Features:</h2>
    <ul>
        <li>✅ Cryptographic verification of all downloaded files</li>
        <li>✅ Automatic metadata refresh and version checking</li>
        <li>✅ Protection against rollback and integrity attacks</li>
        <li>✅ Structured logging for security events</li>
        <li>✅ Health checks for repository availability</li>
    </ul>
    
    <h2>Production Deployment Notes:</h2>
    <ul>
        <li>Configure real TUF repository URLs in appsettings.json</li>
        <li>Distribute trusted root metadata securely with your application</li>
        <li>Implement proper caching and performance monitoring</li>
        <li>Set up alerts for TUF security exceptions</li>
    </ul>
</body>
</html>
""");

app.Run();

static byte[] GetPlaceholderTrustedRoot()
{
    // This is a placeholder implementation.
    // In a real application, you would:
    // 1. Embed a trusted root with your application
    // 2. Or load it from a secure configuration
    // 3. Or obtain it through a secure out-of-band channel

    return System.Text.Encoding.UTF8.GetBytes(@"{
        ""signatures"": [],
        ""signed"": {
            ""_type"": ""root"",
            ""spec_version"": ""1.0.0"",
            ""version"": 1,
            ""expires"": """ + DateTimeOffset.UtcNow.AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ") + @""",
            ""keys"": {},
            ""roles"": {
                ""root"": {""keyids"": [], ""threshold"": 1},
                ""targets"": {""keyids"": [], ""threshold"": 1},
                ""snapshot"": {""keyids"": [], ""threshold"": 1},
                ""timestamp"": {""keyids"": [], ""threshold"": 1}
            }
        }
    }");
}

// Health check for TUF repository connectivity
public class TufHealthCheck : IHealthCheck
{
    private readonly Updater _updater;
    private readonly ILogger<TufHealthCheck> _logger;

    public TufHealthCheck(Updater updater, ILogger<TufHealthCheck> logger)
    {
        _updater = updater;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _updater.RefreshAsync();
            _logger.LogDebug("TUF repository health check passed");
            return HealthCheckResult.Healthy("TUF repository is accessible and metadata is valid");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TUF repository health check failed");
            return HealthCheckResult.Unhealthy("TUF repository is not accessible", ex);
        }
    }
}