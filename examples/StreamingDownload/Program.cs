using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using TUF;
using TUF.Models;

// Simple streaming download demo
if (args.Length < 3)
{
    Console.WriteLine("🚀 TUF Streaming Download Example");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run <metadata-url> <trusted-root-path> <target-path> [output-path] [--progress]");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  dotnet run https://example.com/metadata/ ./root.json large-file.bin ./downloads/large-file.bin --progress");
    Console.WriteLine();
    Console.WriteLine("This example demonstrates memory-efficient streaming downloads of large files,");
    Console.WriteLine("addressing a key parity gap with other mature TUF implementations.");
    return 1;
}

var metadataUrl = args[0];
var trustedRootPath = args[1];
var targetPath = args[2];
var outputPath = args.Length > 3 && !args[3].StartsWith("--") ? args[3] : null;
var showProgress = args.Contains("--progress");

try
{
    Console.WriteLine("🚀 TUF Streaming Download Example");
    Console.WriteLine($"📍 Metadata URL: {metadataUrl}");
    Console.WriteLine($"🔑 Trusted Root: {trustedRootPath}");
    Console.WriteLine($"📂 Target: {targetPath}");
    Console.WriteLine();

    // Read trusted root
    var trustedRootBytes = await File.ReadAllBytesAsync(trustedRootPath);
    
    // Configure updater
    var config = new UpdaterConfig(trustedRootBytes, new Uri(metadataUrl))
    {
        LocalMetadataDir = Path.GetTempPath(),
        LocalTargetsDir = Path.GetTempPath(),
        Client = new HttpClient()
    };

    var updater = new Updater(config);

    Console.WriteLine("🔄 Refreshing TUF metadata...");
    await updater.RefreshAsync();

    Console.WriteLine("🔍 Finding target file...");
    var targetInfo = await updater.GetTargetInfo(targetPath);
    if (targetInfo == null)
    {
        Console.WriteLine($"❌ Target file '{targetPath}' not found in repository");
        return 1;
    }

    var (remotePath, targetFile) = targetInfo.Value;
    Console.WriteLine($"✅ Found target: {remotePath}");
    Console.WriteLine($"📏 Size: {targetFile.Length:N0} bytes ({targetFile.Length / 1024.0 / 1024.0:F2} MB)");
    Console.WriteLine($"🔐 Hashes: {string.Join(", ", targetFile.Hashes.Keys)}");

    // Determine output path
    var finalOutputPath = outputPath ?? Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileName(targetPath));
    Console.WriteLine($"💾 Output: {finalOutputPath}");
    Console.WriteLine();

    // Set up progress reporting if requested
    IProgress<(long bytesDownloaded, long totalBytes)>? progress = null;
    if (showProgress)
    {
        progress = new Progress<(long downloaded, long total)>(report =>
        {
            var percentage = (double)report.downloaded / report.total * 100;
            var downloadedMB = report.downloaded / 1024.0 / 1024.0;
            var totalMB = report.total / 1024.0 / 1024.0;
            Console.Write($"\r📥 Progress: {percentage:F1}% ({downloadedMB:F2}/{totalMB:F2} MB)");
        });
    }

    // Perform streaming download
    Console.WriteLine("📥 Starting streaming download...");
    var startTime = DateTime.UtcNow;

    using var outputStream = File.Create(finalOutputPath);
    await updater.DownloadTargetToStream(targetFile, targetPath, outputStream, finalOutputPath, progress: progress);

    var duration = DateTime.UtcNow - startTime;
    var throughputMbps = (targetFile.Length / 1024.0 / 1024.0) / duration.TotalSeconds;

    if (showProgress)
    {
        Console.WriteLine(); // New line after progress
    }

    Console.WriteLine("✅ Download completed successfully!");
    Console.WriteLine($"⏱️  Duration: {duration.TotalSeconds:F1} seconds");
    Console.WriteLine($"🚀 Throughput: {throughputMbps:F1} MB/s");
    Console.WriteLine($"💾 File saved: {finalOutputPath}");

    // Verify file exists and has correct size
    var fileInfo = new FileInfo(finalOutputPath);
    if (fileInfo.Exists && fileInfo.Length == targetFile.Length)
    {
        Console.WriteLine("✅ File integrity verified!");
    }
    else
    {
        Console.WriteLine("❌ File integrity check failed!");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("🎉 Streaming download demonstration completed!");
    Console.WriteLine("📈 This method enables memory-efficient downloads of large files,");
    Console.WriteLine("   addressing TUF .NET parity with other implementations.");

    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }
    return 1;
}