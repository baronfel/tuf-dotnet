using System.Diagnostics;

namespace TUF.Documentation;

/// <summary>
/// Documentation generation utility for TUF .NET.
/// Runs DocFX to generate API documentation.
/// </summary>
internal class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("TUF .NET Documentation Generator");
        Console.WriteLine("================================");

        var docfxPath = Path.Combine(AppContext.BaseDirectory, "docfx.json");
        if (!File.Exists(docfxPath))
        {
            Console.Error.WriteLine($"DocFX configuration not found at: {docfxPath}");
            return 1;
        }

        try
        {
            Console.WriteLine("Generating API metadata...");
            var metadataResult = await RunDocFxCommand("metadata", docfxPath);
            if (metadataResult != 0)
                return metadataResult;

            Console.WriteLine("Building documentation site...");
            var buildResult = await RunDocFxCommand("build", docfxPath);
            if (buildResult != 0)
                return buildResult;

            var outputDir = Path.Combine(Path.GetDirectoryName(docfxPath)!, "_site");
            Console.WriteLine($"Documentation generated successfully at: {outputDir}");
            
            if (args.Contains("--serve"))
            {
                Console.WriteLine("Starting local server...");
                return await RunDocFxCommand("serve", outputDir);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error generating documentation: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunDocFxCommand(string command, string path)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docfx",
                Arguments = $"{command} \"{path}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();
        
        var output = await outputTask;
        var error = await errorTask;
        
        if (!string.IsNullOrWhiteSpace(output))
            Console.WriteLine(output);
            
        if (!string.IsNullOrWhiteSpace(error))
            Console.Error.WriteLine(error);

        return process.ExitCode;
    }
}