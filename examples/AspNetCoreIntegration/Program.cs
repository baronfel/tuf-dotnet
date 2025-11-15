using TUF;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Configure TUF as a scoped service for dependency injection
builder.Services.AddScoped<ITufService, TufService>();

// Add configuration for TUF
builder.Services.Configure<TufConfiguration>(
    builder.Configuration.GetSection("TufConfiguration"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Add a simple health check endpoint
app.MapGet("/health", () => "TUF ASP.NET Core Integration - OK");

// Secure file download endpoint
app.MapPost("/secure-download", async (
    string fileName, 
    ITufService tufService, 
    ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Secure download requested for {FileName}", fileName);
        
        var result = await tufService.SecureDownloadAsync(fileName);
        
        if (!result.Success)
        {
            logger.LogWarning("Download failed for {FileName}: {Error}", fileName, result.Error);
            return Results.BadRequest(new { error = result.Error });
        }
        
        logger.LogInformation("Successfully downloaded {FileName}, size: {Size} bytes", 
            fileName, result.Data?.Length ?? 0);
            
        return Results.Ok(new 
        { 
            fileName = fileName,
            size = result.Data?.Length ?? 0,
            verified = true,
            downloadPath = result.LocalPath
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during secure download of {FileName}", fileName);
        return Results.Problem("Internal server error during secure download");
    }
});

// Get available files from TUF repository
app.MapGet("/available-files", async (
    ITufService tufService,
    ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Listing available files from TUF repository");
        
        var files = await tufService.GetAvailableFilesAsync();
        
        logger.LogInformation("Found {Count} available files", files.Count);
        
        return Results.Ok(new { files });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error listing available files");
        return Results.Problem("Internal server error while listing files");
    }
});

// TUF repository status endpoint
app.MapGet("/tuf-status", async (
    ITufService tufService,
    ILogger<Program> logger) =>
{
    try
    {
        var status = await tufService.GetRepositoryStatusAsync();
        return Results.Ok(status);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error getting TUF status");
        return Results.Problem("Internal server error while checking TUF status");
    }
});

app.Run();

// TUF Configuration model
public class TufConfiguration
{
    public string TrustedRootPath { get; set; } = "trusted-root.json";
    public string MetadataUrl { get; set; } = "https://example.com/metadata/";
    public string LocalMetadataDir { get; set; } = "./tuf-metadata";
    public string LocalTargetsDir { get; set; } = "./downloads";
    public int HttpTimeoutSeconds { get; set; } = 30;
}

// TUF Service interface for dependency injection
public interface ITufService
{
    Task<DownloadResult> SecureDownloadAsync(string fileName);
    Task<List<string>> GetAvailableFilesAsync();
    Task<object> GetRepositoryStatusAsync();
}

// TUF Service implementation
public class TufService : ITufService
{
    private readonly HttpClient _httpClient;
    private readonly TufConfiguration _config;
    private readonly ILogger<TufService> _logger;
    private Updater? _updater;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);

    public TufService(
        HttpClient httpClient, 
        Microsoft.Extensions.Options.IOptions<TufConfiguration> config,
        ILogger<TufService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
        
        // Configure HTTP client timeout
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.HttpTimeoutSeconds);
    }

    private async Task<Updater> GetOrInitializeUpdaterAsync()
    {
        if (_updater != null)
            return _updater;

        await _initSemaphore.WaitAsync();
        try
        {
            if (_updater != null)
                return _updater;

            _logger.LogInformation("Initializing TUF updater");

            // Check if trusted root exists
            if (!File.Exists(_config.TrustedRootPath))
            {
                _logger.LogWarning("Trusted root file not found at {Path}, creating demo root", 
                    _config.TrustedRootPath);
                await CreateDemoTrustedRoot();
            }

            var rootBytes = await File.ReadAllBytesAsync(_config.TrustedRootPath);
            
            var updaterConfig = new UpdaterConfig(rootBytes, new Uri(_config.MetadataUrl))
            {
                LocalMetadataDir = _config.LocalMetadataDir,
                LocalTargetsDir = _config.LocalTargetsDir,
                Client = _httpClient
            };

            _updater = new Updater(updaterConfig);
            _logger.LogInformation("TUF updater initialized successfully");
            
            return _updater;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    public async Task<DownloadResult> SecureDownloadAsync(string fileName)
    {
        try
        {
            var updater = await GetOrInitializeUpdaterAsync();
            
            _logger.LogInformation("Refreshing TUF metadata");
            await updater.RefreshAsync();
            
            _logger.LogInformation("Looking for target file: {FileName}", fileName);
            var targetInfo = await updater.GetTargetInfo(fileName);
            
            if (!targetInfo.HasValue)
            {
                return new DownloadResult
                {
                    Success = false,
                    Error = $"File '{fileName}' not found in TUF repository"
                };
            }

            var (remotePath, targetFile) = targetInfo.Value;
            _logger.LogInformation("Found target {FileName} at {RemotePath}, size: {Size} bytes", 
                fileName, remotePath, targetFile.Length);

            var (localPath, data) = await updater.DownloadTarget(targetFile, fileName);
            
            _logger.LogInformation("Successfully downloaded and verified {FileName}", fileName);
            
            return new DownloadResult
            {
                Success = true,
                Data = data,
                LocalPath = localPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during secure download of {FileName}", fileName);
            return new DownloadResult
            {
                Success = false,
                Error = $"Download failed: {ex.Message}"
            };
        }
    }

    public async Task<List<string>> GetAvailableFilesAsync()
    {
        try
        {
            var updater = await GetOrInitializeUpdaterAsync();
            await updater.RefreshAsync();
            
            // This would need to be implemented in the TUF library
            // For now, return a placeholder list
            return new List<string> { "example-file.txt", "app.exe", "config.json" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available files");
            throw;
        }
    }

    public async Task<object> GetRepositoryStatusAsync()
    {
        try
        {
            var updater = await GetOrInitializeUpdaterAsync();
            
            return new
            {
                metadataUrl = _config.MetadataUrl,
                localMetadataDir = _config.LocalMetadataDir,
                localTargetsDir = _config.LocalTargetsDir,
                trustedRootPath = _config.TrustedRootPath,
                status = "Ready",
                lastRefresh = DateTime.UtcNow // Placeholder
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repository status");
            return new
            {
                status = "Error",
                error = ex.Message
            };
        }
    }

    private async Task CreateDemoTrustedRoot()
    {
        var demoRoot = new
        {
            signed = new
            {
                _type = "root",
                spec_version = "1.0.0",
                version = 1,
                expires = "2030-01-01T00:00:00Z",
                keys = new { },
                roles = new
                {
                    root = new { keyids = new string[0], threshold = 1 },
                    timestamp = new { keyids = new string[0], threshold = 1 },
                    snapshot = new { keyids = new string[0], threshold = 1 },
                    targets = new { keyids = new string[0], threshold = 1 }
                }
            },
            signatures = new object[0]
        };

        var json = JsonSerializer.Serialize(demoRoot, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await File.WriteAllTextAsync(_config.TrustedRootPath, json);
        _logger.LogInformation("Created demo trusted root at {Path}", _config.TrustedRootPath);
    }
}

// Result model for downloads
public class DownloadResult
{
    public bool Success { get; set; }
    public byte[]? Data { get; set; }
    public string? LocalPath { get; set; }
    public string? Error { get; set; }
}