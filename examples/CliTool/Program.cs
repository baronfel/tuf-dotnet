using System.CommandLine;

using TUF;

namespace CliTool;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("TUF .NET CLI Tool - Demonstrates TUF client operations")
        {
            CreateRefreshCommand(),
            CreateDownloadCommand(),
            CreateInfoCommand()
        };

        return await rootCommand.InvokeAsync(args);
    }

    private static Command CreateRefreshCommand()
    {
        var metadataUrlOption = new Option<string>(
            "--metadata-url",
            description: "URL to the TUF metadata repository")
        { IsRequired = true };

        var metadataDirOption = new Option<DirectoryInfo>(
            "--metadata-dir",
            description: "Local directory to store metadata")
        { IsRequired = true };

        var targetsDirOption = new Option<DirectoryInfo>(
            "--targets-dir",
            description: "Local directory to store targets")
        { IsRequired = true };

        var trustedRootOption = new Option<FileInfo>(
            "--trusted-root",
            description: "Path to trusted root metadata file")
        { IsRequired = true };

        var command = new Command("refresh", "Refresh TUF metadata from repository")
        {
            metadataUrlOption,
            metadataDirOption,
            targetsDirOption,
            trustedRootOption
        };

        command.SetHandler(async (metadataUrl, metadataDir, targetsDir, trustedRoot) =>
        {
            await RefreshMetadata(metadataUrl, metadataDir, targetsDir, trustedRoot);
        }, metadataUrlOption, metadataDirOption, targetsDirOption, trustedRootOption);

        return command;
    }

    private static Command CreateDownloadCommand()
    {
        var metadataUrlOption = new Option<string>(
            "--metadata-url",
            description: "URL to the TUF metadata repository")
        { IsRequired = true };

        var metadataDirOption = new Option<DirectoryInfo>(
            "--metadata-dir",
            description: "Local directory with metadata")
        { IsRequired = true };

        var targetsDirOption = new Option<DirectoryInfo>(
            "--targets-dir",
            description: "Local directory to store targets")
        { IsRequired = true };

        var trustedRootOption = new Option<FileInfo>(
            "--trusted-root",
            description: "Path to trusted root metadata file")
        { IsRequired = true };

        var targetFileOption = new Option<string>(
            "--target-file",
            description: "Name of the target file to download")
        { IsRequired = true };

        var command = new Command("download", "Download a target file from TUF repository")
        {
            metadataUrlOption,
            metadataDirOption,
            targetsDirOption,
            trustedRootOption,
            targetFileOption
        };

        command.SetHandler(async (metadataUrl, metadataDir, targetsDir, trustedRoot, targetFile) =>
        {
            await DownloadTarget(metadataUrl, metadataDir, targetsDir, trustedRoot, targetFile);
        }, metadataUrlOption, metadataDirOption, targetsDirOption, trustedRootOption, targetFileOption);

        return command;
    }

    private static Command CreateInfoCommand()
    {
        var metadataUrlOption = new Option<string>(
            "--metadata-url",
            description: "URL to the TUF metadata repository")
        { IsRequired = true };

        var metadataDirOption = new Option<DirectoryInfo>(
            "--metadata-dir",
            description: "Local directory with metadata")
        { IsRequired = true };

        var targetsDirOption = new Option<DirectoryInfo>(
            "--targets-dir",
            description: "Local directory for targets")
        { IsRequired = true };

        var trustedRootOption = new Option<FileInfo>(
            "--trusted-root",
            description: "Path to trusted root metadata file")
        { IsRequired = true };

        var targetFileOption = new Option<string?>(
            "--target-file",
            description: "Name of the target file to get info about (optional)")
        { IsRequired = false };

        var command = new Command("info", "Get information about TUF repository or target file")
        {
            metadataUrlOption,
            metadataDirOption,
            targetsDirOption,
            trustedRootOption,
            targetFileOption
        };

        command.SetHandler(async (metadataUrl, metadataDir, targetsDir, trustedRoot, targetFile) =>
        {
            await ShowInfo(metadataUrl, metadataDir, targetsDir, trustedRoot, targetFile);
        }, metadataUrlOption, metadataDirOption, targetsDirOption, trustedRootOption, targetFileOption);

        return command;
    }

    private static async Task RefreshMetadata(string metadataUrl, DirectoryInfo metadataDir, DirectoryInfo targetsDir, FileInfo trustedRoot)
    {
        try
        {
            Console.WriteLine("Refreshing TUF metadata...");
            Console.WriteLine($"Metadata URL: {metadataUrl}");
            Console.WriteLine($"Metadata directory: {metadataDir.FullName}");
            Console.WriteLine($"Targets directory: {targetsDir.FullName}");

            metadataDir.Create();
            targetsDir.Create();

            byte[] rootBytes = await File.ReadAllBytesAsync(trustedRoot.FullName);

            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir.FullName,
                LocalTargetsDir = targetsDir.FullName,
                Client = new HttpClient()
            };

            var updater = new Updater(config);
            await updater.Refresh();

            Console.WriteLine("✅ Metadata refresh completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error refreshing metadata: {ex.Message}");
        }
    }

    private static async Task DownloadTarget(string metadataUrl, DirectoryInfo metadataDir, DirectoryInfo targetsDir, FileInfo trustedRoot, string targetFile)
    {
        try
        {
            Console.WriteLine($"Downloading target file: {targetFile}");

            metadataDir.Create();
            targetsDir.Create();

            byte[] rootBytes = await File.ReadAllBytesAsync(trustedRoot.FullName);

            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir.FullName,
                LocalTargetsDir = targetsDir.FullName,
                Client = new HttpClient()
            };

            var updater = new Updater(config);

            Console.WriteLine("Refreshing metadata...");
            await updater.Refresh();

            Console.WriteLine("Getting target information...");
            var targetInfo = await updater.GetTargetInfo(targetFile);

            Console.WriteLine($"Target found: {targetInfo.Path}");
            Console.WriteLine($"Length: {targetInfo.Length} bytes");

            // Check for cached version first
            var cached = await updater.FindCachedTarget(targetInfo, null);
            if (cached.HasValue)
            {
                Console.WriteLine($"✅ Found cached version at: {cached.Value.FilePath}");
            }
            else
            {
                Console.WriteLine("Downloading...");
                var result = await updater.DownloadTarget(targetInfo, null, null);
                Console.WriteLine($"✅ Downloaded to: {result.FilePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error downloading target: {ex.Message}");
        }
    }

    private static async Task ShowInfo(string metadataUrl, DirectoryInfo metadataDir, DirectoryInfo targetsDir, FileInfo trustedRoot, string? targetFile)
    {
        try
        {
            metadataDir.Create();
            targetsDir.Create();

            byte[] rootBytes = await File.ReadAllBytesAsync(trustedRoot.FullName);

            var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir.FullName,
                LocalTargetsDir = targetsDir.FullName,
                Client = new HttpClient()
            };

            var updater = new Updater(config);

            Console.WriteLine("Refreshing metadata...");
            await updater.Refresh();

            var trustedMetadata = updater.GetTrustedMetadataSet();

            Console.WriteLine("=== TUF Repository Information ===");
            Console.WriteLine($"Root version: {trustedMetadata.Root.Signed.Version}");
            Console.WriteLine($"Root expires: {trustedMetadata.Root.Signed.Expires}");
            Console.WriteLine($"Spec version: {trustedMetadata.Root.Signed.SpecVersion}");

            if (targetFile != null)
            {
                Console.WriteLine($"\n=== Target File Information: {targetFile} ===");
                var targetInfo = await updater.GetTargetInfo(targetFile);
                Console.WriteLine($"Path: {targetInfo.Path}");
                Console.WriteLine($"Length: {targetInfo.Length} bytes");
                Console.WriteLine("Hashes:");
                foreach (var hash in targetInfo.Hashes)
                {
                    Console.WriteLine($"  {hash.Algorithm}: {hash.HexEncodedValue}");
                }

                if (targetInfo.Custom != null && targetInfo.Custom.Count > 0)
                {
                    Console.WriteLine("Custom metadata:");
                    foreach (var kvp in targetInfo.Custom)
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
            }
            else
            {
                Console.WriteLine("\n=== Available Targets ===");
                var targets = updater.GetTopLevelTargets();
                if (targets.Count > 0)
                {
                    foreach (var target in targets)
                    {
                        Console.WriteLine($"  {target.Key} ({target.Value.Length} bytes)");
                    }
                }
                else
                {
                    Console.WriteLine("  No targets found");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error getting repository info: {ex.Message}");
        }
    }
}