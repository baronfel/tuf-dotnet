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
        var signer = Ed25519Signer.Generate();
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
        var signer = Ed25519Signer.Generate();

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
        var signer = Ed25519Signer.Generate();
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
        var signer = Ed25519Signer.Generate();
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

    /// <summary>
    /// Test actual Ed25519 signature verification functionality.
    /// This tests the critical security path - ensuring signatures can be verified correctly.
    /// </summary>
    [Test]
    public async Task TestEd25519SignatureActualVerification()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "test data for ed25519 verification"u8.ToArray();

        // Act - Sign the data
        var signature = signer.SignBytes(testData);

        // Assert - Key should verify its own signature
        var isValid = signer.Key.VerifySignature(signature.Sig, testData);
        await Assert.That(isValid).IsTrue();
    }

    /// <summary>
    /// Test RSA-PSS signature verification functionality.
    /// Ensures RSA signatures created by our signer can be verified.
    /// </summary>
    [Test]
    public async Task TestRsaSignatureActualVerification()
    {
        // Arrange
        var signer = RsaSigner.Generate(2048);
        var testData = "test data for rsa verification"u8.ToArray();

        // Act - Sign the data
        var signature = signer.SignBytes(testData);

        // Assert - Key should verify its own signature
        var isValid = signer.Key.VerifySignature(signature.Sig, testData);
        await Assert.That(isValid).IsTrue();
    }

    /// <summary>
    /// Test ECDSA signature verification functionality.
    /// Ensures ECDSA signatures created by our signer can be verified.
    /// </summary>
    [Test]
    public async Task TestEcdsaSignatureActualVerification()
    {
        // Arrange
        var signer = EcdsaSigner.Generate();
        var testData = "test data for ecdsa verification"u8.ToArray();

        // Act - Sign the data
        var signature = signer.SignBytes(testData);

        // Assert - Key should verify its own signature
        var isValid = signer.Key.VerifySignature(signature.Sig, testData);
        await Assert.That(isValid).IsTrue();
    }

    /// <summary>
    /// Test that invalid signatures are properly rejected.
    /// This is critical for security - corrupted or malicious signatures must be detected.
    /// </summary>
    [Test]
    public async Task TestInvalidSignatureRejection()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "test data for invalid signature test"u8.ToArray();

        // Act & Assert - Invalid hex string should be rejected
        var isValidInvalidHex = signer.Key.VerifySignature("invalid_hex_string", testData);
        await Assert.That(isValidInvalidHex).IsFalse();

        // Act & Assert - Valid hex but wrong length should be rejected
        var shortSignature = "1234567890abcdef"; // Too short for any signature type
        var isValidShort = signer.Key.VerifySignature(shortSignature, testData);
        await Assert.That(isValidShort).IsFalse();

        // Act & Assert - Valid hex but wrong signature should be rejected
        var wrongSignature = "0".PadLeft(128, '0'); // Valid hex, wrong signature
        var isValidWrong = signer.Key.VerifySignature(wrongSignature, testData);
        await Assert.That(isValidWrong).IsFalse();
    }

    /// <summary>
    /// Test that signatures from one key cannot be verified by a different key.
    /// This prevents signature substitution attacks.
    /// </summary>
    [Test]
    public async Task TestSignatureMismatchDetection()
    {
        // Arrange
        var signer1 = Ed25519Signer.Generate();
        var signer2 = Ed25519Signer.Generate();
        var testData = "test data for signature mismatch test"u8.ToArray();

        // Act - Sign with first key
        var signature = signer1.SignBytes(testData);

        // Assert - Second key should not verify first key's signature
        var isValid = signer2.Key.VerifySignature(signature.Sig, testData);
        await Assert.That(isValid).IsFalse();
    }

    /// <summary>
    /// Test that changing the data invalidates the signature.
    /// This ensures data integrity protection.
    /// </summary>
    [Test]
    public async Task TestDataIntegrityProtection()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var originalData = "original test data"u8.ToArray();
        var tamperedData = "tampered test data"u8.ToArray();

        // Act - Sign original data
        var signature = signer.SignBytes(originalData);

        // Assert - Signature should be valid for original data
        var isValidOriginal = signer.Key.VerifySignature(signature.Sig, originalData);
        await Assert.That(isValidOriginal).IsTrue();

        // Assert - Signature should be invalid for tampered data
        var isValidTampered = signer.Key.VerifySignature(signature.Sig, tamperedData);
        await Assert.That(isValidTampered).IsFalse();
    }

    /// <summary>
    /// Test cross-algorithm verification failures.
    /// Ensures that RSA signatures can't be verified as Ed25519, etc.
    /// </summary>
    [Test]
    public async Task TestCrossAlgorithmRejection()
    {
        // Arrange
        var ed25519Signer = Ed25519Signer.Generate();
        var rsaSigner = RsaSigner.Generate(2048);
        var ecdsaSigner = EcdsaSigner.Generate();
        var testData = "cross algorithm test data"u8.ToArray();

        // Act - Create signatures
        var ed25519Signature = ed25519Signer.SignBytes(testData);
        var rsaSignature = rsaSigner.SignBytes(testData);
        var ecdsaSignature = ecdsaSigner.SignBytes(testData);

        // Assert - Ed25519 key should not verify RSA signatures
        var ed25519VerifiesRsa = ed25519Signer.Key.VerifySignature(rsaSignature.Sig, testData);
        await Assert.That(ed25519VerifiesRsa).IsFalse();

        // Assert - RSA key should not verify Ed25519 signatures
        var rsaVerifiesEd25519 = rsaSigner.Key.VerifySignature(ed25519Signature.Sig, testData);
        await Assert.That(rsaVerifiesEd25519).IsFalse();

        // Assert - ECDSA key should not verify Ed25519 signatures
        var ecdsaVerifiesEd25519 = ecdsaSigner.Key.VerifySignature(ed25519Signature.Sig, testData);
        await Assert.That(ecdsaVerifiesEd25519).IsFalse();
    }

    /// <summary>
    /// Test verification performance and determinism.
    /// Ensures verification is consistent and reasonably fast.
    /// </summary>
    [Test]
    public async Task TestVerificationConsistencyAndPerformance()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "performance and consistency test data"u8.ToArray();
        var signature = signer.SignBytes(testData);

        // Act & Assert - Multiple verification attempts should give same result
        for (int i = 0; i < 10; i++)
        {
            var isValid = signer.Key.VerifySignature(signature.Sig, testData);
            await Assert.That(isValid).IsTrue();
        }

        // Assert - Verification should be fast enough for practical use
        var startTime = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            signer.Key.VerifySignature(signature.Sig, testData);
        }
        var elapsed = DateTime.UtcNow - startTime;
        
        // 100 verifications should complete in less than 1 second (very generous)
        await Assert.That(elapsed.TotalSeconds).IsLessThan(1.0);
    }
}