using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

using CanonicalJson;

using TUF.Models;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests that verify round-trip serialization works correctly for all TUF metadata types.
/// Ensures that serializing and deserializing metadata results in equivalent objects.
/// </summary>
public class SerializationRoundTripTests
{
    [Test]
    public async Task RootMetadata_RoundTripSerialization_ShouldPreserveEquality()
    {
        // Arrange - Create a Root metadata object with all fields populated
        var originalRoot = new Root
        {
            Type = "root",
            SpecVersion = "1.0.0",
            Version = 42,
            Expires = DateTimeOffset.ParseExact("2025-12-31T23:59:59Z", Proxies.CanonicalDateTimeOffsetProxy.DateTimeOffsetFormat, CultureInfo.InvariantCulture),
            ConsistentSnapshot = true,
            Keys = new Dictionary<string, Key>
            {
                ["test_key_1"] = new Key
                {
                    KeyType = "ed25519",
                    Scheme = "ed25519",
                    KeyVal = new KeyValue { Public = "abcd1234567890abcd1234567890abcd1234567890abcd1234567890abcd1234" }
                },
                ["test_key_2"] = new Key
                {
                    KeyType = "rsa",
                    Scheme = "rsassa-pss-sha256",
                    KeyVal = new KeyValue { Public = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA\n-----END PUBLIC KEY-----" }
                }
            },
            Roles = new Roles
            {
                Root = new RoleKeys { KeyIds = ["test_key_1"], Threshold = 1 },
                Timestamp = new RoleKeys { KeyIds = ["test_key_1"], Threshold = 1 },
                Snapshot = new RoleKeys { KeyIds = ["test_key_2"], Threshold = 1 },
                Targets = new RoleKeys { KeyIds = ["test_key_1", "test_key_2"], Threshold = 2 }
            }
        };

        var originalMetadata = new Metadata<Root>
        {
            Signed = originalRoot,
            Signatures = new List<SignatureObject>
            {
                new() { KeyId = "test_key_1", Sig = "signature_placeholder_hex_value_here" }
            }
        };

        // Act - Serialize and then deserialize
        var serialized = Serde.Json.JsonSerializer.Serialize<Metadata<Root>, MetadataProxy.Ser<Root>>(originalMetadata);
        var deserialized = Serde.Json.JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(Encoding.UTF8.GetBytes(serialized), MetadataProxy.De<Root>.Instance);

        // Assert - Should be equivalent
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Signed).IsEquivalentTo(originalMetadata.Signed);
        await Assert.That(deserialized.Signatures).IsEquivalentTo(originalMetadata.Signatures);
    }

    [Test]
    public async Task TimestampMetadata_RoundTripSerialization_ShouldPreserveEquality()
    {
        // Arrange - Create a Timestamp metadata object with all fields populated
        var originalTimestamp = new Timestamp
        {
            Type = "timestamp",
            SpecVersion = "1.0.0",
            Version = 123,
            Expires = DateTimeOffset.ParseExact("2025-06-15T12:30:45Z", Proxies.CanonicalDateTimeOffsetProxy.DateTimeOffsetFormat, CultureInfo.InvariantCulture),
            Meta = new Dictionary<string, FileMetadata>
            {
                ["snapshot.json"] = new FileMetadata
                {
                    Version = 456,
                    Length = 1024,
                    Hashes = new Dictionary<string, string>
                    {
                        ["sha256"] = "abcd1234567890abcd1234567890abcd1234567890abcd1234567890abcd1234",
                        ["sha512"] = "efgh5678901234efgh5678901234efgh5678901234efgh5678901234efgh5678901234efgh5678901234efgh5678901234efgh5678901234efgh5678"
                    }
                }
            }
        };

        var originalMetadata = new Metadata<Timestamp>
        {
            Signed = originalTimestamp,
            Signatures = new List<SignatureObject>
            {
                new() { KeyId = "timestamp_key", Sig = "timestamp_signature_hex" }
            }
        };

        // Act - Serialize and then deserialize
        var serialized = Serde.Json.JsonSerializer.Serialize<Metadata<Timestamp>, MetadataProxy.Ser<Timestamp>>(originalMetadata);
        var deserialized = Serde.Json.JsonSerializer.Deserialize<Metadata<Timestamp>, MetadataProxy.De<Timestamp>>(Encoding.UTF8.GetBytes(serialized), MetadataProxy.De<Timestamp>.Instance);

        // Assert - Should be equivalent
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Signed).IsEquivalentTo(originalMetadata.Signed);
        await Assert.That(deserialized.Signatures).IsEquivalentTo(originalMetadata.Signatures);
    }

    [Test]
    public async Task SnapshotMetadata_RoundTripSerialization_ShouldPreserveEquality()
    {
        // Arrange - Create a Snapshot metadata object with multiple targets metadata entries
        var originalSnapshot = new Snapshot
        {
            Type = "snapshot",
            SpecVersion = "1.0.0",
            Version = 789,
            Expires = DateTimeOffset.ParseExact("2025-03-20T08:15:30Z", Proxies.CanonicalDateTimeOffsetProxy.DateTimeOffsetFormat, CultureInfo.InvariantCulture),
            Meta = new Dictionary<string, FileMetadata>
            {
                ["targets.json"] = new FileMetadata
                {
                    Version = 101,
                    Length = 2048,
                    Hashes = new Dictionary<string, string>
                    {
                        ["sha256"] = "targets_hash_256_value_here_64_characters_long_abcdef1234567890"
                    }
                },
                ["delegated-role.json"] = new FileMetadata
                {
                    Version = 202,
                    Length = 512,
                    Hashes = new Dictionary<string, string>
                    {
                        ["sha256"] = "delegated_hash_256_value_here_64_characters_long_fedcba0987654321",
                        ["blake2b-256"] = "blake2b_hash_value_here_64_characters_long_1111222233334444"
                    }
                }
            }
        };

        var originalMetadata = new Metadata<Snapshot>
        {
            Signed = originalSnapshot,
            Signatures = new List<SignatureObject>
            {
                new() { KeyId = "snapshot_key", Sig = "snapshot_signature_hex_value" }
            }
        };

        // Act - Serialize and then deserialize
        var serialized = Serde.Json.JsonSerializer.Serialize<Metadata<Snapshot>, MetadataProxy.Ser<Snapshot>>(originalMetadata);
        var deserialized = Serde.Json.JsonSerializer.Deserialize<Metadata<Snapshot>, MetadataProxy.De<Snapshot>>(Encoding.UTF8.GetBytes(serialized), MetadataProxy.De<Snapshot>.Instance);

        // Assert - Should be equivalent
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Signed).IsEquivalentTo(originalMetadata.Signed);
        await Assert.That(deserialized.Signatures).IsEquivalentTo(originalMetadata.Signatures);
    }

    [Test]
    public async Task TargetsMetadata_RoundTripSerialization_ShouldPreserveEquality()
    {
        // Arrange - Create a Targets metadata object with target files and delegations
        var originalTargets = new Targets
        {
            Type = "targets",
            SpecVersion = "1.0.0",
            Version = 999,
            Expires = DateTimeOffset.ParseExact("2025-09-10T16:45:00Z", Proxies.CanonicalDateTimeOffsetProxy.DateTimeOffsetFormat, CultureInfo.InvariantCulture),
            TargetMap = new Dictionary<string, TargetFile>
            {
                ["app/binary.exe"] = new TargetFile
                {
                    Length = 4096,
                    Hashes = new Dictionary<string, string>
                    {
                        ["sha256"] = "binary_hash_256_value_here_64_characters_long_aabbccdd11223344"
                    },
                    Custom = new JsonObject
                    {
                        ["version"] = "1.2.3",
                        ["platform"] = "windows"
                    }
                },
                ["lib/library.dll"] = new TargetFile
                {
                    Length = 8192,
                    Hashes = new Dictionary<string, string>
                    {
                        ["sha256"] = "library_hash_256_value_here_64_characters_long_ddeeff4455667788",
                        ["sha512"] = "library_hash_512_value_here_128_characters_long_aabbccdd11223344aabbccdd11223344aabbccdd11223344aabbccdd11223344aabbccdd11223344aabbccdd11223344aabbccdd11223344aabbccdd11223344"
                    }
                }
            },
            Delegations = new Delegations
            {
                Keys = new Dictionary<string, Key>
                {
                    ["delegated_key_1"] = new Key
                    {
                        KeyType = "ed25519",
                        Scheme = "ed25519",
                        KeyVal = new KeyValue { Public = "delegated_public_key_ed25519_32_bytes_hex_value_here_1234" }
                    }
                },
                Roles = new List<DelegatedRole>
                {
                    new()
                    {
                        Name = "app-team",
                        KeyIds = ["delegated_key_1"],
                        Threshold = 1,
                        Paths = ["app/*"],
                        Terminating = false
                    },
                    new()
                    {
                        Name = "lib-team",
                        KeyIds = ["delegated_key_1"],
                        Threshold = 1,
                        PathHashPrefixes = ["aa", "bb", "cc"],
                        Terminating = true
                    }
                }
            }
        };

        var originalMetadata = new Metadata<Targets>
        {
            Signed = originalTargets,
            Signatures = new List<SignatureObject>
            {
                new() { KeyId = "targets_key", Sig = "targets_signature_hex_value_here" }
            }
        };

        // Act - Serialize and then deserialize
        var serialized = Serde.Json.JsonSerializer.Serialize<Metadata<Targets>, MetadataProxy.Ser<Targets>>(originalMetadata);
        var deserialized = Serde.Json.JsonSerializer.Deserialize<Metadata<Targets>, MetadataProxy.De<Targets>>(Encoding.UTF8.GetBytes(serialized), MetadataProxy.De<Targets>.Instance);

        // Assert - Should be equivalent
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Signed).IsEquivalentTo(originalMetadata.Signed);
        await Assert.That(deserialized.Signatures).IsEquivalentTo(originalMetadata.Signatures);
    }

    [Test]
    public async Task TargetsMetadata_WithoutDelegations_RoundTripSerialization_ShouldPreserveEquality()
    {
        // Arrange - Create a simple Targets metadata object without delegations
        var originalTargets = new Targets
        {
            Type = "targets",
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.ParseExact("2025-01-01T00:00:00Z", Proxies.CanonicalDateTimeOffsetProxy.DateTimeOffsetFormat, CultureInfo.InvariantCulture),
            TargetMap = new Dictionary<string, TargetFile>
            {
                ["simple-file.txt"] = new TargetFile
                {
                    Length = 100,
                    Hashes = new Dictionary<string, string>
                    {
                        ["sha256"] = "simple_file_hash_256_value_here_64_characters_long_9988776655"
                    }
                }
            },
            // Delegations is null/not set, testing the optional field behavior
        };

        var originalMetadata = new Metadata<Targets>
        {
            Signed = originalTargets,
            Signatures = new List<SignatureObject>
            {
                new() { KeyId = "simple_targets_key", Sig = "simple_signature_hex" }
            }
        };

        // Act - Serialize and then deserialize
        var serialized = Serde.Json.JsonSerializer.Serialize<Metadata<Targets>, MetadataProxy.Ser<Targets>>(originalMetadata);
        var deserialized = Serde.Json.JsonSerializer.Deserialize<Metadata<Targets>, MetadataProxy.De<Targets>>(Encoding.UTF8.GetBytes(serialized), MetadataProxy.De<Targets>.Instance);

        // Assert - Should be equivalent
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Signed).IsEquivalentTo(originalMetadata.Signed);
        await Assert.That(deserialized.Signatures).IsEquivalentTo(originalMetadata.Signatures);
    }

    [Test]
    public async Task RootMetadata_WithoutConsistentSnapshot_RoundTripSerialization_ShouldPreserveEquality()
    {
        // Arrange - Create a Root metadata object with ConsistentSnapshot = null (testing optional field)
        var originalRoot = new Root
        {
            Type = "root",
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.ParseExact("2025-01-01T00:00:00Z", Proxies.CanonicalDateTimeOffsetProxy.DateTimeOffsetFormat, CultureInfo.InvariantCulture),
            ConsistentSnapshot = null, // Testing null case
            Keys = new Dictionary<string, Key>
            {
                ["single_key"] = new Key
                {
                    KeyType = "ed25519",
                    Scheme = "ed25519",
                    KeyVal = new KeyValue { Public = "single_key_public_value_32_bytes_hex_encoded_here_abcd1234" }
                }
            },
            Roles = new Roles
            {
                Root = new RoleKeys { KeyIds = ["single_key"], Threshold = 1 },
                Timestamp = new RoleKeys { KeyIds = ["single_key"], Threshold = 1 },
                Snapshot = new RoleKeys { KeyIds = ["single_key"], Threshold = 1 },
                Targets = new RoleKeys { KeyIds = ["single_key"], Threshold = 1 }
            }
        };

        var originalMetadata = new Metadata<Root>
        {
            Signed = originalRoot,
            Signatures = new List<SignatureObject>
            {
                new() { KeyId = "single_key", Sig = "single_key_signature_hex_value" }
            }
        };

        // Act - Serialize and then deserialize
        var serialized = Serde.Json.JsonSerializer.Serialize<Metadata<Root>, MetadataProxy.Ser<Root>>(originalMetadata);
        var deserialized = Serde.Json.JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(Encoding.UTF8.GetBytes(serialized), MetadataProxy.De<Root>.Instance);

        // Assert - Should be equivalent
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Signed).IsEquivalentTo(originalMetadata.Signed);
        await Assert.That(deserialized.Signatures).IsEquivalentTo(originalMetadata.Signatures);
    }
}