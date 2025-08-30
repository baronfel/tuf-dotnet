using System.Text;
using TUF.Signing;
using TUF.Models.Keys;
using TUF.Models.Primitives;

namespace TUF.Tests;

public class SigningTests
{
    [Test]
    public async Task Ed25519Signer_Generate_CreatesValidSigner()
    {
        // Arrange & Act
        var signer = Ed25519Signer.Generate();

        // Assert
        await Assert.That(signer).IsNotNull();
        await Assert.That(signer.Key).IsNotNull();
        await Assert.That(signer.Key).IsTypeOf<WellKnown.Ed25519>();
    }

    [Test]
    public async Task Ed25519Signer_SignAndVerify_Success()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "Hello, TUF World!"u8.ToArray();

        // Act
        var signature = signer.SignBytes(testData);
        var verified = signer.Key.VerifySignature(signature.Value, testData);

        // Assert
        await Assert.That(signature.Value).IsNotEmpty();
        await Assert.That(signature.Value.Length).IsEqualTo(64); // Ed25519 signatures are 64 bytes
        await Assert.That(verified).IsTrue();
    }

    [Test]
    public async Task Ed25519Signer_SignAndVerifyWithDifferentData_Fails()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var originalData = "Original data"u8.ToArray();
        var tamperedData = "Tampered data"u8.ToArray();

        // Act
        var signature = signer.SignBytes(originalData);
        var verified = signer.Key.VerifySignature(signature.Value, tamperedData);

        // Assert
        await Assert.That(verified).IsFalse();
    }

    [Test]
    public async Task Ed25519Signer_MultipleSignatures_AllVerifyCorrectly()
    {
        // Arrange
        var testSigner = Ed25519Signer.Generate();
        var testData = "Test data for consistency"u8.ToArray();

        // Act
        var signature1 = testSigner.SignBytes(testData);
        var signature2 = testSigner.SignBytes(testData);
        var verified1 = testSigner.Key.VerifySignature(signature1.Value, testData);
        var verified2 = testSigner.Key.VerifySignature(signature2.Value, testData);

        // Assert
        await Assert.That(testSigner.Key).IsNotNull();
        await Assert.That(verified1).IsTrue();
        await Assert.That(verified2).IsTrue();
        await Assert.That(signature1.Value).IsNotEmpty();
        await Assert.That(signature2.Value).IsNotEmpty();
    }

    [Test]
    public async Task RsaSigner_Generate_CreatesValidSigner()
    {
        // Arrange & Act
        var signer = RsaSigner.Generate();

        // Assert
        await Assert.That(signer).IsNotNull();
        await Assert.That(signer.Key).IsNotNull();
        await Assert.That(signer.Key).IsTypeOf<WellKnown.Rsa>();
    }

    [Test]
    public async Task RsaSigner_GenerateWithKeySize_CreatesValidSigner()
    {
        // Arrange & Act
        var signer = RsaSigner.Generate(2048);

        // Assert
        await Assert.That(signer).IsNotNull();
        await Assert.That(signer.Key).IsNotNull();
    }

    [Test]
    public async Task RsaSigner_SignAndVerify_Success()
    {
        // Arrange
        var signer = RsaSigner.Generate();
        var testData = "Hello, RSA TUF World!"u8.ToArray();

        // Act
        var signature = signer.SignBytes(testData);
        var verified = signer.Key.VerifySignature(signature.Value, testData);

        // Assert
        await Assert.That(signature.Value).IsNotEmpty();
        await Assert.That(signature.Value.Length).IsGreaterThan(0);
        await Assert.That(verified).IsTrue();
    }

    [Test]
    public async Task RsaSigner_SignAndVerifyWithDifferentData_Fails()
    {
        // Arrange
        var signer = RsaSigner.Generate();
        var originalData = "Original RSA data"u8.ToArray();
        var tamperedData = "Tampered RSA data"u8.ToArray();

        // Act
        var signature = signer.SignBytes(originalData);
        var verified = signer.Key.VerifySignature(signature.Value, tamperedData);

        // Assert
        await Assert.That(verified).IsFalse();
    }

    [Test]
    public async Task RsaSigner_FromPem_CreatesValidSigner()
    {
        // Arrange - Create a test RSA key and export it as PEM
        using var testRsa = System.Security.Cryptography.RSA.Create(2048);
        var privatePem = testRsa.ExportRSAPrivateKeyPem();

        // Act
        var signer = RsaSigner.FromPem(privatePem);
        var testData = "Test data for PEM import"u8.ToArray();
        var signature = signer.SignBytes(testData);
        var verified = signer.Key.VerifySignature(signature.Value, testData);

        // Assert
        await Assert.That(signer).IsNotNull();
        await Assert.That(verified).IsTrue();
    }

    [Test]
    public async Task DifferentSigners_ProduceDifferentSignatures()
    {
        // Arrange
        var ed25519Signer = Ed25519Signer.Generate();
        var rsaSigner = RsaSigner.Generate();
        var testData = "Same data, different signers"u8.ToArray();

        // Act
        var ed25519Signature = ed25519Signer.SignBytes(testData);
        var rsaSignature = rsaSigner.SignBytes(testData);

        // Assert
        await Assert.That(ed25519Signature.Value).IsNotEqualTo(rsaSignature.Value);
        await Assert.That(ed25519Signer.Key.KeyType).IsEqualTo("ed25519");
        await Assert.That(rsaSigner.Key.KeyType).IsEqualTo("rsa");
    }

    [Test]
    public async Task SignersHaveConsistentKeyIds()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();

        // Act
        var keyId1 = signer.Key.Id;
        var keyId2 = signer.Key.Id;

        // Assert
        await Assert.That(keyId1).IsEqualTo(keyId2);
        await Assert.That(keyId1.ToJsonString()).IsNotEmpty();
    }

    [Test]
    public async Task LargeDataSigning_WorksCorrectly()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var largeData = new byte[1024 * 1024]; // 1MB of data
        Random.Shared.NextBytes(largeData);

        // Act
        var signature = signer.SignBytes(largeData);
        var verified = signer.Key.VerifySignature(signature.Value, largeData);

        // Assert
        await Assert.That(signature.Value).IsNotEmpty();
        await Assert.That(verified).IsTrue();
    }

    [Test]
    public async Task EmptyDataSigning_WorksCorrectly()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var emptyData = Array.Empty<byte>();

        // Act
        var signature = signer.SignBytes(emptyData);
        var verified = signer.Key.VerifySignature(signature.Value, emptyData);

        // Assert
        await Assert.That(signature.Value).IsNotEmpty();
        await Assert.That(verified).IsTrue();
    }
}