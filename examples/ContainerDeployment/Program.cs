using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TUF;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContainerDeployment;

/// <summary>
/// TUF Container Deployment Example
/// 
/// This example demonstrates how to use TUF .NET in containerized environments,
/// showing production-ready patterns for secure software updates in cloud-native applications.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("TUF .NET Container Deployment Example");
        Console.WriteLine("=====================================");

        var builder = Host.CreateApplicationBuilder(args);
        
        // Add services
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<TufClientService>();
        builder.Services.AddLogging();

        var app = builder.Build();

        try
        {
            var tufService = app.Services.GetRequiredService<TufClientService>();
            await tufService.DemonstrateAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}

/// <summary>
/// Container-optimized TUF client service implementation
/// </summary>
public class TufClientService
{
    private readonly ILogger<TufClientService> _logger;
    private readonly HttpClient _httpClient;

    public TufClientService(IHttpClientFactory httpClientFactory, ILogger<TufClientService> logger)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30); // Container-appropriate timeout
    }

    public async Task DemonstrateAsync()
    {
        _logger.LogInformation("Starting TUF container deployment demonstration...");

        // Container-optimized configuration using environment variables
        var metadataUrl = Environment.GetEnvironmentVariable("TUF_METADATA_URL") ?? 
                         "https://example.com/metadata";
                         
        var targetsUrl = Environment.GetEnvironmentVariable("TUF_TARGETS_URL") ?? 
                        "https://example.com/targets";

        var trustedRootPath = Environment.GetEnvironmentVariable("TUF_TRUSTED_ROOT_PATH") ?? 
                             "/app/trusted-root.json";

        // Use container-optimized paths
        var metadataDir = "/tmp/tuf-metadata";
        var targetsDir = "/tmp/tuf-targets";

        // Create directories if they don't exist
        Directory.CreateDirectory(metadataDir);
        Directory.CreateDirectory(targetsDir);

        _logger.LogInformation("Container configuration:");
        _logger.LogInformation("  Metadata URL: {MetadataUrl}", metadataUrl);
        _logger.LogInformation("  Targets URL: {TargetsUrl}", targetsUrl);
        _logger.LogInformation("  Trusted Root Path: {TrustedRootPath}", trustedRootPath);
        _logger.LogInformation("  Metadata Dir: {MetadataDir}", metadataDir);
        _logger.LogInformation("  Targets Dir: {TargetsDir}", targetsDir);

        // Load trusted root - in production, this should be embedded in the container image
        byte[] trustedRootBytes;
        if (File.Exists(trustedRootPath))
        {
            trustedRootBytes = await File.ReadAllBytesAsync(trustedRootPath);
            _logger.LogInformation("Loaded trusted root from {Path} ({Size} bytes)", trustedRootPath, trustedRootBytes.Length);
        }
        else
        {
            // Fallback: create a placeholder trusted root for demonstration
            _logger.LogWarning("No trusted root found at {Path}, creating placeholder for demo", trustedRootPath);
            trustedRootBytes = CreatePlaceholderTrustedRoot();
            await File.WriteAllBytesAsync(trustedRootPath, trustedRootBytes);
        }

        try
        {
            var config = new UpdaterConfig(trustedRootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir,
                LocalTargetsDir = targetsDir,
                RemoteTargetsUrl = new Uri(targetsUrl),
                Client = _httpClient,
                DisableLocalCache = false // Enable caching for better performance
            };

            var updater = new Updater(config);

            _logger.LogInformation("TUF client initialized successfully");

            // Display container information
            DisplayContainerInfo();

            // In a real application, you would call updater.RefreshAsync() and download targets
            _logger.LogInformation("TUF container deployment demonstration completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize TUF client");
            throw;
        }
    }

    private void DisplayContainerInfo()
    {
        _logger.LogInformation("Container Environment:");
        _logger.LogInformation("  Hostname: {Hostname}", Environment.MachineName);
        _logger.LogInformation("  Platform: {Platform}", System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        _logger.LogInformation("  Working Directory: {WorkingDirectory}", Environment.CurrentDirectory);
        _logger.LogInformation("  Process ID: {ProcessId}", Environment.ProcessId);
        _logger.LogInformation("  .NET Version: {DotNetVersion}", Environment.Version);
    }

    private static byte[] CreatePlaceholderTrustedRoot()
    {
        // This is a minimal placeholder for demonstration purposes
        // In production, you MUST use a real trusted root obtained through secure channels
        var placeholderJson = """
        {
          "signed": {
            "type": "root",
            "spec_version": "1.0.0",
            "version": 1,
            "expires": "2026-12-04T00:00:00Z",
            "keys": {},
            "roles": {
              "root": {"keyids": [], "threshold": 1},
              "timestamp": {"keyids": [], "threshold": 1},
              "snapshot": {"keyids": [], "threshold": 1},
              "targets": {"keyids": [], "threshold": 1}
            },
            "consistent_snapshot": false
          },
          "signatures": []
        }
        """;

        return System.Text.Encoding.UTF8.GetBytes(placeholderJson);
    }
}