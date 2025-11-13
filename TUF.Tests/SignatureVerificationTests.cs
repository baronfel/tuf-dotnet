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
/// Includes comprehensive cryptographic verification testing for all supported algorithms.
/// </summary>
public class SignatureVerificationTests
{
    /// <summary>
    /// Test Ed25519 signature creation and verification.
    /// Mirrors Go TUF signature verification patterns with actual crypto verification.
    /// </summary>
    [Test]
    public async Task TestEd25519SignatureVerification()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "test message for signing"u8.ToArray();

        // Act
        var signature = signer.SignBytes(testData);

        // Assert - Verify signature properties
        await Assert.That(signature.KeyId).IsEqualTo(signer.Key.GetKeyId());
        await Assert.That(signature.Sig).IsNotEmpty();
        
        // Assert - Verify actual cryptographic signature verification
        var isValidSignature = signer.Key.VerifyKeySignature(signature.Sig, testData);
        await Assert.That(isValidSignature).IsTrue();
    }

    /// <summary>
    /// Test signature verification with canonical JSON and actual cryptographic verification.
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
        
        // Assert - Verify actual cryptographic signature verification
        var isValidSignature = signer.Key.VerifyKeySignature(signature.Sig, canonicalBytes);
        await Assert.That(isValidSignature).IsTrue();

        // Verify canonical serialization is deterministic
        var canonicalBytes2 = Serializer.Serialize(metadata);
        await Assert.That(canonicalBytes).IsEquivalentTo(canonicalBytes2);
        
        // Verify that the same metadata produces the same signature with deterministic algorithms (Ed25519)
        var signature2 = signer.SignBytes(canonicalBytes2);
        await Assert.That(signature2.Sig).IsEqualTo(signature.Sig);
    }


    /// <summary>
    /// Test multiple signatures on same data with cryptographic verification.
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
        
        // Assert - Both signatures should cryptographically verify with their respective keys
        var isValidSignature1 = signer1.Key.VerifyKeySignature(signature1.Sig, testData);
        var isValidSignature2 = signer2.Key.VerifyKeySignature(signature2.Sig, testData);
        await Assert.That(isValidSignature1).IsTrue();
        await Assert.That(isValidSignature2).IsTrue();
        
        // Assert - Cross-verification should fail (signature1 should not verify with key2)
        var crossVerify1 = signer2.Key.VerifyKeySignature(signature1.Sig, testData);
        var crossVerify2 = signer1.Key.VerifyKeySignature(signature2.Sig, testData);
        await Assert.That(crossVerify1).IsFalse();
        await Assert.That(crossVerify2).IsFalse();
    }


    /// <summary>
    /// Test that different data produces different signatures with cryptographic verification.
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
        
        // Assert - Both signatures should verify with the correct data
        var verifyData1 = signer.Key.VerifyKeySignature(signature1.Sig, data1);
        var verifyData2 = signer.Key.VerifyKeySignature(signature2.Sig, data2);
        await Assert.That(verifyData1).IsTrue();
        await Assert.That(verifyData2).IsTrue();
        
        // Assert - Cross-verification should fail (signature1 should not verify with data2)
        var crossVerify1 = signer.Key.VerifyKeySignature(signature1.Sig, data2);
        var crossVerify2 = signer.Key.VerifyKeySignature(signature2.Sig, data1);
        await Assert.That(crossVerify1).IsFalse();
        await Assert.That(crossVerify2).IsFalse();
    }

    /// <summary>
    /// Test signature hex encoding format with verification.
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
            
            // The hex-decoded signature should still verify
            var isValid = signer.Key.VerifyKeySignature(signature.Sig, testData);
            await Assert.That(isValid).IsTrue();
        }
        catch (FormatException)
        {
            throw new Exception("Signature is not valid hex format");
        }
    }

    /// <summary>
    /// Test RSA signature creation and cryptographic verification.
    /// Verifies the complete RSA-PSS-SHA256 signature workflow.
    /// </summary>
    [Test]
    public async Task TestRsaSignatureVerification()
    {
        // Arrange
        var signer = RsaSigner.Generate(2048); // Use minimum key size for faster test
        var testData = "test message for RSA signing"u8.ToArray();

        // Act
        var signature = signer.SignBytes(testData);

        // Assert - Verify signature properties
        await Assert.That(signature.KeyId).IsEqualTo(signer.Key.GetKeyId());
        await Assert.That(signature.Sig).IsNotEmpty();
        
        // Assert - Verify actual cryptographic signature verification
        var isValidSignature = signer.Key.VerifyKeySignature(signature.Sig, testData);
        await Assert.That(isValidSignature).IsTrue();
        
        // Assert - Verify key type/scheme pinning
        await Assert.That(signer.Key.KeyType).IsEqualTo("rsa");
        await Assert.That(signer.Key.Scheme).IsEqualTo("rsassa-pss-sha256");
    }

    /// <summary>
    /// Test ECDSA signature creation and cryptographic verification.
    /// Verifies the complete ECDSA-SHA256-P256 signature workflow.
    /// </summary>
    [Test]
    public async Task TestEcdsaSignatureVerification()
    {
        // Arrange
        var signer = EcdsaSigner.Generate();
        var testData = "test message for ECDSA signing"u8.ToArray();

        // Act
        var signature = signer.SignBytes(testData);

        // Assert - Verify signature properties
        await Assert.That(signature.KeyId).IsEqualTo(signer.Key.GetKeyId());
        await Assert.That(signature.Sig).IsNotEmpty();
        
        // Assert - Verify actual cryptographic signature verification
        var isValidSignature = signer.Key.VerifyKeySignature(signature.Sig, testData);
        await Assert.That(isValidSignature).IsTrue();
        
        // Assert - Verify key type/scheme pinning
        await Assert.That(signer.Key.KeyType).IsEqualTo("ecdsa");
        await Assert.That(signer.Key.Scheme).IsEqualTo("ecdsa-sha2-nistp256");
    }

    /// <summary>
    /// Test that ECDSA produces non-deterministic signatures (unlike Ed25519).
    /// </summary>
    [Test]
    public async Task TestEcdsaNonDeterministicSignatures()
    {
        // Arrange
        var signer = EcdsaSigner.Generate();
        var testData = "test message for ECDSA non-deterministic test"u8.ToArray();

        // Act - Sign the same data twice
        var signature1 = signer.SignBytes(testData);
        var signature2 = signer.SignBytes(testData);

        // Assert - ECDSA signatures should be different each time (non-deterministic)
        await Assert.That(signature1.KeyId).IsEqualTo(signature2.KeyId);
        await Assert.That(signature1.Sig).IsNotEqualTo(signature2.Sig);
        
        // Assert - But both should cryptographically verify
        var isValid1 = signer.Key.VerifyKeySignature(signature1.Sig, testData);
        var isValid2 = signer.Key.VerifyKeySignature(signature2.Sig, testData);
        await Assert.That(isValid1).IsTrue();
        await Assert.That(isValid2).IsTrue();
    }

    /// <summary>
    /// Test cross-algorithm signature verification failures.
    /// Ensures that signatures from one algorithm don't verify with keys from another.
    /// </summary>
    [Test]
    public async Task TestCrossAlgorithmVerificationFailures()
    {
        // Arrange
        var ed25519Signer = Ed25519Signer.Generate();
        var rsaSigner = RsaSigner.Generate(2048);
        var ecdsaSigner = EcdsaSigner.Generate();
        var testData = "test cross-algorithm verification"u8.ToArray();

        // Act - Create signatures with each algorithm
        var ed25519Signature = ed25519Signer.SignBytes(testData);
        var rsaSignature = rsaSigner.SignBytes(testData);
        var ecdsaSignature = ecdsaSigner.SignBytes(testData);

        // Assert - Each signature should verify with its own key
        await Assert.That(ed25519Signer.Key.VerifyKeySignature(ed25519Signature.Sig, testData)).IsTrue();
        await Assert.That(rsaSigner.Key.VerifyKeySignature(rsaSignature.Sig, testData)).IsTrue();
        await Assert.That(ecdsaSigner.Key.VerifyKeySignature(ecdsaSignature.Sig, testData)).IsTrue();
        
        // Assert - Cross-algorithm verification should fail
        await Assert.That(ed25519Signer.Key.VerifyKeySignature(rsaSignature.Sig, testData)).IsFalse();
        await Assert.That(ed25519Signer.Key.VerifyKeySignature(ecdsaSignature.Sig, testData)).IsFalse();
        await Assert.That(rsaSigner.Key.VerifyKeySignature(ed25519Signature.Sig, testData)).IsFalse();
        await Assert.That(rsaSigner.Key.VerifyKeySignature(ecdsaSignature.Sig, testData)).IsFalse();
        await Assert.That(ecdsaSigner.Key.VerifyKeySignature(ed25519Signature.Sig, testData)).IsFalse();
        await Assert.That(ecdsaSigner.Key.VerifyKeySignature(rsaSignature.Sig, testData)).IsFalse();
    }

    /// <summary>
    /// Test that invalid signatures fail verification gracefully.
    /// </summary>
    [Test]
    public async Task TestInvalidSignatureVerificationFailures()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "test invalid signature verification"u8.ToArray();
        var validSignature = signer.SignBytes(testData);

        // Test various types of invalid signatures
        var invalidHexSignature = "invalid_hex_signature";
        var emptySignature = "";
        var tooShortSignature = "abc123";
        var wrongLengthSignature = "0123456789abcdef"; // Valid hex but wrong length
        
        // Create a signature that's valid hex but wrong content
        var corruptedSignature = new string(validSignature.Sig.Select((c, i) => i == 0 ? (c == '0' ? '1' : '0') : c).ToArray());

        // Assert - All invalid signatures should fail verification
        await Assert.That(signer.Key.VerifyKeySignature(invalidHexSignature, testData)).IsFalse();
        await Assert.That(signer.Key.VerifyKeySignature(emptySignature, testData)).IsFalse();
        await Assert.That(signer.Key.VerifyKeySignature(tooShortSignature, testData)).IsFalse();
        await Assert.That(signer.Key.VerifyKeySignature(wrongLengthSignature, testData)).IsFalse();
        await Assert.That(signer.Key.VerifyKeySignature(corruptedSignature, testData)).IsFalse();
    }

    /// <summary>
    /// Test metadata signature verification using the full TUF metadata wrapper.
    /// This tests the signature verification in the context of actual TUF metadata structures.
    /// </summary>
    [Test]
    public async Task TestMetadataSignatureVerification()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        
        // Create a simple root metadata
        var rootContent = new Root
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

        // Create signed bytes (canonical JSON of the signed portion)
        var signedBytes = Serializer.Serialize(rootContent);
        var signature = signer.SignBytes(signedBytes);

        // Create the full metadata structure
        var metadata = new Metadata<Root>
        {
            Signed = rootContent,
            Signatures = [signature]
        };

        // Act - Get canonical signed bytes using the metadata extension method
        var canonicalSignedBytes = metadata.GetSignedBytes();

        // Assert - The signature should verify against the canonical signed bytes
        var isValidSignature = signer.Key.VerifyKeySignature(signature.Sig, canonicalSignedBytes);
        await Assert.That(isValidSignature).IsTrue();
        
        // Assert - The canonical bytes should match what we manually serialized
        await Assert.That(canonicalSignedBytes).IsEquivalentTo(signedBytes);
    }
}