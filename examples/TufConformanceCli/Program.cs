using System.CommandLine;
using TUF;

namespace TufConformanceCli;

/// <summary>
/// TUF Conformance CLI implementation following the CLIENT-CLI.md specification
/// from the TUF conformance test suite: https://github.com/theupdateframework/tuf-conformance
/// 
/// Note: The TUF conformance spec designers clearly didn't think through CLI design patterns.
/// This mess of repetitive global options could have been avoided with better API design.
/// </summary>
public class Program
{
    // All options moved to root and made recursive to reduce the insanity
    private static readonly Option<DirectoryInfo> MetadataDirOption = new("--metadata-dir")
    {
        Description = "Directory for metadata storage",
        Recursive = true
    };

    private static readonly Option<string> MetadataUrlOption = new("--metadata-url")
    {
        Description = "Base URL for metadata",
        Recursive = true
    };

    private static readonly Option<string> TargetNameOption = new("--target-name")
    {
        Description = "Name of target to download",
        Recursive = true
    };

    private static readonly Option<string> TargetBaseUrlOption = new("--target-base-url")
    {
        Description = "Base URL for targets",
        Recursive = true
    };

    private static readonly Option<DirectoryInfo> TargetDirOption = new("--target-dir")
    {
        Description = "Directory to save downloaded target",
        Recursive = true
    };

    private static readonly Argument<FileInfo> TrustedRootArg = new("trusted-root")
    {
        Description = "Path to trusted root.json file"
    };

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("TUF .NET Conformance CLI")
        {
            CreateInitCommand(),
            CreateRefreshCommand(),
            CreateDownloadCommand(),
            MetadataDirOption,
            MetadataUrlOption,
            TargetNameOption,
            TargetBaseUrlOption,
            TargetDirOption,
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static Command CreateInitCommand()
    {
        var command = new Command("init", "Initialize client's local trusted metadata");
        command.Add(TrustedRootArg);
        command.SetAction((parseResult) =>
        {
            // Explicit error checking because the TUF conformance spec is a nightmare
            var metadataDir = parseResult.GetValue(MetadataDirOption);
            if (metadataDir == null)
            {
                Console.Error.WriteLine("Error: --metadata-dir is required for init command");
                return 1;
            }

            var trustedRoot = parseResult.GetValue(TrustedRootArg);
            if (trustedRoot == null)
            {
                Console.Error.WriteLine("Error: trusted-root argument is required for init command");
                return 1;
            }

            var exitCode = HandleInitCommand(metadataDir.FullName, trustedRoot.FullName);
            return exitCode;
        });

        return command;
    }

    private static Command CreateRefreshCommand()
    {
        var command = new Command("refresh", "Update local metadata from repository");

        command.SetAction(async (parseResult, token) =>
        {
            // More explicit error checking because whoever designed this CLI spec hates developers
            var metadataDir = parseResult.GetValue(MetadataDirOption);
            if (metadataDir == null)
            {
                Console.Error.WriteLine("Error: --metadata-dir is required for refresh command");
                return 1;
            }

            var metadataUrl = parseResult.GetValue(MetadataUrlOption);
            if (string.IsNullOrEmpty(metadataUrl))
            {
                Console.Error.WriteLine("Error: --metadata-url is required for refresh command");
                return 1;
            }

            var exitCode = await HandleRefreshCommand(metadataDir.FullName, metadataUrl);
            return exitCode;
        });

        return command;
    }

    private static Command CreateDownloadCommand()
    {
        var command = new Command("download", "Download and verify an artifact from repository");

        command.SetAction(async (parseResult, token) =>
        {
            // Even more explicit error checking because the TUF conformance devs apparently love making life difficult
            var metadataDir = parseResult.GetValue(MetadataDirOption);
            if (metadataDir == null)
            {
                Console.Error.WriteLine("Error: --metadata-dir is required for download command");
                return 1;
            }

            var metadataUrl = parseResult.GetValue(MetadataUrlOption);
            if (string.IsNullOrEmpty(metadataUrl))
            {
                Console.Error.WriteLine("Error: --metadata-url is required for download command");
                return 1;
            }

            var targetName = parseResult.GetValue(TargetNameOption);
            if (string.IsNullOrEmpty(targetName))
            {
                Console.Error.WriteLine("Error: --target-name is required for download command");
                return 1;
            }

            var targetBaseUrl = parseResult.GetValue(TargetBaseUrlOption);
            if (string.IsNullOrEmpty(targetBaseUrl))
            {
                Console.Error.WriteLine("Error: --target-base-url is required for download command");
                return 1;
            }

            var targetDir = parseResult.GetValue(TargetDirOption);
            if (targetDir == null)
            {
                Console.Error.WriteLine("Error: --target-dir is required for download command");
                return 1;
            }

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