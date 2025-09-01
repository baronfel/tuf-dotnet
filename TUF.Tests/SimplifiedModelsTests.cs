using TUF.Models;

namespace TUF.Tests;

public class SimplifiedModelsTests
{
    [Test]
    public async Task RootMetadata_ShouldSerializeToCorrectJsonFormat()
    {
        // Arrange
        var root = new Metadata<Root>
        {
            Signed = new Root
            {
                Type = "root",
                SpecVersion = "1.0.0",
                ConsistentSnapshot = true,
                Version = 1,
                Expires = "2025-01-01T00:00:00Z",
                Keys = new Dictionary<string, Key>
                {
                    ["ed25519_key_id"] = new Key
                    {
                        KeyType = "ed25519",
                        Scheme = "ed25519",
                        KeyVal = new KeyValue
                        {
                            Public = "abcd1234567890"
                        }
                    }
                },
                Roles = new Roles
                {
                    Root = new RoleKeys
                    {
                        KeyIds = new List<string> { "ed25519_key_id" },
                        Threshold = 1
                    },
                    Timestamp = new RoleKeys
                    {
                        KeyIds = new List<string> { "ed25519_key_id" },
                        Threshold = 1
                    },
                    Snapshot = new RoleKeys
                    {
                        KeyIds = new List<string> { "ed25519_key_id" },
                        Threshold = 1
                    },
                    Targets = new RoleKeys
                    {
                        KeyIds = new List<string> { "ed25519_key_id" },
                        Threshold = 1
                    }
                }
            },
            Signatures = new List<SignatureObject>
            {
                new SignatureObject
                {
                    KeyId = "ed25519_key_id",
                    Sig = "signature_hex_value"
                }
            }
        };

        // Act
        var json = Serde.Json.JsonSerializer.Serialize<Metadata<Root>, MetadataProxy.Ser<Root>>(root);

        // Assert
        await Assert.That(json).Contains("\"_type\":\"root\"");
        await Assert.That(json).Contains("\"spec_version\":\"1.0.0\"");
        await Assert.That(json).Contains("\"consistent_snapshot\":true");
        await Assert.That(json).Contains("\"version\":1");
        await Assert.That(json).Contains("\"expires\":\"2025-01-01T00:00:00Z\"");
        await Assert.That(json).Contains("\"keytype\":\"ed25519\"");
        await Assert.That(json).Contains("\"scheme\":\"ed25519\"");
        await Assert.That(json).Contains("\"keyval\":");
        await Assert.That(json).Contains("\"public\":\"abcd1234567890\"");
        await Assert.That(json).Contains("\"keyids\":");
        await Assert.That(json).Contains("\"threshold\":1");
        await Assert.That(json).Contains("\"signatures\":");
        await Assert.That(json).Contains("\"keyid\":\"ed25519_key_id\"");
        await Assert.That(json).Contains("\"sig\":\"signature_hex_value\"");
    }

    [Test]
    public async Task TimestampMetadata_ShouldSerializeToCorrectJsonFormat()
    {
        // Arrange
        var timestamp = new Metadata<Timestamp>
        {
            Signed = new Timestamp
            {
                Type = "timestamp",
                SpecVersion = "1.0.0", 
                Version = 1,
                Expires = "2025-01-01T00:00:00Z",
                Meta = new Dictionary<string, FileMetadata>
                {
                    ["snapshot.json"] = new FileMetadata
                    {
                        Version = 1,
                        Length = 1024,
                        Hashes = new Dictionary<string, string>
                        {
                            ["sha256"] = "abcdef123456"
                        }
                    }
                }
            },
            Signatures = new List<SignatureObject>
            {
                new SignatureObject
                {
                    KeyId = "timestamp_key_id",
                    Sig = "timestamp_signature"
                }
            }
        };

        // Act
        var json = Serde.Json.JsonSerializer.Serialize<Metadata<Timestamp>, MetadataProxy.Ser<Timestamp>>(timestamp);

        // Assert
        await Assert.That(json).Contains("\"_type\":\"timestamp\"");
        await Assert.That(json).Contains("\"spec_version\":\"1.0.0\"");
        await Assert.That(json).Contains("\"version\":1");
        await Assert.That(json).Contains("\"expires\":\"2025-01-01T00:00:00Z\"");
        await Assert.That(json).Contains("\"snapshot.json\":");
        await Assert.That(json).Contains("\"length\":1024");
        await Assert.That(json).Contains("\"hashes\":");
        await Assert.That(json).Contains("\"sha256\":\"abcdef123456\"");
    }

    [Test]
    public async Task TargetsMetadata_ShouldSerializeToCorrectJsonFormat()
    {
        // Arrange
        var targets = new Metadata<Targets>
        {
            Signed = new Targets
            {
                Type = "targets",
                SpecVersion = "1.0.0",
                Version = 1,
                Expires = "2025-01-01T00:00:00Z",
                TargetMap = new Dictionary<string, TargetFile>
                {
                    ["file1.txt"] = new TargetFile
                    {
                        Length = 100,
                        Hashes = new Dictionary<string, string>
                        {
                            ["sha256"] = "file1hash"
                        },
                        Custom = new Dictionary<string, string>
                        {
                            ["author"] = "test"
                        }
                    }
                },
                Delegations = new Delegations
                {
                    Keys = new Dictionary<string, Key>
                    {
                        ["delegation_key"] = new Key
                        {
                            KeyType = "rsa",
                            Scheme = "rsassa-pss-sha256",
                            KeyVal = new KeyValue
                            {
                                Public = "-----BEGIN PUBLIC KEY-----\ntest\n-----END PUBLIC KEY-----"
                            }
                        }
                    },
                    Roles = new List<DelegatedRole>
                    {
                        new DelegatedRole
                        {
                            Name = "delegated-role",
                            KeyIds = new List<string> { "delegation_key" },
                            Threshold = 1,
                            Paths = new List<string> { "delegated/*" },
                            Terminating = false
                        }
                    }
                }
            },
            Signatures = new List<SignatureObject>
            {
                new SignatureObject
                {
                    KeyId = "targets_key_id", 
                    Sig = "targets_signature"
                }
            }
        };

        // Act
        var json = Serde.Json.JsonSerializer.Serialize<Metadata<Targets>, MetadataProxy.Ser<Targets>>(targets);

        // Assert
        await Assert.That(json).Contains("\"_type\":\"targets\"");
        await Assert.That(json).Contains("\"file1.txt\":");
        await Assert.That(json).Contains("\"length\":100");
        await Assert.That(json).Contains("\"custom\":");
        await Assert.That(json).Contains("\"delegations\":");
        await Assert.That(json).Contains("\"name\":\"delegated-role\"");
        await Assert.That(json).Contains("\"paths\":");
        await Assert.That(json).Contains("\"terminating\":false");
    }

    [Test]
    public async Task RootMetadata_ShouldDeserializeFromJson()
    {
        // Arrange - Using TUF spec example format
        var json = @"{
            ""signed"": {
                ""_type"": ""root"",
                ""spec_version"": ""1.0.0"",
                ""consistent_snapshot"": false,
                ""version"": 1,
                ""expires"": ""2025-12-31T23:59:59Z"",
                ""keys"": {
                    ""test_key_id"": {
                        ""keytype"": ""ed25519"",
                        ""scheme"": ""ed25519"",
                        ""keyval"": {
                            ""public"": ""test_public_key_value""
                        }
                    }
                },
                ""roles"": {
                    ""root"": {
                        ""keyids"": [""test_key_id""],
                        ""threshold"": 1
                    },
                    ""timestamp"": {
                        ""keyids"": [""test_key_id""],
                        ""threshold"": 1
                    },
                    ""snapshot"": {
                        ""keyids"": [""test_key_id""],
                        ""threshold"": 1
                    },
                    ""targets"": {
                        ""keyids"": [""test_key_id""],
                        ""threshold"": 1
                    }
                }
            },
            ""signatures"": [
                {
                    ""keyid"": ""test_key_id"",
                    ""sig"": ""test_signature_value""
                }
            ]
        }";

        // Act
        var root = Serde.Json.JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(json);

        // Assert
        await Assert.That(root).IsNotNull();
        await Assert.That(root.Signed.Type).IsEqualTo("root");
        await Assert.That(root.Signed.SpecVersion).IsEqualTo("1.0.0");
        await Assert.That(root.Signed.ConsistentSnapshot).IsEqualTo(false);
        await Assert.That(root.Signed.Version).IsEqualTo(1);
        await Assert.That(root.Signed.Expires).IsEqualTo("2025-12-31T23:59:59Z");
        
        await Assert.That(root.Signed.Keys).HasSingleItem();
        await Assert.That(root.Signed.Keys.Keys).Contains("test_key_id");
        await Assert.That(root.Signed.Keys["test_key_id"].KeyType).IsEqualTo("ed25519");
        await Assert.That(root.Signed.Keys["test_key_id"].Scheme).IsEqualTo("ed25519");
        await Assert.That(root.Signed.Keys["test_key_id"].KeyVal.Public).IsEqualTo("test_public_key_value");
        
        await Assert.That(root.Signatures).HasSingleItem();
        await Assert.That(root.Signatures[0].KeyId).IsEqualTo("test_key_id");
        await Assert.That(root.Signatures[0].Sig).IsEqualTo("test_signature_value");
    }

    [Test]
    public async Task ExtensionMethods_ShouldBeAvailable()
    {
        // Arrange
        var rootMetadata = new Metadata<Root>
        {
            Signed = new Root
            {
                Type = "root",
                SpecVersion = "1.0.0",
                Keys = new Dictionary<string, Key>
                {
                    ["test_key"] = new Key
                    {
                        KeyType = "ed25519",
                        Scheme = "ed25519",
                        KeyVal = new KeyValue { Public = "abcd1234" }
                    }
                },
                Roles = new Roles
                {
                    Root = new RoleKeys { KeyIds = new List<string> { "test_key" }, Threshold = 1 }
                }
            },
            Signatures = new List<SignatureObject>()
        };

        var timestampMetadata = new Metadata<Timestamp>
        {
            Signed = new Timestamp { Type = "timestamp" },
            Signatures = new List<SignatureObject>
            {
                new SignatureObject { KeyId = "test_key", Sig = "abc123" }
            }
        };

        // Act & Assert - Just verify the extension methods are accessible and compile
        await Assert.That(rootMetadata.Signed.Keys).HasCount(1);
        await Assert.That(timestampMetadata.Signatures).HasCount(1);
        
        // Verify extension methods exist and are accessible (they compile)
        await Assert.That(typeof(SimplifiedMetadataExtensions)
            .GetMethod("VerifyRole")).IsNotNull();
    }
}