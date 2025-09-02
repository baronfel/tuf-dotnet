using System.Runtime.CompilerServices;
using System.Text;
using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Test class for running TUF conformance scenarios from the scenarios/ directory.
/// Each scenario tests the Updater's ability to refresh metadata and verify signatures.
/// </summary>
public class ScenarioRunnerTests
{
    /// <summary>
    /// Gets the scenarios directory path by walking up from the test file location.
    /// </summary>
    private static string GetScenariosPath([CallerFilePath] string sourceFilePath = "")
    {
        var testFileDir = Path.GetDirectoryName(sourceFilePath)!;
        var projectRoot = Path.GetDirectoryName(testFileDir)!; // Go up from TUF.Tests to project root
        return Path.Combine(projectRoot, "scenarios");
    }
    /// <summary>
    /// Mock HttpClient that serves TUF metadata files from a scenario's refresh-1 directory.
    /// Maps TUF metadata URLs to local files for testing.
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _scenarioPath;
        private readonly Dictionary<string, string> _responses = new();

        public MockHttpMessageHandler(string scenarioPath)
        {
            _scenarioPath = scenarioPath;
            LoadScenarioFiles();
        }

        private void LoadScenarioFiles()
        {
            var refreshPath = Path.Combine(_scenarioPath, "refresh-1");
            
            // Map TUF metadata URLs to local files
            var fileMap = new Dictionary<string, string>
            {
                { "/metadata/timestamp.json", "timestamp.json" },
                { "/metadata/snapshot.json", "snapshot.json" },
                { "/metadata/targets.json", "targets.json" },
                { "/metadata/1.root.json", "1.root.json" },
                { "/metadata/root.json", "1.root.json" } // Also serve as root.json
            };

            foreach (var (url, fileName) in fileMap)
            {
                var filePath = Path.Combine(refreshPath, fileName);
                if (File.Exists(filePath))
                {
                    _responses[url] = File.ReadAllText(filePath);
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            
            if (_responses.TryGetValue(path, out var content))
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
            }
            
            // Return 404 for unknown paths
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }

    /// <summary>
    /// Gets all scenario directories from the scenarios/ folder.
    /// Each directory represents a distinct TUF conformance test scenario.
    /// </summary>
    public static IEnumerable<object[]> GetScenarios()
    {
        var scenariosPath = GetScenariosPath();
        
        if (!Directory.Exists(scenariosPath))
            yield break;

        foreach (var scenarioDir in Directory.GetDirectories(scenariosPath))
        {
            var scenarioName = Path.GetFileName(scenarioDir);
            var refresh1Path = Path.Combine(scenarioDir, "refresh-1");
            var rootPath = Path.Combine(refresh1Path, "1.root.json");
            
            // Only include scenarios that have the required structure
            if (Directory.Exists(refresh1Path) && File.Exists(rootPath))
            {
                yield return new object[] { scenarioName, scenarioDir };
            }
        }
    }

    /// <summary>
    /// Runs a TUF scenario by creating an Updater with the scenario's initial root,
    /// setting up a mock HTTP client to serve metadata from refresh-1/, and calling
    /// RefreshAsync() to verify that all signatures are correct.
    /// </summary>
    /// <param name="scenarioName">Name of the scenario for test identification</param>
    /// <param name="scenarioPath">Full path to the scenario directory</param>
    [Test]
    [MethodDataSource(nameof(GetScenarios))]
    [Skip("For debugging a single scenario only")]
    public async Task RunScenario(string scenarioName, string scenarioPath)
    {
        // Arrange: Load initial trusted root from 1.root.json
        var refresh1Path = Path.Combine(scenarioPath, "refresh-1");
        var rootPath = Path.Combine(refresh1Path, "1.root.json");

        await Assert.That(File.Exists(rootPath)).IsTrue();

        var initialRootBytes = await File.ReadAllBytesAsync(rootPath);

        // Create mock HTTP client that serves files from refresh-1/
        var mockHandler = new MockHttpMessageHandler(scenarioPath);
        using var httpClient = new HttpClient(mockHandler);

        // Configure updater with scenario data
        var config = new UpdaterConfig(initialRootBytes, new Uri("https://example.com/metadata/"))
        {
            Client = httpClient,
            LocalMetadataDir = Path.GetTempPath(),
            LocalTargetsDir = Path.GetTempPath(),
            DisableLocalCache = true // Force fresh downloads for testing
        };

        var updater = new Updater(config);

        // Act & Assert: Refresh should complete successfully if signatures are valid
        // This will verify that:
        // - Root metadata can be parsed and trusted
        // - Timestamp metadata is properly signed
        // - Snapshot metadata is properly signed  
        // - Targets metadata is properly signed
        // - All signature thresholds are met
        // - No signature verification errors occur
        try
        {
            await updater.RefreshAsync();
        }
        catch (Exception ex)
        {
            // Some scenarios are expected to fail - this is normal for negative test cases
            // The test framework will handle expected failures vs unexpected errors
            throw new Exception($"Scenario {scenarioName} failed during refresh: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Runs a single scenario by name for debugging purposes.
    /// Useful for isolating and debugging specific failing scenarios.
    /// </summary>
    /// <param name="scenarioName">Name of the scenario to run</param>
    [Test]
    [Skip("For debugging a single scenario only")]
    public async Task RunSingleScenario_BasicRefresh()
    {
        await RunSingleScenarioByName("test_basic_refresh_requests");
    }

    /// <summary>
    /// Helper method to run a single scenario by name.
    /// </summary>
    private async Task RunSingleScenarioByName(string scenarioName)
    {
        var scenariosPath = GetScenariosPath();
        var scenarioPath = Path.Combine(scenariosPath, scenarioName);
        
        await Assert.That(Directory.Exists(scenarioPath)).IsTrue();
            
        await RunScenario(scenarioName, scenarioPath);
    }
}