using System.Diagnostics;
using System.Net;
using System.Text.Json;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests for the conformance testing infrastructure components.
/// These tests verify that the test infrastructure itself works correctly.
/// </summary>
public class ConformanceInfrastructureTests
{
    [Test]
    public async Task Test_RandomPortGeneration()
    {
        // Test that we can generate random available ports
        var port1 = GetRandomAvailablePort();
        var port2 = GetRandomAvailablePort();

        await Assert.That(port1).IsGreaterThan(0);
        await Assert.That(port2).IsGreaterThan(0);
        await Assert.That(port1).IsNotEqualTo(port2);
    }

    [Test]
    public async Task Test_TestMetadataGeneration()
    {
        // Test that we can generate valid JSON metadata
        var rootMetadata = CreateTestRootMetadata();
        var timestampMetadata = CreateTestTimestampMetadata();
        var snapshotMetadata = CreateTestSnapshotMetadata();
        var targetsMetadata = CreateTestTargetsMetadata();

        // Verify all metadata is valid JSON - should not throw exceptions
        JsonDocument? rootDoc = null;
        JsonDocument? timestampDoc = null;
        JsonDocument? snapshotDoc = null;
        JsonDocument? targetsDoc = null;

        try
        {
            rootDoc = JsonDocument.Parse(rootMetadata);
            timestampDoc = JsonDocument.Parse(timestampMetadata);
            snapshotDoc = JsonDocument.Parse(snapshotMetadata);
            targetsDoc = JsonDocument.Parse(targetsMetadata);

            // If we get here, all parsing succeeded
            await Assert.That(rootDoc.RootElement.ValueKind).IsEqualTo(JsonValueKind.Object);
            await Assert.That(timestampDoc.RootElement.ValueKind).IsEqualTo(JsonValueKind.Object);
            await Assert.That(snapshotDoc.RootElement.ValueKind).IsEqualTo(JsonValueKind.Object);
            await Assert.That(targetsDoc.RootElement.ValueKind).IsEqualTo(JsonValueKind.Object);
        }
        catch (JsonException ex)
        {
            // Fail the test with the actual exception message
            await Assert.That(ex.Message).IsNull(); // This will fail and show the exception
        }
        finally
        {
            rootDoc?.Dispose();
            timestampDoc?.Dispose();
            snapshotDoc?.Dispose();
            targetsDoc?.Dispose();
        }

        // Verify root metadata contains expected fields
        using var rootDocument = JsonDocument.Parse(rootMetadata);
        await Assert.That(rootDocument.RootElement.TryGetProperty("signatures", out _)).IsTrue();
        await Assert.That(rootDocument.RootElement.TryGetProperty("signed", out _)).IsTrue();

        var signedElement = rootDocument.RootElement.GetProperty("signed");
        await Assert.That(signedElement.TryGetProperty("_type", out _)).IsTrue();
        await Assert.That(signedElement.GetProperty("_type").GetString()).IsEqualTo("root");
    }

    [Test]
    public async Task Test_HttpServerCanStart()
    {
        // Test that we can start an HTTP server on a random port
        var port = GetRandomAvailablePort();
        var baseUrl = $"http://localhost:{port}";

        using var listener = new HttpListener();
        listener.Prefixes.Add($"{baseUrl}/");

        // Should not throw when starting listener
        bool startedSuccessfully = false;
        try
        {
            listener.Start();
            startedSuccessfully = true;
        }
        catch
        {
            // Ignore - will be caught by assertion below
        }
        await Assert.That(startedSuccessfully).IsTrue();
        await Assert.That(listener.IsListening).IsTrue();

        listener.Stop();
    }

    [Test]
    public async Task Test_CliPathDetection()
    {
        // Test CLI path detection logic
        var currentDir = Environment.CurrentDirectory;

        // Should not throw when searching for CLI paths - but might throw InvalidOperationException
        bool noUnexpectedExceptions = true;
        try
        {
            FindConformanceCliPath();
        }
        catch (InvalidOperationException)
        {
            // This is expected if CLI is not found
        }
        catch
        {
            noUnexpectedExceptions = false;
        }
        await Assert.That(noUnexpectedExceptions).IsTrue();

        // The method should either find a path or throw with a descriptive error
        try
        {
            var path = FindConformanceCliPath();
            await Assert.That(File.Exists(path)).IsTrue();
        }
        catch (InvalidOperationException ex)
        {
            await Assert.That(ex.Message).Contains("TufConformanceCli");
        }
    }

    [Test]
    public async Task Test_ProcessExecution()
    {
        // Test that we can execute processes and capture output
        var result = await RunProcess("echo", "test");

        await Assert.That(result.exitCode).IsEqualTo(0);
        await Assert.That(result.output).Contains("test");
    }

    [Test]
    public async Task Test_TempDirectoryCleanup()
    {
        // Test that temporary directories can be created and cleaned up
        var testDir = Path.Combine(Path.GetTempPath(), "tuf-test", Guid.NewGuid().ToString());

        Directory.CreateDirectory(testDir);
        Directory.CreateDirectory(Path.Combine(testDir, "metadata"));
        File.WriteAllText(Path.Combine(testDir, "test.txt"), "test content");

        await Assert.That(Directory.Exists(testDir)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(testDir, "test.txt"))).IsTrue();

        // Cleanup should work without throwing
        bool cleanupSuccessful = false;
        try
        {
            Directory.Delete(testDir, true);
            cleanupSuccessful = true;
        }
        catch
        {
            // Ignore cleanup errors for test purposes
        }
        await Assert.That(cleanupSuccessful).IsTrue();
        await Assert.That(Directory.Exists(testDir)).IsFalse();
    }

    [Test]
    public async Task Test_JsonMetadataStructure()
    {
        // Test that generated metadata follows TUF specification structure
        var rootMetadata = CreateTestRootMetadata();
        var rootDoc = JsonDocument.Parse(rootMetadata);
        var signed = rootDoc.RootElement.GetProperty("signed");

        // Check required root metadata fields
        await Assert.That(signed.TryGetProperty("_type", out _)).IsTrue();
        await Assert.That(signed.TryGetProperty("spec_version", out _)).IsTrue();
        await Assert.That(signed.TryGetProperty("version", out _)).IsTrue();
        await Assert.That(signed.TryGetProperty("expires", out _)).IsTrue();
        await Assert.That(signed.TryGetProperty("keys", out _)).IsTrue();
        await Assert.That(signed.TryGetProperty("roles", out _)).IsTrue();
        await Assert.That(signed.TryGetProperty("consistent_snapshot", out _)).IsTrue();

        // Verify roles structure
        var roles = signed.GetProperty("roles");
        await Assert.That(roles.TryGetProperty("root", out _)).IsTrue();
        await Assert.That(roles.TryGetProperty("timestamp", out _)).IsTrue();
        await Assert.That(roles.TryGetProperty("snapshot", out _)).IsTrue();
        await Assert.That(roles.TryGetProperty("targets", out _)).IsTrue();
    }

    private static int GetRandomAvailablePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindConformanceCliPath()
    {
        var baseDir = Environment.CurrentDirectory;
        var possiblePaths = new[]
        {
            Path.Combine(baseDir, "examples/TufConformanceCli/bin/Release/net10.0/TufConformanceCli"),
            Path.Combine(baseDir, "examples/TufConformanceCli/bin/Debug/net10.0/TufConformanceCli"),
            "/home/runner/work/tuf-dotnet/tuf-dotnet/examples/TufConformanceCli/bin/Release/net10.0/TufConformanceCli"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new InvalidOperationException("Could not find TufConformanceCli executable. Build the project first.");
    }

    private static async Task<(int exitCode, string output, string error)> RunProcess(string fileName, string args)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = args;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }

    private static string CreateTestRootMetadata()
    {
        var rootMetadata = new
        {
            signatures = new[]
            {
                new { signature = "test_signature_placeholder" }
            },
            signed = new
            {
                _type = "root",
                spec_version = "1.0.0",
                version = 1,
                expires = "2025-12-31T23:59:59Z",
                keys = new Dictionary<string, object>
                {
                    ["test_root_key"] = new
                    {
                        keytype = "ed25519",
                        scheme = "ed25519",
                        keyval = new Dictionary<string, string> { ["public"] = "abcd1234567890abcd1234567890abcd1234567890abcd1234567890abcd1234" }
                    },
                    ["test_timestamp_key"] = new
                    {
                        keytype = "ed25519",
                        scheme = "ed25519",
                        keyval = new Dictionary<string, string> { ["public"] = "efgh5678901234efgh5678901234efgh5678901234efgh5678901234efgh5678" }
                    },
                    ["test_snapshot_key"] = new
                    {
                        keytype = "ed25519",
                        scheme = "ed25519",
                        keyval = new Dictionary<string, string> { ["public"] = "ijkl9012345678ijkl9012345678ijkl9012345678ijkl9012345678ijkl9012" }
                    },
                    ["test_targets_key"] = new
                    {
                        keytype = "ed25519",
                        scheme = "ed25519",
                        keyval = new Dictionary<string, string> { ["public"] = "mnop3456789012mnop3456789012mnop3456789012mnop3456789012mnop3456" }
                    }
                },
                roles = new
                {
                    root = new { keyids = new[] { "test_root_key" }, threshold = 1 },
                    timestamp = new { keyids = new[] { "test_timestamp_key" }, threshold = 1 },
                    snapshot = new { keyids = new[] { "test_snapshot_key" }, threshold = 1 },
                    targets = new { keyids = new[] { "test_targets_key" }, threshold = 1 }
                },
                consistent_snapshot = false
            }
        };

        return JsonSerializer.Serialize(rootMetadata, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateTestTimestampMetadata()
    {
        var timestampMetadata = new
        {
            signatures = new[] { new { signature = "test_signature_placeholder" } },
            signed = new
            {
                _type = "timestamp",
                spec_version = "1.0.0",
                version = 1,
                expires = "2025-12-31T23:59:59Z",
                meta = new Dictionary<string, object>
                {
                    ["snapshot.json"] = new { version = 1 }
                }
            }
        };

        return JsonSerializer.Serialize(timestampMetadata, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateTestSnapshotMetadata()
    {
        var snapshotMetadata = new
        {
            signatures = new[] { new { signature = "test_signature_placeholder" } },
            signed = new
            {
                _type = "snapshot",
                spec_version = "1.0.0",
                version = 1,
                expires = "2025-12-31T23:59:59Z",
                meta = new Dictionary<string, object>
                {
                    ["targets.json"] = new { version = 1 }
                }
            }
        };

        return JsonSerializer.Serialize(snapshotMetadata, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateTestTargetsMetadata()
    {
        var targetsMetadata = new
        {
            signatures = new[] { new { signature = "test_signature_placeholder" } },
            signed = new
            {
                _type = "targets",
                spec_version = "1.0.0",
                version = 1,
                expires = "2025-12-31T23:59:59Z",
                targets = new Dictionary<string, object>
                {
                    ["test-target.txt"] = new
                    {
                        length = 12,
                        hashes = new { sha256 = "aec070645fe53ee3b3763059376134f058cc337247c978add178b6ccdfb0019f" }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(targetsMetadata, new JsonSerializerOptions { WriteIndented = true });
    }
}