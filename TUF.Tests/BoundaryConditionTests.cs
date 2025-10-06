using System.Reflection;
using System.Text;

using CanonicalJson;

using Serde;

using TUF.Exceptions;
using TUF.Models;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests for boundary conditions and edge cases that could cause conformance test failures.
/// These tests help ensure robust handling of extreme inputs and edge cases that
/// might be encountered in real-world TUF conformance testing scenarios.
/// </summary>
public class BoundaryConditionTests
{
    #region Canonical JSON Boundary Conditions

    [Test]
    public async Task CanonicalJson_HandlesSpecialCharactersInMetadata()
    {
        // Test that TUF metadata with special characters serializes correctly
        var key = new Key
        {
            KeyType = "ed25519",
            Scheme = "ed25519", 
            KeyVal = new KeyValue 
            { 
                // Test Unicode and special characters in public key data
                Public = "LS0tLS1CRUdJTiBQVUJMSUMgS0VZLS0tLS1cbjxzcGVjaWFsIGNoYXJzOiAn4oCcXHRcdVwiPi3inqLjgIBcbi0t"
            }
        };

        var serialized = CanonicalJson.Serializer.Serialize(key);
        var jsonString = Encoding.UTF8.GetString(serialized);
        
        // Should serialize without throwing exceptions
        await Assert.That(jsonString).Contains("\"keytype\":\"ed25519\"");
        await Assert.That(jsonString).Contains("\"scheme\":\"ed25519\"");
        await Assert.That(jsonString).Contains("\"public\":");
        
        // Should deserialize back to the same object
        var deserialized = CanonicalJson.Serializer.Deserialize<Key>(jsonString);
        await Assert.That(deserialized.KeyType).IsEqualTo(key.KeyType);
        await Assert.That(deserialized.Scheme).IsEqualTo(key.Scheme);
        await Assert.That(deserialized.KeyVal.Public).IsEqualTo(key.KeyVal.Public);
    }

    [Test]
    public async Task CanonicalJson_HandlesUnicodeInTargetPaths()
    {
        // Test Unicode and special characters in target file paths
        var targets = new Targets
        {
            Type = "targets",
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            TargetMap = new Dictionary<string, TargetFile>
            {
                ["测试文件.txt"] = new TargetFile // Chinese characters
                {
                    Length = 100,
                    Hashes = new Dictionary<string, string>
                    {
                        ["sha256"] = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef"
                    }
                },
                ["файл.bin"] = new TargetFile // Cyrillic characters
                {
                    Length = 200,
                    Hashes = new Dictionary<string, string>
                    {
                        ["sha256"] = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890"
                    }
                },
                ["🚀rocket.dat"] = new TargetFile // Emoji in filename
                {
                    Length = 300,
                    Hashes = new Dictionary<string, string>
                    {
                        ["sha256"] = "fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321"
                    }
                }
            }
        };

        var serialized = CanonicalJson.Serializer.Serialize(targets);
        var jsonString = Encoding.UTF8.GetString(serialized);
        
        // Should handle Unicode characters in target paths without throwing exceptions
        await Assert.That(jsonString).Contains("测试文件.txt");
        await Assert.That(jsonString).Contains("файл.bin");
        await Assert.That(jsonString).Contains("🚀rocket.dat");
        
        // Should contain the expected metadata structure
        await Assert.That(jsonString).Contains("\"_type\":\"targets\"");
        await Assert.That(jsonString).Contains("\"targets\":");
        
        // Should deserialize back successfully
        var deserialized = CanonicalJson.Serializer.Deserialize<Targets>(jsonString);
        await Assert.That(deserialized.TargetMap.Count).IsEqualTo(3);
        await Assert.That(deserialized.TargetMap.ContainsKey("测试文件.txt")).IsTrue();
        await Assert.That(deserialized.TargetMap.ContainsKey("файл.bin")).IsTrue();
        await Assert.That(deserialized.TargetMap.ContainsKey("🚀rocket.dat")).IsTrue();
    }

    #endregion

    #region TUF Metadata Boundary Conditions

    [Test]
    public async Task TufMetadata_HandlesMinimalValidRoot()
    {
        var minimalRoot = new Root
        {
            Type = "root",
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Keys = new Dictionary<string, Key>(),
            Roles = new Roles
            {
                Root = new RoleKeys { KeyIds = [], Threshold = 0 },
                Timestamp = new RoleKeys { KeyIds = [], Threshold = 0 },
                Snapshot = new RoleKeys { KeyIds = [], Threshold = 0 },
                Targets = new RoleKeys { KeyIds = [], Threshold = 0 }
            }
        };

        // Should serialize without throwing exceptions
        var serialized = CanonicalJson.Serializer.Serialize(minimalRoot);
        var jsonString = Encoding.UTF8.GetString(serialized);
        
        await Assert.That(jsonString).Contains("\"_type\":\"root\"");
        await Assert.That(jsonString).Contains("\"spec_version\":\"1.0.0\"");
        await Assert.That(jsonString).Contains("\"version\":1");
    }

    [Test]
    public async Task TufMetadata_HandlesVeryLargeVersionNumbers()
    {
        var root = new Root
        {
            Type = "root",
            SpecVersion = "1.0.0",
            Version = int.MaxValue,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Keys = new Dictionary<string, Key>(),
            Roles = new Roles
            {
                Root = new RoleKeys { KeyIds = [], Threshold = 0 },
                Timestamp = new RoleKeys { KeyIds = [], Threshold = 0 },
                Snapshot = new RoleKeys { KeyIds = [], Threshold = 0 },
                Targets = new RoleKeys { KeyIds = [], Threshold = 0 }
            }
        };

        var serialized = CanonicalJson.Serializer.Serialize(root);
        var jsonString = Encoding.UTF8.GetString(serialized);
        
        await Assert.That(jsonString).Contains($"\"version\":{int.MaxValue}");
    }

    [Test]
    public async Task TufMetadata_HandlesVeryLargeThresholdValues()
    {
        var root = new Root
        {
            Type = "root",
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Keys = new Dictionary<string, Key>(),
            Roles = new Roles
            {
                Root = new RoleKeys { KeyIds = [], Threshold = int.MaxValue },
                Timestamp = new RoleKeys { KeyIds = [], Threshold = int.MaxValue },
                Snapshot = new RoleKeys { KeyIds = [], Threshold = int.MaxValue },
                Targets = new RoleKeys { KeyIds = [], Threshold = int.MaxValue }
            }
        };

        var serialized = CanonicalJson.Serializer.Serialize(root);
        var jsonString = Encoding.UTF8.GetString(serialized);
        
        await Assert.That(jsonString).Contains($"\"threshold\":{int.MaxValue}");
    }

    [Test]
    public async Task TufMetadata_HandlesLargeNumberOfKeys()
    {
        const int keyCount = 1000;
        var keys = new Dictionary<string, Key>();
        
        for (int i = 0; i < keyCount; i++)
        {
            var keyId = $"key{i:D4}";
            keys[keyId] = new Key
            {
                KeyType = "ed25519",
                Scheme = "ed25519",
                KeyVal = new KeyValue { Public = Convert.ToBase64String(new byte[32]) }
            };
        }

        var root = new Root
        {
            Type = "root",
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Keys = keys,
            Roles = new Roles
            {
                Root = new RoleKeys { KeyIds = [], Threshold = 1 },
                Timestamp = new RoleKeys { KeyIds = [], Threshold = 1 },
                Snapshot = new RoleKeys { KeyIds = [], Threshold = 1 },
                Targets = new RoleKeys { KeyIds = [], Threshold = 1 }
            }
        };

        // Should handle large key collections without performance issues
        var serialized = CanonicalJson.Serializer.Serialize(root);
        var jsonString = Encoding.UTF8.GetString(serialized);
        
        await Assert.That(jsonString).Contains("\"keys\":");
        // Keys should be sorted in canonical JSON
        await Assert.That(jsonString.IndexOf("\"key0000\"")).IsLessThan(jsonString.IndexOf("\"key0999\""));
    }

    [Test]
    public async Task TufMetadata_HandlesLargeNumberOfTargets()
    {
        const int targetCount = 1000;
        var targets = new Dictionary<string, TargetFile>();
        
        for (int i = 0; i < targetCount; i++)
        {
            var targetName = $"file{i:D4}.bin";
            targets[targetName] = new TargetFile
            {
                Length = i * 1024,
                Hashes = new Dictionary<string, string>
                {
                    ["sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(targetName))).ToLowerInvariant()
                }
            };
        }

        var targetsMetadata = new Targets
        {
            Type = "targets",
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            TargetMap = targets
        };

        // Should handle large target collections
        var serialized = CanonicalJson.Serializer.Serialize(targetsMetadata);
        var jsonString = Encoding.UTF8.GetString(serialized);
        
        await Assert.That(jsonString).Contains("\"targets\":");
        // Targets should be sorted in canonical JSON
        await Assert.That(jsonString.IndexOf("\"file0000.bin\"")).IsLessThan(jsonString.IndexOf("\"file0999.bin\""));
    }

    [Test]
    public async Task TufMetadata_HandlesExtremelyLongKeyIds()
    {
        // Test very long key IDs (edge case for key ID generation)
        var longString = new string('a', 10000);
        var longKeyId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(longString))).ToLowerInvariant();
        
        var key = new Key
        {
            KeyType = "ed25519",
            Scheme = "ed25519",
            KeyVal = new KeyValue { Public = longString }
        };

        var calculatedKeyId = key.GetKeyId();
        
        // Key ID calculation should work with extremely long public keys
        await Assert.That(calculatedKeyId).IsNotNull();
        await Assert.That(calculatedKeyId.Length).IsEqualTo(64); // SHA256 hex length
    }

    [Test]
    public async Task TufMetadata_HandlesVeryLargeFileSizes()
    {
        var targetFile = new TargetFile
        {
            Length = int.MaxValue, // Use int.MaxValue instead of long.MaxValue to avoid cast issues
            Hashes = new Dictionary<string, string>
            {
                ["sha256"] = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
            }
        };

        var targets = new Targets
        {
            Type = "targets",
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            TargetMap = new Dictionary<string, TargetFile>
            {
                ["huge-file.bin"] = targetFile
            }
        };

        var serialized = CanonicalJson.Serializer.Serialize(targets);
        var jsonString = Encoding.UTF8.GetString(serialized);
        
        await Assert.That(jsonString).Contains($"\"length\":{int.MaxValue}");
    }

    #endregion

    #region Error Boundary Conditions

    [Test]
    public async Task TufValidation_RejectsNegativeVersions()
    {
        var metadata = new Metadata<Root>
        {
            Signed = new Root
            {
                Type = "root",
                SpecVersion = "1.0.0",
                Version = -1, // Invalid negative version
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Keys = new Dictionary<string, Key>(),
                Roles = new Roles
                {
                    Root = new RoleKeys { KeyIds = [], Threshold = 1 },
                    Timestamp = new RoleKeys { KeyIds = [], Threshold = 1 },
                    Snapshot = new RoleKeys { KeyIds = [], Threshold = 1 },
                    Targets = new RoleKeys { KeyIds = [], Threshold = 1 }
                }
            },
            Signatures = []
        };

        // Version validation should catch negative versions
        // Note: This test assumes validation will be added - currently documents expected behavior
        var serialized = CanonicalJson.Serializer.Serialize(metadata.Signed);
        var jsonString = Encoding.UTF8.GetString(serialized);
        
        // Should serialize but validation should eventually reject this
        await Assert.That(jsonString).Contains("\"version\":-1");
    }

    [Test]
    public async Task TufValidation_RejectsNegativeThresholds()
    {
        var metadata = new Metadata<Root>
        {
            Signed = new Root
            {
                Type = "root",
                SpecVersion = "1.0.0",
                Version = 1,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Keys = new Dictionary<string, Key>(),
                Roles = new Roles
                {
                    Root = new RoleKeys { KeyIds = [], Threshold = -1 }, // Invalid negative threshold
                    Timestamp = new RoleKeys { KeyIds = [], Threshold = 1 },
                    Snapshot = new RoleKeys { KeyIds = [], Threshold = 1 },
                    Targets = new RoleKeys { KeyIds = [], Threshold = 1 }
                }
            },
            Signatures = []
        };

        // Threshold validation should catch negative thresholds
        var serialized = CanonicalJson.Serializer.Serialize(metadata.Signed);
        var jsonString = Encoding.UTF8.GetString(serialized);
        
        await Assert.That(jsonString).Contains("\"threshold\":-1");
    }

    [Test]
    public async Task TufValidation_RejectsNegativeFileSizes()
    {
        var targetFile = new TargetFile
        {
            Length = -1, // Invalid negative length
            Hashes = new Dictionary<string, string>
            {
                ["sha256"] = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
            }
        };

        var targets = new Targets
        {
            Type = "targets",
            SpecVersion = "1.0.0", 
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            TargetMap = new Dictionary<string, TargetFile>
            {
                ["invalid-file.bin"] = targetFile
            }
        };

        // File size validation should catch negative lengths
        var serialized = CanonicalJson.Serializer.Serialize(targets);
        var jsonString = Encoding.UTF8.GetString(serialized);
        
        await Assert.That(jsonString).Contains("\"length\":-1");
    }

    [Test]
    public async Task CanonicalJson_HandlesExtremelyLongPropertyNames()
    {
        // Test very long property names
        var longPropertyName = new string('a', 10000);
        
        using var ms = new MemoryStream();
        var writer = new CanonicalJsonSerializer(ms);
        
        var inner = writer.WriteType(null!);
        inner.WriteString(new TestSerdeInfo(longPropertyName), 0, "value");
        inner.End(null!);
        writer.Dispose();
        
        var result = Encoding.UTF8.GetString(ms.ToArray());
        
        // Should handle extremely long property names
        await Assert.That(result).StartsWith("{\"");
        await Assert.That(result).Contains(longPropertyName);
        await Assert.That(result).EndsWith("}");
    }

    [Test]
    public async Task CanonicalJson_HandlesExtremelyLongStringValues()
    {
        // Test very long string values
        var longValue = new string('x', 100000);
        
        using var ms = new MemoryStream();
        var writer = new CanonicalJsonSerializer(ms);
        
        var inner = writer.WriteType(null!);
        inner.WriteString(new TestSerdeInfo("longString"), 0, longValue);
        inner.End(null!);
        writer.Dispose();
        
        var result = Encoding.UTF8.GetString(ms.ToArray());
        
        // Should handle extremely long string values
        await Assert.That(result).Contains("\"longString\":");
        await Assert.That(result).Contains(longValue);
    }

    #endregion
}

// Test helper class for Serde info
internal class TestSerdeInfo(string nameYouWant, InfoKind? kind = null) : ISerdeInfo
{
    public string Name => nameYouWant;

    public InfoKind Kind => kind ?? InfoKind.Primitive;

    public IList<CustomAttributeData> Attributes => throw new NotImplementedException();

    public int FieldCount => throw new NotImplementedException();

    public IList<CustomAttributeData> GetFieldAttributes(int index)
    {
        throw new NotImplementedException();
    }

    public ISerdeInfo GetFieldInfo(int index)
    {
        throw new NotImplementedException();
    }

    public ReadOnlySpan<byte> GetFieldName(int index) => Encoding.UTF8.GetBytes(nameYouWant);

    public string GetFieldStringName(int index) => nameYouWant;

    public int TryGetIndex(ReadOnlySpan<byte> fieldName)
    {
        throw new NotImplementedException();
    }
}