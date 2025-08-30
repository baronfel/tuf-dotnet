using System.CommandLine;

using TUF;

namespace TufConformanceCli;

/// <summary>
/// TUF Conformance CLI implementation following the client-under-test protocol
/// from https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var metadataDirOption = new Option<DirectoryInfo>(
            "--metadata-dir",
            description: "Directory to store TUF metadata files")
        { IsRequired = true };

        var metadataUrlOption = new Option<string>(
            "--metadata-url",
            description: "Base URL for TUF metadata repository")
        { IsRequired = false };

        var targetNameOption = new Option<string>(
            "--target-name",
            description: "Name/path of target file to download")
        { IsRequired = false };

        var targetBaseUrlOption = new Option<string>(
            "--target-base-url",
            description: "Base URL for target files")
        { IsRequired = false };

        var targetDirOption = new Option<DirectoryInfo>(
            "--target-dir",
            description: "Directory to store downloaded target files")
        { IsRequired = false };

        var rootCommand = new RootCommand("TUF Conformance CLI - Client under test for TUF conformance suite")
        {
            metadataDirOption,
            metadataUrlOption,
            targetNameOption,
            targetBaseUrlOption,
            targetDirOption
        };

        // Add the three required commands
        rootCommand.AddCommand(CreateInitCommand(metadataDirOption));
        rootCommand.AddCommand(CreateRefreshCommand(metadataDirOption, metadataUrlOption));
        rootCommand.AddCommand(CreateDownloadCommand(metadataDirOption, metadataUrlOption,
            targetNameOption, targetBaseUrlOption, targetDirOption));

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// init command: Initialize the clients local trusted metadata
    /// Format: client --metadata-dir METADATA_DIR init TRUSTED_ROOT
    /// </summary>
    private static Command CreateInitCommand(Option<DirectoryInfo> metadataDirOption)
    {
        var trustedRootArgument = new Argument<FileInfo>(
            "trusted-root",
            description: "Path to trusted root.json file");

        var command = new Command("init", "Initialize the clients local trusted metadata")
        {
            trustedRootArgument
        };

        command.SetHandler(async (metadataDir, trustedRoot) =>
        {
            var exitCode = await ExecuteInit(metadataDir, trustedRoot);
            Environment.Exit(exitCode);
        }, metadataDirOption, trustedRootArgument);

        return command;
    }

    /// <summary>
    /// refresh command: Update local metadata from the repository
    /// Format: client --metadata-dir METADATA_DIR --metadata-url METADATA_URL refresh
    /// </summary>
    private static Command CreateRefreshCommand(Option<DirectoryInfo> metadataDirOption,
        Option<string> metadataUrlOption)
    {
        var command = new Command("refresh", "Update local metadata from the repository");

        command.SetHandler(async (metadataDir, metadataUrl) =>
        {
            var exitCode = await ExecuteRefresh(metadataDir, metadataUrl);
            Environment.Exit(exitCode);
        }, metadataDirOption, metadataUrlOption);

        return command;
    }

    /// <summary>
    /// download command: Download an artifact from repository, store it in local disk
    /// Format: client --metadata-dir METADATA_DIR --metadata-url METADATA_URL 
    ///         --target-name TARGET_PATH --target-base-url TARGET_URL --target-dir TARGET_DIR download
    /// </summary>
    private static Command CreateDownloadCommand(Option<DirectoryInfo> metadataDirOption,
        Option<string> metadataUrlOption, Option<string> targetNameOption,
        Option<string> targetBaseUrlOption, Option<DirectoryInfo> targetDirOption)
    {
        var command = new Command("download", "Download an artifact from repository, store it in local disk");

        command.SetHandler(async (metadataDir, metadataUrl, targetName, targetBaseUrl, targetDir) =>
        {
            var exitCode = await ExecuteDownload(metadataDir, metadataUrl, targetName, targetBaseUrl, targetDir);
            Environment.Exit(exitCode);
        }, metadataDirOption, metadataUrlOption, targetNameOption, targetBaseUrlOption, targetDirOption);

        return command;
    }

    private static async Task<int> ExecuteInit(DirectoryInfo metadataDir, FileInfo trustedRoot)
    {
        try
        {
            // Ensure metadata directory exists
            if (!metadataDir.Exists)
            {
                metadataDir.Create();
            }

            // Copy initial root.json into METADATA_DIR
            var rootDestPath = Path.Combine(metadataDir.FullName, "root.json");
            await File.WriteAllBytesAsync(rootDestPath, await File.ReadAllBytesAsync(trustedRoot.FullName));

            return 0; // Success
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Init failed: {ex.Message}");
            return 1; // Failure
        }
    }

    private static async Task<int> ExecuteRefresh(DirectoryInfo metadataDir, string? metadataUrl)
    {
        try
        {
            if (metadataUrl == null)
            {
                Console.Error.WriteLine("--metadata-url is required for refresh command");
                return 1;
            }

            // Load existing root.json from metadata directory
            var rootPath = Path.Combine(metadataDir.FullName, "root.json");
            if (!File.Exists(rootPath))
            {
                Console.Error.WriteLine("No root.json found in metadata directory. Run init first.");
                return 1;
            }

            byte[] rootBytes = await File.ReadAllBytesAsync(rootPath);

            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir.FullName,
                LocalTargetsDir = Path.Combine(metadataDir.FullName, "targets_temp"), // temp dir
                Client = new HttpClient()
            };

            var updater = new Updater(config);

            // Update top-level metadata per TUF workflow
            await updater.Refresh();

            return 0; // Success
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Refresh failed: {ex.Message}");
            return 1; // Failure
        }
    }

    private static async Task<int> ExecuteDownload(DirectoryInfo metadataDir, string? metadataUrl,
        string? targetName, string? targetBaseUrl, DirectoryInfo? targetDir)
    {
        try
        {
            if (metadataUrl == null)
            {
                Console.Error.WriteLine("--metadata-url is required for download command");
                return 1;
            }

            if (targetName == null)
            {
                Console.Error.WriteLine("--target-name is required for download command");
                return 1;
            }

            if (targetBaseUrl == null)
            {
                Console.Error.WriteLine("--target-base-url is required for download command");
                return 1;
            }

            if (targetDir == null)
            {
                Console.Error.WriteLine("--target-dir is required for download command");
                return 1;
            }

            // Ensure target directory exists
            if (!targetDir.Exists)
            {
                targetDir.Create();
            }

            // Load existing root.json from metadata directory
            var rootPath = Path.Combine(metadataDir.FullName, "root.json");
            if (!File.Exists(rootPath))
            {
                Console.Error.WriteLine("No root.json found in metadata directory. Run init first.");
                return 1;
            }

            byte[] rootBytes = await File.ReadAllBytesAsync(rootPath);

            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir.FullName,
                LocalTargetsDir = targetDir.FullName,
                RemoteTargetsUrl = new Uri(targetBaseUrl),
                Client = new HttpClient()
            };

            var updater = new Updater(config);

            // Ensure metadata is up-to-date before downloading
            await updater.Refresh();

            // Get target information
            var targetInfo = await updater.GetTargetInfo(targetName);

            // Check for cached version first
            var cached = await updater.FindCachedTarget(targetInfo, null);
            if (!cached.HasValue)
            {
                // Download and verify artifact
                await updater.DownloadTarget(targetInfo, null, new Uri(targetBaseUrl));
            }

            return 0; // Success
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Download failed: {ex.Message}");
            return 1; // Failure
        }
    }
}