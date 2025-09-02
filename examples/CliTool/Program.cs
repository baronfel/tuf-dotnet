using System.CommandLine;

using TUF;

namespace CliTool;

public class Program
{
    // Shared options to reduce duplication
    private static readonly Option<string> MetadataUrlOption = new("--metadata-url")
    {
        Description = "URL to the TUF metadata repository",
        Required = true
    };

    private static readonly Option<DirectoryInfo> MetadataDirOption = new("--metadata-dir")
    {
        Description = "Local directory to store metadata",
        Required = true
    };

    private static readonly Option<DirectoryInfo> TargetsDirOption = new("--targets-dir")
    {
        Description = "Local directory to store targets",
        Required = true
    };

    private static readonly Option<FileInfo> TrustedRootOption = new("--trusted-root")
    {
        Description = "Path to trusted root metadata file",
        Required = true
    };

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("TUF .NET CLI Tool - Demonstrates TUF client operations")
        {
            CreateRefreshCommand(),
            CreateDownloadCommand(),
            CreateInfoCommand()
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static Command CreateRefreshCommand()
    {
        var command = new Command("refresh", "Refresh TUF metadata from repository")
        {
            MetadataUrlOption,
            MetadataDirOption,
            TargetsDirOption,
            TrustedRootOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataUrl = parseResult.GetValue(MetadataUrlOption)!;
            var metadataDir = parseResult.GetValue(MetadataDirOption)!;
            var targetsDir = parseResult.GetValue(TargetsDirOption)!;
            var trustedRoot = parseResult.GetValue(TrustedRootOption)!;

            await RefreshMetadata(metadataUrl, metadataDir, targetsDir, trustedRoot);
            return 0;
        });

        return command;
    }

    private static Command CreateDownloadCommand()
    {
        var targetFileOption = new Option<string>("--target-file")
        {
            Description = "Name of the target file to download",
            Required = true
        };

        var command = new Command("download", "Download a target file from TUF repository")
        {
            MetadataUrlOption,
            MetadataDirOption,
            TargetsDirOption,
            TrustedRootOption,
            targetFileOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataUrl = parseResult.GetValue(MetadataUrlOption)!;
            var metadataDir = parseResult.GetValue(MetadataDirOption)!;
            var targetsDir = parseResult.GetValue(TargetsDirOption)!;
            var trustedRoot = parseResult.GetValue(TrustedRootOption)!;
            var targetFile = parseResult.GetValue(targetFileOption)!;

            await DownloadTarget(metadataUrl, metadataDir, targetsDir, trustedRoot, targetFile);
            return 0;
        });

        return command;
    }

    private static Command CreateInfoCommand()
    {
        var targetFileOption = new Option<string?>("--target-file")
        {
            Description = "Name of the target file to get info about (optional)",
            Required = false
        };

        var command = new Command("info", "Get information about TUF repository or target file")
        {
            MetadataUrlOption,
            MetadataDirOption,
            TargetsDirOption,
            TrustedRootOption,
            targetFileOption
        };

        command.SetAction(async (parseResult, token) =>
        {
            var metadataUrl = parseResult.GetValue(MetadataUrlOption)!;
            var metadataDir = parseResult.GetValue(MetadataDirOption)!;
            var targetsDir = parseResult.GetValue(TargetsDirOption)!;
            var trustedRoot = parseResult.GetValue(TrustedRootOption)!;
            var targetFile = parseResult.GetValue(targetFileOption);

            await ShowInfo(metadataUrl, metadataDir, targetsDir, trustedRoot, targetFile);
            return 0;
        });

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
            await updater.RefreshAsync();

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
            await updater.RefreshAsync();

            Console.WriteLine("Getting target information...");
            var (targetPath, targetInfo) = await updater.GetTargetInfo(targetFile);

            Console.WriteLine($"Target found: {targetPath}");
            Console.WriteLine($"Length: {targetInfo.Length} bytes");

            // Check for cached version first
            var cached = await updater.FindCachedTarget(targetInfo, targetPath, null);
            if (cached.HasValue)
            {
                Console.WriteLine($"✅ Found cached version at: {cached.Value.FilePath}");
            }
            else
            {
                Console.WriteLine("Downloading...");
                var result = await updater.DownloadTarget(targetInfo, targetPath, null, null);
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
            await updater.RefreshAsync();

            var trustedMetadata = updater.GetTrustedMetadataSet();

            Console.WriteLine("=== TUF Repository Information ===");
            Console.WriteLine($"Root version: {trustedMetadata.Root.Signed.Version}");
            Console.WriteLine($"Root expires: {trustedMetadata.Root.Signed.Expires}");
            Console.WriteLine($"Spec version: {trustedMetadata.Root.Signed.SpecVersion}");

            if (targetFile != null)
            {
                Console.WriteLine($"\n=== Target File Information: {targetFile} ===");
                var (targetPath, targetInfo) = await updater.GetTargetInfo(targetFile);
                Console.WriteLine($"Path: {targetPath}");
                Console.WriteLine($"Length: {targetInfo.Length} bytes");
                Console.WriteLine("Hashes:");
                foreach (var hash in targetInfo.Hashes)
                {
                    Console.WriteLine($"  {hash.Key}: {hash.Value}");
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