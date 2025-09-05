using System.Text.Json;

using TUF.Models;
using TUF.MultiRepository;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

public class MultiRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly HttpClient _httpClient;

    public MultiRepositoryTests()
    {
        _tempDir = TestOptimizations.GetUniqueTestDirectory();
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
                    Paths: new[] { "*.exe" },
                    Repositories: new[] { "repo-a", "repo-b" },
                    Threshold: 2,
                    Terminating: true
                ),
                new Mapping(
                    Paths: new[] { "*" },
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
            Paths: new[] { "*.exe", "libs/*.dll" },
            Repositories: new[] { "repo-a" },
            Threshold: 1,
            Terminating: true
        );

        // Act & Assert - Use DelegatedRole.PathIsMatch for pattern matching
        await Assert.That(DelegatedRole.PathIsMatch("*.exe", "app.exe")).IsTrue();
        await Assert.That(DelegatedRole.PathIsMatch("libs/*.dll", "libs/helper.dll")).IsTrue();
        await Assert.That(DelegatedRole.PathIsMatch("*.exe", "config.txt")).IsFalse();
        await Assert.That(DelegatedRole.PathIsMatch("*.exe", "other/app.exe")).IsFalse();
    }

    [Test]
    public async Task MultiRepositoryClient_Constructor_InitializesCorrectly()
    {
        // Arrange
        var config = new MultiRepositoryConfig
        {
            MapFilePath = "dummy.json",
            MetadataDir = Path.Combine(_tempDir, "metadata"),
            TargetsDir = Path.Combine(_tempDir, "targets"),
            HttpClient = _httpClient
        };

        // Act
        var client = new MultiRepositoryClient(config);

        // Assert
        await Assert.That(client).IsNotNull();
    }

    [Test]
    public async Task MultiRepositoryTargetResult_IsValid_WorksCorrectly()
    {
        // Arrange
        var targetMetadata = new TargetFile
        {
            Length = 100,
            Hashes = new Dictionary<string, string>
            {
                ["sha256"] = "abcd1234567890abcd1234567890abcd1234567890abcd1234567890abcd1234"
            },
            Custom = null
        };

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
            TargetInfo: null,
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
        var targetMetadata = agreementCount > 0 ? new TargetFile
        {
            Length = 100,
            Hashes = new Dictionary<string, string>
            {
                ["sha256"] = "abcd1234567890abcd1234567890abcd1234567890abcd1234567890abcd1234"
            },
            Custom = null
        } : null;

        var result = new MultiRepositoryTargetResult(
            "test.txt",
            TargetInfo: targetMetadata,
            agreementCount,
            threshold,
            Array.Empty<string>()
        );

        // Act & Assert
        await Assert.That(result.IsValid).IsEqualTo(expectedValid);
    }

    [Test]
    public async Task MultiRepositoryClient_GetTargetInfoAsync_ThrowsWhenNotInitialized()
    {
        // Arrange
        var config = new MultiRepositoryConfig
        {
            MapFilePath = "dummy.json",
            MetadataDir = "metadata",
            TargetsDir = "targets"
        };
        var client = new MultiRepositoryClient(config);

        // Act & Assert
        await Assert.That(async () => await client.GetTargetInfoAsync("test.txt"))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("Client not initialized. Call InitializeAsync() first.");
    }

    [Test]
    public async Task MultiRepositoryClient_RefreshAsync_ThrowsWhenNotInitialized()
    {
        // Arrange
        var config = new MultiRepositoryConfig
        {
            MapFilePath = "dummy.json",
            MetadataDir = "metadata",
            TargetsDir = "targets"
        };
        var client = new MultiRepositoryClient(config);

        // Act & Assert
        await Assert.That(async () => await client.RefreshAsync())
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("Client not initialized. Call InitializeAsync() first.");
    }

    [Test]
    public async Task MultiRepositoryClient_InitializeAsync_ThrowsOnInvalidMapFile()
    {
        // Arrange
        var mapPath = Path.Combine(_tempDir, "invalid-map.json");
        await File.WriteAllTextAsync(mapPath, "invalid json content");

        var config = new MultiRepositoryConfig
        {
            MapFilePath = mapPath,
            MetadataDir = Path.Combine(_tempDir, "metadata"),
            TargetsDir = Path.Combine(_tempDir, "targets"),
            HttpClient = _httpClient
        };

        var client = new MultiRepositoryClient(config);

        // Act & Assert
        await Assert.That(async () => await client.InitializeAsync())
            .Throws<Exception>();
    }

    [Test]
    public async Task MultiRepositoryConfig_DefaultHttpClient_IsNotNull()
    {
        // Arrange & Act
        var config = new MultiRepositoryConfig
        {
            MapFilePath = "test.json",
            MetadataDir = "metadata",
            TargetsDir = "targets"
        };

        // Assert
        await Assert.That(config.HttpClient).IsNotNull();
        config.HttpClient.Dispose(); // Clean up
    }

    [Test]
    public async Task Mapping_JsonSerialization_PreservesAllFields()
    {
        // Arrange
        var originalMapping = new Mapping(
            Paths: new[] { "*.exe", "libs/*.dll" },
            Repositories: new[] { "repo-a", "repo-b", "repo-c" },
            Threshold: 2,
            Terminating: true
        );

        // Act
        var json = JsonSerializer.Serialize(originalMapping);
        var deserializedMapping = JsonSerializer.Deserialize<Mapping>(json);

        // Assert
        // Note: deserializedMapping is a struct, so it's never null - just check one property
        await Assert.That(deserializedMapping.Threshold).IsEqualTo(2);
        await Assert.That(deserializedMapping!.Paths).HasCount().EqualTo(2);
        await Assert.That(deserializedMapping.Repositories).HasCount().EqualTo(3);
        await Assert.That(deserializedMapping.Threshold).IsEqualTo(2);
        await Assert.That(deserializedMapping.Terminating).IsTrue();
        await Assert.That(deserializedMapping.Paths[0]).IsEqualTo("*.exe");
        await Assert.That(deserializedMapping.Repositories[0]).IsEqualTo("repo-a");
    }

    [Test]
    public async Task RepositoryInfo_JsonSerialization_PreservesAllFields()
    {
        // Arrange
        var originalRepoInfo = new RepositoryInfo(
            Name: "production-repo",
            MetadataUrl: "https://secure-metadata.example.com/",
            TargetsUrl: "https://secure-targets.example.com/files/",
            TrustedRootPath: "./production-root.json"
        );

        // Act
        var json = JsonSerializer.Serialize(originalRepoInfo);
        var deserializedRepoInfo = JsonSerializer.Deserialize<RepositoryInfo>(json);

        // Assert
        // Note: deserializedRepoInfo is a struct, so it's never null - just check one property
        await Assert.That(deserializedRepoInfo.Name).IsEqualTo("production-repo");
        await Assert.That(deserializedRepoInfo!.Name).IsEqualTo("production-repo");
        await Assert.That(deserializedRepoInfo.MetadataUrl).IsEqualTo("https://secure-metadata.example.com/");
        await Assert.That(deserializedRepoInfo.TargetsUrl).IsEqualTo("https://secure-targets.example.com/files/");
        await Assert.That(deserializedRepoInfo.TrustedRootPath).IsEqualTo("./production-root.json");
    }

    [Test]
    public async Task Mapping_DefaultTerminating_IsFalse()
    {
        // Arrange & Act
        var mapping = new Mapping(
            Paths: new[] { "*" },
            Repositories: new[] { "repo" },
            Threshold: 1
        );

        // Assert
        await Assert.That(mapping.Terminating).IsFalse();
    }

    [Test]
    public async Task MultiRepositoryTargetResult_WithNullTargetInfo_IsInvalid()
    {
        // Arrange & Act
        var result = new MultiRepositoryTargetResult(
            "missing.txt",
            TargetInfo: null,
            AgreementCount: 0,
            RequiredThreshold: 1,
            Array.Empty<string>()
        );

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.TargetInfo).IsNull();
    }

    [Test]
    public async Task MultiRepositoryTargetResult_WithZeroAgreement_IsInvalid()
    {
        // Arrange
        var targetMetadata = new TargetFile
        {
            Length = 100,
            Hashes = new Dictionary<string, string>
            {
                ["sha256"] = "abcd1234567890abcd1234567890abcd1234567890abcd1234567890abcd1234"
            },
            Custom = null
        };

        // Act
        var result = new MultiRepositoryTargetResult(
            "test.txt",
            TargetInfo: targetMetadata,
            AgreementCount: 0,
            RequiredThreshold: 1,
            Array.Empty<string>()
        );

        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task MultiRepositoryMap_WithEmptyRepositories_SerializesCorrectly()
    {
        // Arrange
        var emptyMap = new MultiRepositoryMap(
            Repositories: new Dictionary<string, RepositoryInfo>(),
            Mapping: Array.Empty<Mapping>()
        );

        // Act
        var json = JsonSerializer.Serialize(emptyMap);
        var deserializedMap = JsonSerializer.Deserialize<MultiRepositoryMap>(json);

        // Assert
        await Assert.That(deserializedMap).IsNotNull();
        await Assert.That(deserializedMap!.Repositories).HasCount().EqualTo(0);
        await Assert.That(deserializedMap.Mapping).HasCount().EqualTo(0);
    }

    [Test]
    public async Task Mapping_WithMultiplePatterns_MatchesAnyPattern()
    {
        // Arrange
        var mapping = new Mapping(
            Paths: new[]
            {
                "*.exe",
                "docs/*.pdf",
                "config.json"
            },
            Repositories: new[] { "repo" },
            Threshold: 1
        );

        // Act & Assert
        await Assert.That(mapping.Paths.Any(p => DelegatedRole.PathIsMatch(p, "application.exe"))).IsTrue();
        await Assert.That(mapping.Paths.Any(p => DelegatedRole.PathIsMatch(p, "docs/manual.pdf"))).IsTrue();
        await Assert.That(mapping.Paths.Any(p => DelegatedRole.PathIsMatch(p, "config.json"))).IsTrue();
        await Assert.That(mapping.Paths.Any(p => DelegatedRole.PathIsMatch(p, "readme.txt"))).IsFalse();
        await Assert.That(mapping.Paths.Any(p => DelegatedRole.PathIsMatch(p, "docs/readme.txt"))).IsFalse();
    }

    [Test]
    [Arguments("critical/*.exe", 3, true)]     // High security files require all repos
    [Arguments("data/*", 2, false)]            // Data files require majority
    [Arguments("*", 1, false)]                 // Fallback requires any repo
    public async Task MultiRepositoryMap_DifferentSecurityPolicies_ConfiguredCorrectly(
        string pathPattern, int threshold, bool terminating)
    {
        // Arrange
        var securityMap = new MultiRepositoryMap(
            Repositories: new Dictionary<string, RepositoryInfo>
            {
                ["security-repo"] = new RepositoryInfo("security-repo", "https://security.com", "https://security.com", "./security-root.json"),
                ["backup-repo"] = new RepositoryInfo("backup-repo", "https://backup.com", "https://backup.com", "./backup-root.json"),
                ["general-repo"] = new RepositoryInfo("general-repo", "https://general.com", "https://general.com", "./general-root.json")
            },
            Mapping: new[]
            {
                new Mapping(
                    Paths: new[] { pathPattern },
                    Repositories: new[] { "security-repo", "backup-repo", "general-repo" },
                    Threshold: threshold,
                    Terminating: terminating
                )
            }
        );

        // Act
        var json = JsonSerializer.Serialize(securityMap);
        var deserializedMap = JsonSerializer.Deserialize<MultiRepositoryMap>(json);

        // Assert
        await Assert.That(deserializedMap).IsNotNull();
        await Assert.That(deserializedMap!.Mapping[0].Threshold).IsEqualTo(threshold);
        await Assert.That(deserializedMap.Mapping[0].Terminating).IsEqualTo(terminating);
        await Assert.That(deserializedMap.Mapping[0].Paths[0]).IsEqualTo(pathPattern);
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