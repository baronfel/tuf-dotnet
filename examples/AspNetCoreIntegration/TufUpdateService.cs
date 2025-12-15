using TUF;
using TUF.Exceptions;

/// <summary>
/// Background service that performs periodic TUF metadata refresh operations.
/// Demonstrates how to integrate TUF operations with ASP.NET Core hosted services
/// for production-ready applications.
/// </summary>
public class TufUpdateService : BackgroundService
{
    private readonly ITufClient _tufClient;
    private readonly ILogger<TufUpdateService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _refreshInterval;

    public TufUpdateService(
        ITufClient tufClient,
        ILogger<TufUpdateService> logger,
        IConfiguration configuration)
    {
        _tufClient = tufClient;
        _logger = logger;
        _configuration = configuration;
        
        // Configure refresh interval from settings (default: 30 minutes)
        var intervalMinutes = _configuration.GetValue("Tuf:RefreshIntervalMinutes", 30);
        _refreshInterval = TimeSpan.FromMinutes(intervalMinutes);
        
        _logger.LogInformation("TUF update service configured with {RefreshInterval} refresh interval", 
            _refreshInterval);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TUF update service starting");

        // Perform initial refresh on startup
        await PerformInitialRefreshAsync(stoppingToken);

        // Start periodic refresh cycle
        await PerformPeriodicRefreshAsync(stoppingToken);
    }

    private async Task PerformInitialRefreshAsync(CancellationToken cancellationToken)
    {
        using var activity = TufActivitySource.StartActivity("TufInitialRefresh");
        
        try
        {
            _logger.LogInformation("Performing initial TUF metadata refresh");
            await _tufClient.RefreshAsync();
            _logger.LogInformation("Initial TUF metadata refresh completed successfully");
            activity?.SetTag("initial_refresh.success", true);
        }
        catch (TufException ex)
        {
            // Log TUF-specific exceptions with structured data
            _logger.LogTufException(ex);
            _logger.LogWarning("Initial TUF refresh failed, will retry on next cycle");
            activity?.SetTag("initial_refresh.success", false);
            activity?.SetTag("initial_refresh.error_type", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during initial TUF refresh");
            activity?.SetTag("initial_refresh.success", false);
            activity?.SetTag("initial_refresh.error_type", "unexpected");
        }
    }

    private async Task PerformPeriodicRefreshAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_refreshInterval);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for next refresh interval
                await timer.WaitForNextTickAsync(stoppingToken);
                
                await PerformScheduledRefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("TUF update service stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in TUF update service loop");
                // Continue the loop to maintain service resilience
            }
        }
    }

    private async Task PerformScheduledRefreshAsync(CancellationToken cancellationToken)
    {
        using var activity = TufActivitySource.StartActivity("TufScheduledRefresh");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Starting scheduled TUF metadata refresh");
            
            // Perform the refresh operation
            await _tufClient.RefreshAsync();
            
            stopwatch.Stop();
            _logger.LogInformation("Scheduled TUF refresh completed successfully in {ElapsedMs}ms", 
                stopwatch.ElapsedMilliseconds);
            
            activity?.SetTag("scheduled_refresh.success", true);
            activity?.SetTag("scheduled_refresh.duration_ms", stopwatch.ElapsedMilliseconds);
            
            // Optionally check for updates after refresh
            await CheckForApplicationUpdatesAsync(cancellationToken);
        }
        catch (TufException ex)
        {
            stopwatch.Stop();
            _logger.LogTufException(ex);
            _logger.LogWarning("Scheduled TUF refresh failed after {ElapsedMs}ms, will retry on next cycle", 
                stopwatch.ElapsedMilliseconds);
            
            activity?.SetTag("scheduled_refresh.success", false);
            activity?.SetTag("scheduled_refresh.error_type", ex.GetType().Name);
            activity?.SetTag("scheduled_refresh.duration_ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during scheduled TUF refresh");
            
            activity?.SetTag("scheduled_refresh.success", false);
            activity?.SetTag("scheduled_refresh.error_type", "unexpected");
            activity?.SetTag("scheduled_refresh.duration_ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task CheckForApplicationUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            var updateInfo = await _tufClient.CheckForUpdatesAsync(currentVersion);
            
            if (updateInfo.UpdateAvailable)
            {
                _logger.LogInformation("Application update available: {LatestVersion} (current: {CurrentVersion})", 
                    updateInfo.LatestVersion, currentVersion);
                
                // In a production application, you might:
                // - Send notification to administrators
                // - Store update information for display in admin UI
                // - Trigger automated update process if configured
                // - Update health check status to indicate update available
                
                _logger.LogInformation("Update notification: {Description}", updateInfo.Description);
            }
            else
            {
                _logger.LogDebug("Application is up to date: {CurrentVersion}", currentVersion);
            }
        }
        catch (TufException ex)
        {
            _logger.LogWarning("Failed to check for application updates: {Error}", ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TUF update service stopping");
        await base.StopAsync(stoppingToken);
        _logger.LogInformation("TUF update service stopped");
    }
}