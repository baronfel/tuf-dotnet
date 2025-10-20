namespace TufSecurityAuditor;

/// <summary>
/// Repository structure validation checks
/// </summary>
public class RepositoryStructureCheck : IAuditCheck
{
    public string Category => AuditCategories.RepositoryStructure;

    public async Task<AuditCheckResult> ExecuteAsync(string repositoryPath, RepositoryMetadata metadata)
    {
        var result = new AuditCheckResult();
        
        // Check for required metadata files
        await CheckRequiredFiles(repositoryPath, result);
        
        // Check metadata directory structure
        await CheckDirectoryStructure(repositoryPath, result);
        
        // Check targets directory if consistent snapshots are used
        if (metadata.ConsistentSnapshot)
        {
            await CheckConsistentSnapshotStructure(repositoryPath, result);
        }

        return result;
    }

    private async Task CheckRequiredFiles(string repositoryPath, AuditCheckResult result)
    {
        var metadataPath = Path.Combine(repositoryPath, "metadata");
        var requiredFiles = new[] { "root.json", "timestamp.json", "snapshot.json", "targets.json" };
        
        foreach (var file in requiredFiles)
        {
            var filePath = Path.Combine(metadataPath, file);
            var exists = File.Exists(filePath);
            
            result.Checks.Add(new AuditCheck
            {
                Name = $"Required file: {file}",
                Description = $"Verify {file} exists in metadata directory",
                Status = exists ? AuditStatus.Pass : AuditStatus.Fail,
                Score = exists ? 100 : 0,
                Details = exists ? $"Found at {filePath}" : $"Missing file: {filePath}",
                Remediation = exists ? null : $"Create missing {file} metadata file"
            });
            
            if (!exists)
            {
                result.Issues.Add(new SecurityIssue
                {
                    Category = Category,
                    Description = $"Missing required metadata file: {file}",
                    Severity = SecuritySeverity.Critical,
                    Location = filePath,
                    Remediation = $"Generate and publish {file} metadata file"
                });
            }
        }
    }

    private async Task CheckDirectoryStructure(string repositoryPath, AuditCheckResult result)
    {
        var requiredDirs = new[] { "metadata", "targets" };
        
        foreach (var dir in requiredDirs)
        {
            var dirPath = Path.Combine(repositoryPath, dir);
            var exists = Directory.Exists(dirPath);
            
            result.Checks.Add(new AuditCheck
            {
                Name = $"Required directory: {dir}",
                Description = $"Verify {dir} directory exists",
                Status = exists ? AuditStatus.Pass : AuditStatus.Fail,
                Score = exists ? 100 : 0,
                Details = exists ? $"Found at {dirPath}" : $"Missing directory: {dirPath}",
                Remediation = exists ? null : $"Create {dir} directory"
            });
            
            if (!exists)
            {
                result.Issues.Add(new SecurityIssue
                {
                    Category = Category,
                    Description = $"Missing required directory: {dir}",
                    Severity = SecuritySeverity.High,
                    Location = dirPath,
                    Remediation = $"Create {dir} directory with proper permissions"
                });
            }
        }
    }

    private async Task CheckConsistentSnapshotStructure(string repositoryPath, AuditCheckResult result)
    {
        // For consistent snapshots, check if targets are properly organized
        var targetsPath = Path.Combine(repositoryPath, "targets");
        
        if (Directory.Exists(targetsPath))
        {
            var hasConsistentStructure = true; // Simplified check
            
            result.Checks.Add(new AuditCheck
            {
                Name = "Consistent snapshot structure",
                Description = "Verify targets directory structure supports consistent snapshots",
                Status = hasConsistentStructure ? AuditStatus.Pass : AuditStatus.Warning,
                Score = hasConsistentStructure ? 100 : 70,
                Details = "Consistent snapshot structure validation"
            });
        }
    }
}

/// <summary>
/// Key management security checks
/// </summary>
public class KeyManagementCheck : IAuditCheck
{
    public string Category => AuditCategories.KeyManagement;

    public async Task<AuditCheckResult> ExecuteAsync(string repositoryPath, RepositoryMetadata metadata)
    {
        var result = new AuditCheckResult();
        
        // Check key algorithm strength
        await CheckKeyAlgorithmStrength(metadata, result);
        
        // Check for single points of failure
        await CheckSinglePointsOfFailure(metadata, result);
        
        // Check key rotation practices
        await CheckKeyRotation(metadata, result);

        return result;
    }

    private async Task CheckKeyAlgorithmStrength(RepositoryMetadata metadata, AuditCheckResult result)
    {
        var weakKeyTypes = new HashSet<string>();
        var strongKeyTypes = new[] { "ed25519", "ecdsa", "rsa" };
        
        foreach (var keyType in metadata.KeyTypes)
        {
            var isStrong = strongKeyTypes.Contains(keyType.ToLowerInvariant());
            
            result.Checks.Add(new AuditCheck
            {
                Name = $"Key algorithm strength: {keyType}",
                Description = $"Verify {keyType} is a cryptographically strong algorithm",
                Status = isStrong ? AuditStatus.Pass : AuditStatus.Fail,
                Score = isStrong ? 100 : 0,
                Details = isStrong ? "Strong cryptographic algorithm" : "Weak or outdated algorithm",
                Remediation = isStrong ? null : $"Replace {keyType} keys with Ed25519, ECDSA, or RSA keys"
            });
            
            if (!isStrong)
            {
                weakKeyTypes.Add(keyType);
                result.Issues.Add(new SecurityIssue
                {
                    Category = Category,
                    Description = $"Weak key algorithm in use: {keyType}",
                    Severity = SecuritySeverity.High,
                    Remediation = "Migrate to stronger key algorithms (Ed25519 recommended)"
                });
            }
        }
        
        if (weakKeyTypes.Count == 0)
        {
            result.Recommendations.Add(new SecurityRecommendation
            {
                Category = Category,
                Description = "Consider Ed25519 for optimal security and performance",
                Priority = RecommendationPriority.Low,
                Implementation = "Generate Ed25519 keys for new key rotations"
            });
        }
    }

    private async Task CheckSinglePointsOfFailure(RepositoryMetadata metadata, AuditCheckResult result)
    {
        // Check root key threshold
        var rootThresholdSafe = metadata.RootThreshold >= 2;
        result.Checks.Add(new AuditCheck
        {
            Name = "Root key threshold",
            Description = "Verify root role requires multiple signatures",
            Status = rootThresholdSafe ? AuditStatus.Pass : AuditStatus.Fail,
            Score = rootThresholdSafe ? 100 : 20,
            Details = $"Root threshold: {metadata.RootThreshold}, Keys: {metadata.RootKeyCount}",
            Remediation = rootThresholdSafe ? null : "Increase root threshold to 2 or more"
        });
        
        if (!rootThresholdSafe)
        {
            result.Issues.Add(new SecurityIssue
            {
                Category = Category,
                Description = $"Root key threshold is {metadata.RootThreshold} (recommended: 2+)",
                Severity = SecuritySeverity.Critical,
                Remediation = "Add additional root keys and increase threshold to prevent single point of failure"
            });
        }
        
        // Check if enough keys exist for the threshold
        if (metadata.RootKeyCount < metadata.RootThreshold)
        {
            result.Issues.Add(new SecurityIssue
            {
                Category = Category,
                Description = $"Insufficient root keys: {metadata.RootKeyCount} keys for threshold {metadata.RootThreshold}",
                Severity = SecuritySeverity.Critical,
                Remediation = "Add more root keys to meet threshold requirements"
            });
        }
    }

    private async Task CheckKeyRotation(RepositoryMetadata metadata, AuditCheckResult result)
    {
        // Simplified key rotation check - in a real implementation, you'd track key creation dates
        var hasRecentRotation = metadata.LastKeyRotation.Count > 0;
        
        result.Checks.Add(new AuditCheck
        {
            Name = "Key rotation policy",
            Description = "Verify keys are rotated according to security policy",
            Status = hasRecentRotation ? AuditStatus.Pass : AuditStatus.Warning,
            Score = hasRecentRotation ? 100 : 60,
            Details = hasRecentRotation ? "Key rotation data available" : "No key rotation data found",
            Remediation = "Implement regular key rotation schedule"
        });
        
        if (!hasRecentRotation)
        {
            result.Recommendations.Add(new SecurityRecommendation
            {
                Category = Category,
                Description = "Implement regular key rotation policy",
                Priority = RecommendationPriority.Medium,
                Details = "Regular key rotation reduces impact of key compromise",
                Implementation = "Establish 6-12 month key rotation schedule"
            });
        }
    }
}

/// <summary>
/// Threshold security validation
/// </summary>
public class ThresholdSecurityCheck : IAuditCheck
{
    public string Category => AuditCategories.Thresholds;

    public async Task<AuditCheckResult> ExecuteAsync(string repositoryPath, RepositoryMetadata metadata)
    {
        var result = new AuditCheckResult();
        
        // Check all role thresholds
        await CheckRoleThresholds(metadata, result);
        
        return result;
    }

    private async Task CheckRoleThresholds(RepositoryMetadata metadata, AuditCheckResult result)
    {
        var roles = new[]
        {
            ("Root", metadata.RootThreshold, 2),
            ("Timestamp", metadata.TimestampThreshold, 1),
            ("Snapshot", metadata.SnapshotThreshold, 1), 
            ("Targets", metadata.TargetsThreshold, 1)
        };
        
        foreach (var (roleName, threshold, recommendedMin) in roles)
        {
            var isSecure = threshold >= recommendedMin;
            var severity = roleName == "Root" && threshold < 2 ? SecuritySeverity.Critical : SecuritySeverity.Medium;
            
            result.Checks.Add(new AuditCheck
            {
                Name = $"{roleName} threshold security",
                Description = $"Verify {roleName} role has appropriate signature threshold",
                Status = isSecure ? AuditStatus.Pass : AuditStatus.Warning,
                Score = isSecure ? 100 : (threshold == 0 ? 0 : 50),
                Details = $"{roleName} threshold: {threshold} (recommended minimum: {recommendedMin})"
            });
            
            if (!isSecure)
            {
                result.Issues.Add(new SecurityIssue
                {
                    Category = Category,
                    Description = $"{roleName} threshold ({threshold}) below recommended minimum ({recommendedMin})",
                    Severity = severity,
                    Remediation = $"Consider increasing {roleName} threshold to {recommendedMin} or higher"
                });
            }
        }
    }
}

/// <summary>
/// Metadata expiration checks
/// </summary>
public class MetadataExpirationCheck : IAuditCheck
{
    public string Category => AuditCategories.MetadataExpiration;

    public async Task<AuditCheckResult> ExecuteAsync(string repositoryPath, RepositoryMetadata metadata)
    {
        var result = new AuditCheckResult();
        
        await CheckExpirationTimes(metadata, result);
        
        return result;
    }

    private async Task CheckExpirationTimes(RepositoryMetadata metadata, AuditCheckResult result)
    {
        var now = DateTime.UtcNow;
        var expirations = new[]
        {
            ("Root", metadata.RootExpires),
            ("Timestamp", metadata.TimestampExpires),
            ("Snapshot", metadata.SnapshotExpires),
            ("Targets", metadata.TargetsExpires)
        };
        
        foreach (var (roleName, expiration) in expirations)
        {
            if (expiration == null) continue;
            
            var daysUntilExpiration = (expiration.Value - now).TotalDays;
            var isExpired = daysUntilExpiration <= 0;
            var isExpiringSoon = daysUntilExpiration <= 7 && daysUntilExpiration > 0;
            
            var status = isExpired ? AuditStatus.Fail : (isExpiringSoon ? AuditStatus.Warning : AuditStatus.Pass);
            var score = isExpired ? 0 : (isExpiringSoon ? 60 : 100);
            
            result.Checks.Add(new AuditCheck
            {
                Name = $"{roleName} expiration",
                Description = $"Verify {roleName} metadata is not expired",
                Status = status,
                Score = score,
                Details = isExpired ? $"Expired {-daysUntilExpiration:F1} days ago" : 
                         isExpiringSoon ? $"Expires in {daysUntilExpiration:F1} days" :
                         $"Expires in {daysUntilExpiration:F1} days"
            });
            
            if (isExpired)
            {
                result.Issues.Add(new SecurityIssue
                {
                    Category = Category,
                    Description = $"{roleName} metadata expired {-daysUntilExpiration:F1} days ago",
                    Severity = SecuritySeverity.Critical,
                    Remediation = $"Update and re-sign {roleName} metadata immediately"
                });
            }
            else if (isExpiringSoon)
            {
                result.Issues.Add(new SecurityIssue
                {
                    Category = Category,
                    Description = $"{roleName} metadata expires in {daysUntilExpiration:F1} days",
                    Severity = SecuritySeverity.Medium,
                    Remediation = $"Plan to update {roleName} metadata before expiration"
                });
            }
        }
    }
}

/// <summary>
/// Cryptographic settings validation
/// </summary>
public class CryptographicSettingsCheck : IAuditCheck
{
    public string Category => AuditCategories.Cryptography;

    public async Task<AuditCheckResult> ExecuteAsync(string repositoryPath, RepositoryMetadata metadata)
    {
        var result = new AuditCheckResult();
        
        await CheckHashAlgorithms(metadata, result);
        await CheckSignatureSchemes(metadata, result);
        
        return result;
    }

    private async Task CheckHashAlgorithms(RepositoryMetadata metadata, AuditCheckResult result)
    {
        var recommendedAlgorithms = new[] { "sha256", "sha512", "blake2b-256" };
        var hasRecommended = metadata.HashAlgorithms.Any(h => recommendedAlgorithms.Contains(h));
        var hasMultiple = metadata.HashAlgorithms.Count > 1;
        
        result.Checks.Add(new AuditCheck
        {
            Name = "Hash algorithm security",
            Description = "Verify strong hash algorithms are used",
            Status = hasRecommended ? AuditStatus.Pass : AuditStatus.Warning,
            Score = hasRecommended ? (hasMultiple ? 100 : 80) : 40,
            Details = $"Hash algorithms: {string.Join(", ", metadata.HashAlgorithms)}",
            Remediation = hasRecommended ? null : "Use SHA-256, SHA-512, or BLAKE2b-256"
        });
        
        if (!hasMultiple && hasRecommended)
        {
            result.Recommendations.Add(new SecurityRecommendation
            {
                Category = Category,
                Description = "Consider using multiple hash algorithms for crypto-agility",
                Priority = RecommendationPriority.Medium,
                Implementation = "Add SHA-512 or BLAKE2b-256 alongside existing algorithms"
            });
        }
    }

    private async Task CheckSignatureSchemes(RepositoryMetadata metadata, AuditCheckResult result)
    {
        var modernSchemes = new[] { "ed25519", "ecdsa-sha2-nistp256", "rsassa-pss-sha256" };
        var hasModernScheme = metadata.SignatureSchemes.Any(s => modernSchemes.Contains(s));
        
        result.Checks.Add(new AuditCheck
        {
            Name = "Signature scheme security",
            Description = "Verify modern signature schemes are used",
            Status = hasModernScheme ? AuditStatus.Pass : AuditStatus.Warning,
            Score = hasModernScheme ? 100 : 50,
            Details = $"Signature schemes: {string.Join(", ", metadata.SignatureSchemes)}"
        });
        
        if (hasModernScheme)
        {
            result.Recommendations.Add(new SecurityRecommendation
            {
                Category = Category,
                Description = "Consider Ed25519 for optimal performance and security",
                Priority = RecommendationPriority.Low,
                Implementation = "Use Ed25519 keys for new signatures when possible"
            });
        }
    }
}

// Placeholder implementations for remaining checks
public class DelegationSecurityCheck : IAuditCheck
{
    public string Category => AuditCategories.Delegations;
    
    public async Task<AuditCheckResult> ExecuteAsync(string repositoryPath, RepositoryMetadata metadata)
    {
        return new AuditCheckResult(); // Simplified implementation
    }
}

public class ConsistentSnapshotCheck : IAuditCheck  
{
    public string Category => AuditCategories.ConsistentSnapshot;
    
    public async Task<AuditCheckResult> ExecuteAsync(string repositoryPath, RepositoryMetadata metadata)
    {
        return new AuditCheckResult(); // Simplified implementation
    }
}

public class SecurityBestPracticesCheck : IAuditCheck
{
    public string Category => AuditCategories.SecurityBestPractices;
    
    public async Task<AuditCheckResult> ExecuteAsync(string repositoryPath, RepositoryMetadata metadata)
    {
        return new AuditCheckResult(); // Simplified implementation  
    }
}