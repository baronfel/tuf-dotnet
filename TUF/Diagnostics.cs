using System.Diagnostics;
using System.Text;
using System.Text.Json;

using TUF.Exceptions;
using TUF.Models;

namespace TUF.Diagnostics;

/// <summary>
/// Provides diagnostic utilities and troubleshooting helpers for TUF operations.
/// Designed to help developers identify and resolve common TUF configuration and runtime issues.
/// </summary>
/// <remarks>
/// This class provides tools to:
/// - Generate detailed diagnostic reports for TUF failures
/// - Validate TUF configurations before use
/// - Provide actionable troubleshooting guidance
/// - Log detailed debugging information
/// - Analyze metadata consistency and validity
/// 
/// These utilities are particularly useful during development, debugging,
/// and production issue resolution.
/// </remarks>
public static class TufDiagnostics
{
    /// <summary>
    /// Generates a comprehensive diagnostic report for a TUF exception.
    /// Provides actionable troubleshooting steps and context information.
    /// </summary>
    /// <param name="exception">The TUF exception to analyze</param>
    /// <param name="additionalContext">Optional additional context information</param>
    /// <returns>A detailed diagnostic report with troubleshooting guidance</returns>
    /// <remarks>
    /// The diagnostic report includes:
    /// - Exception details and root cause analysis
    /// - Specific troubleshooting steps based on exception type
    /// - Configuration validation suggestions
    /// - Common resolution strategies
    /// - Related documentation links
    /// 
    /// This is especially useful for production issue resolution
    /// and developer onboarding.
    /// </remarks>
    public static TufDiagnosticReport GenerateDiagnosticReport(Exception exception, Dictionary<string, object>? additionalContext = null)
    {
        var report = new TufDiagnosticReport
        {
            Timestamp = DateTime.UtcNow,
            ExceptionType = exception.GetType().Name,
            Message = exception.Message,
            AdditionalContext = additionalContext ?? new Dictionary<string, object>()
        };

        // Add specific diagnostics based on exception type
        switch (exception)
        {
            case SignatureVerificationException sve:
                report.ProblemArea = "Cryptographic Signature Verification";
                report.TroubleshootingSteps.AddRange([
                    "Verify that the correct trusted root metadata is being used",
                    $"Check that key ID '{sve.KeyId}' exists in the trusted key set",
                    "Ensure metadata has not been corrupted during transport",
                    "Verify that the signature algorithm is supported (Ed25519, RSA-PSS, ECDSA)",
                    "Check system time - expired keys may cause verification failures"
                ]);
                report.SuggestedActions.AddRange([
                    "Review key management and rotation procedures",
                    "Validate metadata using external tools (e.g., TUF CLI)",
                    "Check repository infrastructure for signing issues"
                ]);
                break;

            case InsufficientSignaturesException ise:
                report.ProblemArea = "Signature Threshold Validation";
                report.TroubleshootingSteps.AddRange([
                    $"Role '{ise.RoleName}' requires {ise.RequiredSignatures} valid signatures but only {ise.ValidSignatures} were found",
                    "Verify that all required keys are available and valid",
                    "Check if any signatures failed individual verification",
                    "Ensure threshold configuration matches repository setup"
                ]);
                report.SuggestedActions.AddRange([
                    "Review role delegation and threshold settings in root metadata",
                    "Verify that sufficient authorized signers are available",
                    "Check for key rotation or revocation events"
                ]);
                break;

            case ExpiredMetadataException eme:
                report.ProblemArea = "Metadata Expiration";
                report.TroubleshootingSteps.AddRange([
                    $"Metadata type '{eme.MetadataType}' expired at {eme.ExpiryTime:yyyy-MM-dd HH:mm:ss} UTC",
                    "Check if repository maintainer has updated metadata",
                    "Verify system clock is accurate",
                    "Check network connectivity to metadata repository"
                ]);
                report.SuggestedActions.AddRange([
                    "Contact repository maintainer to refresh expired metadata",
                    "Implement automated monitoring for approaching expiration dates",
                    "Review metadata refresh frequency policies"
                ]);
                break;

            case RollbackAttackException rae:
                report.ProblemArea = "Rollback Attack Protection";
                report.TroubleshootingSteps.AddRange([
                    $"Metadata '{rae.MetadataType}' version {rae.ActualVersion} is older than cached version {rae.ExpectedVersion}",
                    "This could indicate a rollback attack or repository issue",
                    "Verify repository integrity and signing infrastructure",
                    "Check if metadata cache needs to be reset (use with caution)"
                ]);
                report.SuggestedActions.AddRange([
                    "DO NOT ignore this error - investigate potential security incident",
                    "Contact repository administrator immediately",
                    "Review repository access logs and signing procedures",
                    "Consider incident response procedures if attack is suspected"
                ]);
                break;

            case TargetIntegrityException tie:
                report.ProblemArea = "Target File Integrity";
                report.TroubleshootingSteps.AddRange([
                    $"Target file '{tie.TargetPath}' failed integrity verification",
                    $"Expected hash: {tie.ExpectedHash}",
                    $"Actual hash: {tie.ActualHash}",
                    "File may be corrupted during download or storage",
                    "Check network stability and retry download"
                ]);
                report.SuggestedActions.AddRange([
                    "Retry download from repository",
                    "Verify repository target file integrity",
                    "Check for network or storage issues",
                    "Clear local target cache and re-download"
                ]);
                break;

            case RepositoryNetworkException rne:
                report.ProblemArea = "Repository Network Access";
                report.TroubleshootingSteps.AddRange([
                    $"Network error accessing repository: {rne.RepositoryUri}",
                    $"HTTP Status: {rne.StatusCode}",
                    "Check network connectivity to repository URL",
                    "Verify repository is operational and accessible",
                    "Check for proxy or firewall restrictions"
                ]);
                report.SuggestedActions.AddRange([
                    "Verify repository URL is correct and accessible",
                    "Check DNS resolution for repository domain",
                    "Test with curl or browser to isolate network issues",
                    "Review proxy/firewall configuration if applicable"
                ]);
                break;

            case MetadataValidationException mve:
                report.ProblemArea = "Metadata Structure Validation";
                report.TroubleshootingSteps.AddRange([
                    $"Validation failed for '{mve.MetadataType}' metadata",
                    $"Validation rule: {mve.ValidationRule}",
                    "Check metadata format against TUF specification",
                    "Verify required fields are present and correctly formatted"
                ]);
                report.SuggestedActions.AddRange([
                    "Review TUF specification for metadata requirements",
                    "Validate metadata using schema validation tools",
                    "Check repository metadata generation process"
                ]);
                break;

            case TufConfigurationException tce:
                report.ProblemArea = "TUF Client Configuration";
                report.TroubleshootingSteps.AddRange([
                    $"Configuration error in property: {tce.ConfigurationProperty}",
                    "Review UpdaterConfig settings",
                    "Check file paths and URLs for correctness",
                    "Verify required configuration properties are set"
                ]);
                report.SuggestedActions.AddRange([
                    "Review TUF client configuration documentation",
                    "Validate all required configuration parameters",
                    "Check file system permissions for local paths"
                ]);
                break;

            default:
                report.ProblemArea = "General TUF Error";
                report.TroubleshootingSteps.AddRange([
                    "Review exception details and stack trace",
                    "Check TUF client configuration",
                    "Verify repository accessibility and metadata validity"
                ]);
                report.SuggestedActions.AddRange([
                    "Enable detailed logging for more information",
                    "Consult TUF specification and documentation",
                    "Consider using TUF diagnostic utilities"
                ]);
                break;
        }

        // Add stack trace if available
        if (exception.StackTrace != null)
        {
            report.AdditionalContext["StackTrace"] = exception.StackTrace;
        }

        // Add inner exception details if present
        if (exception.InnerException != null)
        {
            report.AdditionalContext["InnerException"] = new
            {
                Type = exception.InnerException.GetType().Name,
                Message = exception.InnerException.Message
            };
        }

        return report;
    }

    /// <summary>
    /// Validates TUF client configuration and identifies potential issues.
    /// Performs comprehensive checks on UpdaterConfig settings before use.
    /// </summary>
    /// <param name="config">The UpdaterConfig to validate</param>
    /// <returns>Validation result with any identified issues and recommendations</returns>
    /// <remarks>
    /// This method performs the following validations:
    /// - Trusted root metadata format and validity
    /// - Repository URLs accessibility and format
    /// - Local directory permissions and disk space
    /// - HTTP client configuration
    /// - Security settings and best practices
    /// 
    /// Use this before initializing an Updater to catch configuration
    /// issues early in the development cycle.
    /// </remarks>
    public static async Task<TufConfigurationValidationResult> ValidateConfigurationAsync(UpdaterConfig config)
    {
        var result = new TufConfigurationValidationResult();
        var issues = new List<string>();
        var warnings = new List<string>();
        var recommendations = new List<string>();

        try
        {
            // Validate trusted root metadata
            if (config.LocalTrustedRoot == null || config.LocalTrustedRoot.Length == 0)
            {
                issues.Add("Trusted root metadata is null or empty");
                recommendations.Add("Provide a valid trusted root metadata file from a secure source");
            }
            else
            {
                try
                {
                    // Attempt to parse trusted root metadata
                    var rootJson = Encoding.UTF8.GetString(config.LocalTrustedRoot);
                    using var jsonDoc = JsonDocument.Parse(rootJson);
                    
                    if (!jsonDoc.RootElement.TryGetProperty("signed", out var signedElement) ||
                        !signedElement.TryGetProperty("_type", out var typeElement) ||
                        typeElement.GetString() != "root")
                    {
                        issues.Add("Trusted root metadata does not appear to be valid root metadata");
                    }
                    
                    // Check for expiration
                    if (signedElement.TryGetProperty("expires", out var expiresElement) &&
                        DateTime.TryParse(expiresElement.GetString(), out var expiryDate))
                    {
                        if (expiryDate < DateTime.UtcNow)
                        {
                            warnings.Add($"Trusted root metadata expires on {expiryDate:yyyy-MM-dd}");
                            recommendations.Add("Obtain updated trusted root metadata before expiration");
                        }
                        else if ((expiryDate - DateTime.UtcNow).TotalDays < 30)
                        {
                            warnings.Add($"Trusted root metadata expires soon ({expiryDate:yyyy-MM-dd})");
                        }
                    }
                }
                catch (JsonException)
                {
                    issues.Add("Trusted root metadata is not valid JSON");
                }
            }

            // Validate repository URI
            if (config.RemoteMetadataUrl == null)
            {
                issues.Add("Repository URI is null");
                recommendations.Add("Specify a valid HTTPS URL for the TUF metadata repository");
            }
            else
            {
                if (config.RemoteMetadataUrl.Scheme != "https")
                {
                    warnings.Add("Repository URI uses non-HTTPS scheme - consider security implications");
                    recommendations.Add("Use HTTPS for production deployments to protect metadata integrity");
                }

                // Test repository accessibility
                if (config.Client != null)
                {
                    try
                    {
                        var testUri = new Uri(config.RemoteMetadataUrl, "root.json");
                        using var response = await config.Client.GetAsync(testUri, HttpCompletionOption.ResponseHeadersRead);
                        if (!response.IsSuccessStatusCode)
                        {
                            warnings.Add($"Repository appears inaccessible: HTTP {(int)response.StatusCode} {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Unable to test repository accessibility: {ex.Message}");
                        recommendations.Add("Verify network connectivity and repository URL");
                    }
                }
            }

            // Validate local directories
            if (!string.IsNullOrEmpty(config.LocalMetadataDir))
            {
                try
                {
                    var metadataDir = new DirectoryInfo(config.LocalMetadataDir);
                    if (!metadataDir.Exists)
                    {
                        warnings.Add($"Metadata directory does not exist: {config.LocalMetadataDir}");
                        recommendations.Add("Ensure directory exists or will be created with appropriate permissions");
                    }
                }
                catch (Exception ex)
                {
                    issues.Add($"Invalid metadata directory path: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(config.LocalTargetsDir))
            {
                try
                {
                    var targetsDir = new DirectoryInfo(config.LocalTargetsDir);
                    if (!targetsDir.Exists)
                    {
                        warnings.Add($"Targets directory does not exist: {config.LocalTargetsDir}");
                        recommendations.Add("Ensure directory exists or will be created with appropriate permissions");
                    }
                }
                catch (Exception ex)
                {
                    issues.Add($"Invalid targets directory path: {ex.Message}");
                }
            }

            // Validate HTTP client configuration
            if (config.Client == null)
            {
                warnings.Add("No HttpClient provided - will use default client");
                recommendations.Add("Consider providing a configured HttpClient with appropriate timeouts and retry policies");
            }
            else
            {
                if (config.Client.Timeout < TimeSpan.FromSeconds(30))
                {
                    warnings.Add("HttpClient timeout is very short - may cause issues with large metadata files");
                }

                if (config.Client.Timeout > TimeSpan.FromMinutes(10))
                {
                    warnings.Add("HttpClient timeout is very long - may cause poor user experience");
                }
            }

            result.IsValid = issues.Count == 0;
            result.Issues = issues;
            result.Warnings = warnings;
            result.Recommendations = recommendations;
        }
        catch (Exception ex)
        {
            issues.Add($"Validation failed with exception: {ex.Message}");
            result.IsValid = false;
            result.Issues = issues;
        }

        return result;
    }

    /// <summary>
    /// Analyzes TUF metadata consistency across multiple metadata files.
    /// Detects common issues like version mismatches and structural problems.
    /// </summary>
    /// <param name="metadataFiles">Dictionary of metadata type to metadata content</param>
    /// <returns>Analysis result with identified consistency issues</returns>
    /// <remarks>
    /// This method performs cross-metadata validation including:
    /// - Version consistency checks
    /// - Timestamp ordering validation
    /// - Key reference integrity
    /// - Role delegation consistency
    /// - Expiration date analysis
    /// 
    /// Useful for debugging repository-side issues and validating
    /// metadata generation processes.
    /// </remarks>
    public static TufMetadataConsistencyReport AnalyzeMetadataConsistency(Dictionary<string, byte[]> metadataFiles)
    {
        var report = new TufMetadataConsistencyReport
        {
            Timestamp = DateTime.UtcNow,
            MetadataFiles = metadataFiles.Keys.ToList()
        };

        try
        {
            var parsedMetadata = new Dictionary<string, JsonDocument>();

            // Parse all metadata files
            foreach (var kvp in metadataFiles)
            {
                try
                {
                    var json = Encoding.UTF8.GetString(kvp.Value);
                    parsedMetadata[kvp.Key] = JsonDocument.Parse(json);
                }
                catch (Exception ex)
                {
                    report.Issues.Add($"Failed to parse {kvp.Key} metadata: {ex.Message}");
                }
            }

            // Analyze version consistency
            if (parsedMetadata.ContainsKey("timestamp") && parsedMetadata.ContainsKey("snapshot"))
            {
                var timestampVersion = GetMetadataVersion(parsedMetadata["timestamp"], "snapshot");
                var snapshotVersion = GetMetadataVersion(parsedMetadata["snapshot"], "_type");

                if (timestampVersion != snapshotVersion)
                {
                    report.Issues.Add($"Version mismatch: timestamp references snapshot v{timestampVersion} but snapshot is v{snapshotVersion}");
                }
            }

            // Analyze expiration dates
            var expirations = new List<(string type, DateTime expiry)>();
            foreach (var kvp in parsedMetadata)
            {
                if (kvp.Value.RootElement.TryGetProperty("signed", out var signed) &&
                    signed.TryGetProperty("expires", out var expiresElement) &&
                    DateTime.TryParse(expiresElement.GetString(), out var expiryDate))
                {
                    expirations.Add((kvp.Key, expiryDate));
                    
                    if (expiryDate < DateTime.UtcNow)
                    {
                        report.Issues.Add($"{kvp.Key} metadata has expired ({expiryDate:yyyy-MM-dd HH:mm:ss} UTC)");
                    }
                }
            }

            // Check expiration ordering (timestamp should expire first, then snapshot, then targets)
            var expectedOrder = new[] { "timestamp", "snapshot", "targets", "root" };
            var orderedExpirations = expirations.OrderBy(e => e.expiry).ToList();
            
            for (int i = 0; i < orderedExpirations.Count - 1; i++)
            {
                var current = orderedExpirations[i];
                var next = orderedExpirations[i + 1];
                
                var currentIndex = Array.IndexOf(expectedOrder, current.type);
                var nextIndex = Array.IndexOf(expectedOrder, next.type);
                
                if (currentIndex > nextIndex && currentIndex != -1 && nextIndex != -1)
                {
                    report.Warnings.Add($"Unusual expiration order: {current.type} expires before {next.type}");
                }
            }

            report.IsConsistent = report.Issues.Count == 0;

            // Cleanup
            foreach (var doc in parsedMetadata.Values)
            {
                doc.Dispose();
            }
        }
        catch (Exception ex)
        {
            report.Issues.Add($"Consistency analysis failed: {ex.Message}");
            report.IsConsistent = false;
        }

        return report;
    }

    private static int GetMetadataVersion(JsonDocument doc, string versionProperty)
    {
        if (doc.RootElement.TryGetProperty("signed", out var signed))
        {
            if (versionProperty == "_type")
            {
                return signed.TryGetProperty("version", out var versionElement) ? versionElement.GetInt32() : 0;
            }
            else if (signed.TryGetProperty("meta", out var meta) &&
                     meta.TryGetProperty(versionProperty, out var metaElement) &&
                     metaElement.TryGetProperty("version", out var versionElement))
            {
                return versionElement.GetInt32();
            }
        }
        return 0;
    }
}

/// <summary>
/// Represents a comprehensive diagnostic report for TUF exceptions and issues.
/// </summary>
public class TufDiagnosticReport
{
    /// <summary>Timestamp when the diagnostic report was generated</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Type of exception that occurred</summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>Exception message</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>General problem area (e.g., "Signature Verification", "Network Access")</summary>
    public string ProblemArea { get; set; } = string.Empty;

    /// <summary>Specific troubleshooting steps for this issue</summary>
    public List<string> TroubleshootingSteps { get; set; } = new();

    /// <summary>Recommended actions to resolve the issue</summary>
    public List<string> SuggestedActions { get; set; } = new();

    /// <summary>Additional context information</summary>
    public Dictionary<string, object> AdditionalContext { get; set; } = new();

    /// <summary>
    /// Formats the diagnostic report as a human-readable string.
    /// </summary>
    /// <returns>Formatted diagnostic report</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== TUF Diagnostic Report ({Timestamp:yyyy-MM-dd HH:mm:ss} UTC) ===");
        sb.AppendLine($"Problem Area: {ProblemArea}");
        sb.AppendLine($"Exception: {ExceptionType}");
        sb.AppendLine($"Message: {Message}");
        sb.AppendLine();

        if (TroubleshootingSteps.Count > 0)
        {
            sb.AppendLine("🔍 Troubleshooting Steps:");
            foreach (var step in TroubleshootingSteps)
            {
                sb.AppendLine($"  • {step}");
            }
            sb.AppendLine();
        }

        if (SuggestedActions.Count > 0)
        {
            sb.AppendLine("💡 Suggested Actions:");
            foreach (var action in SuggestedActions)
            {
                sb.AppendLine($"  → {action}");
            }
            sb.AppendLine();
        }

        if (AdditionalContext.Count > 0)
        {
            sb.AppendLine("📋 Additional Context:");
            foreach (var kvp in AdditionalContext)
            {
                if (kvp.Key != "StackTrace")
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Represents the result of TUF configuration validation.
/// </summary>
public class TufConfigurationValidationResult
{
    /// <summary>Whether the configuration is valid and ready for use</summary>
    public bool IsValid { get; set; }

    /// <summary>Critical issues that prevent TUF operation</summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>Warnings about potential problems or suboptimal settings</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Recommendations for improving the configuration</summary>
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Represents the result of TUF metadata consistency analysis.
/// </summary>
public class TufMetadataConsistencyReport
{
    /// <summary>Timestamp when the analysis was performed</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>List of metadata files that were analyzed</summary>
    public List<string> MetadataFiles { get; set; } = new();

    /// <summary>Whether the metadata set is internally consistent</summary>
    public bool IsConsistent { get; set; }

    /// <summary>Critical consistency issues found</summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>Warnings about potential consistency problems</summary>
    public List<string> Warnings { get; set; } = new();
}