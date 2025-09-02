using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests for the conformance testing documentation.
/// These tests verify that the documentation is complete and accurate.
/// </summary>
public class ConformanceDocumentationTests
{
    [Test]
    public async Task Test_LocalConformanceTestingDoc_Exists()
    {
        var docPath = Path.Combine(Environment.CurrentDirectory, "docs/local-conformance-testing.md");
        await Assert.That(File.Exists(docPath)).IsTrue();
    }

    [Test]
    public async Task Test_Documentation_HasRequiredSections()
    {
        var docPath = Path.Combine(Environment.CurrentDirectory, "docs/local-conformance-testing.md");
        var content = await File.ReadAllTextAsync(docPath);

        // Check for key sections
        await Assert.That(content).Contains("# Local TUF Conformance Testing");
        await Assert.That(content).Contains("## Overview");
        await Assert.That(content).Contains("## Quick Start");
        await Assert.That(content).Contains("### Prerequisites");
        await Assert.That(content).Contains("### Running Tests");
        await Assert.That(content).Contains("## Architecture");
        await Assert.That(content).Contains("### Components");
        await Assert.That(content).Contains("### Test Flow");
        await Assert.That(content).Contains("### Test Structure");
        await Assert.That(content).Contains("## Test Cases");
        await Assert.That(content).Contains("## Debugging");
    }

    [Test]
    public async Task Test_Documentation_HasCodeExamples()
    {
        var docPath = Path.Combine(Environment.CurrentDirectory, "docs/local-conformance-testing.md");
        var content = await File.ReadAllTextAsync(docPath);

        // Check for PowerShell examples
        await Assert.That(content).Contains("./test-conformance-local.ps1");

        // Check for Bash examples
        await Assert.That(content).Contains("./test-conformance-local.sh");

        // Check for dotnet commands
        await Assert.That(content).Contains("dotnet build");

        // Check for code blocks
        await Assert.That(content).Contains("```powershell");
        await Assert.That(content).Contains("```bash");
    }

    [Test]
    public async Task Test_Documentation_ReferencesCorrectFiles()
    {
        var docPath = Path.Combine(Environment.CurrentDirectory, "docs/local-conformance-testing.md");
        var content = await File.ReadAllTextAsync(docPath);

        // Should reference the correct test files
        await Assert.That(content).Contains("ConformanceTests.cs");
        await Assert.That(content).Contains("test-conformance-local.sh");
        await Assert.That(content).Contains("test-conformance-local.ps1");

        // Should reference the correct CLI project
        await Assert.That(content).Contains("TufConformanceCli");

        // Should reference correct .NET version
        await Assert.That(content).Contains("NET 10");
    }

    [Test]
    public async Task Test_Documentation_HasDebuggingGuidance()
    {
        var docPath = Path.Combine(Environment.CurrentDirectory, "docs/local-conformance-testing.md");
        var content = await File.ReadAllTextAsync(docPath);

        // Should provide debugging information
        await Assert.That(content).Contains("signature validation");
        await Assert.That(content).Contains("CLI output");
        await Assert.That(content).Contains("HTTP server");
        await Assert.That(content).Contains("debugging");

        // Should explain common issues
        await Assert.That(content).Contains("Issue:");
        await Assert.That(content).Contains("Solution:");
    }

    [Test]
    public async Task Test_Documentation_HasCorrectLinks()
    {
        var docPath = Path.Combine(Environment.CurrentDirectory, "docs/local-conformance-testing.md");
        var content = await File.ReadAllTextAsync(docPath);

        // Should reference external TUF resources
        await Assert.That(content).Contains("tuf-conformance");
        await Assert.That(content).Contains("theupdateframework");

        // Should have proper markdown link format for external resources
        var linkPattern = @"\[.*\]\(https://.*\)";
        await Assert.That(System.Text.RegularExpressions.Regex.IsMatch(content, linkPattern)).IsTrue();
    }

    [Test]
    public async Task Test_Documentation_HasTestScenarios()
    {
        var docPath = Path.Combine(Environment.CurrentDirectory, "docs/local-conformance-testing.md");
        var content = await File.ReadAllTextAsync(docPath);

        // Should describe test scenarios
        await Assert.That(content).Contains("Test_Init_Command");
        await Assert.That(content).Contains("Test_Refresh_Command");
        await Assert.That(content).Contains("Test_Download_Command");
        await Assert.That(content).Contains("Test_Missing_Args_Error_Handling");

        // Should explain what each test does
        await Assert.That(content).Contains("init");
        await Assert.That(content).Contains("refresh");
        await Assert.That(content).Contains("download");
    }

    [Test]
    public async Task Test_Documentation_ExplainsArchitecture()
    {
        var docPath = Path.Combine(Environment.CurrentDirectory, "docs/local-conformance-testing.md");
        var content = await File.ReadAllTextAsync(docPath);

        // Should explain the architecture components
        await Assert.That(content).Contains("Local HTTP Server");
        await Assert.That(content).Contains("Test Data Generator");
        await Assert.That(content).Contains("CLI Process Runner");

        // Should have architecture diagram or flow description
        await Assert.That(content).Contains("Test Setup");
        await Assert.That(content).Contains("HTTP Server");
        await Assert.That(content).Contains("TufConformance");
    }

    [Test]
    public async Task Test_Documentation_HasFutureImprovements()
    {
        var docPath = Path.Combine(Environment.CurrentDirectory, "docs/local-conformance-testing.md");
        var content = await File.ReadAllTextAsync(docPath);

        // Should outline future improvements
        await Assert.That(content).Contains("Future Improvements");
        await Assert.That(content).Contains("signature generation");
        await Assert.That(content).Contains("test scenarios");
    }

    [Test]
    public async Task Test_Documentation_HasMarkdownFormat()
    {
        var docPath = Path.Combine(Environment.CurrentDirectory, "docs/local-conformance-testing.md");
        var content = await File.ReadAllTextAsync(docPath);

        // Should be properly formatted Markdown
        await Assert.That(content).Contains("# "); // H1 headers
        await Assert.That(content).Contains("## "); // H2 headers
        await Assert.That(content).Contains("### "); // H3 headers
        await Assert.That(content).Contains("- "); // List items
        await Assert.That(content).Contains("```"); // Code blocks
    }
}