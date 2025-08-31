using System.Text.Json;

using TUF.Models.Primitives;
using TUF.Models.Roles.Targets;
using TUF.MultiRepository;

namespace TUF.Tests;

public class MultiRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly HttpClient _httpClient;

    public MultiRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _httpClient = new HttpClient();
    }

    [Test]
    public async Task MultiRepositoryMap_Serialization_RoundTrip()
    {
        // Arrange
        var originalMap = new MultiRepositoryMap(
            Repositories: new Dictionary<string, RepositoryInfo>
            {
                ["repo-a"] = new RepositoryInfo("repo-a", "https://example.com/a", "https://example.com/a", "./root-a.json"),
                ["repo-b"] = new RepositoryInfo("repo-b", "https://example.com/b", "https://example.com/b", "./root-b.json")
            },
            Mapping: new[]
            {
                new Mapping(
                    Paths: new[] { new PathPattern("*.exe") },
                    Repositories: new[] { "repo-a", "repo-b" },
                    Threshold: 2,
                    Terminating: true
                ),
                new Mapping(
                    Paths: new[] { new PathPattern("*") },
                    Repositories: new[] { "repo-a" },
                    Threshold: 1,
                    Terminating: false
                )
            }
        );

        // Act
        var json = JsonSerializer.Serialize(originalMap);
        var deserializedMap = JsonSerializer.Deserialize<MultiRepositoryMap>(json);

        // Assert
        await Assert.That(deserializedMap).IsNotNull();
        await Assert.That(deserializedMap!.Repositories.Count).IsEqualTo(2);
        await Assert.That(deserializedMap.Mapping.Length).IsEqualTo(2);
        await Assert.That(deserializedMap.Repositories["repo-a"].Name).IsEqualTo("repo-a");
        await Assert.That(deserializedMap.Repositories["repo-a"].MetadataUrl).IsEqualTo("https://example.com/a");
        await Assert.That(deserializedMap.Mapping[0].Threshold).IsEqualTo(2);
        await Assert.That(deserializedMap.Mapping[0].Terminating).IsTrue();
        await Assert.That(deserializedMap.Mapping[1].Threshold).IsEqualTo(1);
        await Assert.That(deserializedMap.Mapping[1].Terminating).IsFalse();
    }

    [Test]
    public async Task Mapping_PathMatching_WorksCorrectly()
    {
        // Arrange
        var mapping = new Mapping(
            Paths: new[] { new PathPattern("*.exe"), new PathPattern("libs/*.dll") },
            Repositories: new[] { "repo-a" },
            Threshold: 1,
            Terminating: true
        );

        // Act & Assert
        await Assert.That(mapping.Paths.Any(p => p.IsMatch("app.exe"))).IsTrue();
        await Assert.That(mapping.Paths.Any(p => p.IsMatch("libs/helper.dll"))).IsTrue();
        await Assert.That(mapping.Paths.Any(p => p.IsMatch("config.txt"))).IsFalse();
        await Assert.That(mapping.Paths.Any(p => p.IsMatch("other/app.exe"))).IsFalse();
    }

    [Test]
    public async Task MultiRepositoryClient_InitializeAsync_CreatesDirectories()
    {
        // Arrange
        var mapPath = Path.Combine(_tempDir, "test-map.json");
        var testMap = new MultiRepositoryMap(
            Repositories: new Dictionary<string, RepositoryInfo>
            {
                ["test-repo"] = new RepositoryInfo("test-repo", "https://example.com", "https://example.com", "./root.json")
            },
            Mapping: new[]
            {
                new Mapping(new[] { new PathPattern("*") }, new[] { "test-repo" }, 1, false)
            }
        );

        var json = JsonSerializer.Serialize(testMap);
        await File.WriteAllTextAsync(mapPath, json);

        // Create dummy root file
        var rootPath = Path.Combine(_tempDir, "root.json");
        await File.WriteAllTextAsync(rootPath, "{}");

        var config = new MultiRepositoryConfig
        {
            MapFilePath = mapPath,
            MetadataDir = Path.Combine(_tempDir, "metadata"),
            TargetsDir = Path.Combine(_tempDir, "targets"),
            HttpClient = _httpClient
        };

        var client = new MultiRepositoryClient(config);

        // Act
        await client.InitializeAsync();

        // Assert
        await Assert.That(Directory.Exists(Path.Combine(_tempDir, "metadata"))).IsTrue();
        await Assert.That(Directory.Exists(Path.Combine(_tempDir, "targets"))).IsTrue();
        await Assert.That(Directory.Exists(Path.Combine(_tempDir, "metadata", "test-repo"))).IsTrue();
        await Assert.That(Directory.Exists(Path.Combine(_tempDir, "targets", "test-repo"))).IsTrue();
    }

    [Test]
    public async Task MultiRepositoryTargetResult_IsValid_WorksCorrectly()
    {
        // Arrange
        var targetMetadata = new TargetMetadata(
            Length: 100,
            Hashes: new List<TUF.Models.DigestAlgorithms.DigestValue>(),
            Custom: null,
            Path: new RelativePath("test.txt")
        );

        // Valid result (meets threshold)
        var validResult = new MultiRepositoryTargetResult(
            "test.txt",
            targetMetadata,
            AgreementCount: 2,
            RequiredThreshold: 2,
            new[] { "repo-a", "repo-b" }
        );

        // Invalid result (doesn't meet threshold)
        var invalidResult = new MultiRepositoryTargetResult(
            "test.txt",
            targetMetadata,
            AgreementCount: 1,
            RequiredThreshold: 2,
            new[] { "repo-a" }
        );

        // No target found
        var notFoundResult = new MultiRepositoryTargetResult(
            "test.txt",
            null,
            AgreementCount: 0,
            RequiredThreshold: 1,
            Array.Empty<string>()
        );

        // Act & Assert
        await Assert.That(validResult.IsValid).IsTrue();
        await Assert.That(invalidResult.IsValid).IsFalse();
        await Assert.That(notFoundResult.IsValid).IsFalse();
    }

    [Test]
    public async Task RepositoryInfo_PropertiesSetCorrectly()
    {
        // Arrange & Act
        var repoInfo = new RepositoryInfo(
            Name: "test-repo",
            MetadataUrl: "https://example.com/metadata",
            TargetsUrl: "https://example.com/targets",
            TrustedRootPath: "./trusted-root.json"
        );

        // Assert
        await Assert.That(repoInfo.Name).IsEqualTo("test-repo");
        await Assert.That(repoInfo.MetadataUrl).IsEqualTo("https://example.com/metadata");
        await Assert.That(repoInfo.TargetsUrl).IsEqualTo("https://example.com/targets");
        await Assert.That(repoInfo.TrustedRootPath).IsEqualTo("./trusted-root.json");
    }

    [Test]
    [Arguments(2, 2, true)]   // Exactly meets threshold
    [Arguments(3, 2, true)]   // Exceeds threshold
    [Arguments(1, 2, false)]  // Below threshold
    [Arguments(0, 1, false)]  // No agreements
    public async Task MultiRepositoryTargetResult_ConsensusValidation(int agreementCount, int threshold, bool expectedValid)
    {
        // Arrange
        var targetMetadata = agreementCount > 0 ? new TargetMetadata(
            Length: 100,
            Hashes: new List<TUF.Models.DigestAlgorithms.DigestValue>(),
            Custom: null,
            Path: new RelativePath("test.txt")
        ) : null;

        var result = new MultiRepositoryTargetResult(
            "test.txt",
            targetMetadata,
            agreementCount,
            threshold,
            Array.Empty<string>()
        );

        // Act & Assert
        await Assert.That(result.IsValid).IsEqualTo(expectedValid);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();

        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}