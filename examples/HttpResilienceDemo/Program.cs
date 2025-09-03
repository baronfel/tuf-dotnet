using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TUF;
using TUF.Http;

namespace HttpResilienceDemo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 TUF .NET HTTP Resilience Demo");
        Console.WriteLine("==================================\n");

        // Set up logging to see HTTP resilience in action
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var logger = loggerFactory.CreateLogger<Updater>();

        // Create a custom HTTP client with resilience configuration
        var httpClient = new HttpClient();
        
        // Configure HTTP resilience settings
        var resilienceConfig = new HttpResilienceConfig
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(10),
            RequestTimeout = TimeSpan.FromSeconds(30),
            UserAgent = "TUF-HttpResilienceDemo/1.0"
        };

        Console.WriteLine("📋 HTTP Resilience Configuration:");
        Console.WriteLine($"   Max Retries: {resilienceConfig.MaxRetries}");
        Console.WriteLine($"   Base Delay: {resilienceConfig.BaseDelay}");
        Console.WriteLine($"   Max Delay: {resilienceConfig.MaxDelay}");
        Console.WriteLine($"   Request Timeout: {resilienceConfig.RequestTimeout}");
        Console.WriteLine($"   User Agent: {resilienceConfig.UserAgent}");
        Console.WriteLine($"   Retry Status Codes: {string.Join(", ", resilienceConfig.RetryStatusCodes)}");
        Console.WriteLine();

        // Demonstrate ResilientHttpClient directly
        Console.WriteLine("🔗 Testing ResilientHttpClient directly...");
        
        var resilientClient = new ResilientHttpClient(
            httpClient, 
            resilienceConfig, 
            loggerFactory.CreateLogger<ResilientHttpClient>());

        try
        {
            // Try to download a file that doesn't exist to show error handling
            var testUri = new Uri("https://httpbin.org/status/500");
            Console.WriteLine($"📥 Attempting download from {testUri}");
            
            var data = await resilientClient.DownloadFileAsync(testUri, 1000);
            Console.WriteLine($"✅ Downloaded {data.Length} bytes successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Download failed: {ex.GetType().Name}: {ex.Message}");
        }
        
        Console.WriteLine();

        // Demonstrate Updater with HTTP resilience
        Console.WriteLine("🔄 Testing Updater with HTTP resilience...");
        
        try
        {
            var updaterConfig = new UpdaterConfig(
                initialRootBytes: CreateDummyRootJson(),
                remoteMetadataUrl: new Uri("https://httpbin.org/metadata/"))
            {
                LocalMetadataDir = Path.GetTempPath(),
                LocalTargetsDir = Path.GetTempPath(),
                Client = httpClient,
                HttpResilienceConfig = resilienceConfig,
                Logger = logger
            };

            var updater = new Updater(updaterConfig);
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Console.WriteLine("📡 Attempting TUF refresh with cancellation token...");
            
            await updater.RefreshAsync(cts.Token);
            Console.WriteLine("✅ TUF refresh completed successfully");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("⏰ TUF refresh was cancelled (as expected for demo)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TUF refresh failed: {ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("🎯 Key HTTP Resilience Features Demonstrated:");
        Console.WriteLine("   ✅ Configurable retry policies with exponential backoff");
        Console.WriteLine("   ✅ Request timeout handling");
        Console.WriteLine("   ✅ Cancellation token support throughout TUF operations");
        Console.WriteLine("   ✅ Structured logging for HTTP operations");
        Console.WriteLine("   ✅ Custom user agent strings");
        Console.WriteLine("   ✅ Production-ready error handling with specific exceptions");
        Console.WriteLine();
        Console.WriteLine("🚀 This brings TUF .NET HTTP handling to parity with other mature implementations!");
    }

    /// <summary>
    /// Creates a minimal dummy root.json for demo purposes
    /// </summary>
    private static byte[] CreateDummyRootJson()
    {
        var rootJson = """
        {
          "signed": {
            "_type": "root",
            "spec_version": "1.0.0",
            "version": 1,
            "expires": "2099-01-01T00:00:00Z",
            "keys": {},
            "roles": {
              "root": { "keyids": [], "threshold": 1 },
              "timestamp": { "keyids": [], "threshold": 1 },
              "snapshot": { "keyids": [], "threshold": 1 },
              "targets": { "keyids": [], "threshold": 1 }
            },
            "consistent_snapshot": false
          },
          "signatures": []
        }
        """;
        return System.Text.Encoding.UTF8.GetBytes(rootJson);
    }
}