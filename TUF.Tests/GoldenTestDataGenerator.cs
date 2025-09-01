using TUF.Models;
using System.Text.Json;

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
        var rootSignature = rootSigner.SignBytes(System.Text.Encoding.UTF8.GetBytes(
            Serde.Json.JsonSerializer.Serialize(rootMetadata.Signed)));
        rootMetadata = rootMetadata with { Signatures = [rootSignature] };

        // Create timestamp metadata
        var timestampMetadata = CreateTimestampMetadata();
        var timestampSignedBytes = System.Text.Encoding.UTF8.GetBytes(
            Serde.Json.JsonSerializer.Serialize(timestampMetadata.Signed));
        var timestampSignature = timestampSigner.SignBytes(timestampSignedBytes);
        timestampMetadata = timestampMetadata with { Signatures = [timestampSignature] };

        // Create snapshot metadata
        var snapshotMetadata = CreateSnapshotMetadata();
        var snapshotSignedBytes = System.Text.Encoding.UTF8.GetBytes(
            Serde.Json.JsonSerializer.Serialize(snapshotMetadata.Signed));
        var snapshotSignature = snapshotSigner.SignBytes(snapshotSignedBytes);
        snapshotMetadata = snapshotMetadata with { Signatures = [snapshotSignature] };

        // Create targets metadata
        var targetsMetadata = CreateTargetsMetadata();
        var targetsSignedBytes = System.Text.Encoding.UTF8.GetBytes(
            Serde.Json.JsonSerializer.Serialize(targetsMetadata.Signed));
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
                Expires = DateTimeOffset.UtcNow.AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ"),
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
                Expires = DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ"),
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
                Expires = DateTimeOffset.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ssZ"),
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
                Expires = DateTimeOffset.UtcNow.AddDays(30).ToString("yyyy-MM-ddTHH:mm:ssZ"),
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