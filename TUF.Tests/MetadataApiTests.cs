using System.Text;
using CanonicalJson;

using Serde.Json;

using TUF.Models;
using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests mirroring the Go TUF metadata_api_test.go patterns.
/// Focuses on metadata parsing, signature verification, key management, and validation.
/// </summary>
public class MetadataApiTests
{
    private static readonly Dictionary<string, string> SampleMetadata = new()
    {
        ["root"] = """
        {
          "signatures": [
            {
              "keyid": "test_key_1",
              "sig": "test_signature_1"
            }
          ],
          "signed": {
            "_type": "root",
            "consistent_snapshot": true,
            "expires": "2025-12-31T23:59:59Z",
            "keys": {
              "test_key_1": {
                "keytype": "ed25519",
                "keyval": {
                  "public": "test_public_key_1"
                },
                "scheme": "ed25519"
              }
            },
            "roles": {
              "root": {
                "keyids": ["test_key_1"],
                "threshold": 1
              },
              "snapshot": {
                "keyids": ["test_key_1"],
                "threshold": 1
              },
              "targets": {
                "keyids": ["test_key_1"],
                "threshold": 1
              },
              "timestamp": {
                "keyids": ["test_key_1"],
                "threshold": 1
              }
            },
            "spec_version": "1.0.0",
            "version": 1
          }
        }
        """,

        ["timestamp"] = """
        {
          "signatures": [
            {
              "keyid": "test_key_1",
              "sig": "test_signature_1"
            }
          ],
          "signed": {
            "_type": "timestamp",
            "expires": "2025-12-31T23:59:59Z",
            "meta": {
              "snapshot.json": {
                "version": 1,
                "length": 1024,
                "hashes": {
                  "sha256": "test_hash_value"
                }
              }
            },
            "spec_version": "1.0.0",
            "version": 1
          }
        }
        """,

        ["snapshot"] = """
        {
          "signatures": [
            {
              "keyid": "test_key_1",
              "sig": "test_signature_1"
            }
          ],
          "signed": {
            "_type": "snapshot",
            "expires": "2025-12-31T23:59:59Z",
            "meta": {
              "targets.json": {
                "version": 1,
                "length": 2048,
                "hashes": {
                  "sha256": "test_targets_hash"
                }
              }
            },
            "spec_version": "1.0.0",
            "version": 1
          }
        }
        """,

        ["targets"] = """
        {
          "signatures": [
            {
              "keyid": "test_key_1",
              "sig": "test_signature_1"
            }
          ],
          "signed": {
            "_type": "targets",
            "expires": "2025-12-31T23:59:59Z",
            "targets": {
              "example.txt": {
                "length": 100,
                "hashes": {
                  "sha256": "test_file_hash"
                }
              }
            },
            "spec_version": "1.0.0",
            "version": 1
          }
        }
        """
    };





    /// <summary>
    /// Test canonical JSON serialization consistency.
    /// Mirrors Go TUF TestCompareFromBytesFromFileToBytes pattern.
    /// </summary>
    [Test]
    public async Task TestCanonicalSerializationConsistency()
    {
        // Arrange
        var originalJson = SampleMetadata["root"];
        var metadata = JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(originalJson);

        // Act - Round trip serialize/deserialize
        var serializedBytes = JsonSerializer.Serialize<Metadata<Root>, MetadataProxy.Ser<Root>>(metadata);
        var roundTripMetadata = JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(serializedBytes);

        // Assert - Content should be equivalent
        await Assert.That(roundTripMetadata.Signed.Type).IsEqualTo(metadata.Signed.Type);
        await Assert.That(roundTripMetadata.Signed.Version).IsEqualTo(metadata.Signed.Version);
        await Assert.That(roundTripMetadata.Signatures).HasCount().EqualTo(metadata.Signatures.Count);
    }

    /// <summary>
    /// Test metadata expiration validation.
    /// </summary>
    [Test]
    public async Task TestMetadataExpiration_NotExpired()
    {
        // Arrange
        var futureDate = DateTimeOffset.UtcNow.AddDays(30);
        var rootJson = SampleMetadata["root"].Replace("2025-12-31T23:59:59Z", futureDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var metadata = JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(rootJson);

        // Act & Assert
        await Assert.That(metadata.Signed.Expires).IsGreaterThan(DateTimeOffset.UtcNow);
    }

    [Test]
    public async Task TestMetadataExpiration_Expired()
    {
        // Arrange
        var pastDate = DateTimeOffset.UtcNow.AddDays(-30);
        var rootJson = SampleMetadata["root"].Replace("2025-12-31T23:59:59Z", pastDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var metadata = JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(rootJson);

        // Act & Assert
        await Assert.That(metadata.Signed.Expires).IsLessThan(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Test key threshold validation in roles.
    /// </summary>
    [Test]
    public async Task TestKeyThresholdValidation()
    {
        // Arrange
        var metadata = JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(SampleMetadata["root"]);

        // Act & Assert
        await Assert.That(metadata.Signed.Roles.Root.Threshold).IsEqualTo(1);
        await Assert.That(metadata.Signed.Roles.Root.KeyIds).HasCount().GreaterThanOrEqualTo((int)metadata.Signed.Roles.Root.Threshold);
        
        await Assert.That(metadata.Signed.Roles.Targets!.Threshold).IsEqualTo(1);
        await Assert.That(metadata.Signed.Roles.Targets.KeyIds).HasCount().GreaterThanOrEqualTo((int)metadata.Signed.Roles.Targets.Threshold);
    }

    /// <summary>
    /// Test key existence in metadata keys dictionary.
    /// </summary>
    [Test]
    public async Task TestKeyExistenceValidation()
    {
        // Arrange
        var metadata = JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(SampleMetadata["root"]);

        // Act & Assert - All role key IDs should exist in keys dictionary
        foreach (var keyId in metadata.Signed.Roles.Root.KeyIds)
        {
            await Assert.That(metadata.Signed.Keys).ContainsKey(keyId);
        }

        foreach (var keyId in metadata.Signed.Roles.Targets!.KeyIds)
        {
            await Assert.That(metadata.Signed.Keys).ContainsKey(keyId);
        }

        foreach (var keyId in metadata.Signed.Roles.Snapshot!.KeyIds)
        {
            await Assert.That(metadata.Signed.Keys).ContainsKey(keyId);
        }

        foreach (var keyId in metadata.Signed.Roles.Timestamp!.KeyIds)
        {
            await Assert.That(metadata.Signed.Keys).ContainsKey(keyId);
        }
    }



    /// <summary>
    /// Test spec version format validation.
    /// Important: validates semantic version format.
    /// </summary>
    [Test]
    public async Task TestSpecVersionFormat()
    {
        // Arrange
        var metadata = JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(SampleMetadata["root"]);

        // Act & Assert - Verify spec version format (should be semantic version)
        await Assert.That(metadata.Signed.SpecVersion).Matches(@"^\d+\.\d+\.\d+$");
    }


    /// <summary>
    /// Test key type and scheme consistency.
    /// Important: validates TUF spec compliance for key types.
    /// </summary>
    [Test]
    public async Task TestKeyTypeSchemeConsistency()
    {
        // Arrange
        var metadata = JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(SampleMetadata["root"]);

        // Act & Assert - Key type and scheme should be consistent per TUF spec
        foreach (var (keyId, key) in metadata.Signed.Keys)
        {
            if (key.KeyType == "ed25519")
            {
                await Assert.That(key.Scheme).IsEqualTo("ed25519");
            }
            else if (key.KeyType == "rsa")
            {
                await Assert.That(key.Scheme).IsEqualTo("rsassa-pss-sha256");
            }
            else if (key.KeyType == "ecdsa")
            {
                await Assert.That(key.Scheme).IsEqualTo("ecdsa-sha2-nistp256");
            }
        }
    }
}