using System.Text;

using CanonicalJson;

using TUF.Models;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests for key management functionality that validates actual business logic,
/// not trivial object construction.
/// </summary>
public class KeyManagementTests
{
    /// <summary>
    /// Test that Ed25519 signer generates consistent key type and scheme.
    /// This is important for TUF specification compliance.
    /// </summary>
    [Test]
    [TestCategories.SmokeTest("Ed25519 key type validation")]
    [TestCategories.FastTest("Uses cached signers for improved performance")]
    public async Task TestEd25519KeyTypeConsistency()
    {
        // Arrange - Use cached signer for better performance
        var ed25519Signer = CachedTestData.GetEd25519Signer();

        // Act & Assert - Ed25519 key should have consistent type and scheme per TUF spec
        await Assert.That(ed25519Signer.Key.KeyType).IsEqualTo("ed25519");
        await Assert.That(ed25519Signer.Key.Scheme).IsEqualTo("ed25519");
        await Assert.That(ed25519Signer.Key.KeyVal.Public).IsNotEmpty();
    }

    /// <summary>
    /// Test that generated Ed25519 signers produce unique keys.
    /// Important for security - no key reuse.
    /// </summary>
    [Test]
    [TestCategories.ComprehensiveTest("Security validation for unique key generation")]
    public async Task TestGeneratedSignersAreUnique()
    {
        // Arrange & Act - This test needs fresh generation to verify uniqueness
        var signer1 = Ed25519Signer.Generate();
        var signer2 = Ed25519Signer.Generate();
        var signer3 = Ed25519Signer.Generate();

        // Assert - All keys should be different
        await Assert.That(signer1.Key.GetKeyId()).IsNotEqualTo(signer2.Key.GetKeyId());
        await Assert.That(signer1.Key.GetKeyId()).IsNotEqualTo(signer3.Key.GetKeyId());
        await Assert.That(signer2.Key.GetKeyId()).IsNotEqualTo(signer3.Key.GetKeyId());

        await Assert.That(signer1.Key.KeyVal.Public).IsNotEqualTo(signer2.Key.KeyVal.Public);
        await Assert.That(signer1.Key.KeyVal.Public).IsNotEqualTo(signer3.Key.KeyVal.Public);
        await Assert.That(signer2.Key.KeyVal.Public).IsNotEqualTo(signer3.Key.KeyVal.Public);
    }

    /// <summary>
    /// Test that key IDs are computed consistently.
    /// Important for signature verification - KeyId must be deterministic.
    /// </summary>
    [Test]
    [TestCategories.SmokeTest("Key ID determinism validation")]
    [TestCategories.FastTest("Uses cached signers for improved performance")]
    public async Task TestKeyIdDeterministic()
    {
        // Arrange - Use cached signer for better performance
        var signer = CachedTestData.GetEd25519Signer();

        // Act - Get key ID multiple times
        var keyId1 = signer.Key.GetKeyId();
        var keyId2 = signer.Key.GetKeyId();
        var keyId3 = signer.Key.GetKeyId();

        // Assert - Should always be the same for the same key
        await Assert.That(keyId1).IsEqualTo(keyId2);
        await Assert.That(keyId2).IsEqualTo(keyId3);
        await Assert.That(keyId1).IsNotEmpty();

        // Should be lowercase hex
        await Assert.That(keyId1).Matches("^[0-9a-f]+$");
    }
}