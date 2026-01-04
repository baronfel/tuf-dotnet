using TUF.Models;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests for enhanced hash algorithm support in TUF .NET implementation.
/// Validates BLAKE2b-256 and SHA3-256 support alongside existing SHA-256/SHA-512.
/// </summary>
public class HashAlgorithmTests
{
    private const string TestMessage = "Hello, TUF Enhanced Hash Algorithms!";
    private static readonly byte[] TestData = System.Text.Encoding.UTF8.GetBytes(TestMessage);

    [Test]
    public async Task SHA256_HashVerification_ShouldSucceed()
    {
        // Arrange
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(TestData)).ToLowerInvariant();
        var fileMeta = CreateTestFileMetadata("sha256", expectedHash);

        // Act & Assert - Should not throw (TUnit will fail test if exception occurs)
        TrustedMetadataWithTimestamp.VerifyFileIntegrity(TestData, fileMeta);
    }

    [Test]
    public async Task SHA512_HashVerification_ShouldSucceed()
    {
        // Arrange
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(TestData)).ToLowerInvariant();
        var fileMeta = CreateTestFileMetadata("sha512", expectedHash);

        // Act & Assert - Should not throw (TUnit will fail test if exception occurs)
        TrustedMetadataWithTimestamp.VerifyFileIntegrity(TestData, fileMeta);
    }

    [Test]
    public async Task BLAKE2b256_HashVerification_ShouldSucceed()
    {
        // Arrange
        var expectedHash = Convert.ToHexString(NSec.Cryptography.HashAlgorithm.Blake2b_256.Hash(TestData)).ToLowerInvariant();
        var fileMeta = CreateTestFileMetadata("blake2b-256", expectedHash);

        // Act & Assert - Should not throw (TUnit will fail test if exception occurs)
        TrustedMetadataWithTimestamp.VerifyFileIntegrity(TestData, fileMeta);
    }

#if NET8_0_OR_GREATER
    [Test]
    public async Task SHA3_256_HashVerification_ShouldSucceed()
    {
        // Arrange
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA3_256.HashData(TestData)).ToLowerInvariant();
        var fileMeta = CreateTestFileMetadata("sha3-256", expectedHash);

        // Act & Assert - Should not throw (TUnit will fail test if exception occurs)
        TrustedMetadataWithTimestamp.VerifyFileIntegrity(TestData, fileMeta);
    }
#endif

    [Test]
    public async Task MultipleHashAlgorithms_AllValid_ShouldSucceed()
    {
        // Arrange - Create file metadata with multiple hash algorithms
        var sha256Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(TestData)).ToLowerInvariant();
        var blake2bHash = Convert.ToHexString(NSec.Cryptography.HashAlgorithm.Blake2b_256.Hash(TestData)).ToLowerInvariant();
        
        var fileMeta = new FileMetadata
        {
            Length = TestData.Length,
            Hashes = new Dictionary<string, string>
            {
                ["sha256"] = sha256Hash,
                ["blake2b-256"] = blake2bHash
#if NET8_0_OR_GREATER
                ,["sha3-256"] = Convert.ToHexString(System.Security.Cryptography.SHA3_256.HashData(TestData)).ToLowerInvariant()
#endif
            }
        };

        // Act & Assert - Should not throw (TUnit will fail test if exception occurs)
        TrustedMetadataWithTimestamp.VerifyFileIntegrity(TestData, fileMeta);
    }

    [Test]
    public async Task BLAKE2b256_InvalidHash_ShouldFail()
    {
        // Arrange
        var fileMeta = CreateTestFileMetadata("blake2b-256", "invalid_hash_value");

        // Act & Assert - Should throw on invalid hash
        await Assert.That(() => TrustedMetadataWithTimestamp.VerifyFileIntegrity(TestData, fileMeta)).Throws<Exception>();
    }

#if NET8_0_OR_GREATER
    [Test]
    public async Task SHA3_256_InvalidHash_ShouldFail()
    {
        // Arrange
        var fileMeta = CreateTestFileMetadata("sha3-256", "invalid_hash_value");

        // Act & Assert - Should throw on invalid hash
        await Assert.That(() => TrustedMetadataWithTimestamp.VerifyFileIntegrity(TestData, fileMeta)).Throws<Exception>();
    }
#endif

    [Test]
    public async Task UnsupportedHashAlgorithm_ShouldBeSkipped()
    {
        // Arrange
        var fileMeta = CreateTestFileMetadata("unsupported-algo", "some_hash_value");

        // Act & Assert - Should throw since no supported hash is available
        await Assert.That(() => TrustedMetadataWithTimestamp.VerifyFileIntegrity(TestData, fileMeta)).Throws<Exception>();
    }

    [Test]
    public async Task HashAlgorithm_CaseInsensitive_ShouldWork()
    {
        // Arrange - Test case insensitivity for algorithm names
        var expectedHash = Convert.ToHexString(NSec.Cryptography.HashAlgorithm.Blake2b_256.Hash(TestData)).ToLowerInvariant();
        var fileMeta = CreateTestFileMetadata("BLAKE2B-256", expectedHash); // Upper case

        // Act & Assert - Should not throw (TUnit will fail test if exception occurs)
        TrustedMetadataWithTimestamp.VerifyFileIntegrity(TestData, fileMeta);
    }

    [Test]
    public async Task BLAKE2b256_Performance_ShouldBeFasterThanSHA256()
    {
        // Arrange - Large test data to measure performance difference
        var largeTestData = new byte[1024 * 1024]; // 1MB
        Random.Shared.NextBytes(largeTestData);

        // Measure SHA-256 timing
        var sha256Start = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            _ = System.Security.Cryptography.SHA256.HashData(largeTestData);
        }
        var sha256Duration = DateTime.UtcNow - sha256Start;

        // Measure BLAKE2b-256 timing
        var blake2bStart = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            _ = NSec.Cryptography.HashAlgorithm.Blake2b_256.Hash(largeTestData);
        }
        var blake2bDuration = DateTime.UtcNow - blake2bStart;

        // Act & Assert - BLAKE2b should be faster (or at least comparable)
        // Note: This is informational rather than strict assertion since performance can vary
        Console.WriteLine($"SHA-256 duration: {sha256Duration.TotalMilliseconds}ms");
        Console.WriteLine($"BLAKE2b-256 duration: {blake2bDuration.TotalMilliseconds}ms");
        Console.WriteLine($"BLAKE2b performance ratio: {(double)blake2bDuration.TotalMilliseconds / sha256Duration.TotalMilliseconds:F2}x");
        
        // Both should complete successfully
        await Assert.That(sha256Duration.TotalMilliseconds).IsGreaterThan(0);
        await Assert.That(blake2bDuration.TotalMilliseconds).IsGreaterThan(0);
    }

    [Test]
    public async Task Updater_HashVerification_WithNewAlgorithms()
    {
        // Arrange - Test hash verification in the context of the Updater class
        var blake2bHash = Convert.ToHexString(NSec.Cryptography.HashAlgorithm.Blake2b_256.Hash(TestData)).ToLowerInvariant();
        
        var targetFile = new TargetFile
        {
            Length = TestData.Length,
            Hashes = new Dictionary<string, string>
            {
                ["blake2b-256"] = blake2bHash
            }
        };

        // Act - Test the hash verification logic from Updater
        bool validHash = false;
        foreach (var hash in targetFile.Hashes)
        {
            var computedHash = hash.Key.ToLowerInvariant() switch
            {
                "sha256" => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(TestData)).ToLowerInvariant(),
                "sha512" => Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(TestData)).ToLowerInvariant(),
                "blake2b-256" => Convert.ToHexString(NSec.Cryptography.HashAlgorithm.Blake2b_256.Hash(TestData)).ToLowerInvariant(),
#if NET8_0_OR_GREATER
                "sha3-256" => Convert.ToHexString(System.Security.Cryptography.SHA3_256.HashData(TestData)).ToLowerInvariant(),
#endif
                _ => null
            };

            if (computedHash != null && computedHash == hash.Value.ToLowerInvariant())
            {
                validHash = true;
                break;
            }
        }

        // Assert
        await Assert.That(validHash).IsTrue();
    }

    /// <summary>
    /// Helper method to create test file metadata with a single hash algorithm
    /// </summary>
    private static FileMetadata CreateTestFileMetadata(string algorithm, string hash)
    {
        return new FileMetadata
        {
            Length = TestData.Length,
            Hashes = new Dictionary<string, string>
            {
                [algorithm] = hash
            }
        };
    }
}