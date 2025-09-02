using TUF;

namespace BasicClient;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("TUF .NET Basic Client Example");
        Console.WriteLine("=============================");

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: BasicClient <metadata-url> <target-file-name>");
            Console.WriteLine("Example: BasicClient https://example.com/metadata file.txt");
            return;
        }

        string metadataUrl = args[0];
        string targetFileName = args[1];

        try
        {
            // Create temporary directories for metadata and targets
            string tempDir = Path.Combine(Path.GetTempPath(), "tuf-example", Guid.NewGuid().ToString());
            string metadataDir = Path.Combine(tempDir, "metadata");
            string targetsDir = Path.Combine(tempDir, "targets");

            Directory.CreateDirectory(metadataDir);
            Directory.CreateDirectory(targetsDir);

            Console.WriteLine($"Using temporary directory: {tempDir}");
            Console.WriteLine($"Metadata URL: {metadataUrl}");
            Console.WriteLine($"Target file: {targetFileName}");

            // For this example, we'll use a placeholder trusted root.
            // In a real scenario, this would be distributed with your application
            // or obtained through a secure out-of-band channel.
            byte[] trustedRoot = GetPlaceholderTrustedRoot();

            var config = new UpdaterConfig(trustedRoot, new Uri(metadataUrl))
            {
                LocalMetadataDir = metadataDir,
                LocalTargetsDir = targetsDir,
                Client = new HttpClient()
            };

            var updater = new Updater(config);

            Console.WriteLine("Refreshing TUF metadata...");
            await updater.RefreshAsync();

            Console.WriteLine("Getting target information...");
            var (path, targetInfo) = await updater.GetTargetInfo(targetFileName);

            Console.WriteLine($"Target found:");
            Console.WriteLine($"  Path: {path}");
            Console.WriteLine($"  Length: {targetInfo.Length} bytes");
            Console.WriteLine($"  Hashes: {string.Join(", ", targetInfo.Hashes.Select(h => $"{h.Key}:{h.Value[..8]}..."))}");

            Console.WriteLine("Checking for cached version...");
            var cached = await updater.FindCachedTarget(targetInfo, path, null);
            if (cached.HasValue)
            {
                Console.WriteLine($"Found cached version at: {cached.Value.FilePath}");
                Console.WriteLine($"Cached file size: {cached.Value.Data.Length} bytes");
            }
            else
            {
                Console.WriteLine("Downloading target file...");
                var result = await updater.DownloadTarget(targetInfo, path, null, null);
                Console.WriteLine($"Downloaded to: {result.FilePath}");
                Console.WriteLine($"File size: {result.Data.Length} bytes");
            }

            Console.WriteLine("TUF client example completed successfully!");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static byte[] GetPlaceholderTrustedRoot()
    {
        // This is a placeholder implementation.
        // In a real application, you would:
        // 1. Embed a trusted root with your application
        // 2. Or load it from a secure configuration
        // 3. Or obtain it through a secure out-of-band channel

        Console.WriteLine("WARNING: Using placeholder trusted root for demonstration purposes only!");
        Console.WriteLine("In production, use a real trusted root obtained through secure means.");

        return System.Text.Encoding.UTF8.GetBytes(@"{
            ""signatures"": [],
            ""signed"": {
                ""_type"": ""root"",
                ""spec_version"": ""1.0.0"",
                ""version"": 1,
                ""expires"": """ + DateTimeOffset.UtcNow.AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ") + @""",
                ""keys"": {},
                ""roles"": {
                    ""root"": {""keyids"": [], ""threshold"": 1},
                    ""targets"": {""keyids"": [], ""threshold"": 1},
                    ""snapshot"": {""keyids"": [], ""threshold"": 1},
                    ""timestamp"": {""keyids"": [], ""threshold"": 1}
                }
            }
        }");
    }
}