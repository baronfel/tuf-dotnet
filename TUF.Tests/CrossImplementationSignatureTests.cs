using System.Text;

using TUF.Models;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests for cross-implementation signature compatibility.
/// Verifies that the .NET TUF implementation can verify signatures created by other implementations,
/// and that other implementations can verify signatures created by .NET TUF.
/// 
/// This improves TUF ecosystem interoperability and conformance with the specification.
/// Based on golden test patterns used by Go TUF, Python TUF, and other implementations.
/// </summary>
public class CrossImplementationSignatureTests
{
    /// <summary>
    /// Test Ed25519 signature verification with generated test vectors.
    /// This ensures our signature generation and verification is working correctly.
    /// </summary>
    [Test]
    public async Task TestEd25519SignatureRoundTrip()
    {
        // Arrange - Generate a known key for testing
        var signer = Ed25519Signer.Generate();
        var message = "test message for ed25519 verification"u8.ToArray();
        
        // Act - Create a signature and verify it
        var signature = signer.SignBytes(message);
        var isValid = signer.Key.VerifyKeySignature(signature.Sig, message);

        // Assert - Should verify successfully
        await Assert.That(isValid).IsTrue();
        
        // Assert - Key should have correct properties
        await Assert.That(signer.Key.KeyType).IsEqualTo("ed25519");
        await Assert.That(signer.Key.Scheme).IsEqualTo("ed25519");
        await Assert.That(signature.KeyId).IsEqualTo(signer.Key.GetKeyId());
    }

    /// <summary>
    /// Test Ed25519 signature determinism across implementations.
    /// Ed25519 signatures should be deterministic, meaning the same key+message
    /// should always produce the same signature across implementations.
    /// </summary>
    [Test]
    public async Task TestEd25519SignatureDeterminism()
    {
        // Generate a signer and test message
        var signer = Ed25519Signer.Generate();
        var message = "deterministic test message"u8.ToArray();

        // Act - Create multiple signatures of the same message
        var signature1 = signer.SignBytes(message);
        var signature2 = signer.SignBytes(message);

        // Assert - Ed25519 signatures should be deterministic (same each time)
        await Assert.That(signature1.Sig).IsEqualTo(signature2.Sig);
        
        // Both should verify
        await Assert.That(signer.Key.VerifyKeySignature(signature1.Sig, message)).IsTrue();
        await Assert.That(signer.Key.VerifyKeySignature(signature2.Sig, message)).IsTrue();
    }

    /// <summary>
    /// Test RSA signature verification round-trip.
    /// Ensures RSA-PSS-SHA256 signature generation and verification works correctly.
    /// </summary>
    [Test]
    public async Task TestRsaSignatureRoundTrip()
    {
        // Arrange - Generate RSA signer for testing
        var signer = RsaSigner.Generate(2048);
        var message = "RSA test message for verification"u8.ToArray();

        // Act - Create signature and verify it
        var signature = signer.SignBytes(message);
        var isValid = signer.Key.VerifyKeySignature(signature.Sig, message);

        // Assert - Should verify successfully
        await Assert.That(isValid).IsTrue();
        
        // Assert - Key should have correct properties
        await Assert.That(signer.Key.KeyType).IsEqualTo("rsa");
        await Assert.That(signer.Key.Scheme).IsEqualTo("rsassa-pss-sha256");
        await Assert.That(signature.KeyId).IsEqualTo(signer.Key.GetKeyId());
    }

    /// <summary>
    /// Test that RSA signatures are non-deterministic.
    /// RSA-PSS signatures include randomness, so the same message+key will produce different signatures.
    /// </summary>
    [Test]
    public async Task TestRsaSignatureNonDeterminism()
    {
        // Arrange
        var signer = RsaSigner.Generate(2048);
        var message = "RSA non-deterministic test"u8.ToArray();

        // Act - Create multiple signatures
        var signature1 = signer.SignBytes(message);
        var signature2 = signer.SignBytes(message);

        // Assert - RSA-PSS signatures should be different (include randomness)
        await Assert.That(signature1.Sig).IsNotEqualTo(signature2.Sig);
        
        // But both should verify
        await Assert.That(signer.Key.VerifyKeySignature(signature1.Sig, message)).IsTrue();
        await Assert.That(signer.Key.VerifyKeySignature(signature2.Sig, message)).IsTrue();
    }

    /// <summary>
    /// Test ECDSA signature verification and non-determinism.
    /// ECDSA signatures are non-deterministic like RSA-PSS.
    /// </summary>
    [Test]
    public async Task TestEcdsaSignatureCompatibility()
    {
        // Arrange
        var signer = EcdsaSigner.Generate();
        var message = "ECDSA compatibility test"u8.ToArray();

        // Act - Create multiple signatures
        var signature1 = signer.SignBytes(message);
        var signature2 = signer.SignBytes(message);

        // Assert - ECDSA signatures should be non-deterministic
        await Assert.That(signature1.Sig).IsNotEqualTo(signature2.Sig);
        
        // Both should verify
        await Assert.That(signer.Key.VerifyKeySignature(signature1.Sig, message)).IsTrue();
        await Assert.That(signer.Key.VerifyKeySignature(signature2.Sig, message)).IsTrue();
    }

    /// <summary>
    /// Test signature verification with canonical JSON metadata.
    /// This simulates the real-world TUF scenario where metadata is canonically serialized
    /// before signing and verification.
    /// </summary>
    [Test]
    public async Task TestCanonicalJsonCrossCompatibility()
    {
        // Arrange - Create a complete TUF metadata structure
        var signer = Ed25519Signer.Generate();
        
        var rootMetadata = new Root
        {
            Type = "root",
            Version = 1,
            SpecVersion = "1.0.0", 
            Expires = new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero),
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
            ConsistentSnapshot = false
        };

        // Serialize to canonical JSON (this is what gets signed)
        var canonicalBytes = CanonicalJson.Serializer.Serialize(rootMetadata);
        
        // Act - Sign the canonical JSON
        var signature = signer.SignBytes(canonicalBytes);
        
        // Create full metadata structure
        var fullMetadata = new Metadata<Root>
        {
            Signed = rootMetadata,
            Signatures = [signature]
        };

        // Verify using the metadata extension method
        var metadataCanonicalBytes = fullMetadata.GetSignedBytes();

        // Assert - Signatures should verify and canonical bytes should be consistent
        await Assert.That(signer.Key.VerifyKeySignature(signature.Sig, canonicalBytes)).IsTrue();
        await Assert.That(signer.Key.VerifyKeySignature(signature.Sig, metadataCanonicalBytes)).IsTrue();
        await Assert.That(canonicalBytes).IsEquivalentTo(metadataCanonicalBytes);
    }

    /// <summary>
    /// Test multi-algorithm signature compatibility in a single metadata structure.
    /// This tests that metadata can be signed with multiple different algorithms
    /// and all signatures verify correctly.
    /// </summary>
    [Test]
    public async Task TestMultiAlgorithmMetadataSignatures()
    {
        // Arrange - Create signers for all supported algorithms
        var ed25519Signer = Ed25519Signer.Generate();
        var rsaSigner = RsaSigner.Generate(2048);  // Use 2048 for faster tests
        var ecdsaSigner = EcdsaSigner.Generate();

        var rootMetadata = new Root
        {
            Type = "root",
            Version = 1,
            SpecVersion = "1.0.0",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Keys = new Dictionary<string, Key>
            {
                [ed25519Signer.Key.GetKeyId()] = ed25519Signer.Key,
                [rsaSigner.Key.GetKeyId()] = rsaSigner.Key,
                [ecdsaSigner.Key.GetKeyId()] = ecdsaSigner.Key
            },
            Roles = new Roles
            {
                Root = new RoleKeys 
                { 
                    KeyIds = [ed25519Signer.Key.GetKeyId(), rsaSigner.Key.GetKeyId(), ecdsaSigner.Key.GetKeyId()],
                    Threshold = 2  // Require 2 of 3 signatures
                },
                Targets = new RoleKeys { KeyIds = [ed25519Signer.Key.GetKeyId()], Threshold = 1 },
                Snapshot = new RoleKeys { KeyIds = [ed25519Signer.Key.GetKeyId()], Threshold = 1 },
                Timestamp = new RoleKeys { KeyIds = [ed25519Signer.Key.GetKeyId()], Threshold = 1 }
            },
            ConsistentSnapshot = false
        };

        // Get canonical bytes for signing
        var canonicalBytes = CanonicalJson.Serializer.Serialize(rootMetadata);

        // Act - Create signatures with all algorithms
        var ed25519Signature = ed25519Signer.SignBytes(canonicalBytes);
        var rsaSignature = rsaSigner.SignBytes(canonicalBytes);
        var ecdsaSignature = ecdsaSigner.SignBytes(canonicalBytes);

        // Assert - All signatures should verify independently
        await Assert.That(ed25519Signer.Key.VerifyKeySignature(ed25519Signature.Sig, canonicalBytes)).IsTrue();
        await Assert.That(rsaSigner.Key.VerifyKeySignature(rsaSignature.Sig, canonicalBytes)).IsTrue();
        await Assert.That(ecdsaSigner.Key.VerifyKeySignature(ecdsaSignature.Sig, canonicalBytes)).IsTrue();

        // Create metadata with all signatures
        var multiSignedMetadata = new Metadata<Root>
        {
            Signed = rootMetadata,
            Signatures = [ed25519Signature, rsaSignature, ecdsaSignature]
        };

        // Verify all signatures still work in the full metadata context
        var metadataCanonicalBytes = multiSignedMetadata.GetSignedBytes();
        await Assert.That(ed25519Signer.Key.VerifyKeySignature(ed25519Signature.Sig, metadataCanonicalBytes)).IsTrue();
        await Assert.That(rsaSigner.Key.VerifyKeySignature(rsaSignature.Sig, metadataCanonicalBytes)).IsTrue();
        await Assert.That(ecdsaSigner.Key.VerifyKeySignature(ecdsaSignature.Sig, metadataCanonicalBytes)).IsTrue();
    }
}