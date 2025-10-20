using System.Text.Json.Serialization;

namespace TufSecurityAuditor;

/// <summary>
/// Configuration options for the TUF repository audit
/// </summary>
public class AuditOptions
{
    public required string RepositoryPath { get; init; }
    public OutputFormat OutputFormat { get; init; } = OutputFormat.Console;
    public string? OutputFile { get; init; }
    public bool Detailed { get; init; }
    public bool IncludeRecommendations { get; init; } = true;
    public List<string> Focus { get; init; } = new();
    public bool CiMode { get; init; }
    public bool FailOnWarnings { get; init; }
    public int MinimumScore { get; init; } = 70;
}

/// <summary>
/// Complete result of a TUF repository security audit
/// </summary>
public class AuditResult
{
    public required string RepositoryPath { get; init; }
    public DateTime AuditDate { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Overall security score (0-100)
    /// </summary>
    public int SecurityScore { get; init; }
    
    /// <summary>
    /// Critical security issues that should be addressed immediately
    /// </summary>
    public List<SecurityIssue> CriticalIssues { get; init; } = new();
    
    /// <summary>
    /// Warnings about potential security concerns
    /// </summary>
    public List<SecurityIssue> Warnings { get; init; } = new();
    
    /// <summary>
    /// Actionable recommendations for improving security
    /// </summary>
    public List<SecurityRecommendation> Recommendations { get; init; } = new();
    
    /// <summary>
    /// Whether the repository structure is valid
    /// </summary>
    public bool RepositoryStructureValid { get; init; }
    
    /// <summary>
    /// Whether metadata is internally consistent
    /// </summary>
    public bool MetadataConsistent { get; init; }
    
    /// <summary>
    /// Key management security score (0-100)
    /// </summary>
    public int KeyManagementScore { get; init; }
    
    /// <summary>
    /// Cryptographic settings security score (0-100)
    /// </summary>
    public int CryptoSettingsScore { get; init; }
    
    /// <summary>
    /// Detailed audit results by category
    /// </summary>
    public Dictionary<string, List<AuditCheck>> DetailedResults { get; init; } = new();
}

/// <summary>
/// A specific security issue found during the audit
/// </summary>
public class SecurityIssue
{
    public required string Category { get; init; }
    public required string Description { get; init; }
    public string? Details { get; init; }
    public SecuritySeverity Severity { get; init; }
    public string? Location { get; init; }
    public string? Remediation { get; init; }
}

/// <summary>
/// A security recommendation for improving the repository
/// </summary>
public class SecurityRecommendation
{
    public required string Category { get; init; }
    public required string Description { get; init; }
    public string? Details { get; init; }
    public RecommendationPriority Priority { get; init; }
    public string? Implementation { get; init; }
}

/// <summary>
/// Individual audit check result
/// </summary>
public class AuditCheck
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public AuditStatus Status { get; init; }
    public string? Details { get; init; }
    public int Score { get; init; } // 0-100
    public string? Remediation { get; init; }
}

/// <summary>
/// Severity levels for security issues
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SecuritySeverity
{
    Critical,
    High,
    Medium,
    Low,
    Info
}

/// <summary>
/// Priority levels for recommendations
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationPriority
{
    Urgent,
    High,
    Medium,
    Low
}

/// <summary>
/// Status of individual audit checks
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuditStatus
{
    Pass,
    Fail,
    Warning,
    NotApplicable,
    Error
}

/// <summary>
/// Categories for organizing audit checks
/// </summary>
public static class AuditCategories
{
    public const string RepositoryStructure = "repository-structure";
    public const string KeyManagement = "key-management";
    public const string Thresholds = "thresholds";
    public const string MetadataExpiration = "metadata-expiration";
    public const string Cryptography = "cryptography";
    public const string Delegations = "delegations";
    public const string ConsistentSnapshot = "consistent-snapshot";
    public const string SecurityBestPractices = "security-best-practices";
}

/// <summary>
/// TUF repository metadata summary for auditing
/// </summary>
public class RepositoryMetadata
{
    public string? RootVersion { get; set; }
    public DateTime? RootExpires { get; set; }
    public int RootThreshold { get; set; }
    public int RootKeyCount { get; set; }
    
    public DateTime? TimestampExpires { get; set; }
    public int TimestampThreshold { get; set; }
    
    public DateTime? SnapshotExpires { get; set; }
    public int SnapshotThreshold { get; set; }
    
    public DateTime? TargetsExpires { get; set; }
    public int TargetsThreshold { get; set; }
    
    public bool ConsistentSnapshot { get; set; }
    public List<string> HashAlgorithms { get; set; } = new();
    public List<string> KeyTypes { get; set; } = new();
    public List<string> SignatureSchemes { get; set; } = new();
    
    public int DelegationDepth { get; set; }
    public int TotalTargets { get; set; }
    
    public Dictionary<string, DateTime> LastKeyRotation { get; set; } = new();
}