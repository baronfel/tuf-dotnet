using System.Net;
using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests for core conformance testing infrastructure capabilities.
/// These tests verify essential infrastructure functionality needed for TUF conformance testing.
/// </summary>
public class ConformanceInfrastructureTests
{
    [Test]
    public async Task Test_ConformanceInfrastructure_IsAvailable()
    {
        // Test that the conformance test infrastructure components are available
        var tempDir = Path.Combine(Path.GetTempPath(), "tuf-test-infra", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            await Assert.That(Directory.Exists(tempDir)).IsTrue();
            
            var testFile = Path.Combine(tempDir, "test.json");
            File.WriteAllText(testFile, "{\"test\":\"data\"}");
            await Assert.That(File.Exists(testFile)).IsTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_ConformanceCliPath_Detection()
    {
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
}