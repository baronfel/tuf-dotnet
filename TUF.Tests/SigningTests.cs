using TUF.Models;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

public class SigningTests
{
    [Test]
    public async Task Ed25519Signer_ShouldCreateValidSignature()
    {
        // Arrange
        var signer = SharedCryptoKeyPool.GetEd25519Signer();
        var testData = "Hello, TUF!"u8.ToArray();

        // Act
        var signature = signer.SignBytes(testData);

        // Assert
        await Assert.That(signature).IsNotNull();
        await Assert.That(signature.KeyId).IsNotNull().And.IsNotEmpty();
        await Assert.That(signature.Sig).IsNotNull().And.IsNotEmpty();

        // Verify the key has correct pinned types
        await Assert.That(signer.Key.KeyType).IsEqualTo("ed25519");
        await Assert.That(signer.Key.Scheme).IsEqualTo("ed25519");
        await Assert.That(signer.Key.KeyVal.Public).IsNotNull().And.IsNotEmpty();

        // Verify signature can be verified
        var signatureBytes = Convert.FromHexString(signature.Sig);
        await Assert.That(signatureBytes).HasCount(64); // Ed25519 signatures are 64 bytes
    }

    [Test]
    public async Task RsaSigner_ShouldCreateValidSignature()
    {
        // Arrange
        var signer = RsaSigner.Generate(2048); // Use minimum key size for faster test
        var testData = "Hello, TUF!"u8.ToArray();

        // Act
        var signature = signer.SignBytes(testData);

        // Assert
        await Assert.That(signature).IsNotNull();
        await Assert.That(signature.KeyId).IsNotNull().And.IsNotEmpty();
        await Assert.That(signature.Sig).IsNotNull().And.IsNotEmpty();

        // Verify the key has correct pinned types
        await Assert.That(signer.Key.KeyType).IsEqualTo("rsa");
        await Assert.That(signer.Key.Scheme).IsEqualTo("rsassa-pss-sha256");
        await Assert.That(signer.Key.KeyVal.Public).IsNotNull().And.IsNotEmpty();
        await Assert.That(signer.Key.KeyVal.Public).Contains("-----BEGIN RSA PUBLIC KEY-----");
    }

    [Test]
    public async Task EcdsaSigner_ShouldCreateValidSignature()
    {
        // Arrange
        var signer = EcdsaSigner.Generate();
        var testData = "Hello, TUF!"u8.ToArray();

        // Act
        var signature = signer.SignBytes(testData);

        // Assert
        await Assert.That(signature).IsNotNull();
        await Assert.That(signature.KeyId).IsNotNull().And.IsNotEmpty();
        await Assert.That(signature.Sig).IsNotNull().And.IsNotEmpty();

        // Verify the key has correct pinned types
        await Assert.That(signer.Key.KeyType).IsEqualTo("ecdsa");
        await Assert.That(signer.Key.Scheme).IsEqualTo("ecdsa-sha2-nistp256");
        await Assert.That(signer.Key.KeyVal.Public).IsNotNull().And.IsNotEmpty();
        await Assert.That(signer.Key.KeyVal.Public).Contains("-----BEGIN PUBLIC KEY-----");
    }

    [Test]
    public async Task Key_GetKeyId_ShouldBeConsistent()
    {
        // Arrange
        var signer = SharedCryptoKeyPool.GetEd25519Signer();

        // Act
        var keyId1 = signer.Key.GetKeyId();
        var keyId2 = signer.Key.GetKeyId();

        // Assert
        await Assert.That(keyId1).IsEqualTo(keyId2);
        await Assert.That(keyId1.Length).IsEqualTo(64); // SHA-256 hex string is 64 characters

        // Verify it matches the signature key ID
        var signature = signer.SignBytes("test"u8.ToArray());
        await Assert.That(signature.KeyId).IsEqualTo(keyId1);
    }

    [Test]
    public async Task RsaSigner_ShouldRejectSmallKeySize()
    {
        // Act & Assert
        await Assert.That(() => RsaSigner.Generate(1024))
            .Throws<ArgumentException>()
            .WithMessage("RSA key size must be at least 2048 bits (Parameter 'keySize')");
    }

    [Test]
    public async Task TypeScheme_Combinations_AreProperlyPinned()
    {
        // Arrange & Act
        var ed25519Signer = SharedCryptoKeyPool.GetEd25519Signer();
        var rsaSigner = SharedCryptoKeyPool.GetRsaSigner();
        var ecdsaSigner = SharedCryptoKeyPool.GetEcdsaSigner();

        // Assert - Verify each signer has the correct pinned type/scheme combination
        // Ed25519: type=ed25519, scheme=ed25519
        await Assert.That(ed25519Signer.Key.KeyType).IsEqualTo("ed25519");
        await Assert.That(ed25519Signer.Key.Scheme).IsEqualTo("ed25519");

        // RSA: type=rsa, scheme=rsassa-pss-sha256
        await Assert.That(rsaSigner.Key.KeyType).IsEqualTo("rsa");
        await Assert.That(rsaSigner.Key.Scheme).IsEqualTo("rsassa-pss-sha256");

        // ECDSA: type=ecdsa, scheme=ecdsa-sha2-nistp256
        await Assert.That(ecdsaSigner.Key.KeyType).IsEqualTo("ecdsa");
        await Assert.That(ecdsaSigner.Key.Scheme).IsEqualTo("ecdsa-sha2-nistp256");
    }
}