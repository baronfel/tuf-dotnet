using System.Security.Cryptography;

using TUnit.Assertions;
using TUnit.Core;

using TUF.Models;
using TUF.Repository;

namespace TUF.Tests;

/// <summary>
/// Tests for SHA-512 hash algorithm support in the TUF repository builder.
/// Verifies that the repository builder can generate SHA-512 hashes alongside SHA-256
/// for target files and metadata, enabling broader ecosystem compatibility.
/// </summary>
public class Sha512HashSupportTests
{
    [Test]
    public async Task RepositoryBuilder_DefaultConfiguration_UsesOnlySha256()
    {
        // Arrange
        var builder = new RepositoryBuilder();
        var rootSigner = Ed25519Signer.Generate();
        var timestampSigner = Ed25519Signer.Generate();
        var snapshotSigner = Ed25519Signer.Generate();
        var targetsSigner = Ed25519Signer.Generate();
        
        builder.AddSigner("root", rootSigner)
               .AddSigner("timestamp", timestampSigner)
               .AddSigner("snapshot", snapshotSigner)
               .AddSigner("targets", targetsSigner)
               .AddTarget("test-file.txt", "Hello, World!"u8.ToArray());

        // Act
        var repository = builder.Build();

        // Assert - should only have SHA-256 hashes by default
        var targetFile = repository.Targets.Signed.TargetMap["test-file.txt"];
        await Assert.That(targetFile.Hashes).ContainsKey("sha256");
        await Assert.That(targetFile.Hashes).DoesNotContainKey("sha512");
        await Assert.That(targetFile.Hashes).HasCount().EqualTo(1);

        // Check snapshot metadata hashes
        var snapshotMeta = repository.Snapshot.Signed.Meta["targets.json"];
        await Assert.That(snapshotMeta.Hashes).ContainsKey("sha256");
        await Assert.That(snapshotMeta.Hashes).DoesNotContainKey("sha512");
        await Assert.That(snapshotMeta.Hashes).HasCount().EqualTo(1);

        // Check timestamp metadata hashes
        var timestampMeta = repository.Timestamp.Signed.Meta["snapshot.json"];
        await Assert.That(timestampMeta.Hashes).ContainsKey("sha256");
        await Assert.That(timestampMeta.Hashes).DoesNotContainKey("sha512");
        await Assert.That(timestampMeta.Hashes).HasCount().EqualTo(1);
    }

    [Test]
    public async Task RepositoryBuilder_ConfiguredForSha512Only_UsesOnlySha512()
    {
        // Arrange
        var builder = new RepositoryBuilder();
        var rootSigner = Ed25519Signer.Generate();
        var timestampSigner = Ed25519Signer.Generate();
        var snapshotSigner = Ed25519Signer.Generate();
        var targetsSigner = Ed25519Signer.Generate();
        
        builder.AddSigner("root", rootSigner)
               .AddSigner("timestamp", timestampSigner)
               .AddSigner("snapshot", snapshotSigner)
               .AddSigner("targets", targetsSigner)
               .SetHashAlgorithms("sha512")
               .AddTarget("test-file.txt", "Hello, SHA-512!"u8.ToArray());

        // Act
        var repository = builder.Build();

        // Assert - should only have SHA-512 hashes
        var targetFile = repository.Targets.Signed.TargetMap["test-file.txt"];
        await Assert.That(targetFile.Hashes).ContainsKey("sha512");
        await Assert.That(targetFile.Hashes).DoesNotContainKey("sha256");
        await Assert.That(targetFile.Hashes).HasCount().EqualTo(1);

        // Verify the SHA-512 hash is correct
        var expectedSha512 = Convert.ToHexString(SHA512.HashData("Hello, SHA-512!"u8)).ToLowerInvariant();
        await Assert.That(targetFile.Hashes["sha512"]).IsEqualTo(expectedSha512);
    }

    [Test]
    public async Task RepositoryBuilder_ConfiguredForBothHashAlgorithms_UsesBothSha256AndSha512()
    {
        // Arrange
        var builder = new RepositoryBuilder();
        var rootSigner = Ed25519Signer.Generate();
        var timestampSigner = Ed25519Signer.Generate();
        var snapshotSigner = Ed25519Signer.Generate();
        var targetsSigner = Ed25519Signer.Generate();
        var testContent = "Hello, dual hash world!"u8.ToArray();
        
        builder.AddSigner("root", rootSigner)
               .AddSigner("timestamp", timestampSigner)
               .AddSigner("snapshot", snapshotSigner)
               .AddSigner("targets", targetsSigner)
               .SetHashAlgorithms("sha256", "sha512")
               .AddTarget("dual-hash-file.txt", testContent);

        // Act
        var repository = builder.Build();

        // Assert - should have both hash algorithms
        var targetFile = repository.Targets.Signed.TargetMap["dual-hash-file.txt"];
        await Assert.That(targetFile.Hashes).ContainsKey("sha256");
        await Assert.That(targetFile.Hashes).ContainsKey("sha512");
        await Assert.That(targetFile.Hashes).HasCount().EqualTo(2);

        // Verify both hashes are correct
        var expectedSha256 = Convert.ToHexString(SHA256.HashData(testContent)).ToLowerInvariant();
        var expectedSha512 = Convert.ToHexString(SHA512.HashData(testContent)).ToLowerInvariant();
        
        await Assert.That(targetFile.Hashes["sha256"]).IsEqualTo(expectedSha256);
        await Assert.That(targetFile.Hashes["sha512"]).IsEqualTo(expectedSha512);

        // Verify metadata also uses both hash algorithms
        var snapshotMeta = repository.Snapshot.Signed.Meta["targets.json"];
        await Assert.That(snapshotMeta.Hashes).ContainsKey("sha256");
        await Assert.That(snapshotMeta.Hashes).ContainsKey("sha512");
        await Assert.That(snapshotMeta.Hashes).HasCount().EqualTo(2);

        var timestampMeta = repository.Timestamp.Signed.Meta["snapshot.json"];
        await Assert.That(timestampMeta.Hashes).ContainsKey("sha256");
        await Assert.That(timestampMeta.Hashes).ContainsKey("sha512");
        await Assert.That(timestampMeta.Hashes).HasCount().EqualTo(2);
    }

    [Test]
    public async Task RepositoryBuilder_SetHashAlgorithms_ThrowsOnUnsupportedAlgorithm()
    {
        // Arrange
        var builder = new RepositoryBuilder();

        // Act & Assert
        await Assert.That(() => builder.SetHashAlgorithms("sha256", "md5"))
              .ThrowsExactly<ArgumentException>()
              .WithMessage("Unsupported hash algorithm: md5. Supported: sha256, sha512 (Parameter 'algorithms')");
    }

    [Test]
    public async Task RepositoryBuilder_SetHashAlgorithms_ThrowsOnEmptyArray()
    {
        // Arrange
        var builder = new RepositoryBuilder();

        // Act & Assert
        await Assert.That(() => builder.SetHashAlgorithms())
              .ThrowsExactly<ArgumentException>()
              .WithMessage("At least one hash algorithm must be specified (Parameter 'algorithms')");
    }

    [Test]
    public async Task RepositoryBuilder_SetHashAlgorithms_HandlesNullArray()
    {
        // Arrange
        var builder = new RepositoryBuilder();

        // Act & Assert
        await Assert.That(() => builder.SetHashAlgorithms(null!))
              .ThrowsExactly<ArgumentException>()
              .WithMessage("At least one hash algorithm must be specified (Parameter 'algorithms')");
    }

    [Test]
    public async Task RepositoryBuilder_SetHashAlgorithms_IsCaseInsensitive()
    {
        // Arrange
        var builder = new RepositoryBuilder();
        var rootSigner = Ed25519Signer.Generate();
        var timestampSigner = Ed25519Signer.Generate();
        var snapshotSigner = Ed25519Signer.Generate();
        var targetsSigner = Ed25519Signer.Generate();
        
        builder.AddSigner("root", rootSigner)
               .AddSigner("timestamp", timestampSigner)
               .AddSigner("snapshot", snapshotSigner)
               .AddSigner("targets", targetsSigner)
               .SetHashAlgorithms("SHA256", "SHA512")  // Mixed case input
               .AddTarget("case-test.txt", "Testing case sensitivity"u8.ToArray());

        // Act
        var repository = builder.Build();

        // Assert - should work despite mixed case input
        var targetFile = repository.Targets.Signed.TargetMap["case-test.txt"];
        await Assert.That(targetFile.Hashes).ContainsKey("sha256");
        await Assert.That(targetFile.Hashes).ContainsKey("sha512");
        await Assert.That(targetFile.Hashes).HasCount().EqualTo(2);
    }

    [Test]
    public async Task RepositoryBuilder_MultipleTargets_AllUseConfiguredHashAlgorithms()
    {
        // Arrange
        var builder = new RepositoryBuilder();
        var rootSigner = Ed25519Signer.Generate();
        var timestampSigner = Ed25519Signer.Generate();
        var snapshotSigner = Ed25519Signer.Generate();
        var targetsSigner = Ed25519Signer.Generate();
        
        builder.AddSigner("root", rootSigner)
               .AddSigner("timestamp", timestampSigner)
               .AddSigner("snapshot", snapshotSigner)
               .AddSigner("targets", targetsSigner)
               .SetHashAlgorithms("sha256", "sha512")
               .AddTarget("file1.txt", "Content of file 1"u8.ToArray())
               .AddTarget("file2.txt", "Content of file 2"u8.ToArray())
               .AddTarget("subdir/file3.txt", "Content of file 3"u8.ToArray());

        // Act
        var repository = builder.Build();

        // Assert - all targets should have both hash algorithms
        foreach (var (fileName, targetFile) in repository.Targets.Signed.TargetMap)
        {
            await Assert.That(targetFile.Hashes).ContainsKey("sha256");
            await Assert.That(targetFile.Hashes).ContainsKey("sha512");
            await Assert.That(targetFile.Hashes).HasCount().EqualTo(2);
        }

        // Verify we have all three targets
        await Assert.That(repository.Targets.Signed.TargetMap).ContainsKey("file1.txt");
        await Assert.That(repository.Targets.Signed.TargetMap).ContainsKey("file2.txt");
        await Assert.That(repository.Targets.Signed.TargetMap).ContainsKey("subdir/file3.txt");
    }
}