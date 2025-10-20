using CanonicalJson;
using TUF.Models;

namespace TufSecurityAuditor;

/// <summary>
/// Simplified TUF Security Auditor for demonstration purposes
/// </summary>
public class SimpleTufAuditor
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("TUF Security Auditor v1.0.0");
        Console.WriteLine("============================");
        Console.WriteLine();

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run <repository-path>");
            Console.WriteLine();
            Console.WriteLine("Example: dotnet run /path/to/tuf/repository");
            return 1;
        }

        var repositoryPath = args[0];
        
        if (!Directory.Exists(repositoryPath))
        {
            Console.WriteLine($"❌ Error: Repository path does not exist: {repositoryPath}");
            return 1;
        }

        var auditor = new SimplifiedAuditor();
        var result = await auditor.AuditRepositoryAsync(repositoryPath);
        
        // Display results
        Console.WriteLine($"Repository: {repositoryPath}");
        Console.WriteLine($"Audit Date: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        Console.WriteLine($"Security Score: {result.Score}/100 ({GetScoreDescription(result.Score)})");
        Console.WriteLine();

        if (result.CriticalIssues.Count > 0)
        {
            Console.WriteLine($"CRITICAL ISSUES ({result.CriticalIssues.Count}):");
            foreach (var issue in result.CriticalIssues)
            {
                Console.WriteLine($"❌ {issue}");
            }
            Console.WriteLine();
        }

        if (result.Warnings.Count > 0)
        {
            Console.WriteLine($"WARNINGS ({result.Warnings.Count}):");
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"⚠️  {warning}");
            }
            Console.WriteLine();
        }

        if (result.Recommendations.Count > 0)
        {
            Console.WriteLine("RECOMMENDATIONS:");
            foreach (var rec in result.Recommendations)
            {
                Console.WriteLine($"✅ {rec}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("AUDIT SUMMARY:");
        Console.WriteLine($"Repository Structure: {(result.StructureValid ? "✅ Valid" : "❌ Invalid")}");
        Console.WriteLine($"Key Management: {GetScoreSummary(result.KeyManagementScore)}");
        Console.WriteLine($"Metadata Status: {GetScoreSummary(result.MetadataScore)}");

        return result.CriticalIssues.Count > 0 ? 2 : 0;
    }

    private static string GetScoreDescription(int score)
    {
        return score switch
        {
            >= 90 => "Excellent",
            >= 80 => "Good",
            >= 70 => "Acceptable", 
            >= 60 => "Needs Improvement",
            _ => "Poor"
        };
    }

    private static string GetScoreSummary(int score)
    {
        return score switch
        {
            >= 90 => "✅ Excellent",
            >= 70 => "⚠️  Acceptable",
            _ => "❌ Needs attention"
        };
    }
}

/// <summary>
/// Simplified audit result
/// </summary>
public class SimpleAuditResult
{
    public int Score { get; init; }
    public List<string> CriticalIssues { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
    public bool StructureValid { get; init; }
    public int KeyManagementScore { get; init; }
    public int MetadataScore { get; init; }
}

/// <summary>
/// Simplified auditor implementation
/// </summary>
public class SimplifiedAuditor
{
    public async Task<SimpleAuditResult> AuditRepositoryAsync(string repositoryPath)
    {
        var result = new SimpleAuditResult();
        var criticalIssues = new List<string>();
        var warnings = new List<string>();
        var recommendations = new List<string>();
        
        // Check repository structure
        var structureScore = await CheckRepositoryStructure(repositoryPath, criticalIssues, warnings);
        
        // Check key management
        var keyMgmtScore = await CheckKeyManagement(repositoryPath, criticalIssues, warnings, recommendations);
        
        // Check metadata status
        var metadataScore = await CheckMetadataStatus(repositoryPath, criticalIssues, warnings);
        
        var overallScore = (structureScore + keyMgmtScore + metadataScore) / 3;
        
        return new SimpleAuditResult
        {
            Score = overallScore,
            CriticalIssues = criticalIssues,
            Warnings = warnings,
            Recommendations = recommendations,
            StructureValid = structureScore >= 80,
            KeyManagementScore = keyMgmtScore,
            MetadataScore = metadataScore
        };
    }

    private async Task<int> CheckRepositoryStructure(string repositoryPath, List<string> criticalIssues, List<string> warnings)
    {
        var score = 100;
        
        // Check required directories
        var requiredDirs = new[] { "metadata", "targets" };
        foreach (var dir in requiredDirs)
        {
            var dirPath = Path.Combine(repositoryPath, dir);
            if (!Directory.Exists(dirPath))
            {
                criticalIssues.Add($"Missing required directory: {dir}");
                score -= 40;
            }
        }
        
        // Check required metadata files
        var metadataPath = Path.Combine(repositoryPath, "metadata");
        var requiredFiles = new[] { "root.json", "timestamp.json", "snapshot.json", "targets.json" };
        
        foreach (var file in requiredFiles)
        {
            var filePath = Path.Combine(metadataPath, file);
            if (!File.Exists(filePath))
            {
                criticalIssues.Add($"Missing required metadata file: {file}");
                score -= 20;
            }
        }
        
        return Math.Max(0, score);
    }

    private async Task<int> CheckKeyManagement(string repositoryPath, List<string> criticalIssues, List<string> warnings, List<string> recommendations)
    {
        var score = 100;
        
        try
        {
            var rootPath = Path.Combine(repositoryPath, "metadata", "root.json");
            if (File.Exists(rootPath))
            {
                var rootJson = await File.ReadAllTextAsync(rootPath);
                var rootMetadata = Serializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(rootJson);
                
                // Check root key threshold
                var rootThreshold = rootMetadata.Signed.Roles.Root.Threshold;
                if (rootThreshold < 2)
                {
                    criticalIssues.Add($"Root key threshold is {rootThreshold} (recommended: 2+)");
                    score -= 30;
                }
                
                // Check key types
                var keyTypes = rootMetadata.Signed.Keys.Values.Select(k => k.KeyType).Distinct().ToList();
                var hasModernKeys = keyTypes.Any(k => k == "ed25519" || k == "ecdsa" || k == "rsa");
                
                if (!hasModernKeys)
                {
                    warnings.Add("No modern key algorithms detected");
                    score -= 20;
                }
                else
                {
                    recommendations.Add("Consider Ed25519 for optimal security and performance");
                }
                
                // Check if sufficient keys exist
                var rootKeyCount = rootMetadata.Signed.Keys.Count;
                if (rootKeyCount < rootThreshold)
                {
                    criticalIssues.Add($"Insufficient root keys: {rootKeyCount} keys for threshold {rootThreshold}");
                    score -= 40;
                }
            }
            else
            {
                criticalIssues.Add("Cannot analyze key management: root.json not found");
                score = 0;
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not fully analyze key management: {ex.Message}");
            score -= 30;
        }
        
        return Math.Max(0, score);
    }

    private async Task<int> CheckMetadataStatus(string repositoryPath, List<string> criticalIssues, List<string> warnings)
    {
        var score = 100;
        var now = DateTime.UtcNow;
        
        var metadataFiles = new[]
        {
            ("root.json", "Root"),
            ("timestamp.json", "Timestamp"),
            ("snapshot.json", "Snapshot"),
            ("targets.json", "Targets")
        };
        
        foreach (var (fileName, roleName) in metadataFiles)
        {
            try
            {
                var filePath = Path.Combine(repositoryPath, "metadata", fileName);
                if (File.Exists(filePath))
                {
                    var jsonContent = await File.ReadAllTextAsync(filePath);
                    
                    // Try to parse expiration date (simplified)
                    if (jsonContent.Contains("\"expires\":"))
                    {
                        // This is a very simplified expiration check
                        // In a real implementation, you'd properly deserialize the metadata
                        var expiresMatch = System.Text.RegularExpressions.Regex.Match(jsonContent, @"""expires""\s*:\s*""([^""]+)""");
                        if (expiresMatch.Success && DateTime.TryParse(expiresMatch.Groups[1].Value, out var expirationDate))
                        {
                            var daysUntilExpiration = (expirationDate - now).TotalDays;
                            
                            if (daysUntilExpiration <= 0)
                            {
                                criticalIssues.Add($"{roleName} metadata expired {-daysUntilExpiration:F1} days ago");
                                score -= 25;
                            }
                            else if (daysUntilExpiration <= 7)
                            {
                                warnings.Add($"{roleName} metadata expires in {daysUntilExpiration:F1} days");
                                score -= 10;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not check {roleName} metadata expiration: {ex.Message}");
                score -= 5;
            }
        }
        
        return Math.Max(0, score);
    }
}