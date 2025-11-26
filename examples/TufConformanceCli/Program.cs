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

    private static readonly Option<bool> VerboseOption = new("--verbose")
    {
        Description = "Enable verbose logging for conformance test debugging",
        Recursive = true
    };

    private static bool _verbose = false;

    private static void LogVerbose(string message)
    {
        if (_verbose)
        {
            Console.WriteLine($"[VERBOSE] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }
    }

    private static void LogInfo(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    private static void LogError(string message)
    {
        Console.Error.WriteLine($"[ERROR] {message}");
    }

    private static void LogError(string message, Exception ex)
    {
        Console.Error.WriteLine($"[ERROR] {message}");
        if (_verbose && ex != null)
        {
            Console.Error.WriteLine($"[DEBUG] Exception details: {ex}");
        }
        else if (ex != null)
        {
            Console.Error.WriteLine($"[DEBUG] {ex.Message}");
        }
    }

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
            VerboseOption,
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static Command CreateInitCommand()
    {
        var command = new Command("init", "Initialize client's local trusted metadata");
        command.Add(TrustedRootArg);
        command.SetAction((parseResult) =>
        {
            // Set verbose flag from parse result
            _verbose = parseResult.GetValue(VerboseOption);
            
            LogVerbose("Starting init command");
            
            // Explicit error checking because the TUF conformance spec is a nightmare
            var metadataDir = parseResult.GetValue(MetadataDirOption);
            if (metadataDir == null)
            {
                LogError("--metadata-dir is required for init command");
                return 1;
            }

            var trustedRoot = parseResult.GetValue(TrustedRootArg);
            if (trustedRoot == null)
            {
                LogError("trusted-root argument is required for init command");
                return 1;
            }

            LogVerbose($"Metadata directory: {metadataDir.FullName}");
            LogVerbose($"Trusted root: {trustedRoot.FullName}");

            var exitCode = HandleInitCommand(metadataDir.FullName, trustedRoot.FullName);
            LogVerbose($"Init command completed with exit code: {exitCode}");
            return exitCode;
        });

        return command;
    }

    private static Command CreateRefreshCommand()
    {
        var command = new Command("refresh", "Update local metadata from repository");

        command.SetAction(async (parseResult, token) =>
        {
            // Set verbose flag from parse result
            _verbose = parseResult.GetValue(VerboseOption);
            
            LogVerbose("Starting refresh command");
            
            // More explicit error checking because whoever designed this CLI spec hates developers
            var metadataDir = parseResult.GetValue(MetadataDirOption);
            if (metadataDir == null)
            {
                LogError("--metadata-dir is required for refresh command");
                return 1;
            }

            var metadataUrl = parseResult.GetValue(MetadataUrlOption);
            if (string.IsNullOrEmpty(metadataUrl))
            {
                LogError("--metadata-url is required for refresh command");
                return 1;
            }

            LogVerbose($"Metadata directory: {metadataDir.FullName}");
            LogVerbose($"Metadata URL: {metadataUrl}");

            var exitCode = await HandleRefreshCommand(metadataDir.FullName, metadataUrl);
            LogVerbose($"Refresh command completed with exit code: {exitCode}");
            return exitCode;
        });

        return command;
    }

    private static Command CreateDownloadCommand()
    {
        var command = new Command("download", "Download and verify an artifact from repository");

        command.SetAction(async (parseResult, token) =>
        {
            // Set verbose flag from parse result
            _verbose = parseResult.GetValue(VerboseOption);
            
            LogVerbose("Starting download command");
            
            // Even more explicit error checking because the TUF conformance devs apparently love making life difficult
            var metadataDir = parseResult.GetValue(MetadataDirOption);
            if (metadataDir == null)
            {
                LogError("--metadata-dir is required for download command");
                return 1;
            }

            var metadataUrl = parseResult.GetValue(MetadataUrlOption);
            if (string.IsNullOrEmpty(metadataUrl))
            {
                LogError("--metadata-url is required for download command");
                return 1;
            }

            var targetName = parseResult.GetValue(TargetNameOption);
            if (string.IsNullOrEmpty(targetName))
            {
                LogError("--target-name is required for download command");
                return 1;
            }

            var targetBaseUrl = parseResult.GetValue(TargetBaseUrlOption);
            if (string.IsNullOrEmpty(targetBaseUrl))
            {
                LogError("--target-base-url is required for download command");
                return 1;
            }

            var targetDir = parseResult.GetValue(TargetDirOption);
            if (targetDir == null)
            {
                LogError("--target-dir is required for download command");
                return 1;
            }

            LogVerbose($"Metadata directory: {metadataDir.FullName}");
            LogVerbose($"Metadata URL: {metadataUrl}");
            LogVerbose($"Target name: {targetName}");
            LogVerbose($"Target base URL: {targetBaseUrl}");
            LogVerbose($"Target directory: {targetDir.FullName}");

            var exitCode = await HandleDownloadCommand(metadataDir.FullName, metadataUrl, targetName, targetBaseUrl, targetDir.FullName);
            LogVerbose($"Download command completed with exit code: {exitCode}");
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
            LogInfo($"Initializing TUF client with metadata dir: {metadataDir}");
            LogInfo($"Using trusted root: {trustedRootPath}");

            // Ensure metadata directory exists
            LogVerbose("Creating metadata directory if it doesn't exist");
            Directory.CreateDirectory(metadataDir);

            // Validate trusted root file exists
            if (!File.Exists(trustedRootPath))
            {
                LogError($"Trusted root file not found: {trustedRootPath}");
                return 1;
            }

            // Copy trusted root to metadata directory as root.json (non-versioned filename)
            var rootPath = Path.Combine(metadataDir, "root.json");
            LogVerbose($"Copying trusted root to: {rootPath}");
            File.Copy(trustedRootPath, rootPath, overwrite: true);

            LogInfo("TUF client initialized successfully");
            return 0;
        }
        catch (Exception ex)
        {
            LogError("Error during init", ex);
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
            LogInfo($"Refreshing metadata from: {metadataUrl}");
            LogInfo($"Metadata directory: {metadataDir}");

            // Validate metadata directory exists and has root.json
            var rootPath = Path.Combine(metadataDir, "root.json");
            if (!File.Exists(rootPath))
            {
                LogError("No root.json found. Run 'init' command first.");
                return 1;
            }

            // Read the trusted root
            LogVerbose($"Reading trusted root from: {rootPath}");
            byte[] rootBytes = await File.ReadAllBytesAsync(rootPath);
            LogVerbose($"Root metadata size: {rootBytes.Length} bytes");

            // Configure the updater with the metadata directory and repository URL
            var targetsDir = Path.Combine(metadataDir, "targets");
            LogVerbose($"Targets directory: {targetsDir}");
            
            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir,
                LocalTargetsDir = targetsDir,
                Client = new HttpClient()
            };

            // Create and initialize the updater
            LogVerbose("Creating TUF updater and starting refresh");
            var updater = new Updater(config);
            await updater.RefreshAsync();

            LogInfo("Metadata refresh completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            LogError("Error during refresh", ex);
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
            LogInfo($"Downloading target: {targetName}");
            LogInfo($"From: {targetBaseUrl}");
            LogInfo($"To: {targetDir}");

            // Validate metadata directory exists and has root.json
            var rootPath = Path.Combine(metadataDir, "root.json");
            if (!File.Exists(rootPath))
            {
                LogError("No root.json found. Run 'init' command first.");
                return 1;
            }

            // Ensure target directory exists
            LogVerbose($"Creating target directory: {targetDir}");
            Directory.CreateDirectory(targetDir);

            // Read the trusted root
            LogVerbose($"Reading trusted root from: {rootPath}");
            byte[] rootBytes = await File.ReadAllBytesAsync(rootPath);
            LogVerbose($"Root metadata size: {rootBytes.Length} bytes");

            // Configure the updater - use targetBaseUrl as the metadata URL base for now
            // In a full implementation this might be more sophisticated
            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir,
                LocalTargetsDir = targetDir,
                Client = new HttpClient()
            };

            // Create and initialize the updater
            LogVerbose("Creating TUF updater");
            var updater = new Updater(config);

            // First ensure metadata is up-to-date
            LogVerbose("Refreshing metadata before download");
            await updater.RefreshAsync();

            // Get target information and download
            LogVerbose($"Looking up target info for: {targetName}");
            if (await updater.GetTargetInfo(targetName) is not (var targetPath, var targetInfo))
            {
                throw new Exception($"Target '{targetName}' not found in repository.");
            }
            LogInfo($"Target found: {targetPath}");
            LogVerbose($"Target info - Length: {targetInfo.Length}, Hashes: {string.Join(", ", targetInfo.Hashes.Keys)}");

            // Check for cached version first
            LogVerbose("Checking for cached version of target");
            var cached = await updater.FindCachedTarget(targetInfo, targetPath, null);
            if (cached.HasValue)
            {
                LogInfo($"Target already cached at: {cached.Value.FilePath}");
                return 0;
            }
            else
            {
                // Download the target file
                LogVerbose($"Downloading target from: {targetBaseUrl}");
                var result = await updater.DownloadTarget(targetInfo, targetPath, null, new Uri(targetBaseUrl));
                LogInfo($"Target downloaded and verified successfully: {result.FilePath}");
                LogVerbose($"Downloaded file size: {new FileInfo(result.FilePath).Length} bytes");
                return 0;
            }
        }
        catch (Exception ex)
        {
            LogError("Error during download", ex);
            return 1;
        }
    }
}