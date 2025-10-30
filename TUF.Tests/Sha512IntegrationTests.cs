using System.Security.Cryptography;

using TUnit.Assertions;
using TUnit.Core;

using TUF.Models;
using TUF.Repository;

namespace TUF.Tests;

/// <summary>
/// Integration tests verifying end-to-end SHA-512 support in TUF workflows.
/// Tests the complete pipeline from repository creation with SHA-512 hashes
/// to JSON serialization and deserialization.
/// </summary>
public class Sha512IntegrationTests
{
    [Test]
    public async Task TufWorkflow_SerializeAndDeserialize_PreservesHashAlgorithms()
    {
        // Arrange - Create repository with SHA-512
        var builder = new RepositoryBuilder();
        var rootSigner = Ed25519Signer.Generate();
        var timestampSigner = Ed25519Signer.Generate();
        var snapshotSigner = Ed25519Signer.Generate();
        var targetsSigner = Ed25519Signer.Generate();
        var testContent = "Serialization test content"u8.ToArray();
        
        var repository = builder
            .AddSigner("root", rootSigner)
            .AddSigner("timestamp", timestampSigner)
            .AddSigner("snapshot", snapshotSigner)
            .AddSigner("targets", targetsSigner)
            .SetHashAlgorithms("sha512")
            .AddTarget("serialization-test.txt", testContent)
            .Build();

        // Act - Serialize targets metadata to canonical JSON
        var targetsJson = CanonicalJson.Serializer.Serialize<Metadata<Targets>, MetadataProxy.Ser<Targets>>(repository.Targets);
        var targetsJsonString = System.Text.Encoding.UTF8.GetString(targetsJson);

        // Deserialize back
        var deserializedTargets = CanonicalJson.Serializer.Deserialize<Metadata<Targets>, MetadataProxy.De<Targets>>(System.Text.Encoding.UTF8.GetString(targetsJson));

        // Assert - SHA-512 hashes should be preserved
        var originalTarget = repository.Targets.Signed.TargetMap["serialization-test.txt"];
        var deserializedTarget = deserializedTargets.Signed.TargetMap["serialization-test.txt"];

        await Assert.That(deserializedTarget.Hashes).ContainsKey("sha512");
        await Assert.That(deserializedTarget.Hashes).DoesNotContainKey("sha256");
        await Assert.That(deserializedTarget.Hashes["sha512"]).IsEqualTo(originalTarget.Hashes["sha512"]);

        // Verify the JSON contains the SHA-512 hash
        await Assert.That(targetsJsonString).Contains("sha512");
        await Assert.That(targetsJsonString).DoesNotContain("sha256");
    }

    [Test] 
    public async Task TufWorkflow_WithSha512_CreatesValidRepositoryStructure()
    {
        // Arrange
        var builder = new RepositoryBuilder();
        var rootSigner = Ed25519Signer.Generate();
        var timestampSigner = Ed25519Signer.Generate();
        var snapshotSigner = Ed25519Signer.Generate();
        var targetsSigner = Ed25519Signer.Generate();
        var testContent1 = "File 1 content"u8.ToArray();
        var testContent2 = "File 2 content with different length"u8.ToArray();
        
        var repository = builder
            .AddSigner("root", rootSigner)
            .AddSigner("timestamp", timestampSigner)
            .AddSigner("snapshot", snapshotSigner)
            .AddSigner("targets", targetsSigner)
            .SetHashAlgorithms("sha512")
            .AddTarget("file1.txt", testContent1)
            .AddTarget("subdir/file2.txt", testContent2)
            .Build();

        // Act & Assert - Verify repository structure
        await Assert.That(repository.Root.Signed.SpecVersion).IsEqualTo("1.0.0");
        await Assert.That(repository.Timestamp.Signed.SpecVersion).IsEqualTo("1.0.0");
        await Assert.That(repository.Snapshot.Signed.SpecVersion).IsEqualTo("1.0.0");
        await Assert.That(repository.Targets.Signed.SpecVersion).IsEqualTo("1.0.0");

        // Verify all metadata has valid signatures
        await Assert.That(repository.Root.Signatures).HasCount().EqualTo(1);
        await Assert.That(repository.Timestamp.Signatures).HasCount().EqualTo(1);
        await Assert.That(repository.Snapshot.Signatures).HasCount().EqualTo(1);
        await Assert.That(repository.Targets.Signatures).HasCount().EqualTo(1);

        // Verify snapshot metadata references use SHA-512
        var snapshotMeta = repository.Snapshot.Signed.Meta["targets.json"];
        await Assert.That(snapshotMeta.Hashes).ContainsKey("sha512");
        await Assert.That(snapshotMeta.Hashes).DoesNotContainKey("sha256");

        // Verify timestamp metadata references use SHA-512
        var timestampMeta = repository.Timestamp.Signed.Meta["snapshot.json"];
        await Assert.That(timestampMeta.Hashes).ContainsKey("sha512");
        await Assert.That(timestampMeta.Hashes).DoesNotContainKey("sha256");
    }

    [Test]
    public async Task TufWorkflow_HashComputationCorrectness_MatchesDirectCalculation()
    {
        // Arrange
        var builder = new RepositoryBuilder();
        var rootSigner = Ed25519Signer.Generate();
        var timestampSigner = Ed25519Signer.Generate();
        var snapshotSigner = Ed25519Signer.Generate();
        var targetsSigner = Ed25519Signer.Generate();
        var testContent = "Test content for hash verification"u8.ToArray();
        
        var repository = builder
            .AddSigner("root", rootSigner)
            .AddSigner("timestamp", timestampSigner)
            .AddSigner("snapshot", snapshotSigner)
            .AddSigner("targets", targetsSigner)
            .SetHashAlgorithms("sha256", "sha512")
            .AddTarget("hash-test.txt", testContent)
            .Build();

        // Act - Get computed hashes from repository
        var targetFile = repository.Targets.Signed.TargetMap["hash-test.txt"];

        // Assert - Verify computed hashes match direct calculations
        var expectedSha256 = Convert.ToHexString(SHA256.HashData(testContent)).ToLowerInvariant();
        var expectedSha512 = Convert.ToHexString(SHA512.HashData(testContent)).ToLowerInvariant();

        await Assert.That(targetFile.Hashes["sha256"]).IsEqualTo(expectedSha256);
        await Assert.That(targetFile.Hashes["sha512"]).IsEqualTo(expectedSha512);
        await Assert.That(targetFile.Length).IsEqualTo(testContent.Length);
    }
}