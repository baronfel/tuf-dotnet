using System.CommandLine;
using System.Text.Json;

namespace TufSecurityAuditor;

/// <summary>
/// TUF Security Auditor - Comprehensive security validation for TUF repositories
/// 
/// This tool provides enterprise-grade security auditing capabilities for TUF repositories,
/// helping achieve parity with mature TUF implementations in other languages.
/// 
/// Features:
/// - Repository structure validation
/// - Security configuration auditing  
/// - Key management analysis
/// - Metadata consistency checks
/// - Comprehensive security reporting
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("TUF Security Auditor - Comprehensive security validation for TUF repositories");

        // Repository path argument
        var repositoryPathArg = new Argument<DirectoryInfo>("repository-path")
        {
            Description = "Path to the TUF repository to audit"
        };

        // Output options
        var outputFormatOption = new Option<OutputFormat>("--output-format")
        {
            Description = "Output format for the audit report",
            Value = OutputFormat.Console
        };

        var outputFileOption = new Option<FileInfo?>("--output")
        {
            Description = "Output file path (default: console output)"
        };

        // Audit focus options
        var detailedOption = new Option<bool>("--detailed")
        {
            Description = "Generate detailed audit report with explanations"
        };

        var recommendationsOption = new Option<bool>("--recommendations")
        {
            Description = "Include security recommendations in the report"
        };

        var focusOption = new Option<string[]>("--focus")
        {
            Description = "Focus on specific security aspects (keys, thresholds, expiration, crypto, structure)",
            AllowMultipleArgumentsPerToken = true
        };

        // CI/CD integration options
        var ciModeOption = new Option<bool>("--ci-mode")
        {
            Description = "Enable CI/CD mode with appropriate exit codes"
        };

        var failOnWarningsOption = new Option<bool>("--fail-on-warnings")
        {
            Description = "Exit with non-zero code on warnings (requires --ci-mode)"
        };

        var minScoreOption = new Option<int>("--min-score")
        {
            Description = "Minimum security score required (0-100, default: 70)"
        };
        minScoreOption.SetDefaultValue(70);

        // Create audit command
        var auditCommand = new Command("audit", "Audit a TUF repository for security issues");
        auditCommand.Add(repositoryPathArg);
        auditCommand.Add(outputFormatOption);
        auditCommand.Add(outputFileOption);
        auditCommand.Add(detailedOption);
        auditCommand.Add(recommendationsOption);
        auditCommand.Add(focusOption);
        auditCommand.Add(ciModeOption);
        auditCommand.Add(failOnWarningsOption);
        auditCommand.Add(minScoreOption);

        auditCommand.SetHandler(async (DirectoryInfo repositoryPath, OutputFormat outputFormat, FileInfo? outputFile, 
            bool detailed, bool recommendations, string[] focus, bool ciMode, bool failOnWarnings, int minScore) =>
        {
            var options = new AuditOptions
            {
                RepositoryPath = repositoryPath.FullName,
                OutputFormat = outputFormat,
                OutputFile = outputFile?.FullName,
                Detailed = detailed,
                IncludeRecommendations = recommendations,
                Focus = focus?.ToList() ?? new List<string>(),
                CiMode = ciMode,
                FailOnWarnings = failOnWarnings,
                MinimumScore = minScore
            };

            var auditor = new TufRepositoryAuditor();
            var result = await auditor.AuditAsync(options);
            
            await GenerateOutput(result, options);
            
            // Return appropriate exit code for CI/CD integration
            if (options.CiMode)
            {
                if (result.CriticalIssues.Count > 0)
                    return 2; // Critical issues found
                if (options.FailOnWarnings && result.Warnings.Count > 0)
                    return 1; // Warnings found (and configured to fail)
                if (result.SecurityScore < options.MinimumScore)
                    return 3; // Security score below threshold
            }
            
            return 0; // Success
        }, repositoryPathArg, outputFormatOption, outputFileOption, detailedOption, 
           recommendationsOption, focusOption, ciModeOption, failOnWarningsOption, minScoreOption);

        rootCommand.Add(auditCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task GenerateOutput(AuditResult result, AuditOptions options)
    {
        string output = options.OutputFormat switch
        {
            OutputFormat.Console => FormatConsoleOutput(result, options),
            OutputFormat.Json => JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
            OutputFormat.Html => FormatHtmlOutput(result, options),
            _ => throw new ArgumentException($"Unsupported output format: {options.OutputFormat}")
        };

        if (!string.IsNullOrEmpty(options.OutputFile))
        {
            await File.WriteAllTextAsync(options.OutputFile, output);
            if (!options.CiMode)
                Console.WriteLine($"Audit report written to: {options.OutputFile}");
        }
        else
        {
            Console.Write(output);
        }
    }

    private static string FormatConsoleOutput(AuditResult result, AuditOptions options)
    {
        var output = new System.Text.StringBuilder();
        
        output.AppendLine("TUF Security Auditor v1.0.0");
        output.AppendLine("============================");
        output.AppendLine();
        output.AppendLine($"Repository: {result.RepositoryPath}");
        output.AppendLine($"Audit Date: {result.AuditDate:yyyy-MM-ddTHH:mm:ssZ}");
        
        // Security score with color coding
        var scoreDescription = result.SecurityScore switch
        {
            >= 90 => "Excellent",
            >= 80 => "Good", 
            >= 70 => "Acceptable",
            >= 60 => "Needs Improvement",
            _ => "Poor"
        };
        
        output.AppendLine($"Security Score: {result.SecurityScore}/100 ({scoreDescription})");
        output.AppendLine();

        // Critical issues
        if (result.CriticalIssues.Count > 0)
        {
            output.AppendLine($"CRITICAL ISSUES ({result.CriticalIssues.Count}):");
            foreach (var issue in result.CriticalIssues)
            {
                output.AppendLine($"❌ {issue.Description}");
                if (options.Detailed && !string.IsNullOrEmpty(issue.Details))
                    output.AppendLine($"   {issue.Details}");
            }
            output.AppendLine();
        }

        // Warnings
        if (result.Warnings.Count > 0)
        {
            output.AppendLine($"WARNINGS ({result.Warnings.Count}):");
            foreach (var warning in result.Warnings)
            {
                output.AppendLine($"⚠️  {warning.Description}");
                if (options.Detailed && !string.IsNullOrEmpty(warning.Details))
                    output.AppendLine($"   {warning.Details}");
            }
            output.AppendLine();
        }

        // Recommendations
        if (options.IncludeRecommendations && result.Recommendations.Count > 0)
        {
            output.AppendLine("RECOMMENDATIONS:");
            foreach (var rec in result.Recommendations)
            {
                output.AppendLine($"✅ {rec.Description}");
                if (options.Detailed && !string.IsNullOrEmpty(rec.Details))
                    output.AppendLine($"   {rec.Details}");
            }
            output.AppendLine();
        }

        // Summary
        output.AppendLine("AUDIT SUMMARY:");
        output.AppendLine($"Repository Structure: {(result.RepositoryStructureValid ? "✅ Valid" : "❌ Invalid")}");
        output.AppendLine($"Metadata Consistency: {(result.MetadataConsistent ? "✅ Valid" : "❌ Invalid")}");
        output.AppendLine($"Key Management: {GetSummaryStatus(result.KeyManagementScore)}");
        output.AppendLine($"Crypto Settings: {GetSummaryStatus(result.CryptoSettingsScore)}");

        return output.ToString();
    }

    private static string GetSummaryStatus(int score)
    {
        return score switch
        {
            >= 90 => "✅ Excellent",
            >= 70 => "⚠️  Acceptable", 
            _ => "❌ Needs attention"
        };
    }

    private static string FormatHtmlOutput(AuditResult result, AuditOptions options)
    {
        // Basic HTML report generation
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>TUF Security Audit Report</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ border-bottom: 2px solid #333; padding-bottom: 10px; }}
        .critical {{ color: #d32f2f; }}
        .warning {{ color: #f57c00; }}
        .success {{ color: #388e3c; }}
        .score {{ font-size: 1.2em; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>TUF Security Audit Report</h1>
        <p><strong>Repository:</strong> {result.RepositoryPath}</p>
        <p><strong>Audit Date:</strong> {result.AuditDate:yyyy-MM-dd HH:mm:ss}</p>
        <p class='score'><strong>Security Score:</strong> {result.SecurityScore}/100</p>
    </div>
    
    <h2>Critical Issues ({result.CriticalIssues.Count})</h2>
    <ul>
        {string.Join("", result.CriticalIssues.Select(i => $"<li class='critical'>{i.Description}</li>"))}
    </ul>
    
    <h2>Warnings ({result.Warnings.Count})</h2>
    <ul>
        {string.Join("", result.Warnings.Select(w => $"<li class='warning'>{w.Description}</li>"))}
    </ul>
    
    <h2>Recommendations ({result.Recommendations.Count})</h2>
    <ul>
        {string.Join("", result.Recommendations.Select(r => $"<li class='success'>{r.Description}</li>"))}
    </ul>
</body>
</html>";
        
        return html;
    }
}

public enum OutputFormat
{
    Console,
    Json, 
    Html
}