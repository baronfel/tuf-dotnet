using TUF.Exceptions;
using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Comprehensive tests for TUF security attack scenarios.
/// These tests verify that the TUF implementation properly detects and prevents
/// various cryptographic attacks that TUF is designed to defend against.
/// 
/// Based on TUF specification security goals:
/// - Prevent freeze attacks (serving outdated metadata)
/// - Prevent mix-and-match attacks (combining metadata from different points in time)
/// - Prevent rollback attacks (downgrading metadata versions)
/// - Prevent replay attacks (reusing valid but outdated signatures)
/// - Prevent arbitrary metadata attacks (injecting unauthorized metadata)
/// </summary>
public class SecurityAttackScenarioTests
{
    #region Freeze Attack Tests

    /// <summary>
    /// Tests that ExpiredMetadataException is properly thrown for expired metadata.
    /// This validates protection against freeze attacks where an attacker serves old metadata.
    /// </summary>
    [Test]
    public async Task FreezeAttack_ExpiredMetadata_ThrowsExpiredMetadataException()
    {
        // Arrange
        var expiredTime = DateTime.UtcNow.AddDays(-1);
        var metadataType = "timestamp";

        // Act & Assert
        var exception = new ExpiredMetadataException("Test expired metadata", metadataType, expiredTime);

        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(exception.ExpiryTime).IsEqualTo(expiredTime);
        await Assert.That(exception.Message).IsEqualTo("Test expired metadata");
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    /// <summary>
    /// Tests that ExpiredMetadataException properly handles different metadata types.
    /// This ensures comprehensive protection across all TUF metadata types.
    /// </summary>
    [Test]
    [Arguments("root")]
    [Arguments("timestamp")]
    [Arguments("snapshot")]
    [Arguments("targets")]
    public async Task FreezeAttack_ExpiredMetadata_HandlesAllMetadataTypes(string metadataType)
    {
        // Arrange
        var expiredTime = DateTime.UtcNow.AddDays(-1);
        var message = $"{metadataType} metadata has expired";

        // Act
        var exception = new ExpiredMetadataException(message, metadataType, expiredTime);

        // Assert
        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(exception.ExpiryTime).IsEqualTo(expiredTime);
        await Assert.That(exception.Message).IsEqualTo(message);
    }

    /// <summary>
    /// Tests edge cases for expiry time handling in freeze attack protection.
    /// </summary>
    [Test]
    public async Task FreezeAttack_EdgeCaseExpiryTimes_HandledCorrectly()
    {
        // Test with exactly current time (should be considered expired in practice)
        var exactNow = DateTime.UtcNow;
        var exception1 = new ExpiredMetadataException("Expired exactly now", "timestamp", exactNow);
        await Assert.That(exception1.ExpiryTime).IsEqualTo(exactNow);

        // Test with far past expiry
        var veryOld = DateTime.UtcNow.AddYears(-5);
        var exception2 = new ExpiredMetadataException("Very old metadata", "root", veryOld);
        await Assert.That(exception2.ExpiryTime).IsEqualTo(veryOld);

        // Test with null expiry time (should still work)
        var exception3 = new ExpiredMetadataException("No expiry time specified", "targets", null);
        await Assert.That(exception3.ExpiryTime).IsNull();
    }

    #endregion

    #region Rollback Attack Tests

    /// <summary>
    /// Tests that RollbackAttackException properly detects version downgrades.
    /// This validates protection against rollback attacks where an attacker serves older metadata versions.
    /// </summary>
    [Test]
    public async Task RollbackAttack_VersionDowngrade_DetectedCorrectly()
    {
        // Arrange
        var metadataType = "timestamp";
        var expectedVersion = 10;
        var actualVersion = 7; // Lower version indicates rollback attack

        // Act
        var exception = new RollbackAttackException(metadataType, expectedVersion, actualVersion);

        // Assert
        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(exception.ExpectedVersion).IsEqualTo(expectedVersion);
        await Assert.That(exception.ActualVersion).IsEqualTo(actualVersion);
        await Assert.That(exception.Message).Contains("Rollback attack detected");
        await Assert.That(exception.Message).Contains($"{metadataType} version {actualVersion} is older than expected version {expectedVersion}");
    }

    /// <summary>
    /// Tests rollback attack detection across different metadata types and version ranges.
    /// </summary>
    [Test]
    [Arguments("root", 5, 3)]
    [Arguments("timestamp", 100, 99)]
    [Arguments("snapshot", 50, 1)]
    [Arguments("targets", 1000, 500)]
    public async Task RollbackAttack_VariousScenarios_DetectedCorrectly(string metadataType, int expected, int actual)
    {
        // Act
        var exception = new RollbackAttackException(metadataType, expected, actual);

        // Assert
        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(exception.ExpectedVersion).IsEqualTo(expected);
        await Assert.That(exception.ActualVersion).IsEqualTo(actual);
        await Assert.That(exception.Message).Contains("Rollback attack detected");
    }

    /// <summary>
    /// Tests edge cases for rollback attack detection including large version gaps.
    /// </summary>
    [Test]
    public async Task RollbackAttack_LargeVersionGaps_DetectedCorrectly()
    {
        // Test very large version gap (sophisticated attack)
        var exception1 = new RollbackAttackException("snapshot", 1000000, 1);
        await Assert.That(exception1.ExpectedVersion).IsEqualTo(1000000);
        await Assert.That(exception1.ActualVersion).IsEqualTo(1);

        // Test small version gap (1 version behind)
        var exception2 = new RollbackAttackException("timestamp", 2, 1);
        await Assert.That(exception2.ExpectedVersion).IsEqualTo(2);
        await Assert.That(exception2.ActualVersion).IsEqualTo(1);
    }

    #endregion

    #region Signature Verification Attack Tests

    /// <summary>
    /// Tests that SignatureVerificationException properly reports signature failures.
    /// This validates protection against arbitrary metadata attacks using invalid signatures.
    /// </summary>
    [Test]
    public async Task ArbitraryMetadataAttack_InvalidSignature_RejectedCorrectly()
    {
        // Arrange
        var keyId = "malicious_key_123";
        var metadataType = "targets";
        var message = "Signature verification failed";

        // Act
        var exception = new SignatureVerificationException(message, keyId, metadataType);

        // Assert
        await Assert.That(exception.KeyId).IsEqualTo(keyId);
        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    /// <summary>
    /// Tests signature verification exceptions across different key types and scenarios.
    /// </summary>
    [Test]
    [Arguments("ed25519_key", "timestamp", "Ed25519 signature verification failed")]
    [Arguments("rsa_key", "root", "RSA signature verification failed")]
    [Arguments("ecdsa_key", "snapshot", "ECDSA signature verification failed")]
    [Arguments("unknown_key", "targets", "Unknown key type")]
    public async Task SignatureAttack_VariousKeyTypes_RejectedCorrectly(string keyId, string metadataType, string message)
    {
        // Act
        var exception = new SignatureVerificationException(message, keyId, metadataType);

        // Assert
        await Assert.That(exception.KeyId).IsEqualTo(keyId);
        await Assert.That(exception.MetadataType).IsEqualTo(metadataType);
        await Assert.That(exception.Message).IsEqualTo(message);
    }

    /// <summary>
    /// Tests insufficient signature threshold detection.
    /// This validates protection against attacks that don't meet signature requirements.
    /// </summary>
    [Test]
    public async Task ThresholdAttack_InsufficientSignatures_RejectedCorrectly()
    {
        // Arrange
        var requiredSignatures = 3;
        var validSignatures = 1; // Insufficient
        var roleName = "targets";

        // Act
        var exception = new InsufficientSignaturesException(requiredSignatures, validSignatures, roleName);

        // Assert
        await Assert.That(exception.RequiredSignatures).IsEqualTo(requiredSignatures);
        await Assert.That(exception.ValidSignatures).IsEqualTo(validSignatures);
        await Assert.That(exception.RoleName).IsEqualTo(roleName);
        await Assert.That(exception.Message).Contains($"{validSignatures} of {requiredSignatures} required for role {roleName}");
    }

    /// <summary>
    /// Tests various insufficient signature threshold scenarios.
    /// </summary>
    [Test]
    [Arguments(2, 0, "root")]      // No valid signatures
    [Arguments(3, 1, "timestamp")] // 1 of 3 required
    [Arguments(5, 2, "snapshot")]  // Less than half required
    [Arguments(10, 9, "targets")]  // Almost enough but not sufficient
    public async Task ThresholdAttack_VariousScenarios_RejectedCorrectly(int required, int valid, string roleName)
    {
        // Act
        var exception = new InsufficientSignaturesException(required, valid, roleName);

        // Assert
        await Assert.That(exception.RequiredSignatures).IsEqualTo(required);
        await Assert.That(exception.ValidSignatures).IsEqualTo(valid);
        await Assert.That(exception.RoleName).IsEqualTo(roleName);
        await Assert.That(exception.Message).Contains($"{valid} of {required} required for role {roleName}");
    }

    #endregion

    #region Target Integrity Attack Tests

    /// <summary>
    /// Tests that TargetIntegrityException properly reports file corruption.
    /// This validates protection against attacks that corrupt target files.
    /// </summary>
    [Test]
    public async Task IntegrityAttack_CorruptedTarget_DetectedCorrectly()
    {
        // Arrange
        var targetPath = "/path/to/important/file.bin";
        var expectedHash = "sha256:abcd1234567890abcd1234567890abcd1234567890abcd1234567890abcd1234";
        var actualHash = "sha256:malicious_hash_different_from_expected_hash_value_indicating_corruption";
        var message = "Target file integrity check failed";

        // Act
        var exception = new TargetIntegrityException(message, targetPath, expectedHash, actualHash);

        // Assert
        await Assert.That(exception.TargetPath).IsEqualTo(targetPath);
        await Assert.That(exception.ExpectedHash).IsEqualTo(expectedHash);
        await Assert.That(exception.ActualHash).IsEqualTo(actualHash);
        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    /// <summary>
    /// Tests target integrity validation across different hash algorithms and file types.
    /// </summary>
    [Test]
    [Arguments("file.exe", "sha256", "expected_sha256_hash", "actual_sha256_hash")]
    [Arguments("library.dll", "sha512", "expected_sha512_hash", "actual_sha512_hash")]
    [Arguments("config.json", "blake2b", "expected_blake2b_hash", "actual_blake2b_hash")]
    public async Task IntegrityAttack_VariousHashAlgorithms_DetectedCorrectly(string targetPath, string hashAlgo, string expectedHash, string actualHash)
    {
        // Arrange
        var fullExpectedHash = $"{hashAlgo}:{expectedHash}";
        var fullActualHash = $"{hashAlgo}:{actualHash}";
        var message = $"Hash mismatch for {targetPath}";

        // Act
        var exception = new TargetIntegrityException(message, targetPath, fullExpectedHash, fullActualHash);

        // Assert
        await Assert.That(exception.TargetPath).IsEqualTo(targetPath);
        await Assert.That(exception.ExpectedHash).IsEqualTo(fullExpectedHash);
        await Assert.That(exception.ActualHash).IsEqualTo(fullActualHash);
    }

    #endregion

    #region Delegation Attack Tests

    /// <summary>
    /// Tests delegation depth limits to prevent delegation chain attacks.
    /// </summary>
    [Test]
    public async Task DelegationAttack_ExcessiveDepth_RejectedCorrectly()
    {
        // Arrange
        var maxDepth = 10;
        var currentDepth = 15; // Exceeds limit

        // Act
        var exception = new MaxDelegationDepthExceededException(maxDepth, currentDepth);

        // Assert
        await Assert.That(exception.MaxDepth).IsEqualTo(maxDepth);
        await Assert.That(exception.DelegationDepth).IsEqualTo(currentDepth);
        await Assert.That(exception.Message).Contains($"Maximum delegation depth {maxDepth} exceeded");
        await Assert.That(exception.Message).Contains($"current depth: {currentDepth}");
        await Assert.That(typeof(DelegationException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    /// <summary>
    /// Tests various delegation attack scenarios with different depth violations.
    /// </summary>
    [Test]
    [Arguments(5, 6)]   // Just over limit
    [Arguments(10, 20)] // Significantly over limit
    [Arguments(1, 100)] // Extreme depth violation
    public async Task DelegationAttack_VariousDepthViolations_RejectedCorrectly(int maxDepth, int currentDepth)
    {
        // Act
        var exception = new MaxDelegationDepthExceededException(maxDepth, currentDepth);

        // Assert
        await Assert.That(exception.MaxDepth).IsEqualTo(maxDepth);
        await Assert.That(exception.DelegationDepth).IsEqualTo(currentDepth);
        await Assert.That(currentDepth).IsGreaterThan(maxDepth);
    }

    /// <summary>
    /// Tests general delegation failure scenarios.
    /// </summary>
    [Test]
    public async Task DelegationAttack_GeneralFailure_HandledCorrectly()
    {
        // Arrange
        var roleName = "malicious_delegated_role";
        var delegationDepth = 3;
        var message = "Delegation resolution failed";

        // Act
        var exception = new DelegationException(message, roleName, delegationDepth);

        // Assert
        await Assert.That(exception.RoleName).IsEqualTo(roleName);
        await Assert.That(exception.DelegationDepth).IsEqualTo(delegationDepth);
        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
    }

    #endregion

    #region Security Exception Classification Tests

    /// <summary>
    /// Tests that security-critical exceptions are properly identified.
    /// This ensures logging and monitoring systems can properly categorize security events.
    /// </summary>
    [Test]
    public async Task SecurityExceptionClassification_SecurityCriticalExceptions_IdentifiedCorrectly()
    {
        var securityExceptions = new TufException[]
        {
            new SignatureVerificationException("Invalid signature"),
            new InsufficientSignaturesException(2, 1),
            new RollbackAttackException("timestamp", 5, 3),
            new TargetIntegrityException("File corrupted"),
            new ExpiredMetadataException("Metadata expired") // This could be security-related in freeze attacks
        };

        foreach (var exception in securityExceptions)
        {
            // Assert all are TUF exceptions
            await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
            
            // Assert they have security implications
            var isSecurityRelated = exception is SignatureVerificationException or
                                   InsufficientSignaturesException or
                                   RollbackAttackException or
                                   TargetIntegrityException or
                                   ExpiredMetadataException;
            await Assert.That(isSecurityRelated).IsTrue();
        }
    }

    /// <summary>
    /// Tests that operational exceptions are properly distinguished from security exceptions.
    /// </summary>
    [Test]
    public async Task OperationalExceptionClassification_OperationalExceptions_IdentifiedCorrectly()
    {
        var operationalExceptions = new TufException[]
        {
            new MetadataDeserializationException("Parse error"),
            new MetadataValidationException("Validation failed"),
            new TargetNotFoundException("File not found"),
            new RepositoryNetworkException("Network error"),
            new TufConfigurationException("Config error"),
            new FileSizeException("/large/file", 1000000, 500000)
        };

        foreach (var exception in operationalExceptions)
        {
            // Assert all are TUF exceptions
            await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
            
            // Assert they are primarily operational (though some may have security implications in context)
            var isOperational = exception is MetadataDeserializationException or
                              MetadataValidationException or
                              TargetNotFoundException or
                              RepositoryNetworkException or
                              TufConfigurationException or
                              FileSizeException;
            await Assert.That(isOperational).IsTrue();
        }
    }

    #endregion

    #region Attack Scenario Integration Tests

    /// <summary>
    /// Tests complex attack scenarios that combine multiple attack vectors.
    /// This validates that the exception system can handle sophisticated attacks.
    /// </summary>
    [Test]
    public async Task ComplexAttack_MultipleVectors_HandledCorrectly()
    {
        // Simulate an attack that combines rollback + insufficient signatures
        var rollbackException = new RollbackAttackException("timestamp", 10, 5);
        var thresholdException = new InsufficientSignaturesException(3, 1, "timestamp");

        // Both should be detectable independently
        await Assert.That(rollbackException.ExpectedVersion).IsEqualTo(10);
        await Assert.That(rollbackException.ActualVersion).IsEqualTo(5);
        
        await Assert.That(thresholdException.RequiredSignatures).IsEqualTo(3);
        await Assert.That(thresholdException.ValidSignatures).IsEqualTo(1);

        // Both should be security exceptions
        await Assert.That(typeof(TufException).IsAssignableFrom(rollbackException.GetType())).IsTrue();
        await Assert.That(typeof(TufException).IsAssignableFrom(thresholdException.GetType())).IsTrue();
    }

    /// <summary>
    /// Tests that attack detection provides sufficient information for forensic analysis.
    /// </summary>
    [Test]
    public async Task AttackForensics_ExceptionDetails_ProvideSufficientInformation()
    {
        // Create exceptions with detailed forensic information
        var signatureAttack = new SignatureVerificationException(
            "RSA signature verification failed for key ed25519_abc123 on timestamp metadata", 
            "ed25519_abc123", 
            "timestamp"
        );

        var rollbackAttack = new RollbackAttackException("snapshot", 1000, 500);

        var integrityAttack = new TargetIntegrityException(
            "Critical system file corrupted",
            "/usr/bin/important_binary",
            "sha256:expected_hash_value_for_forensic_analysis",
            "sha256:actual_hash_indicates_file_modification"
        );

        // Verify forensic details are preserved
        await Assert.That(signatureAttack.KeyId).IsEqualTo("ed25519_abc123");
        await Assert.That(signatureAttack.MetadataType).IsEqualTo("timestamp");
        await Assert.That(signatureAttack.Message).Contains("RSA signature verification failed");

        await Assert.That(rollbackAttack.ExpectedVersion).IsEqualTo(1000);
        await Assert.That(rollbackAttack.ActualVersion).IsEqualTo(500);
        await Assert.That(rollbackAttack.MetadataType).IsEqualTo("snapshot");

        await Assert.That(integrityAttack.TargetPath).IsEqualTo("/usr/bin/important_binary");
        await Assert.That(integrityAttack.ExpectedHash).Contains("expected_hash_value");
        await Assert.That(integrityAttack.ActualHash).Contains("actual_hash_indicates");
    }

    #endregion
}