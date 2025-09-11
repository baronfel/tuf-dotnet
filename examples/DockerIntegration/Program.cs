using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TUF;

namespace DockerIntegration;

/// <summary>
/// Demonstrates TUF .NET integration in containerized environments.
/// 
/// This example shows how to:
/// - Configure TUF in containerized applications with environment variables
/// - Handle secure software updates in containerized deployments
/// - Implement proper logging and error handling for production containers
/// - Work with volume mounts and container-specific file paths
/// </summary>
public class Program
{
    private static IConfiguration _configuration = null!;
    private static ILogger _logger = null!;

    public static async Task<int> Main(string[] args)
    {
        // Build configuration from environment variables and appsettings.json
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables("TUF_")
            .Build();

        // Set up logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole(options =>
            {
                options.FormatterName = "simple";
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        _logger = loggerFactory.CreateLogger<Program>();

        // Create the root command for container operations
        var rootCommand = new RootCommand("TUF .NET Docker Integration Demo")
        {
            CreateRefreshCommand(),
            CreateDownloadCommand(),
            CreateHealthCheckCommand(),
            CreateInitCommand()
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }

    /// <summary>
    /// Creates the refresh command for updating TUF metadata in containers
    /// </summary>
    private static Command CreateRefreshCommand()
    {
        var refreshCommand = new Command("refresh", "Refresh TUF metadata from the repository");
        
        refreshCommand.SetAction(async (parseResult, token) =>
        {
            _logger.LogInformation("🐳 Starting TUF metadata refresh in container environment");
            
            try
            {
                var updater = CreateTufUpdater();
                await updater.RefreshAsync();
                _logger.LogInformation("✅ TUF metadata refresh completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ TUF metadata refresh failed: {Message}", ex.Message);
                return 1;
            }
        });
        
        return refreshCommand;
    }

    /// <summary>
    /// Creates the download command for securely downloading files in containers
    /// </summary>
    private static Command CreateDownloadCommand()
    {
        var downloadCommand = new Command("download", "Download and verify target files");
        
        var targetOption = new Option<string>("--target") 
        { 
            Description = "Target file path to download",
            Required = true 
        };
        var outputOption = new Option<string>("--output") 
        { 
            Description = "Output file path (defaults to /app/downloads/)" 
        };
            
        downloadCommand.Add(targetOption);
        downloadCommand.Add(outputOption);
        
        downloadCommand.SetAction(async (parseResult, token) =>
        {
            var target = parseResult.GetValue(targetOption)!;
            var output = parseResult.GetValue(outputOption) ?? Path.Combine("/app/downloads", Path.GetFileName(target));
            
            _logger.LogInformation("🐳 Downloading target {Target} to {Output} in container", target, output);
            
            try
            {
                var updater = CreateTufUpdater();
                
                // First refresh metadata
                await updater.RefreshAsync();
                
                // Get target info
                var targetInfo = await updater.GetTargetInfo(target);
                if (targetInfo == null)
                {
                    _logger.LogError("❌ Target file {Target} not found in TUF metadata", target);
                    return 1;
                }

                _logger.LogInformation("Target info - Length: {Length}, File: {File}", 
                    targetInfo.Value.File.Length, targetInfo.Value.RemotePath);

                // Ensure output directory exists
                var outputDir = Path.GetDirectoryName(output);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Download target with verification
                await updater.DownloadTarget(targetInfo.Value.File, targetInfo.Value.RemotePath, output);
                
                _logger.LogInformation("✅ Target {Target} downloaded and verified successfully", target);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Target download failed: {Message}", ex.Message);
                return 1;
            }
        });
        
        return downloadCommand;
    }

    /// <summary>
    /// Creates a health check command for container orchestration
    /// </summary>
    private static Command CreateHealthCheckCommand()
    {
        var healthCommand = new Command("health", "Check TUF service health for container orchestration");
        
        healthCommand.SetAction(async (parseResult, token) =>
        {
            _logger.LogInformation("🐳 Performing TUF health check");
            
            try
            {
                // Basic configuration check
                var metadataUrl = _configuration["MetadataUrl"];
                if (string.IsNullOrEmpty(metadataUrl))
                {
                    _logger.LogWarning("⚠️ MetadataUrl not configured");
                    return 1;
                }

                var rootKeyPath = _configuration["RootKeyPath"] ?? "/app/trusted/root.json";
                if (!File.Exists(rootKeyPath))
                {
                    _logger.LogWarning("⚠️ Trusted root metadata not found at {RootPath}", rootKeyPath);
                    return 1;
                }

                // Test connectivity to TUF repository
                using var httpClient = new HttpClient();
                try
                {
                    using var response = await httpClient.GetAsync($"{metadataUrl.TrimEnd('/')}/timestamp.json");
                    var isHealthy = response.IsSuccessStatusCode;
                    
                    if (isHealthy)
                    {
                        _logger.LogInformation("✅ TUF service is healthy");
                        return 0;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ TUF repository not accessible: {StatusCode}", response.StatusCode);
                        return 1;
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning("⚠️ TUF repository connectivity failed: {Message}", ex.Message);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Health check error: {Message}", ex.Message);
                return 1;
            }
        });
        
        return healthCommand;
    }

    /// <summary>
    /// Creates an initialization command for container startup
    /// </summary>
    private static Command CreateInitCommand()
    {
        var initCommand = new Command("init", "Initialize TUF client for container environment");
        
        initCommand.SetAction((parseResult, token) =>
        {
            _logger.LogInformation("🐳 Initializing TUF client in container environment");
            
            try
            {
                // Ensure directories exist
                var downloadsDir = _configuration["DownloadsDir"] ?? "/app/downloads";
                var metadataDir = _configuration["MetadataDir"] ?? "/app/metadata";
                
                Directory.CreateDirectory(downloadsDir);
                Directory.CreateDirectory(metadataDir);
                Directory.CreateDirectory("/app/trusted");

                _logger.LogInformation("Container paths - Downloads: {Downloads}, Metadata: {Metadata}", 
                    downloadsDir, metadataDir);

                // Validate configuration
                var metadataUrl = _configuration["MetadataUrl"];
                if (string.IsNullOrEmpty(metadataUrl))
                {
                    throw new InvalidOperationException("TUF_MetadataUrl environment variable is required");
                }

                var rootKeyPath = _configuration["RootKeyPath"] ?? "/app/trusted/root.json";
                if (!File.Exists(rootKeyPath))
                {
                    throw new FileNotFoundException($"Trusted root metadata not found at {rootKeyPath}. " +
                        "Mount trusted root metadata as volume or configure TUF_RootKeyPath");
                }

                _logger.LogInformation("Validated configuration - Metadata URL: {MetadataUrl}, Root: {RootPath}", 
                    metadataUrl, rootKeyPath);

                // Test TUF updater creation
                var updater = CreateTufUpdater();
                _logger.LogInformation("✅ TUF client initialization completed successfully");
                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ TUF client initialization failed: {Message}", ex.Message);
                return Task.FromResult(1);
            }
        });
        
        return initCommand;
    }

    /// <summary>
    /// Creates a TUF updater with container-specific configuration
    /// </summary>
    private static Updater CreateTufUpdater()
    {
        // Get configuration from environment variables and appsettings.json
        var metadataUrl = _configuration["MetadataUrl"] ?? 
            throw new InvalidOperationException("TUF_MetadataUrl environment variable is required");
        var rootKeyPath = _configuration["RootKeyPath"] ?? "/app/trusted/root.json";
        var downloadsDir = _configuration["DownloadsDir"] ?? "/app/downloads";
        var metadataDir = _configuration["MetadataDir"] ?? "/app/metadata";

        // Read trusted root metadata
        if (!File.Exists(rootKeyPath))
        {
            throw new FileNotFoundException($"Trusted root metadata not found at {rootKeyPath}");
        }

        var rootBytes = File.ReadAllBytes(rootKeyPath);
        _logger.LogInformation("Loaded trusted root metadata from {RootPath}", rootKeyPath);

        // Configure TUF updater for container environment
        var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
        {
            LocalMetadataDir = metadataDir,
            LocalTargetsDir = downloadsDir,
            Client = new HttpClient(),
            // Container-specific limits from configuration
            RootMaxLength = uint.Parse(_configuration["MaxMetadataBytes"] ?? "1048576"), // 1MB default
        };

        return new Updater(config);
    }
}