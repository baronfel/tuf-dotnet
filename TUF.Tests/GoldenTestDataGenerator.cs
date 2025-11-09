using System.Text.Json;

using CanonicalJson;

using TUF.Models;

namespace TUF.Tests;

/// <summary>
/// Generates valid TUF test data with real signatures for use in tests.
/// This ensures we have valid metadata that will pass cryptographic verification.
/// </summary>
public static class GoldenTestDataGenerator
{
    public static GoldenTestData Generate()
    {
        // Create signers for each role
        var rootSigner = Ed25519Signer.Generate();
        var timestampSigner = Ed25519Signer.Generate();
        var snapshotSigner = Ed25519Signer.Generate();
        var targetsSigner = Ed25519Signer.Generate();

        // Create root metadata
        var rootMetadata = CreateRootMetadata(rootSigner, timestampSigner, snapshotSigner, targetsSigner);
        var rootJson = Serde.Json.JsonSerializer.Serialize<Metadata<Root>, MetadataProxy.Ser<Root>>(rootMetadata);
        var rootSignature = rootSigner.SignBytes(
            CanonicalJson.Serializer.Serialize(rootMetadata.Signed));
        rootMetadata = rootMetadata with { Signatures = [rootSignature] };

        // Create timestamp metadata
        var timestampMetadata = CreateTimestampMetadata();
        var timestampSignedBytes = CanonicalJson.Serializer.Serialize(timestampMetadata.Signed);
        var timestampSignature = timestampSigner.SignBytes(timestampSignedBytes);
        timestampMetadata = timestampMetadata with { Signatures = [timestampSignature] };

        // Create snapshot metadata
        var snapshotMetadata = CreateSnapshotMetadata();
        var snapshotSignedBytes = CanonicalJson.Serializer.Serialize(snapshotMetadata.Signed);
        var snapshotSignature = snapshotSigner.SignBytes(snapshotSignedBytes);
        snapshotMetadata = snapshotMetadata with { Signatures = [snapshotSignature] };

        // Create targets metadata
        var targetsMetadata = CreateTargetsMetadata();
        var targetsSignedBytes = CanonicalJson.Serializer.Serialize(targetsMetadata.Signed);
        var targetsSignature = targetsSigner.SignBytes(targetsSignedBytes);
        targetsMetadata = targetsMetadata with { Signatures = [targetsSignature] };

        // Serialize all to JSON
        var rootJsonFinal = Serde.Json.JsonSerializer.Serialize<Metadata<Root>, MetadataProxy.Ser<Root>>(rootMetadata);
        var timestampJsonFinal = Serde.Json.JsonSerializer.Serialize<Metadata<Timestamp>, MetadataProxy.Ser<Timestamp>>(timestampMetadata);
        var snapshotJsonFinal = Serde.Json.JsonSerializer.Serialize<Metadata<Snapshot>, MetadataProxy.Ser<Snapshot>>(snapshotMetadata);
        var targetsJsonFinal = Serde.Json.JsonSerializer.Serialize<Metadata<Targets>, MetadataProxy.Ser<Targets>>(targetsMetadata);

        return new GoldenTestData(
            rootJsonFinal,
            timestampJsonFinal,
            snapshotJsonFinal,
            targetsJsonFinal,
            rootMetadata,
            timestampMetadata,
            snapshotMetadata,
            targetsMetadata
        );
    }

    /// <summary>
    /// Generates test data using multiple cryptographic algorithms to test cross-implementation compatibility.
    /// This creates a more diverse set of test vectors that better reflect real-world usage.
    /// </summary>
    public static MultiAlgorithmGoldenTestData GenerateMultiAlgorithm()
    {
        // Create signers using different algorithms
        var ed25519RootSigner = Ed25519Signer.Generate();
        var rsaTimestampSigner = RsaSigner.Generate(2048);
        var ecdsaSnapshotSigner = EcdsaSigner.Generate();
        var ed25519TargetsSigner = Ed25519Signer.Generate();

        // Create multi-algorithm root metadata
        var rootMetadata = CreateMultiAlgorithmRootMetadata(ed25519RootSigner, rsaTimestampSigner, ecdsaSnapshotSigner, ed25519TargetsSigner);
        var rootSignedBytes = CanonicalJson.Serializer.Serialize(rootMetadata.Signed);
        var rootSignature = ed25519RootSigner.SignBytes(rootSignedBytes);
        rootMetadata = rootMetadata with { Signatures = [rootSignature] };

        // Create timestamp metadata with RSA signature
        var timestampMetadata = CreateTimestampMetadata();
        var timestampSignedBytes = CanonicalJson.Serializer.Serialize(timestampMetadata.Signed);
        var timestampSignature = rsaTimestampSigner.SignBytes(timestampSignedBytes);
        timestampMetadata = timestampMetadata with { Signatures = [timestampSignature] };

        // Create snapshot metadata with ECDSA signature
        var snapshotMetadata = CreateSnapshotMetadata();
        var snapshotSignedBytes = CanonicalJson.Serializer.Serialize(snapshotMetadata.Signed);
        var snapshotSignature = ecdsaSnapshotSigner.SignBytes(snapshotSignedBytes);
        snapshotMetadata = snapshotMetadata with { Signatures = [snapshotSignature] };

        // Create targets metadata with Ed25519 signature
        var targetsMetadata = CreateComplexTargetsMetadata();
        var targetsSignedBytes = CanonicalJson.Serializer.Serialize(targetsMetadata.Signed);
        var targetsSignature = ed25519TargetsSigner.SignBytes(targetsSignedBytes);
        targetsMetadata = targetsMetadata with { Signatures = [targetsSignature] };

        return new MultiAlgorithmGoldenTestData(
            rootMetadata,
            timestampMetadata,
            snapshotMetadata,
            targetsMetadata,
            new AlgorithmSigners(ed25519RootSigner, rsaTimestampSigner, ecdsaSnapshotSigner, ed25519TargetsSigner)
        );
    }

    private static Metadata<Root> CreateRootMetadata(Ed25519Signer rootSigner, Ed25519Signer timestampSigner,
        Ed25519Signer snapshotSigner, Ed25519Signer targetsSigner)
    {
        var rootKeyId = rootSigner.Key.GetKeyId();
        var timestampKeyId = timestampSigner.Key.GetKeyId();
        var snapshotKeyId = snapshotSigner.Key.GetKeyId();
        var targetsKeyId = targetsSigner.Key.GetKeyId();

        return new Metadata<Root>
        {
            Signed = new Root
            {
                Type = "root",
                SpecVersion = "1.0.0",
                Version = 1,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Keys = new Dictionary<string, Key>
                {
                    [rootKeyId] = rootSigner.Key,
                    [timestampKeyId] = timestampSigner.Key,
                    [snapshotKeyId] = snapshotSigner.Key,
                    [targetsKeyId] = targetsSigner.Key
                },
                Roles = new Roles
                {
                    Root = new RoleKeys { KeyIds = [rootKeyId], Threshold = 1 },
                    Timestamp = new RoleKeys { KeyIds = [timestampKeyId], Threshold = 1 },
                    Snapshot = new RoleKeys { KeyIds = [snapshotKeyId], Threshold = 1 },
                    Targets = new RoleKeys { KeyIds = [targetsKeyId], Threshold = 1 }
                }
            },
            Signatures = [] // Will be filled in later
        };
    }

    private static Metadata<Timestamp> CreateTimestampMetadata()
    {
        return new Metadata<Timestamp>
        {
            Signed = new Timestamp
            {
                Type = "timestamp",
                SpecVersion = "1.0.0",
                Version = 1,
                Expires = DateTimeOffset.UtcNow.AddDays(1),
                Meta = new Dictionary<string, FileMetadata>
                {
                    ["snapshot.json"] = new FileMetadata
                    {
                        Version = 1,
                        Length = 500, // Will be updated with actual snapshot size
                        Hashes = new Dictionary<string, string>
                        {
                            ["sha256"] = "placeholder_hash_will_be_updated"
                        }
                    }
                }
            },
            Signatures = [] // Will be filled in later
        };
    }

    private static Metadata<Snapshot> CreateSnapshotMetadata()
    {
        return new Metadata<Snapshot>
        {
            Signed = new Snapshot
            {
                Type = "snapshot",
                SpecVersion = "1.0.0",
                Version = 1,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Meta = new Dictionary<string, FileMetadata>
                {
                    ["targets.json"] = new FileMetadata
                    {
                        Version = 1,
                        Length = 800, // Will be updated with actual targets size
                        Hashes = new Dictionary<string, string>
                        {
                            ["sha256"] = "placeholder_targets_hash_will_be_updated"
                        }
                    }
                }
            },
            Signatures = [] // Will be filled in later
        };
    }

    private static Metadata<Targets> CreateTargetsMetadata()
    {
        return new Metadata<Targets>
        {
            Signed = new Targets
            {
                Type = "targets",
                SpecVersion = "1.0.0",
                Version = 1,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                TargetMap = new Dictionary<string, TargetFile>
                {
                    ["hello.txt"] = new TargetFile
                    {
                        Length = 13,
                        Hashes = new Dictionary<string, string>
                        {
                            ["sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("Hello, world!"u8.ToArray())).ToLowerInvariant()
                        }
                    }
                }
            },
            Signatures = [] // Will be filled in later
        };
    }

    private static Metadata<Root> CreateMultiAlgorithmRootMetadata(Ed25519Signer ed25519Signer, RsaSigner rsaSigner,
        EcdsaSigner ecdsaSigner, Ed25519Signer targetsSigner)
    {
        var ed25519KeyId = ed25519Signer.Key.GetKeyId();
        var rsaKeyId = rsaSigner.Key.GetKeyId();
        var ecdsaKeyId = ecdsaSigner.Key.GetKeyId();
        var targetsKeyId = targetsSigner.Key.GetKeyId();

        return new Metadata<Root>
        {
            Signed = new Root
            {
                Type = "root",
                SpecVersion = "1.0.0",
                Version = 1,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                ConsistentSnapshot = true,
                Keys = new Dictionary<string, Key>
                {
                    [ed25519KeyId] = ed25519Signer.Key,
                    [rsaKeyId] = rsaSigner.Key,
                    [ecdsaKeyId] = ecdsaSigner.Key,
                    [targetsKeyId] = targetsSigner.Key
                },
                Roles = new Roles
                {
                    Root = new RoleKeys { KeyIds = [ed25519KeyId], Threshold = 1 },
                    Timestamp = new RoleKeys { KeyIds = [rsaKeyId], Threshold = 1 },
                    Snapshot = new RoleKeys { KeyIds = [ecdsaKeyId], Threshold = 1 },
                    Targets = new RoleKeys { KeyIds = [targetsKeyId], Threshold = 1 }
                }
            },
            Signatures = []
        };
    }

    private static Metadata<Targets> CreateComplexTargetsMetadata()
    {
        return new Metadata<Targets>
        {
            Signed = new Targets
            {
                Type = "targets",
                SpecVersion = "1.0.0",
                Version = 1,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                TargetMap = new Dictionary<string, TargetFile>
                {
                    ["hello.txt"] = new TargetFile
                    {
                        Length = 13,
                        Hashes = new Dictionary<string, string>
                        {
                            ["sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("Hello, world!"u8.ToArray())).ToLowerInvariant()
                        }
                    },
                    ["binary-data.bin"] = new TargetFile
                    {
                        Length = 1024,
                        Hashes = new Dictionary<string, string>
                        {
                            ["sha256"] = "e258d248fda94c63753607f7c4494ee0fcbe92f1a76bfdac795c9d84101eb317",
                            ["sha512"] = "ba3253876aed6bc22d4a6739b0be73f7a1b74b3d0aebc29b8b2c2f6a2d5e6f7c8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8"
                        }
                    },
                    ["config/app.json"] = new TargetFile
                    {
                        Length = 256,
                        Hashes = new Dictionary<string, string>
                        {
                            ["sha256"] = "d4735e3a265e16eee03f59718b9b5d03019c07d8b6c51f90da3a666eec13ab35"
                        }
                    }
                }
            },
            Signatures = []
        };
    }
}

/// <summary>
/// Container for valid TUF test data with real signatures.
/// </summary>
public record GoldenTestData(
    string RootJson,
    string TimestampJson,
    string SnapshotJson,
    string TargetsJson,
    Metadata<Root> RootMetadata,
    Metadata<Timestamp> TimestampMetadata,
    Metadata<Snapshot> SnapshotMetadata,
    Metadata<Targets> TargetsMetadata
);

/// <summary>
/// Container for multi-algorithm TUF test data with different signature types.
/// </summary>
public record MultiAlgorithmGoldenTestData(
    Metadata<Root> RootMetadata,
    Metadata<Timestamp> TimestampMetadata,
    Metadata<Snapshot> SnapshotMetadata,
    Metadata<Targets> TargetsMetadata,
    AlgorithmSigners Signers
);

/// <summary>
/// Container for signers using different cryptographic algorithms.
/// </summary>
public record AlgorithmSigners(
    Ed25519Signer Ed25519RootSigner,
    RsaSigner RsaTimestampSigner,
    EcdsaSigner EcdsaSnapshotSigner,
    Ed25519Signer Ed25519TargetsSigner
);