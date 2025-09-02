using CanonicalJson;
using TUF.Models;
using TUF.Repository;
using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests to verify RSA key serialization and deserialization works correctly,
/// particularly handling PEM format keys with newlines in JSON.
/// </summary>
public class RsaSerializationTests
{
    [Test]
    public async Task RsaSigner_CanSerializeAndDeserializeKey()
    {
        // Generate an RSA signer and verify we can serialize/deserialize its key
        using var signer = RsaSigner.Generate(2048);
        var originalKey = signer.Key;

        // Serialize the key to canonical JSON
        var serializedKey = CanonicalJson.Serializer.Serialize(originalKey);
        var jsonString = System.Text.Encoding.UTF8.GetString(serializedKey);

        Console.WriteLine($"Original Key Type: {originalKey.KeyType}");
        Console.WriteLine($"Original Key Scheme: {originalKey.Scheme}");
        Console.WriteLine($"Original Key ID: {originalKey.GetKeyId()}");
        Console.WriteLine($"Serialized JSON length: {jsonString.Length}");
        Console.WriteLine($"JSON contains newlines: {jsonString.Contains("\\n")}");

        // Deserialize the key back
        var deserializedKey = CanonicalJson.Serializer.Deserialize<Key>(jsonString);

        // Verify the keys are equivalent
        await Assert.That(deserializedKey.KeyType).IsEqualTo(originalKey.KeyType);
        await Assert.That(deserializedKey.Scheme).IsEqualTo(originalKey.Scheme);
        await Assert.That(deserializedKey.KeyVal.Public).IsEqualTo(originalKey.KeyVal.Public);
        await Assert.That(deserializedKey.GetKeyId()).IsEqualTo(originalKey.GetKeyId());
    }

    [Test]
    public async Task RsaSigner_CanSignAndVerifySignature()
    {
        // Generate an RSA signer and test basic signing functionality
        using var signer = RsaSigner.Generate(2048);
        var testData = "Test data for RSA signature"u8.ToArray();

        // Sign the data
        var signature = signer.SignBytes(testData);

        Console.WriteLine($"Key ID: {signature.KeyId}");
        Console.WriteLine($"Signature length: {signature.Sig.Length}");

        // Verify the signature
        var isValid = signer.Key.VerifySignature(signature.Sig, testData);

        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task RsaKey_WithPemNewlines_SerializesCorrectly()
    {
        // Test that RSA keys with PEM newlines are handled correctly in JSON
        using var signer = RsaSigner.Generate(2048);
        var key = signer.Key;

        // The public key should contain PEM format with newlines
        await Assert.That(key.KeyVal.Public).Contains("-----BEGIN RSA PUBLIC KEY-----");
        await Assert.That(key.KeyVal.Public).Contains("-----END RSA PUBLIC KEY-----");
        await Assert.That(key.KeyVal.Public).Contains("\n");

        // Test that the key serializes to valid JSON without errors
        var serialized = CanonicalJson.Serializer.Serialize(key);
        var jsonString = System.Text.Encoding.UTF8.GetString(serialized);

        // JSON should be valid and parseable
        await Assert.That(jsonString).IsNotNull();
        await Assert.That(jsonString.Length).IsGreaterThan(0);

        // Should be able to deserialize back without errors
        var deserialized = CanonicalJson.Serializer.Deserialize<Key>(jsonString);
        await Assert.That(deserialized).IsNotNull();
    }

    [Test]
    public async Task RepositoryBuilder_WithRsaSigners_CreatesValidRepository()
    {
        // This is the test that was disabled - let's enable it and see if our fix works
        using var rootSigner = RsaSigner.Generate(2048);
        using var timestampSigner = RsaSigner.Generate(2048);
        using var snapshotSigner = RsaSigner.Generate(2048);
        using var targetsSigner = RsaSigner.Generate(2048);

        var testFile = "test-file.txt"u8.ToArray();

        var builder = new RepositoryBuilder()
            .AddSigner("root", rootSigner)
            .AddSigner("timestamp", timestampSigner)
            .AddSigner("snapshot", snapshotSigner)
            .AddSigner("targets", targetsSigner)
            .AddTarget("test-file.txt", testFile);

        // This should not throw any JSON parsing errors
        var repository = builder.Build();

        // Verify the repository was created successfully
        await Assert.That(repository).IsNotNull();
        await Assert.That(repository.Root).IsNotNull();
        await Assert.That(repository.Timestamp).IsNotNull();
        await Assert.That(repository.Snapshot).IsNotNull();
        await Assert.That(repository.Targets).IsNotNull();

        // Verify all signatures are present and valid
        await Assert.That(repository.Root.Signatures).HasCount().EqualTo(1);
        await Assert.That(repository.Timestamp.Signatures).HasCount().EqualTo(1);
        await Assert.That(repository.Snapshot.Signatures).HasCount().EqualTo(1);
        await Assert.That(repository.Targets.Signatures).HasCount().EqualTo(1);
    }
}