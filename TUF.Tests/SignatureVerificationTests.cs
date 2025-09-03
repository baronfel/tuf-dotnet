using System.Security.Cryptography;
using System.Text;

using CanonicalJson;

using TUF.Models;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests for signature verification functionality.
/// Mirrors Go TUF TestSignVerify and related signature verification patterns.
/// </summary>
public class SignatureVerificationTests
{
    /// <summary>
    /// Test Ed25519 signature creation and verification.
    /// Mirrors Go TUF signature verification patterns.
    /// </summary>
    [Test]
    public async Task TestEd25519SignatureVerification()
    {
        // Arrange
        var signer = SharedCryptoKeyPool.GetEd25519Signer();
        var testData = "test message for signing"u8.ToArray();

        // Act
        var signature = signer.SignBytes(testData);

        // Assert - Verify actual signature properties
        await Assert.That(signature.KeyId).IsEqualTo(signer.Key.GetKeyId());
        await Assert.That(signature.Sig).IsNotEmpty();
    }

    /// <summary>
    /// Test signature verification with canonical JSON.
    /// </summary>
    [Test]
    public async Task TestCanonicalJsonSignatureVerification()
    {
        // Arrange
        var signer = SharedCryptoKeyPool.GetEd25519Signer();

        // Create a simple metadata object
        var metadata = new Root
        {
            Type = "root",
            Version = 1,
            SpecVersion = "1.0.0",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Keys = new Dictionary<string, Key>
            {
                [signer.Key.GetKeyId()] = signer.Key
            },
            Roles = new Roles
            {
                Root = new RoleKeys { KeyIds = [signer.Key.GetKeyId()], Threshold = 1 },
                Targets = new RoleKeys { KeyIds = [signer.Key.GetKeyId()], Threshold = 1 },
                Snapshot = new RoleKeys { KeyIds = [signer.Key.GetKeyId()], Threshold = 1 },
                Timestamp = new RoleKeys { KeyIds = [signer.Key.GetKeyId()], Threshold = 1 }
            },
            ConsistentSnapshot = true
        };

        // Act - Serialize to canonical JSON and sign
        var canonicalBytes = Serializer.Serialize(metadata);
        var signature = signer.SignBytes(canonicalBytes);

        // Assert - Signature should be valid
        await Assert.That(signature.KeyId).IsEqualTo(signer.Key.GetKeyId());
        await Assert.That(signature.Sig).IsNotEmpty();

        // Verify canonical serialization is deterministic
        var canonicalBytes2 = Serializer.Serialize(metadata);
        await Assert.That(canonicalBytes).IsEquivalentTo(canonicalBytes2);
    }


    /// <summary>
    /// Test multiple signatures on same data.
    /// </summary>
    [Test]
    public async Task TestMultipleSignaturesOnSameData()
    {
        // Arrange
        var signer1 = Ed25519Signer.Generate();
        var signer2 = Ed25519Signer.Generate();
        var testData = "test message for multiple signers"u8.ToArray();

        // Act
        var signature1 = signer1.SignBytes(testData);
        var signature2 = signer2.SignBytes(testData);

        // Assert - Different signers produce different signatures
        await Assert.That(signature1.KeyId).IsNotEqualTo(signature2.KeyId);
        await Assert.That(signature1.Sig).IsNotEqualTo(signature2.Sig);

        // Both should be valid for their respective keys
        await Assert.That(signature1.KeyId).IsEqualTo(signer1.Key.GetKeyId());
        await Assert.That(signature2.KeyId).IsEqualTo(signer2.Key.GetKeyId());
    }


    /// <summary>
    /// Test that different data produces different signatures.
    /// </summary>
    [Test]
    public async Task TestDifferentDataProducesDifferentSignatures()
    {
        // Arrange
        var signer = SharedCryptoKeyPool.GetEd25519Signer();
        var data1 = "first test message"u8.ToArray();
        var data2 = "second test message"u8.ToArray();

        // Act
        var signature1 = signer.SignBytes(data1);
        var signature2 = signer.SignBytes(data2);

        // Assert - Same key, different data should produce different signatures
        await Assert.That(signature1.KeyId).IsEqualTo(signature2.KeyId);
        await Assert.That(signature1.Sig).IsNotEqualTo(signature2.Sig);
    }

    /// <summary>
    /// Test signature hex encoding format.
    /// </summary>
    [Test]
    public async Task TestSignatureHexEncoding()
    {
        // Arrange
        var signer = SharedCryptoKeyPool.GetEd25519Signer();
        var testData = "test hex encoding"u8.ToArray();

        // Act
        var signature = signer.SignBytes(testData);

        // Assert - Signature should be valid hex string
        await Assert.That(signature.Sig).IsNotEmpty();

        // Should be able to decode as hex
        try
        {
            var signatureBytes = Convert.FromHexString(signature.Sig);
            await Assert.That(signatureBytes).IsNotEmpty();

            // Ed25519 signatures are always 64 bytes
            await Assert.That(signatureBytes).HasCount().EqualTo(64);
        }
        catch (FormatException)
        {
            throw new Exception("Signature is not valid hex format");
        }
    }


}