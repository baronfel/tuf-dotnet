using CanonicalJson;
using TUF;
using TUF.Models;

namespace TufSecurityAuditor;

/// <summary>
/// Core TUF repository security auditor that validates repository structure, 
/// security configurations, and generates comprehensive audit reports.
/// </summary>
public class TufRepositoryAuditor
{
    private readonly List<IAuditCheck> _auditChecks;

    public TufRepositoryAuditor()
    {
        _auditChecks = new List<IAuditCheck>
        {
            new RepositoryStructureCheck(),
            new KeyManagementCheck(),
            new ThresholdSecurityCheck(), 
            new MetadataExpirationCheck(),
            new CryptographicSettingsCheck(),
            new DelegationSecurityCheck(),
            new ConsistentSnapshotCheck(),
            new SecurityBestPracticesCheck()
        };
    }

    /// <summary>
    /// Performs a comprehensive security audit of a TUF repository
    /// </summary>
    public async Task<AuditResult> AuditAsync(AuditOptions options)
    {
        var repositoryPath = options.RepositoryPath;
        
        // Validate repository path exists
        if (!Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException($"Repository path does not exist: {repositoryPath}");
        }

        // Load repository metadata
        var metadata = await LoadRepositoryMetadataAsync(repositoryPath);
        
        // Run all audit checks
        var auditChecks = new List<AuditCheck>();
        var criticalIssues = new List<SecurityIssue>();
        var warnings = new List<SecurityIssue>();
        var recommendations = new List<SecurityRecommendation>();
        
        foreach (var check in _auditChecks)
        {
            // Skip checks not in focus if focus is specified
            if (options.Focus.Any() && !options.Focus.Contains(check.Category, StringComparer.OrdinalIgnoreCase))
                continue;
                
            try
            {
                var result = await check.ExecuteAsync(repositoryPath, metadata);
                auditChecks.AddRange(result.Checks);
                
                // Categorize issues based on severity
                foreach (var issue in result.Issues)
                {
                    if (issue.Severity == SecuritySeverity.Critical || issue.Severity == SecuritySeverity.High)
                        criticalIssues.Add(issue);
                    else
                        warnings.Add(issue);
                }
                
                recommendations.AddRange(result.Recommendations);
            }
            catch (Exception ex)
            {
                // Add error as critical issue if check fails unexpectedly
                criticalIssues.Add(new SecurityIssue
                {
                    Category = check.Category,
                    Description = $"Audit check failed: {check.GetType().Name}",
                    Details = ex.Message,
                    Severity = SecuritySeverity.High
                });
            }
        }

        // Calculate overall security score
        var securityScore = CalculateSecurityScore(auditChecks, criticalIssues.Count, warnings.Count);
        
        // Calculate category-specific scores
        var keyMgmtScore = CalculateCategoryScore(auditChecks, AuditCategories.KeyManagement);
        var cryptoScore = CalculateCategoryScore(auditChecks, AuditCategories.Cryptography);
        
        // Group detailed results by category
        var detailedResults = auditChecks
            .GroupBy(c => GetCheckCategory(c.Name))
            .ToDictionary(g => g.Key, g => g.ToList());

        return new AuditResult
        {
            RepositoryPath = repositoryPath,
            SecurityScore = securityScore,
            CriticalIssues = criticalIssues,
            Warnings = warnings,
            Recommendations = recommendations,
            RepositoryStructureValid = !criticalIssues.Any(i => i.Category == AuditCategories.RepositoryStructure),
            MetadataConsistent = !criticalIssues.Any(i => i.Category == "metadata-consistency"),
            KeyManagementScore = keyMgmtScore,
            CryptoSettingsScore = cryptoScore,
            DetailedResults = detailedResults
        };
    }

    private async Task<RepositoryMetadata> LoadRepositoryMetadataAsync(string repositoryPath)
    {
        var metadata = new RepositoryMetadata();
        
        try
        {
            // Load root metadata
            var rootPath = Path.Combine(repositoryPath, "metadata", "root.json");
            if (File.Exists(rootPath))
            {
                var rootJson = await File.ReadAllTextAsync(rootPath);
                var rootMetadata = Serializer.Deserialize<Metadata<Root>>(rootJson);
                
                metadata.RootVersion = rootMetadata.Signed.Version.ToString();
                metadata.RootExpires = rootMetadata.Signed.Expires.DateTime;
                metadata.RootThreshold = rootMetadata.Signed.Roles.Root.Threshold;
                metadata.RootKeyCount = rootMetadata.Signed.Keys.Count;
                metadata.ConsistentSnapshot = rootMetadata.Signed.ConsistentSnapshot;
                
                // Extract key information
                metadata.KeyTypes = rootMetadata.Signed.Keys.Values.Select(k => k.KeyType).Distinct().ToList();
                metadata.SignatureSchemes = rootMetadata.Signed.Keys.Values.Select(k => k.Scheme).Distinct().ToList();
                
                // Extract other role thresholds
                metadata.TimestampThreshold = rootMetadata.Signed.Roles.Timestamp.Threshold;
                metadata.SnapshotThreshold = rootMetadata.Signed.Roles.Snapshot.Threshold;
                metadata.TargetsThreshold = rootMetadata.Signed.Roles.Targets.Threshold;
            }
            
            // Load timestamp metadata 
            var timestampPath = Path.Combine(repositoryPath, "metadata", "timestamp.json");
            if (File.Exists(timestampPath))
            {
                var timestampJson = await File.ReadAllTextAsync(timestampPath);
                var timestampMetadata = Serializer.Deserialize<Metadata<Timestamp>>(timestampJson);
                metadata.TimestampExpires = timestampMetadata.Signed.Expires.DateTime;
            }
            
            // Load snapshot metadata
            var snapshotPath = Path.Combine(repositoryPath, "metadata", "snapshot.json");
            if (File.Exists(snapshotPath))
            {
                var snapshotJson = await File.ReadAllTextAsync(snapshotPath);
                var snapshotMetadata = Serializer.Deserialize<Metadata<Snapshot>>(snapshotJson);
                metadata.SnapshotExpires = snapshotMetadata.Signed.Expires.DateTime;
            }
            
            // Load targets metadata
            var targetsPath = Path.Combine(repositoryPath, "metadata", "targets.json");
            if (File.Exists(targetsPath))
            {
                var targetsJson = await File.ReadAllTextAsync(targetsPath);
                var targetsMetadata = Serializer.Deserialize<Metadata<Targets>>(targetsJson);
                metadata.TargetsExpires = targetsMetadata.Signed.Expires.DateTime;
                metadata.TotalTargets = targetsMetadata.Signed.TargetFiles.Count;
                
                // Extract hash algorithms from targets
                var hashAlgorithms = new HashSet<string>();
                foreach (var target in targetsMetadata.Signed.TargetFiles.Values)
                {
                    foreach (var hash in target.Hashes.Keys)
                    {
                        hashAlgorithms.Add(hash.ToLowerInvariant());
                    }
                }
                metadata.HashAlgorithms = hashAlgorithms.ToList();
                
                // Calculate delegation depth if delegations exist
                if (targetsMetadata.Signed.Delegations != null)
                {
                    metadata.DelegationDepth = CalculateDelegationDepth(targetsMetadata.Signed.Delegations);
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue - individual checks will handle missing metadata
            Console.WriteLine($"Warning: Could not fully load repository metadata: {ex.Message}");
        }
        
        return metadata;
    }

    private int CalculateDelegationDepth(Delegations delegations, int currentDepth = 1)
    {
        var maxDepth = currentDepth;
        
        // Note: This is a simplified calculation. In a full implementation,
        // you would recursively load delegated role metadata to calculate true depth.
        // For now, we'll return the current depth.
        
        return maxDepth;
    }

    private int CalculateSecurityScore(List<AuditCheck> checks, int criticalIssues, int warnings)
    {
        if (checks.Count == 0) return 0;
        
        // Base score from individual check scores
        var baseScore = (int)checks.Average(c => c.Score);
        
        // Apply penalties for issues
        var criticalPenalty = criticalIssues * 15; // 15 points per critical issue
        var warningPenalty = warnings * 5; // 5 points per warning
        
        var finalScore = Math.Max(0, baseScore - criticalPenalty - warningPenalty);
        return Math.Min(100, finalScore);
    }

    private int CalculateCategoryScore(List<AuditCheck> checks, string category)
    {
        var categoryChecks = checks.Where(c => GetCheckCategory(c.Name) == category).ToList();
        return categoryChecks.Count > 0 ? (int)categoryChecks.Average(c => c.Score) : 100;
    }

    private string GetCheckCategory(string checkName)
    {
        return checkName.ToLowerInvariant() switch
        {
            var name when name.Contains("key") => AuditCategories.KeyManagement,
            var name when name.Contains("threshold") => AuditCategories.Thresholds,
            var name when name.Contains("expir") => AuditCategories.MetadataExpiration,
            var name when name.Contains("hash") || name.Contains("crypto") => AuditCategories.Cryptography,
            var name when name.Contains("delegation") => AuditCategories.Delegations,
            var name when name.Contains("snapshot") => AuditCategories.ConsistentSnapshot,
            var name when name.Contains("structure") => AuditCategories.RepositoryStructure,
            _ => AuditCategories.SecurityBestPractices
        };
    }
}

/// <summary>
/// Interface for individual audit checks
/// </summary>
public interface IAuditCheck
{
    string Category { get; }
    Task<AuditCheckResult> ExecuteAsync(string repositoryPath, RepositoryMetadata metadata);
}

/// <summary>
/// Result of executing an audit check
/// </summary>
public class AuditCheckResult
{
    public List<AuditCheck> Checks { get; init; } = new();
    public List<SecurityIssue> Issues { get; init; } = new();
    public List<SecurityRecommendation> Recommendations { get; init; } = new();
}