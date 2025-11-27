using System.Text.Json;

using TUF;
using TUF.Diagnostics;
using TUF.Exceptions;

namespace DiagnosticsDemo;

/// <summary>
/// Demonstrates TUF diagnostic utilities for troubleshooting common issues.
/// 
/// This example shows how to:
/// 1. Use TufDiagnostics.ValidateConfigurationAsync() to check configuration before use
/// 2. Generate diagnostic reports from TUF exceptions 
/// 3. Analyze metadata consistency across multiple files
/// 4. Get actionable troubleshooting guidance for TUF issues
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🔧 TUF Diagnostics Demo - Troubleshooting Utilities");
        Console.WriteLine("====================================================");

        // Demo 1: Configuration validation
        Console.WriteLine("\n📋 Demo 1: Configuration Validation");
        await DemoConfigurationValidation();

        // Demo 2: Exception diagnostics  
        Console.WriteLine("\n🚨 Demo 2: Exception Diagnostic Reports");
        DemoExceptionDiagnostics();

        // Demo 3: Metadata consistency analysis
        Console.WriteLine("\n📊 Demo 3: Metadata Consistency Analysis");
        DemoMetadataConsistencyAnalysis();

        Console.WriteLine("\n✅ Diagnostics demo completed!");
        Console.WriteLine("\n💡 These utilities can help you:");
        Console.WriteLine("   • Catch configuration issues before runtime");
        Console.WriteLine("   • Get specific troubleshooting steps for TUF errors");
        Console.WriteLine("   • Validate metadata consistency in repositories");
        Console.WriteLine("   • Improve developer experience and support workflows");
    }

    static async Task DemoConfigurationValidation()
    {
        Console.WriteLine("Testing various configuration scenarios...\n");

        // Test 1: Valid configuration
        try
        {
            var validConfig = CreateSampleValidConfig();
            var result = await TufDiagnostics.ValidateConfigurationAsync(validConfig);

            Console.WriteLine("✅ Valid configuration:");
            Console.WriteLine($"   Is Valid: {result.IsValid}");
            Console.WriteLine($"   Issues: {result.Issues.Count}");
            Console.WriteLine($"   Warnings: {result.Warnings.Count}");

            if (result.Warnings.Count > 0)
            {
                Console.WriteLine("   Warnings found:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"     • {warning}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Configuration validation failed: {ex.Message}");
        }

        Console.WriteLine();

        // Test 2: Configuration with issues
        try
        {
            var problematicConfig = CreateProblematicConfig();
            var result = await TufDiagnostics.ValidateConfigurationAsync(problematicConfig);

            Console.WriteLine("⚠️  Problematic configuration:");
            Console.WriteLine($"   Is Valid: {result.IsValid}");
            Console.WriteLine($"   Issues: {result.Issues.Count}");
            Console.WriteLine($"   Warnings: {result.Warnings.Count}");

            if (result.Issues.Count > 0)
            {
                Console.WriteLine("   Issues found:");
                foreach (var issue in result.Issues)
                {
                    Console.WriteLine($"     ❌ {issue}");
                }
            }

            if (result.Recommendations.Count > 0)
            {
                Console.WriteLine("   Recommendations:");
                foreach (var rec in result.Recommendations)
                {
                    Console.WriteLine($"     💡 {rec}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Configuration validation failed: {ex.Message}");
        }
    }

    static void DemoExceptionDiagnostics()
    {
        // Demo different types of TUF exceptions and their diagnostic reports

        // Example 1: Signature verification failure
        Console.WriteLine("🔐 Example 1: Signature Verification Exception");
        var sigException = new SignatureVerificationException(
            "Ed25519 signature verification failed for root metadata",
            keyId: "ed25519-root-key-2024",
            metadataType: "root"
        );

        var sigReport = TufDiagnostics.GenerateDiagnosticReport(sigException, new Dictionary<string, object>
        {
            ["Repository"] = "https://example.com/metadata",
            ["ClientVersion"] = "TUF.NET v1.0.0"
        });

        Console.WriteLine(sigReport.ToString());

        // Example 2: Rollback attack detection
        Console.WriteLine("🛡️  Example 2: Rollback Attack Exception");
        var rollbackException = new RollbackAttackException("timestamp", expectedVersion: 145, actualVersion: 142);
        var rollbackReport = TufDiagnostics.GenerateDiagnosticReport(rollbackException);
        
        Console.WriteLine("Key troubleshooting points:");
        foreach (var step in rollbackReport.TroubleshootingSteps.Take(3))
        {
            Console.WriteLine($"   • {step}");
        }
        Console.WriteLine();

        // Example 3: Target integrity failure
        Console.WriteLine("🎯 Example 3: Target Integrity Exception");
        var integrityException = new TargetIntegrityException(
            "SHA-256 hash mismatch for downloaded target",
            targetPath: "critical-app-v2.1.0.exe",
            expectedHash: "sha256:a1b2c3d4e5f6...",
            actualHash: "sha256:f6e5d4c3b2a1..."
        );

        var integrityReport = TufDiagnostics.GenerateDiagnosticReport(integrityException);
        Console.WriteLine($"Problem Area: {integrityReport.ProblemArea}");
        Console.WriteLine($"Primary Action: {integrityReport.SuggestedActions.First()}");
        Console.WriteLine();
    }

    static void DemoMetadataConsistencyAnalysis()
    {
        Console.WriteLine("Analyzing a set of TUF metadata files...\n");

        // Create sample metadata for analysis
        var metadataFiles = new Dictionary<string, byte[]>
        {
            ["root"] = CreateSampleRootMetadata(),
            ["timestamp"] = CreateSampleTimestampMetadata(),
            ["snapshot"] = CreateSampleSnapshotMetadata(),
            ["targets"] = CreateSampleTargetsMetadata()
        };

        var report = TufDiagnostics.AnalyzeMetadataConsistency(metadataFiles);

        Console.WriteLine($"📊 Metadata Consistency Report ({report.Timestamp:yyyy-MM-dd HH:mm:ss} UTC)");
        Console.WriteLine($"   Files Analyzed: {string.Join(", ", report.MetadataFiles)}");
        Console.WriteLine($"   Is Consistent: {report.IsConsistent}");
        Console.WriteLine($"   Issues Found: {report.Issues.Count}");
        Console.WriteLine($"   Warnings: {report.Warnings.Count}");

        if (report.Issues.Count > 0)
        {
            Console.WriteLine("\n   Issues:");
            foreach (var issue in report.Issues)
            {
                Console.WriteLine($"     ❌ {issue}");
            }
        }

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine("\n   Warnings:");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"     ⚠️  {warning}");
            }
        }

        // Demo with problematic metadata
        Console.WriteLine("\n🚨 Testing with expired metadata:");
        var problematicMetadata = new Dictionary<string, byte[]>
        {
            ["root"] = CreateExpiredMetadata("root"),
            ["targets"] = CreateSampleTargetsMetadata()
        };

        var problematicReport = TufDiagnostics.AnalyzeMetadataConsistency(problematicMetadata);
        Console.WriteLine($"   Is Consistent: {problematicReport.IsConsistent}");
        
        if (problematicReport.Issues.Count > 0)
        {
            Console.WriteLine("   Critical Issues:");
            foreach (var issue in problematicReport.Issues)
            {
                Console.WriteLine($"     🔥 {issue}");
            }
        }
    }

    static UpdaterConfig CreateSampleValidConfig()
    {
        var validRoot = CreateSampleRootMetadata();
        return new UpdaterConfig(validRoot, new Uri("https://example.com/metadata"))
        {
            LocalMetadataDir = Path.Combine(Path.GetTempPath(), "tuf-demo-meta"),
            LocalTargetsDir = Path.Combine(Path.GetTempPath(), "tuf-demo-targets"),
            Client = new HttpClient()
        };
    }

    static UpdaterConfig CreateProblematicConfig()
    {
        // Configuration with null trusted root and HTTP URL
        return new UpdaterConfig(null!, new Uri("http://insecure-example.com/metadata"))
        {
            LocalMetadataDir = "/nonexistent/directory",
            LocalTargetsDir = "/another/nonexistent/directory",
            Client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(100) } // Very short timeout
        };
    }

    static byte[] CreateSampleRootMetadata()
    {
        var futureExpiry = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var metadata = new
        {
            signed = new
            {
                _type = "root",
                version = 1,
                expires = futureExpiry,
                keys = new
                {
                    ed25519_key = new
                    {
                        keytype = "ed25519",
                        scheme = "ed25519",
                        keyval = new { @public = "abcd1234..." }
                    }
                },
                roles = new
                {
                    root = new
                    {
                        keyids = new[] { "ed25519_key" },
                        threshold = 1
                    },
                    timestamp = new
                    {
                        keyids = new[] { "ed25519_key" },
                        threshold = 1
                    }
                }
            },
            signatures = new[]
            {
                new
                {
                    keyid = "ed25519_key",
                    sig = "sample_signature_data_here"
                }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(metadata, new JsonSerializerOptions { WriteIndented = true });
    }

    static byte[] CreateSampleTimestampMetadata()
    {
        var nearFutureExpiry = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var metadata = new
        {
            signed = new
            {
                _type = "timestamp",
                version = 42,
                expires = nearFutureExpiry,
                meta = new
                {
                    snapshot = new
                    {
                        version = 15,
                        hashes = new { sha256 = "abc123..." },
                        length = 1234
                    }
                }
            },
            signatures = new[]
            {
                new
                {
                    keyid = "timestamp_key",
                    sig = "timestamp_signature"
                }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(metadata, new JsonSerializerOptions { WriteIndented = true });
    }

    static byte[] CreateSampleSnapshotMetadata()
    {
        var mediumFutureExpiry = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var metadata = new
        {
            signed = new
            {
                _type = "snapshot", 
                version = 15,
                expires = mediumFutureExpiry,
                meta = new
                {
                    targets = new
                    {
                        version = 8,
                        hashes = new { sha256 = "def456..." },
                        length = 5678
                    }
                }
            },
            signatures = new[]
            {
                new
                {
                    keyid = "snapshot_key",
                    sig = "snapshot_signature"
                }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(metadata, new JsonSerializerOptions { WriteIndented = true });
    }

    static byte[] CreateSampleTargetsMetadata()
    {
        var longFutureExpiry = DateTime.UtcNow.AddMonths(6).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var metadata = new
        {
            signed = new
            {
                _type = "targets",
                version = 8,
                expires = longFutureExpiry,
                targets = new
                {
                    app_exe = new
                    {
                        length = 12345678,
                        hashes = new
                        {
                            sha256 = "target_file_hash_here"
                        }
                    }
                }
            },
            signatures = new[]
            {
                new
                {
                    keyid = "targets_key", 
                    sig = "targets_signature"
                }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(metadata, new JsonSerializerOptions { WriteIndented = true });
    }

    static byte[] CreateExpiredMetadata(string metadataType)
    {
        var pastExpiry = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var metadata = new
        {
            signed = new
            {
                _type = metadataType,
                version = 1,
                expires = pastExpiry,
                keys = new { },
                roles = new { }
            },
            signatures = new[]
            {
                new
                {
                    keyid = "expired_key",
                    sig = "expired_signature"
                }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(metadata, new JsonSerializerOptions { WriteIndented = true });
    }
}