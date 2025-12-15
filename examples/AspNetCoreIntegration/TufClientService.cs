using TUF;
using TUF.Exceptions;

/// <summary>
/// Production-ready TUF client service for ASP.NET Core applications.
/// Demonstrates proper integration patterns including logging, error handling,
/// and dependency injection.
/// </summary>
public class TufClientService : ITufClient
{
    private readonly ILogger<TufClientService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Updater? _updater;
    private readonly string _metadataDir;
    private readonly string _targetsDir;
    private DateTime _lastRefresh = DateTime.MinValue;
    private int _targetCount = 0;

    public TufClientService(ILogger<TufClientService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        // Get TUF configuration from appsettings.json
        var repositoryUrl = _configuration["Tuf:RepositoryUrl"] ?? "https://example.com/tuf-repo";
        _metadataDir = _configuration["Tuf:MetadataDirectory"] ?? Path.Combine(Path.GetTempPath(), "tuf-metadata");
        _targetsDir = _configuration["Tuf:TargetsDirectory"] ?? Path.Combine(Path.GetTempPath(), "tuf-targets");
        
        try
        {
            // Ensure directories exist
            Directory.CreateDirectory(_metadataDir);
            Directory.CreateDirectory(_targetsDir);
            
            // Initialize TUF updater with activity tracing
            using var activity = TufActivitySource.StartActivity("TufClientInitialization");
            activity?.SetTag("repository.url", repositoryUrl);
            activity?.SetTag("metadata.directory", _metadataDir);
            
            _logger.LogInformation("Initializing TUF client for repository: {RepositoryUrl}", repositoryUrl);
            
            // For this demo, we'll create a mock updater since we don't have a real TUF repository
            // In production, you would initialize the real Updater here
            _logger.LogWarning("Demo mode: Using mock TUF implementation. Configure a real TUF repository for production use.");
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize TUF client");
            throw new TufConfigurationException("Failed to initialize TUF client", ex.Message);
        }
    }

    public async Task<TufStatus> GetStatusAsync()
    {
        using var activity = TufActivitySource.StartActivity("GetTufStatus");
        
        try
        {
            var repositoryUrl = _configuration["Tuf:RepositoryUrl"] ?? "https://example.com/tuf-repo";
            var isHealthy = await CheckRepositoryHealthAsync();
            
            var status = new TufStatus(
                LastRefresh: _lastRefresh,
                TargetCount: _targetCount,
                RepositoryUrl: repositoryUrl,
                IsHealthy: isHealthy,
                NextScheduledRefresh: _lastRefresh.AddHours(1) // Example: refresh every hour
            );
            
            _logger.LogDebug("TUF status retrieved: {TargetCount} targets, last refresh: {LastRefresh}", 
                status.TargetCount, status.LastRefresh);
                
            activity?.SetTag("status.healthy", isHealthy);
            activity?.SetTag("status.target_count", _targetCount);
            
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get TUF status");
            activity?.SetTag("error", true);
            throw new TufConfigurationException($"Failed to get TUF status: {ex.Message}");
        }
    }

    public async Task RefreshAsync()
    {
        using var activity = TufActivitySource.StartActivity("RefreshTufMetadata");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting TUF metadata refresh");
            
            // In a real implementation, this would call _updater.Refresh()
            // For demo purposes, we'll simulate the refresh operation
            await SimulateRefreshAsync();
            
            _lastRefresh = DateTime.UtcNow;
            _logger.LogInformation("TUF metadata refresh completed successfully in {ElapsedMs}ms", 
                stopwatch.ElapsedMilliseconds);
            
            activity?.SetTag("refresh.success", true);
            activity?.SetTag("refresh.duration_ms", stopwatch.ElapsedMilliseconds);
        }
        catch (TufException)
        {
            activity?.SetTag("refresh.success", false);
            throw; // Re-throw TUF-specific exceptions as-is
        }
        catch (Exception ex)
        {
            activity?.SetTag("refresh.success", false);
            _logger.LogError(ex, "TUF metadata refresh failed");
            throw new RepositoryNetworkException("Failed to refresh TUF metadata", null, null, ex);
        }
    }

    public async Task<IEnumerable<string>> GetAvailableTargetsAsync()
    {
        using var activity = TufActivitySource.StartActivity("GetAvailableTargets");
        
        try
        {
            // In a real implementation, this would enumerate targets from TrustedMetadata
            // For demo purposes, return mock target list
            var targets = await GetMockTargetsAsync();
            
            _logger.LogDebug("Retrieved {TargetCount} available targets", targets.Count());
            activity?.SetTag("targets.count", targets.Count());
            
            return targets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available targets");
            activity?.SetTag("error", true);
            throw new TufConfigurationException($"Failed to get available targets: {ex.Message}");
        }
    }

    public async Task<string> DownloadTargetAsync(string targetName)
    {
        using var activity = TufActivitySource.StartActivity("DownloadTarget");
        activity?.SetTag("target.name", targetName);
        
        try
        {
            _logger.LogInformation("Downloading target: {TargetName}", targetName);
            
            // In a real implementation, this would call _updater.DownloadTarget()
            var targetPath = await SimulateDownloadAsync(targetName);
            
            _logger.LogInformation("Successfully downloaded target {TargetName} to {TargetPath}", 
                targetName, targetPath);
            
            activity?.SetTag("download.success", true);
            activity?.SetTag("download.path", targetPath);
            
            return targetPath;
        }
        catch (TargetNotFoundException)
        {
            activity?.SetTag("download.success", false);
            activity?.SetTag("download.error", "target_not_found");
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetTag("download.success", false);
            _logger.LogError(ex, "Failed to download target {TargetName}", targetName);
            throw new TargetIntegrityException($"Failed to download target '{targetName}': {ex.Message}", targetName);
        }
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync(string currentVersion)
    {
        using var activity = TufActivitySource.StartActivity("CheckForUpdates");
        activity?.SetTag("current.version", currentVersion);
        
        try
        {
            _logger.LogInformation("Checking for updates, current version: {CurrentVersion}", currentVersion);
            
            // In a real implementation, this would check targets metadata for version updates
            var updateInfo = await SimulateUpdateCheckAsync(currentVersion);
            
            _logger.LogInformation("Update check completed: {UpdateAvailable}", updateInfo.UpdateAvailable);
            activity?.SetTag("update.available", updateInfo.UpdateAvailable);
            activity?.SetTag("latest.version", updateInfo.LatestVersion);
            
            return updateInfo;
        }
        catch (Exception ex)
        {
            activity?.SetTag("error", true);
            _logger.LogError(ex, "Failed to check for updates");
            throw new MetadataValidationException($"Failed to check for updates: {ex.Message}");
        }
    }

    // Helper methods for demo purposes
    private async Task<bool> CheckRepositoryHealthAsync()
    {
        // In production, this would perform actual health checks against the TUF repository
        await Task.Delay(10); // Simulate network check
        return true; // Mock healthy status
    }

    private async Task SimulateRefreshAsync()
    {
        // Simulate metadata refresh operation
        await Task.Delay(100);
        _targetCount = Random.Shared.Next(3, 10);
    }

    private async Task<IEnumerable<string>> GetMockTargetsAsync()
    {
        await Task.Delay(10);
        return new[] 
        { 
            "app.exe", 
            "config.json", 
            "update.zip",
            "readme.txt",
            "lib/core.dll" 
        };
    }

    private async Task<string> SimulateDownloadAsync(string targetName)
    {
        var availableTargets = await GetMockTargetsAsync();
        if (!availableTargets.Contains(targetName))
        {
            throw new TargetNotFoundException($"Target '{targetName}' not found in repository", targetName);
        }

        // Simulate download with integrity verification
        await Task.Delay(200);
        
        var targetPath = Path.Combine(_targetsDir, targetName);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        
        // Create mock file
        await File.WriteAllTextAsync(targetPath, $"Mock content for {targetName}\nDownloaded at: {DateTime.UtcNow}");
        
        return targetPath;
    }

    private async Task<UpdateInfo> SimulateUpdateCheckAsync(string currentVersion)
    {
        await Task.Delay(50);
        
        // Mock update logic - in production, compare against targets metadata
        var isNewer = Version.TryParse(currentVersion, out var current) && current < new Version("2.0.0");
        
        return new UpdateInfo(
            UpdateAvailable: isNewer,
            LatestVersion: "2.0.0",
            Description: "Bug fixes and performance improvements",
            DownloadUrl: "/api/tuf/targets/app-v2.0.0.zip/download");
    }
}