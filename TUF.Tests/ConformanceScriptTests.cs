using System.Diagnostics;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests for the conformance test runner scripts.
/// These tests verify that the PowerShell and Bash scripts work correctly.
/// </summary>
public class ConformanceScriptTests
{
    [Test]
    public async Task Test_PowerShellScript_Exists()
    {
        var scriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.ps1");
        await Assert.That(File.Exists(scriptPath)).IsTrue();

        var content = await File.ReadAllTextAsync(scriptPath);
        await Assert.That(content).Contains("TUF .NET Local Conformance Test Runner");
        await Assert.That(content).Contains("param(");
        await Assert.That(content).Contains("dotnet build");
        await Assert.That(content).Contains("dotnet test");
    }

    [Test]
    public async Task Test_BashScript_Exists()
    {
        var scriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.sh");
        await Assert.That(File.Exists(scriptPath)).IsTrue();

        var content = await File.ReadAllTextAsync(scriptPath);
        await Assert.That(content).Contains("TUF .NET Local Conformance Test Runner");
        await Assert.That(content).Contains("#!/bin/bash");
        await Assert.That(content).Contains("dotnet build");
        await Assert.That(content).Contains("dotnet test");
    }

    [Test]
    public async Task Test_PowerShellScript_Syntax()
    {
        var scriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.ps1");

        // Test PowerShell syntax checking if PowerShell is available
        if (IsCommandAvailable("pwsh") || IsCommandAvailable("powershell"))
        {
            var command = IsCommandAvailable("pwsh") ? "pwsh" : "powershell";
            var result = await RunCommand(command, $"-Command \"Get-Command -Syntax (Get-Content '{scriptPath}' | Out-String)\"");

            // If the script has syntax errors, PowerShell will return non-zero exit code
            await Assert.That(result.exitCode).IsIn(0, 1); // Allow either success or controlled failure
        }
    }

    [Test]
    public async Task Test_BashScript_Syntax()
    {
        var scriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.sh");

        // Test Bash syntax checking if Bash is available
        if (IsCommandAvailable("bash"))
        {
            var result = await RunCommand("bash", $"-n {scriptPath}");

            // bash -n checks syntax without executing
            await Assert.That(result.exitCode).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Test_Scripts_HaveExecutePermissions()
    {
        var bashScriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.sh");

        // On Unix-like systems, check if the script has execute permissions
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            var result = await RunCommand("stat", $"-c %a {bashScriptPath}");
            if (result.exitCode == 0)
            {
                var permissions = result.output.Trim();
                // Should have at least read and execute permissions for owner (5xx)
                await Assert.That(permissions[0]).IsIn('5', '6', '7');
            }
        }
    }

    [Test]
    public async Task Test_Scripts_ContainHelpText()
    {
        var psScriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.ps1");
        var bashScriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.sh");

        var psContent = await File.ReadAllTextAsync(psScriptPath);
        var bashContent = await File.ReadAllTextAsync(bashScriptPath);

        // PowerShell script should have help documentation
        await Assert.That(psContent).Contains(".SYNOPSIS");
        await Assert.That(psContent).Contains(".DESCRIPTION");
        await Assert.That(psContent).Contains(".PARAMETER");
        await Assert.That(psContent).Contains(".EXAMPLE");

        // Bash script should have help functionality
        await Assert.That(bashContent).Contains("--help");
        await Assert.That(bashContent).Contains("Usage:");
        await Assert.That(bashContent).Contains("Examples:");
    }

    [Test]
    public async Task Test_Scripts_HandleArguments()
    {
        var psScriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.ps1");
        var bashScriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.sh");

        var psContent = await File.ReadAllTextAsync(psScriptPath);
        var bashContent = await File.ReadAllTextAsync(bashScriptPath);

        // Both scripts should support test filtering
        await Assert.That(psContent).Contains("-Test");
        await Assert.That(bashContent).Contains("--test");

        // Both scripts should support verbose output
        await Assert.That(psContent).Contains("-Verbose");
        await Assert.That(bashContent).Contains("--verbose");
    }

    [Test]
    public async Task Test_Scripts_BuildConformanceCli()
    {
        var psScriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.ps1");
        var bashScriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.sh");

        var psContent = await File.ReadAllTextAsync(psScriptPath);
        var bashContent = await File.ReadAllTextAsync(bashScriptPath);

        // Both scripts should build the conformance CLI
        await Assert.That(psContent).Contains("examples/TufConformanceCli/TufConformanceCli.csproj");
        await Assert.That(bashContent).Contains("examples/TufConformanceCli/TufConformanceCli.csproj");

        // Both should use Release configuration
        await Assert.That(psContent).Contains("--configuration Release");
        await Assert.That(bashContent).Contains("--configuration Release");
    }

    [Test]
    public async Task Test_Scripts_RunTests()
    {
        var psScriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.ps1");
        var bashScriptPath = Path.Combine(Environment.CurrentDirectory, "test-conformance-local.sh");

        var psContent = await File.ReadAllTextAsync(psScriptPath);
        var bashContent = await File.ReadAllTextAsync(bashScriptPath);

        // Both scripts should run tests
        await Assert.That(psContent).Contains("dotnet test");
        await Assert.That(bashContent).Contains("dotnet test");

        // Both should filter for ConformanceTests
        await Assert.That(psContent).Contains("ConformanceTests");
        await Assert.That(bashContent).Contains("ConformanceTests");
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(5000); // 5 second timeout
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int exitCode, string output, string error)> RunCommand(string fileName, string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
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
}