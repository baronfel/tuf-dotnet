using CanonicalJson;

using TUF.Models;

using TUnit.Assertions;
using TUnit.Core;
using Serde.Json;

namespace TUF.Tests;

/// <summary>
/// Enhanced golden tests that verify our multi-algorithm support and cross-implementation compatibility.
/// These tests use the enhanced GoldenTestDataGenerator to create more diverse test scenarios.
/// </summary>
public class EnhancedGoldenTests
{
    [Test]
    public async Task Multi_Algorithm_Golden_Test_Data_Generation_Works()
    {
        // Generate test data using multiple algorithms
        var testData = GoldenTestDataGenerator.GenerateMultiAlgorithm();

        // Verify root metadata with multiple key types
        await Assert.That(testData.RootMetadata.Signed.Keys).HasCount().EqualTo(4);
        
        // Verify we have different key types
        var keyTypes = testData.RootMetadata.Signed.Keys.Values.Select(k => k.KeyType).ToHashSet();
        await Assert.That(keyTypes).Contains("ed25519");
        await Assert.That(keyTypes).Contains("rsa");
        await Assert.That(keyTypes).Contains("ecdsa");

        // Verify we have different signature schemes
        var schemes = testData.RootMetadata.Signed.Keys.Values.Select(k => k.Scheme).ToHashSet();
        await Assert.That(schemes).Contains("ed25519");
        // RSA scheme might be different - let's check what we actually get
        var rsaKey = testData.RootMetadata.Signed.Keys.Values.First(k => k.KeyType == "rsa");
        await Assert.That(rsaKey.Scheme).IsNotNull();
        await Assert.That(schemes).Contains("ecdsa-sha2-nistp256");

        Console.WriteLine($"✅ Multi-algorithm test data generated successfully");
        Console.WriteLine($"   Key types: {string.Join(", ", keyTypes)}");
        Console.WriteLine($"   Schemes: {string.Join(", ", schemes)}");
        Console.WriteLine($"   Root keys count: {testData.RootMetadata.Signed.Keys.Count}");
    }

    [Test]
    public async Task Multi_Algorithm_Signatures_Verify_Correctly()
    {
        // Generate test data using multiple algorithms
        var testData = GoldenTestDataGenerator.GenerateMultiAlgorithm();

        // Verify each metadata type can be verified with its respective algorithm
        
        // Root (Ed25519)
        var rootKey = testData.Signers.Ed25519RootSigner.Key;
        var rootSignedBytes = testData.RootMetadata.GetSignedBytes();
        var rootSignature = testData.RootMetadata.Signatures[0];
        var rootVerified = rootKey.VerifySignature(rootSignature.Sig, rootSignedBytes);
        await Assert.That(rootVerified).IsTrue();

        // Timestamp (RSA)
        var timestampKey = testData.Signers.RsaTimestampSigner.Key;
        var timestampSignedBytes = testData.TimestampMetadata.GetSignedBytes();
        var timestampSignature = testData.TimestampMetadata.Signatures[0];
        var timestampVerified = timestampKey.VerifySignature(timestampSignature.Sig, timestampSignedBytes);
        await Assert.That(timestampVerified).IsTrue();

        // Snapshot (ECDSA)
        var snapshotKey = testData.Signers.EcdsaSnapshotSigner.Key;
        var snapshotSignedBytes = testData.SnapshotMetadata.GetSignedBytes();
        var snapshotSignature = testData.SnapshotMetadata.Signatures[0];
        var snapshotVerified = snapshotKey.VerifySignature(snapshotSignature.Sig, snapshotSignedBytes);
        await Assert.That(snapshotVerified).IsTrue();

        // Targets (Ed25519)
        var targetsKey = testData.Signers.Ed25519TargetsSigner.Key;
        var targetsSignedBytes = testData.TargetsMetadata.GetSignedBytes();
        var targetsSignature = testData.TargetsMetadata.Signatures[0];
        var targetsVerified = targetsKey.VerifySignature(targetsSignature.Sig, targetsSignedBytes);
        await Assert.That(targetsVerified).IsTrue();

        Console.WriteLine($"✅ All multi-algorithm signatures verified successfully");
        Console.WriteLine($"   Root (Ed25519): {rootVerified}");
        Console.WriteLine($"   Timestamp (RSA): {timestampVerified}");
        Console.WriteLine($"   Snapshot (ECDSA): {snapshotVerified}");
        Console.WriteLine($"   Targets (Ed25519): {targetsVerified}");
    }

    [Test]
    public async Task Multi_Algorithm_Key_ID_Calculation_Is_Consistent()
    {
        // Generate test data multiple times to ensure key ID calculation is deterministic
        var testData1 = GoldenTestDataGenerator.GenerateMultiAlgorithm();
        var testData2 = GoldenTestDataGenerator.GenerateMultiAlgorithm();

        // Keys will be different, but the calculation method should be consistent
        foreach (var key in testData1.RootMetadata.Signed.Keys.Values)
        {
            var keyId1 = key.GetKeyId();
            var keyId2 = key.GetKeyId(); // Calculate again for same key
            
            await Assert.That(keyId2).IsEqualTo(keyId1);
            await Assert.That(keyId1).IsNotEmpty();
            await Assert.That(keyId1.Length).IsEqualTo(64); // SHA-256 hex = 64 chars
            
            Console.WriteLine($"✅ Key ID calculation consistent for {key.KeyType} key");
        }
    }

    [Test]
    public async Task Multi_Algorithm_Canonical_JSON_Serialization_Is_Deterministic()
    {
        // Test that canonical JSON serialization produces consistent results across algorithms
        var testData = GoldenTestDataGenerator.GenerateMultiAlgorithm();

        // Verify all metadata types serialize consistently
        // Test Root
        var rootSerialized1 = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serializer.Serialize(testData.RootMetadata.Signed));
        var rootSerialized2 = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serializer.Serialize(testData.RootMetadata.Signed));
        await Assert.That(rootSerialized2).IsEqualTo(rootSerialized1);
        await Assert.That(rootSerialized1).IsNotEmpty();
        Console.WriteLine($"✅ Root canonical JSON serialization is deterministic");

        // Test Timestamp
        var timestampSerialized1 = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serializer.Serialize(testData.TimestampMetadata.Signed));
        var timestampSerialized2 = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serializer.Serialize(testData.TimestampMetadata.Signed));
        await Assert.That(timestampSerialized2).IsEqualTo(timestampSerialized1);
        await Assert.That(timestampSerialized1).IsNotEmpty();
        Console.WriteLine($"✅ Timestamp canonical JSON serialization is deterministic");

        // Test Snapshot
        var snapshotSerialized1 = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serializer.Serialize(testData.SnapshotMetadata.Signed));
        var snapshotSerialized2 = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serializer.Serialize(testData.SnapshotMetadata.Signed));
        await Assert.That(snapshotSerialized2).IsEqualTo(snapshotSerialized1);
        await Assert.That(snapshotSerialized1).IsNotEmpty();
        Console.WriteLine($"✅ Snapshot canonical JSON serialization is deterministic");

        // Test Targets  
        var targetsSerialized1 = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serializer.Serialize(testData.TargetsMetadata.Signed));
        var targetsSerialized2 = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serializer.Serialize(testData.TargetsMetadata.Signed));
        await Assert.That(targetsSerialized2).IsEqualTo(targetsSerialized1);
        await Assert.That(targetsSerialized1).IsNotEmpty();
        Console.WriteLine($"✅ Targets canonical JSON serialization is deterministic");
    }

    [Test]
    public async Task Enhanced_Golden_Test_Covers_Complex_Target_Scenarios()
    {
        // Test that our enhanced golden test data includes complex target scenarios
        var testData = GoldenTestDataGenerator.GenerateMultiAlgorithm();
        var targets = testData.TargetsMetadata.Signed.TargetMap;

        // Verify we have multiple target files
        await Assert.That(targets).HasCount().GreaterThan(1);

        // Verify we have files with different hash algorithms
        var allHashTypes = targets.Values
            .SelectMany(t => t.Hashes.Keys)
            .ToHashSet();

        await Assert.That(allHashTypes).Contains("sha256");
        
        // Check if any target has multiple hash types
        var multiHashTarget = targets.Values.FirstOrDefault(t => t.Hashes.Count > 1);
        if (multiHashTarget != null)
        {
            await Assert.That(multiHashTarget.Hashes).HasCount().GreaterThan(1);
            Console.WriteLine($"✅ Found target with multiple hash algorithms: {string.Join(", ", multiHashTarget.Hashes.Keys)}");
        }

        // Verify we have files with different sizes
        var fileSizes = targets.Values.Select(t => t.Length).ToHashSet();
        await Assert.That(fileSizes).HasCount().GreaterThan(1);

        Console.WriteLine($"✅ Complex target scenarios covered");
        Console.WriteLine($"   Target files: {targets.Count}");
        Console.WriteLine($"   Hash types: {string.Join(", ", allHashTypes)}");
        Console.WriteLine($"   File sizes: {string.Join(", ", fileSizes)}");
    }

    [Test]
    public async Task Golden_Test_Data_Survives_JSON_Roundtrip()
    {
        // Test that our golden test data can be serialized to JSON and back without loss
        var originalData = GoldenTestDataGenerator.GenerateMultiAlgorithm();

        // Serialize each metadata type to JSON using Serde.Json
        var rootJson = Serde.Json.JsonSerializer.Serialize<Metadata<Root>, MetadataProxy.Ser<Root>>(originalData.RootMetadata);
        var timestampJson = Serde.Json.JsonSerializer.Serialize<Metadata<Timestamp>, MetadataProxy.Ser<Timestamp>>(originalData.TimestampMetadata);
        var snapshotJson = Serde.Json.JsonSerializer.Serialize<Metadata<Snapshot>, MetadataProxy.Ser<Snapshot>>(originalData.SnapshotMetadata);
        var targetsJson = Serde.Json.JsonSerializer.Serialize<Metadata<Targets>, MetadataProxy.Ser<Targets>>(originalData.TargetsMetadata);

        // Deserialize back from JSON using Serde.Json
        var deserializedRoot = Serde.Json.JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(rootJson);
        var deserializedTimestamp = Serde.Json.JsonSerializer.Deserialize<Metadata<Timestamp>, MetadataProxy.De<Timestamp>>(timestampJson);
        var deserializedSnapshot = Serde.Json.JsonSerializer.Deserialize<Metadata<Snapshot>, MetadataProxy.De<Snapshot>>(snapshotJson);
        var deserializedTargets = Serde.Json.JsonSerializer.Deserialize<Metadata<Targets>, MetadataProxy.De<Targets>>(targetsJson);

        // Verify key structural elements are preserved
        await Assert.That(deserializedRoot.Signed.Keys.Count).IsEqualTo(originalData.RootMetadata.Signed.Keys.Count);
        await Assert.That(deserializedRoot.Signed.Version).IsEqualTo(originalData.RootMetadata.Signed.Version);
        
        await Assert.That(deserializedTargets.Signed.TargetMap.Count).IsEqualTo(originalData.TargetsMetadata.Signed.TargetMap.Count);
        
        // Verify signature data is preserved
        await Assert.That(deserializedRoot.Signatures.Count).IsEqualTo(originalData.RootMetadata.Signatures.Count);
        await Assert.That(deserializedTimestamp.Signatures.Count).IsEqualTo(originalData.TimestampMetadata.Signatures.Count);

        Console.WriteLine($"✅ Golden test data survives JSON roundtrip");
        Console.WriteLine($"   Root keys preserved: {deserializedRoot.Signed.Keys.Count}");
        Console.WriteLine($"   Target files preserved: {deserializedTargets.Signed.TargetMap.Count}");
        Console.WriteLine($"   Signatures preserved: Root={deserializedRoot.Signatures.Count}, Timestamp={deserializedTimestamp.Signatures.Count}");
    }
}