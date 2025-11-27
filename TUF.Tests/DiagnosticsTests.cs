using System.Text;
using System.Text.Json;

using TUF;
using TUF.Diagnostics;
using TUF.Exceptions;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests for TUF diagnostic utilities and troubleshooting helpers.
/// Validates that diagnostic reports provide useful information for common TUF issues.
/// </summary>
public class DiagnosticsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly HttpClient _httpClient;

    public DiagnosticsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _httpClient = new HttpClient();
    }

    [Test]
    public async Task GenerateDiagnosticReport_SignatureVerificationException_ProvidesTroubleshootingSteps()
    {
        // Arrange
        var exception = new SignatureVerificationException(
            "Signature verification failed",
            keyId: "test-key-123",
            metadataType: "root"
        );

        // Act
        var report = TufDiagnostics.GenerateDiagnosticReport(exception);

        // Assert
        await Assert.That(report.ExceptionType).IsEqualTo("SignatureVerificationException");
        await Assert.That(report.ProblemArea).IsEqualTo("Cryptographic Signature Verification");
        await Assert.That(report.Message).IsEqualTo("Signature verification failed");
        await Assert.That(report.TroubleshootingSteps).IsNotEmpty();
        await Assert.That(report.SuggestedActions).IsNotEmpty();
        
        // Verify specific troubleshooting guidance
        await Assert.That(report.TroubleshootingSteps.Any(step => step.Contains("test-key-123"))).IsTrue();
        await Assert.That(report.SuggestedActions.Any(action => action.Contains("key management"))).IsTrue();
    }

    [Test]
    public async Task GenerateDiagnosticReport_InsufficientSignaturesException_ShowsThresholdDetails()
    {
        // Arrange
        var exception = new InsufficientSignaturesException(3, 1, "targets");

        // Act
        var report = TufDiagnostics.GenerateDiagnosticReport(exception);

        // Assert
        await Assert.That(report.ProblemArea).IsEqualTo("Signature Threshold Validation");
        await Assert.That(report.TroubleshootingSteps.Any(step => step.Contains("3") && step.Contains("1"))).IsTrue();
        await Assert.That(report.TroubleshootingSteps.Any(step => step.Contains("targets"))).IsTrue();
    }

    [Test]
    public async Task GenerateDiagnosticReport_RollbackAttackException_ShowsSecurityWarning()
    {
        // Arrange
        var exception = new RollbackAttackException("snapshot", 5, 3);

        // Act
        var report = TufDiagnostics.GenerateDiagnosticReport(exception);

        // Assert
        await Assert.That(report.ProblemArea).IsEqualTo("Rollback Attack Protection");
        await Assert.That(report.SuggestedActions.Any(action => action.Contains("DO NOT ignore"))).IsTrue();
        await Assert.That(report.SuggestedActions.Any(action => action.Contains("security incident"))).IsTrue();
    }

    [Test]
    public async Task GenerateDiagnosticReport_FormatAsString_ProducesReadableOutput()
    {
        // Arrange
        var exception = new TargetIntegrityException(
            "Hash mismatch",
            targetPath: "app.exe",
            expectedHash: "abc123",
            actualHash: "def456"
        );

        // Act
        var report = TufDiagnostics.GenerateDiagnosticReport(exception);
        var formatted = report.ToString();

        // Assert
        await Assert.That(formatted).Contains("TUF Diagnostic Report");
        await Assert.That(formatted).Contains("Target File Integrity");
        await Assert.That(formatted).Contains("Troubleshooting Steps:");
        await Assert.That(formatted).Contains("Suggested Actions:");
        await Assert.That(formatted).Contains("app.exe");
        await Assert.That(formatted).Contains("abc123");
        await Assert.That(formatted).Contains("def456");
    }

    [Test]
    public async Task ValidateConfigurationAsync_ValidConfig_ReturnsValid()
    {
        // Arrange
        var validRootMetadata = CreateValidRootMetadataJson();
        var config = new UpdaterConfig(validRootMetadata, new Uri("https://example.com/metadata"))
        {
            LocalMetadataDir = Path.Combine(_tempDir, "metadata"),
            LocalTargetsDir = Path.Combine(_tempDir, "targets"),
            Client = _httpClient
        };

        // Create directories
        Directory.CreateDirectory(config.LocalMetadataDir);
        Directory.CreateDirectory(config.LocalTargetsDir);

        // Act
        var result = await TufDiagnostics.ValidateConfigurationAsync(config);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Issues).IsEmpty();
    }

    [Test]
    public async Task ValidateConfigurationAsync_EmptyTrustedRoot_ReportsIssue()
    {
        // Arrange
        var config = new UpdaterConfig(Array.Empty<byte>(), new Uri("https://example.com/metadata"))
        {
            LocalMetadataDir = _tempDir,
            LocalTargetsDir = _tempDir,
            Client = _httpClient
        };

        // Act
        var result = await TufDiagnostics.ValidateConfigurationAsync(config);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Issues.Any(issue => issue.Contains("null or empty"))).IsTrue();
        await Assert.That(result.Recommendations.Any(rec => rec.Contains("trusted root metadata"))).IsTrue();
    }

    [Test]
    public async Task ValidateConfigurationAsync_HttpScheme_ReportsWarning()
    {
        // Arrange
        var validRootMetadata = CreateValidRootMetadataJson();
        var config = new UpdaterConfig(validRootMetadata, new Uri("http://example.com/metadata"))
        {
            LocalMetadataDir = _tempDir,
            LocalTargetsDir = _tempDir,
            Client = _httpClient
        };

        // Act
        var result = await TufDiagnostics.ValidateConfigurationAsync(config);

        // Assert
        await Assert.That(result.Warnings.Any(warning => warning.Contains("non-HTTPS"))).IsTrue();
        await Assert.That(result.Recommendations.Any(rec => rec.Contains("HTTPS"))).IsTrue();
    }

    [Test]
    public async Task ValidateConfigurationAsync_ExpiringSoonRoot_ReportsWarning()
    {
        // Arrange
        var expiringRootMetadata = CreateExpiringRootMetadataJson();
        var config = new UpdaterConfig(expiringRootMetadata, new Uri("https://example.com/metadata"))
        {
            LocalMetadataDir = _tempDir,
            LocalTargetsDir = _tempDir,
            Client = _httpClient
        };

        // Act
        var result = await TufDiagnostics.ValidateConfigurationAsync(config);

        // Assert
        await Assert.That(result.Warnings.Any(warning => warning.Contains("expires soon"))).IsTrue();
    }

    [Test]
    public async Task AnalyzeMetadataConsistency_ValidMetadata_ReturnsConsistent()
    {
        // Arrange
        var metadataFiles = new Dictionary<string, byte[]>
        {
            ["root"] = CreateValidRootMetadataJson(),
            ["timestamp"] = CreateValidTimestampMetadataJson(),
            ["snapshot"] = CreateValidSnapshotMetadataJson()
        };

        // Act
        var report = TufDiagnostics.AnalyzeMetadataConsistency(metadataFiles);

        // Assert
        await Assert.That(report.IsConsistent).IsTrue();
        await Assert.That(report.Issues).IsEmpty();
        await Assert.That(report.MetadataFiles).Contains("root");
        await Assert.That(report.MetadataFiles).Contains("timestamp");
        await Assert.That(report.MetadataFiles).Contains("snapshot");
    }

    [Test]
    public async Task AnalyzeMetadataConsistency_ExpiredMetadata_ReportsIssue()
    {
        // Arrange
        var metadataFiles = new Dictionary<string, byte[]>
        {
            ["root"] = CreateExpiredRootMetadataJson()
        };

        // Act
        var report = TufDiagnostics.AnalyzeMetadataConsistency(metadataFiles);

        // Assert
        await Assert.That(report.IsConsistent).IsFalse();
        await Assert.That(report.Issues.Any(issue => issue.Contains("expired"))).IsTrue();
    }

    [Test]
    public async Task AnalyzeMetadataConsistency_InvalidJson_ReportsIssue()
    {
        // Arrange
        var metadataFiles = new Dictionary<string, byte[]>
        {
            ["root"] = "invalid json"u8.ToArray()
        };

        // Act
        var report = TufDiagnostics.AnalyzeMetadataConsistency(metadataFiles);

        // Assert
        await Assert.That(report.IsConsistent).IsFalse();
        await Assert.That(report.Issues.Any(issue => issue.Contains("Failed to parse"))).IsTrue();
    }

    private byte[] CreateValidRootMetadataJson()
    {
        var futureExpiry = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var rootMetadata = new
        {
            signed = new
            {
                _type = "root",
                version = 1,
                expires = futureExpiry,
                keys = new { },
                roles = new { }
            },
            signatures = new object[]
            {
                new { keyid = "test", sig = "test" }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(rootMetadata);
    }

    private byte[] CreateExpiringRootMetadataJson()
    {
        var nearFutureExpiry = DateTime.UtcNow.AddDays(15).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var rootMetadata = new
        {
            signed = new
            {
                _type = "root",
                version = 1,
                expires = nearFutureExpiry,
                keys = new { },
                roles = new { }
            },
            signatures = new object[]
            {
                new { keyid = "test", sig = "test" }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(rootMetadata);
    }

    private byte[] CreateExpiredRootMetadataJson()
    {
        var pastExpiry = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var rootMetadata = new
        {
            signed = new
            {
                _type = "root",
                version = 1,
                expires = pastExpiry,
                keys = new { },
                roles = new { }
            },
            signatures = new object[]
            {
                new { keyid = "test", sig = "test" }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(rootMetadata);
    }

    private byte[] CreateValidTimestampMetadataJson()
    {
        var futureExpiry = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var timestampMetadata = new
        {
            signed = new
            {
                _type = "timestamp",
                version = 1,
                expires = futureExpiry,
                meta = new
                {
                    snapshot = new { version = 1 }
                }
            },
            signatures = new object[]
            {
                new { keyid = "test", sig = "test" }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(timestampMetadata);
    }

    private byte[] CreateValidSnapshotMetadataJson()
    {
        var futureExpiry = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var snapshotMetadata = new
        {
            signed = new
            {
                _type = "snapshot",
                version = 1,
                expires = futureExpiry,
                meta = new { }
            },
            signatures = new object[]
            {
                new { keyid = "test", sig = "test" }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(snapshotMetadata);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}