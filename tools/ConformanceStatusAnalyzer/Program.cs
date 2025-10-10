using System.CommandLine;
using System.Diagnostics;
using CanonicalJson;

namespace ConformanceStatusAnalyzer;

/// <summary>
/// TUF .NET Conformance Status Analyzer
/// 
/// This tool helps analyze and report the current status of TUF conformance testing
/// for the .NET TUF implementation. It can:
/// - Validate the conformance CLI is working properly
/// - Generate reports on conformance test readiness
/// - Update the .tuf-conformance-xfails file based on analysis
/// - Provide recommendations for improving conformance
/// </summary>
public class Program
{
    private static readonly Option<bool> VerboseOption = new("--verbose")
    {
        Description = "Enable verbose output"
    };
    private static readonly Option<string> ConformanceCliPathOption = new("--conformance-cli-path")
    {
        Description = "Path to the TUF conformance CLI executable",
        Required = false
    };
    private static readonly Option<FileInfo> OutputReportOption = new("--output-report")
    {
        Description = "Output file for conformance status report"
    };

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("TUF .NET Conformance Status Analyzer")
        {
            VerboseOption,
            ConformanceCliPathOption,
            OutputReportOption
        };

        rootCommand.SetAction(async (parseResult, token) =>
        {
            var verbose = parseResult.GetValue(VerboseOption);
            var cliPath = parseResult.GetValue(ConformanceCliPathOption);
            var outputReport = parseResult.GetValue(OutputReportOption);
            
            await RunAnalysisAsync(verbose, cliPath, outputReport);
            return 0;
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static async Task RunAnalysisAsync(bool verbose, string? cliPath, FileInfo? outputReport)
    {
        Console.WriteLine("🔍 TUF .NET Conformance Status Analysis");
        Console.WriteLine("=====================================\n");

        var report = new ConformanceStatusReport();
        
        // Step 1: Validate conformance CLI exists and works
        await AnalyzeConformanceCliAsync(cliPath, report, verbose);
        
        // Step 2: Check current test infrastructure
        await AnalyzeTestInfrastructureAsync(report, verbose);
        
        // Step 3: Analyze xfails file status
        await AnalyzeXfailsFileAsync(report, verbose);
        
        // Step 4: Check for existing conformance test integration
        await AnalyzeConformanceIntegrationAsync(report, verbose);
        
        // Step 5: Generate recommendations
        GenerateRecommendations(report, verbose);
        
        // Step 6: Output report
        await OutputReportAsync(report, outputReport, verbose);
    }

    private static async Task AnalyzeConformanceCliAsync(string? cliPath, ConformanceStatusReport report, bool verbose)
    {
        Console.WriteLine("📋 Analyzing TUF Conformance CLI...");
        
        try
        {
            // Default to the examples project if no path specified
            cliPath ??= "examples/TufConformanceCli/TufConformanceCli.csproj";
            
            if (verbose)
            {
                Console.WriteLine($"   Using CLI path: {cliPath}");
            }

            // Test if CLI can be invoked
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project {cliPath} -- --help",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && output.Contains("TUF .NET Conformance CLI"))
            {
                report.ConformanceCliStatus = "✅ Working - CLI responds to --help";
                Console.WriteLine("   ✅ Conformance CLI is functional");
            }
            else
            {
                report.ConformanceCliStatus = $"❌ Failed - Exit code: {process.ExitCode}, Error: {error}";
                Console.WriteLine("   ❌ Conformance CLI has issues");
            }

            if (verbose && !string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"   Error output: {error}");
            }
        }
        catch (Exception ex)
        {
            report.ConformanceCliStatus = $"❌ Exception: {ex.Message}";
            Console.WriteLine($"   ❌ Failed to analyze CLI: {ex.Message}");
        }
    }

    private static async Task AnalyzeTestInfrastructureAsync(ConformanceStatusReport report, bool verbose)
    {
        Console.WriteLine("\n🧪 Analyzing Test Infrastructure...");
        
        var testProjects = new[]
        {
            "TUF.Tests/TUF.Tests.csproj",
            "CanonicalJson.Tests/CanonicalJson.Tests.csproj",
            "TUF.ConformanceTests/TUF.ConformanceTests.csproj"
        };

        var workingProjects = new List<string>();
        var brokenProjects = new List<string>();

        foreach (var project in testProjects)
        {
            if (File.Exists(project))
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"test {project} --no-build --verbosity quiet",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        workingProjects.Add(project);
                        if (verbose) Console.WriteLine($"   ✅ {project}");
                    }
                    else
                    {
                        brokenProjects.Add(project);
                        if (verbose) Console.WriteLine($"   ❌ {project}");
                    }
                }
                catch
                {
                    brokenProjects.Add(project);
                    if (verbose) Console.WriteLine($"   ❌ {project} (exception)");
                }
            }
            else
            {
                if (verbose) Console.WriteLine($"   ⚠️  {project} (not found)");
            }
        }

        report.TestProjectsWorking = workingProjects.Count;
        report.TestProjectsBroken = brokenProjects.Count;
        
        Console.WriteLine($"   ✅ {workingProjects.Count} test projects working, ❌ {brokenProjects.Count} broken");
    }

    private static async Task AnalyzeXfailsFileAsync(ConformanceStatusReport report, bool verbose)
    {
        Console.WriteLine("\n📄 Analyzing .tuf-conformance-xfails file...");
        
        const string xfailsFile = ".tuf-conformance-xfails";
        
        if (File.Exists(xfailsFile))
        {
            var content = await File.ReadAllTextAsync(xfailsFile);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var actualFailures = lines.Where(line => !line.Trim().StartsWith('#') && !string.IsNullOrWhiteSpace(line)).ToArray();
            
            report.XfailsFileExists = true;
            report.ExpectedFailuresCount = actualFailures.Length;
            
            if (actualFailures.Length == 0)
            {
                Console.WriteLine("   ⚠️  File exists but contains no expected failures");
                Console.WriteLine("       This suggests conformance status is unknown");
            }
            else
            {
                Console.WriteLine($"   📋 {actualFailures.Length} expected failures documented");
                if (verbose)
                {
                    foreach (var failure in actualFailures.Take(5))
                    {
                        Console.WriteLine($"      - {failure}");
                    }
                    if (actualFailures.Length > 5)
                    {
                        Console.WriteLine($"      ... and {actualFailures.Length - 5} more");
                    }
                }
            }
        }
        else
        {
            report.XfailsFileExists = false;
            report.ExpectedFailuresCount = -1;
            Console.WriteLine("   ❌ .tuf-conformance-xfails file not found");
        }
    }

    private static async Task AnalyzeConformanceIntegrationAsync(ConformanceStatusReport report, bool verbose)
    {
        Console.WriteLine("\n🔗 Analyzing Conformance Integration...");
        
        // Check GitHub Actions workflows for conformance testing
        var workflowsDir = ".github/workflows";
        var hasConformanceWorkflow = false;
        
        if (Directory.Exists(workflowsDir))
        {
            var workflows = Directory.GetFiles(workflowsDir, "*.yml").Concat(Directory.GetFiles(workflowsDir, "*.yaml"));
            
            foreach (var workflow in workflows)
            {
                var content = await File.ReadAllTextAsync(workflow);
                if (content.Contains("tuf-conformance") || content.Contains("conformance"))
                {
                    hasConformanceWorkflow = true;
                    if (verbose)
                    {
                        Console.WriteLine($"   📁 Found conformance reference in: {Path.GetFileName(workflow)}");
                    }
                }
            }
        }
        
        report.HasConformanceWorkflow = hasConformanceWorkflow;
        
        if (hasConformanceWorkflow)
        {
            Console.WriteLine("   ✅ Conformance workflow integration found");
        }
        else
        {
            Console.WriteLine("   ❌ No conformance workflow integration found");
        }
    }

    private static void GenerateRecommendations(ConformanceStatusReport report, bool verbose)
    {
        Console.WriteLine("\n💡 Generating Recommendations...");
        
        var recommendations = new List<string>();
        
        if (report.ConformanceCliStatus.StartsWith("❌"))
        {
            recommendations.Add("Fix conformance CLI issues to enable testing");
        }
        
        if (report.ExpectedFailuresCount == 0 || report.ExpectedFailuresCount == -1)
        {
            recommendations.Add("Run official TUF conformance test suite to establish baseline");
            recommendations.Add("Update .tuf-conformance-xfails with actual test results");
        }
        
        if (!report.HasConformanceWorkflow)
        {
            recommendations.Add("Add GitHub Actions workflow for conformance testing");
        }
        
        if (report.TestProjectsBroken > 0)
        {
            recommendations.Add($"Fix {report.TestProjectsBroken} broken test projects");
        }
        
        // Always recommend conformance integration if not fully set up
        if (report.ExpectedFailuresCount == 0)
        {
            recommendations.Add("Integrate with theupdateframework/tuf-conformance@v2 action");
            recommendations.Add("Create automated conformance status reporting");
        }
        
        report.Recommendations = recommendations;
        
        foreach (var recommendation in recommendations)
        {
            Console.WriteLine($"   📌 {recommendation}");
        }
    }

    private static async Task OutputReportAsync(ConformanceStatusReport report, FileInfo? outputFile, bool verbose)
    {
        Console.WriteLine("\n📊 Conformance Status Summary");
        Console.WriteLine("=============================");
        Console.WriteLine($"Conformance CLI: {report.ConformanceCliStatus}");
        Console.WriteLine($"Test Projects: {report.TestProjectsWorking} working, {report.TestProjectsBroken} broken");
        Console.WriteLine($"Expected Failures: {(report.ExpectedFailuresCount == -1 ? "Unknown (no xfails file)" : report.ExpectedFailuresCount.ToString())}");
        Console.WriteLine($"Workflow Integration: {(report.HasConformanceWorkflow ? "Present" : "Missing")}");
        Console.WriteLine($"Recommendations: {report.Recommendations.Count}");
        
        if (outputFile != null)
        {
            var reportText = $@"TUF .NET Conformance Status Report
Generated: {report.AnalysisDate:yyyy-MM-dd HH:mm:ss} UTC

CONFORMANCE CLI: {report.ConformanceCliStatus}
TEST PROJECTS: {report.TestProjectsWorking} working, {report.TestProjectsBroken} broken  
EXPECTED FAILURES: {(report.ExpectedFailuresCount == -1 ? "Unknown (no xfails file)" : report.ExpectedFailuresCount.ToString())}
WORKFLOW INTEGRATION: {(report.HasConformanceWorkflow ? "Present" : "Missing")}

RECOMMENDATIONS:
{string.Join("\n", report.Recommendations.Select(r => $"- {r}"))}
";
            await File.WriteAllTextAsync(outputFile.FullName, reportText);
            Console.WriteLine($"\n📄 Report saved to: {outputFile.FullName}");
        }
    }
}

/// <summary>
/// Report containing the analysis results of TUF conformance status
/// </summary>
public class ConformanceStatusReport
{
    public string ConformanceCliStatus { get; set; } = "";
    public int TestProjectsWorking { get; set; }
    public int TestProjectsBroken { get; set; }
    public bool XfailsFileExists { get; set; }
    public int ExpectedFailuresCount { get; set; }
    public bool HasConformanceWorkflow { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
}