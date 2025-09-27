using System.CommandLine;
using System.Text;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace AzureIntegration;

/// <summary>
/// Azure Integration example demonstrating TUF with Azure Key Vault and Blob Storage.
/// 
/// This example showcases enterprise-grade TUF integration using Azure's cloud-native services:
/// - Azure Key Vault for HSM-backed cryptographic operations
/// - Azure Blob Storage for globally distributed repository hosting
/// - Azure Active Directory for identity and access management
/// - Integration with .NET logging and configuration patterns
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Set up logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger("AzureIntegration");

        // Create root command
        var rootCommand = new RootCommand("Azure Integration example for TUF .NET")
        {
            CreateBlobStorageDemoCommand()
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }

    /// <summary>
    /// Creates a command to demonstrate Azure Blob Storage repository operations.
    /// </summary>
    private static Command CreateBlobStorageDemoCommand()
    {
        var storageAccountOption = new Option<string>("--storage-account")
        {
            Description = "Azure Storage account name",
            Required = true
        };
        var containerNameOption = new Option<string>("--container-name")
        {
            Description = "Blob container name",
            Required = true
        };

        var command = new Command("blob-demo", "Demonstrate Azure Blob Storage repository backend")
        {
            storageAccountOption,
            containerNameOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var storageAccount = parseResult.GetValue(storageAccountOption)!;
            var containerName = parseResult.GetValue(containerNameOption)!;
            
            try
            {
                Console.WriteLine("☁️  Azure Blob Storage TUF Repository Demo");
                Console.WriteLine($"   Storage Account: {storageAccount}");
                Console.WriteLine($"   Container: {containerName}");
                Console.WriteLine();

                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<AzureBlobRepositoryBackend>();

                // Create the Azure Blob Storage backend
                Console.WriteLine("Connecting to Azure Blob Storage...");
                var backend = new AzureBlobRepositoryBackend(storageAccount, containerName, logger: logger);

                // Upload a test metadata file
                Console.WriteLine("Uploading test metadata file...");
                var testMetadata = """{"test": "metadata", "version": 1}""";
                var metadataBytes = Encoding.UTF8.GetBytes(testMetadata);
                var metadataUri = await backend.UploadFileAsync("metadata/test.json", metadataBytes);

                Console.WriteLine($"✅ Uploaded metadata file: {metadataUri}");

                // Upload a test target file
                Console.WriteLine("Uploading test target file...");
                var testTarget = "This is a test target file content"u8.ToArray();
                var targetUri = await backend.UploadFileAsync("targets/test-file.txt", testTarget);

                Console.WriteLine($"✅ Uploaded target file: {targetUri}");

                // List files in the repository
                Console.WriteLine("\nListing repository files:");
                await foreach (var filePath in backend.ListFilesAsync())
                {
                    Console.WriteLine($"   📄 {filePath}");
                }

                // Test file existence
                Console.WriteLine("\nTesting file existence:");
                var exists = await backend.FileExistsAsync("metadata/test.json");
                Console.WriteLine($"   metadata/test.json exists: {exists}");

                // Download and verify
                Console.WriteLine("\nDownloading and verifying files:");
                var downloadedMetadata = await backend.DownloadFileAsync("metadata/test.json");
                if (downloadedMetadata != null)
                {
                    var downloadedText = Encoding.UTF8.GetString(downloadedMetadata);
                    Console.WriteLine($"   Downloaded metadata: {downloadedText}");
                }

                Console.WriteLine("\n✅ Azure Blob Storage demo completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return 1;
            }

            return 0;
        });

        return command;
    }
}