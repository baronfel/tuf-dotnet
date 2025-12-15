using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Diagnostics;
using TUF;
using TUF.Exceptions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure TUF services for dependency injection
builder.Services.AddSingleton<ITufClient, TufClientService>();
builder.Services.AddHostedService<TufUpdateService>();

// Add health checks including TUF repository status
builder.Services.AddHealthChecks()
    .AddCheck<TufHealthCheck>("tuf-repository");

// Configure logging to include TUF events
builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Name == "tuf-repository",
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

// TUF API endpoints
var tufApi = app.MapGroup("/api/tuf")
    .WithTags("TUF Operations")
    .WithOpenApi();

tufApi.MapGet("/status", async (ITufClient tufClient) =>
{
    var status = await tufClient.GetStatusAsync();
    return Results.Ok(status);
})
.WithName("GetTufStatus")
.WithSummary("Get TUF repository status");

tufApi.MapPost("/refresh", async (ITufClient tufClient) =>
{
    try
    {
        await tufClient.RefreshAsync();
        return Results.Ok(new { message = "TUF metadata refreshed successfully" });
    }
    catch (TufException ex)
    {
        return Results.BadRequest(new { error = ex.Message, type = ex.GetType().Name });
    }
})
.WithName("RefreshTufMetadata")
.WithSummary("Refresh TUF metadata from repository");

tufApi.MapGet("/targets", async (ITufClient tufClient) =>
{
    var targets = await tufClient.GetAvailableTargetsAsync();
    return Results.Ok(targets);
})
.WithName("GetAvailableTargets")
.WithSummary("Get list of available target files");

tufApi.MapPost("/targets/{targetName}/download", async (string targetName, ITufClient tufClient) =>
{
    try
    {
        var filePath = await tufClient.DownloadTargetAsync(targetName);
        return Results.Ok(new { targetName, localPath = filePath });
    }
    catch (TargetNotFoundException)
    {
        return Results.NotFound(new { error = $"Target '{targetName}' not found" });
    }
    catch (TufException ex)
    {
        return Results.BadRequest(new { error = ex.Message, type = ex.GetType().Name });
    }
})
.WithName("DownloadTarget")
.WithSummary("Download and verify a specific target file");

// Application update endpoint (practical example)
app.MapPost("/api/app/check-updates", async (ITufClient tufClient) =>
{
    try
    {
        var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        var updateInfo = await tufClient.CheckForUpdatesAsync(currentVersion);
        
        return Results.Ok(new
        {
            currentVersion,
            updateAvailable = updateInfo.UpdateAvailable,
            latestVersion = updateInfo.LatestVersion,
            updateDescription = updateInfo.Description,
            downloadUrl = updateInfo.DownloadUrl
        });
    }
    catch (TufException ex)
    {
        return Results.Problem(
            detail: ex.Message,
            title: "TUF Update Check Failed",
            type: ex.GetType().Name);
    }
})
.WithName("CheckForUpdates")
.WithSummary("Check for application updates via TUF");

app.Run();

// TUF Client Interface
public interface ITufClient
{
    Task<TufStatus> GetStatusAsync();
    Task RefreshAsync();
    Task<IEnumerable<string>> GetAvailableTargetsAsync();
    Task<string> DownloadTargetAsync(string targetName);
    Task<UpdateInfo> CheckForUpdatesAsync(string currentVersion);
}

// TUF Status Model
public record TufStatus(
    DateTime LastRefresh,
    int TargetCount,
    string RepositoryUrl,
    bool IsHealthy,
    DateTime? NextScheduledRefresh);

// Update Information Model
public record UpdateInfo(
    bool UpdateAvailable,
    string LatestVersion,
    string Description,
    string DownloadUrl);