using System.CommandLine;
using TUF;

namespace TufConformanceCli;

/// <summary>
/// TUF Conformance CLI implementation following the CLIENT-CLI.md specification
/// from the TUF conformance test suite: https://github.com/theupdateframework/tuf-conformance
/// </summary>
public class Program
{
    // Shared options to reduce duplication
    private static readonly Option<DirectoryInfo> MetadataDirOption = new("--metadata-dir")
    {
        Description = "Directory for metadata storage",
        Required = true,
        Recursive = true
    };

    private static readonly Option<string> MetadataUrlOption = new("--metadata-url")
    {
        Description = "Base URL for metadata",
        Required = true
    };

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("TUF .NET Conformance CLI")
        {
            CreateInitCommand(),
            CreateRefreshCommand(),
            CreateDownloadCommand(),
            MetadataDirOption
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static Command CreateInitCommand()
    {
        var trustedRootArg = new Argument<FileInfo>("trusted-root")
        {
            Description = "Path to trusted root.json file"
        };

        var command = new Command("init", "Initialize client's local trusted metadata")
        {
            trustedRootArg
        };

        command.SetAction((parseResult) =>
        {
            var metadataDir = parseResult.GetValue(MetadataDirOption)!;
            var trustedRoot = parseResult.GetValue(trustedRootArg)!;
            var exitCode = HandleInitCommand(metadataDir.FullName, trustedRoot.FullName);
            return exitCode;
        });

        return command;
    }

    private static Command CreateRefreshCommand()
    {
        var command = new Command("refresh", "Update local metadata from repository")
        {
            MetadataUrlOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataDir = parseResult.GetValue(MetadataDirOption)!;
            var metadataUrl = parseResult.GetValue(MetadataUrlOption)!;
            var exitCode = await HandleRefreshCommand(metadataDir.FullName, metadataUrl);
            return exitCode;
        });

        return command;
    }

    private static Command CreateDownloadCommand()
    {
        var targetNameOption = new Option<string>("--target-name")
        {
            Description = "Name of target to download",
            Required = true
        };

        var targetBaseUrlOption = new Option<string>("--target-base-url")
        {
            Description = "Base URL for targets",
            Required = true
        };

        var targetDirOption = new Option<DirectoryInfo>("--target-dir")
        {
            Description = "Directory to save downloaded target",
            Required = true
        };

        var command = new Command("download", "Download and verify an artifact from repository")
        {
            MetadataUrlOption,
            targetNameOption,
            targetBaseUrlOption,
            targetDirOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataDir = parseResult.GetValue(MetadataDirOption)!;
            var metadataUrl = parseResult.GetValue(MetadataUrlOption)!;
            var targetName = parseResult.GetValue(targetNameOption)!;
            var targetBaseUrl = parseResult.GetValue(targetBaseUrlOption)!;
            var targetDir = parseResult.GetValue(targetDirOption)!;
            
            var exitCode = await HandleDownloadCommand(metadataDir.FullName, metadataUrl, targetName, targetBaseUrl, targetDir.FullName);
            return exitCode;
        });

        return command;
    }

    /// <summary>
    /// Handle the init command: Initialize client's local trusted metadata
    /// </summary>
    private static int HandleInitCommand(string metadataDir, string trustedRootPath)
    {
        try
        {
            Console.WriteLine($"Initializing TUF client with metadata dir: {metadataDir}");
            Console.WriteLine($"Using trusted root: {trustedRootPath}");

            // Ensure metadata directory exists
            Directory.CreateDirectory(metadataDir);

            // Validate trusted root file exists
            if (!File.Exists(trustedRootPath))
            {
                Console.Error.WriteLine($"Error: Trusted root file not found: {trustedRootPath}");
                return 1;
            }

            // Copy trusted root to metadata directory as root.json (non-versioned filename)
            var rootPath = Path.Combine(metadataDir, "root.json");
            File.Copy(trustedRootPath, rootPath, overwrite: true);

            Console.WriteLine("TUF client initialized successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during init: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handle the refresh command: Update local metadata from repository
    /// </summary>
    private static async Task<int> HandleRefreshCommand(string metadataDir, string metadataUrl)
    {
        try
        {
            Console.WriteLine($"Refreshing metadata from: {metadataUrl}");
            Console.WriteLine($"Metadata directory: {metadataDir}");

            // Validate metadata directory exists and has root.json
            var rootPath = Path.Combine(metadataDir, "root.json");
            if (!File.Exists(rootPath))
            {
                Console.Error.WriteLine("Error: No root.json found. Run 'init' command first.");
                return 1;
            }

            // Read the trusted root
            byte[] rootBytes = await File.ReadAllBytesAsync(rootPath);

            // Configure the updater with the metadata directory and repository URL
            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir,
                LocalTargetsDir = Path.Combine(metadataDir, "targets"), // Default targets directory
                Client = new HttpClient()
            };

            // Create and initialize the updater
            var updater = new Updater(config);
            await updater.RefreshAsync();

            Console.WriteLine("Metadata refresh completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during refresh: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handle the download command: Download and verify an artifact from repository
    /// </summary>
    private static async Task<int> HandleDownloadCommand(string metadataDir, string metadataUrl,
                                                         string targetName, string targetBaseUrl, string targetDir)
    {
        try
        {
            Console.WriteLine($"Downloading target: {targetName}");
            Console.WriteLine($"From: {targetBaseUrl}");
            Console.WriteLine($"To: {targetDir}");

            // Validate metadata directory exists and has root.json
            var rootPath = Path.Combine(metadataDir, "root.json");
            if (!File.Exists(rootPath))
            {
                Console.Error.WriteLine("Error: No root.json found. Run 'init' command first.");
                return 1;
            }

            // Ensure target directory exists
            Directory.CreateDirectory(targetDir);

            // Read the trusted root
            byte[] rootBytes = await File.ReadAllBytesAsync(rootPath);

            // Configure the updater - use targetBaseUrl as the metadata URL base for now
            // In a full implementation this might be more sophisticated
            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir,
                LocalTargetsDir = targetDir,
                Client = new HttpClient()
            };

            // Create and initialize the updater
            var updater = new Updater(config);

            // First ensure metadata is up-to-date
            await updater.RefreshAsync();

            // Get target information and download
            var (targetPath, targetInfo) = await updater.GetTargetInfo(targetName);
            Console.WriteLine($"Target found: {targetPath}");

            // Check for cached version first
            var cached = await updater.FindCachedTarget(targetInfo, targetPath, null);
            if (cached.HasValue)
            {
                Console.WriteLine($"Target already cached at: {cached.Value.FilePath}");
                return 0;
            }
            else
            {
                // Download the target file
                var result = await updater.DownloadTarget(targetInfo, targetPath, null, new Uri(targetBaseUrl));
                Console.WriteLine($"Target downloaded and verified successfully: {result.FilePath}");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during download: {ex.Message}");
            return 1;
        }
    }
}