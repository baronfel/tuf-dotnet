using CanonicalJson;

using Serde;

using TUF.Models;

namespace TUF.Repository;

/// <summary>
/// High-level API for creating and managing TUF repositories
/// </summary>
public class RepositoryBuilder
{
    private readonly Dictionary<string, ISigner> _signers = new();
    private readonly Dictionary<string, List<string>> _roleSigners = new();
    private readonly List<TargetFileInfo> _targets = new();
    private DateTimeOffset _defaultExpiry = DateTimeOffset.UtcNow.AddYears(1);
    private bool _consistentSnapshots = true;

    /// <summary>
    /// Add a signer for a specific role
    /// </summary>
    public RepositoryBuilder AddSigner(string role, ISigner signer, string? signerId = null)
    {
        signerId ??= Guid.NewGuid().ToString();
        _signers[signerId] = signer;

        if (!_roleSigners.ContainsKey(role))
            _roleSigners[role] = new List<string>();
        _roleSigners[role].Add(signerId);

        return this;
    }

    /// <summary>
    /// Set default expiry time for all metadata
    /// </summary>
    public RepositoryBuilder SetDefaultExpiry(DateTimeOffset expiry)
    {
        _defaultExpiry = expiry;
        return this;
    }

    /// <summary>
    /// Set whether to use consistent snapshots
    /// </summary>
    public RepositoryBuilder SetConsistentSnapshots(bool enabled)
    {
        _consistentSnapshots = enabled;
        return this;
    }

    /// <summary>
    /// Add a target file to the repository
    /// </summary>
    public RepositoryBuilder AddTarget(string path, byte[] content, Dictionary<string, string>? custom = null)
    {
        _targets.Add(new TargetFileInfo(path, content, custom));
        return this;
    }

    /// <summary>
    /// Add a target file from a file path
    /// </summary>
    public RepositoryBuilder AddTargetFromFile(string targetPath, string filePath, Dictionary<string, string>? custom = null)
    {
        var content = File.ReadAllBytes(filePath);
        return AddTarget(targetPath, content, custom);
    }

    /// <summary>
    /// Build the TUF repository
    /// </summary>
    public TufRepository Build()
    {
        ValidateConfiguration();

        // Create role configurations
        var rootKeys = CreateKeysForRole("root");
        var timestampKeys = CreateKeysForRole("timestamp");
        var snapshotKeys = CreateKeysForRole("snapshot");
        var targetsKeys = CreateKeysForRole("targets");

        // Build root metadata
        var rootRole = new Root
        {
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = _defaultExpiry,
            ConsistentSnapshot = _consistentSnapshots,
            Keys = rootKeys.Keys.Concat(timestampKeys.Keys)
                           .Concat(snapshotKeys.Keys)
                           .Concat(targetsKeys.Keys)
                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Roles = new Roles
            {
                Root = rootKeys.RoleKeys,
                Timestamp = timestampKeys.RoleKeys,
                Snapshot = snapshotKeys.RoleKeys,
                Targets = targetsKeys.RoleKeys,
                Mirrors = null
            }
        };

        var rootMetadata = new Metadata<Root> { Signed = rootRole, Signatures = new List<SignatureObject>() };
        SignMetadata(rootMetadata, "root");

        // Build targets metadata
        var targetMetadataDict = CreateTargetMetadata();
        var targetsRole = new Targets
        {
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = _defaultExpiry,
            TargetMap = targetMetadataDict
        };

        var targetsMetadata = new Metadata<Targets> { Signed = targetsRole, Signatures = new List<SignatureObject>() };
        SignMetadata(targetsMetadata, "targets");

        // Build snapshot metadata
        var snapshotRole = new Snapshot
        {
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = _defaultExpiry,
            Meta = CreateSnapshotMeta(targetsMetadata)
        };

        var snapshotMetadata = new Metadata<Snapshot> { Signed = snapshotRole, Signatures = new List<SignatureObject>() };
        SignMetadata(snapshotMetadata, "snapshot");

        // Build timestamp metadata
        var timestampRole = new Timestamp
        {
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = _defaultExpiry,
            Meta = CreateTimestampMeta(snapshotMetadata)
        };

        var timestampMetadata = new Metadata<Timestamp> { Signed = timestampRole, Signatures = new List<SignatureObject>() };
        SignMetadata(timestampMetadata, "timestamp");

        return new TufRepository(
            rootMetadata,
            timestampMetadata,
            snapshotMetadata,
            targetsMetadata,
            _targets.ToDictionary(t => t.Path, t => t)
        );
    }

    private void ValidateConfiguration()
    {
        var requiredRoles = new[] { "root", "timestamp", "snapshot", "targets" };

        foreach (var role in requiredRoles)
        {
            if (!_roleSigners.ContainsKey(role) || _roleSigners[role].Count == 0)
                throw new InvalidOperationException($"No signers configured for {role} role");
        }
    }

    private RoleConfiguration CreateKeysForRole(string role)
    {
        if (!_roleSigners.TryGetValue(role, out var signerIds))
            throw new InvalidOperationException($"No signers configured for {role}");

        var keys = new Dictionary<string, Key>();
        var keyIds = new List<string>();

        foreach (var signerId in signerIds)
        {
            var signer = _signers[signerId];
            var keyId = signer.Key.GetKeyId(); // Use extension method to compute key ID
            keys[keyId] = signer.Key;
            keyIds.Add(keyId);
        }

        return new RoleConfiguration(
            new RoleKeys { KeyIds = keyIds, Threshold = Math.Max(1, keyIds.Count) }, // Default threshold = all keys
            keys
        );
    }

    private Dictionary<string, TargetFile> CreateTargetMetadata()
    {
        var result = new Dictionary<string, TargetFile>();

        foreach (var target in _targets)
        {
            var sha256Hash = System.Security.Cryptography.SHA256.HashData(target.Content);
            var hashes = new Dictionary<string, string>
            {
                ["sha256"] = Convert.ToHexString(sha256Hash).ToLowerInvariant()
            };

            var metadata = new TargetFile
            {
                Length = target.Content.Length,
                Hashes = hashes,
                Custom = target.Custom
            };

            result[target.Path] = metadata;
        }

        return result;
    }

    private Dictionary<string, FileMetadata> CreateSnapshotMeta(Metadata<Targets> targetsMetadata)
    {
        var targetsBytes = System.Text.Encoding.UTF8.GetBytes(Serde.Json.JsonSerializer.Serialize<Metadata<Targets>, MetadataProxy.Ser<Targets>>(targetsMetadata));

        var sha256Hash = System.Security.Cryptography.SHA256.HashData(targetsBytes);
        var hashes = new Dictionary<string, string>
        {
            ["sha256"] = Convert.ToHexString(sha256Hash).ToLowerInvariant()
        };

        return new Dictionary<string, FileMetadata>
        {
            ["targets.json"] = new FileMetadata
            {
                Version = 1,
                Length = targetsBytes.Length,
                Hashes = hashes
            }
        };
    }

    private Dictionary<string, FileMetadata> CreateTimestampMeta(Metadata<Snapshot> snapshotMetadata)
    {
        var snapshotBytes = System.Text.Encoding.UTF8.GetBytes(Serde.Json.JsonSerializer.Serialize<Metadata<Snapshot>, MetadataProxy.Ser<Snapshot>>(snapshotMetadata));

        var sha256Hash = System.Security.Cryptography.SHA256.HashData(snapshotBytes);
        var hashes = new Dictionary<string, string>
        {
            ["sha256"] = Convert.ToHexString(sha256Hash).ToLowerInvariant()
        };

        return new Dictionary<string, FileMetadata>
        {
            ["snapshot.json"] = new FileMetadata
            {
                Version = 1,
                Length = snapshotBytes.Length,
                Hashes = hashes
            }
        };
    }

    private void SignMetadata<T>(Metadata<T> metadata, string role)
        where T : ISerializeProvider<T>, IDeserializeProvider<T>
    {
        if (!_roleSigners.TryGetValue(role, out var signerIds))
            throw new InvalidOperationException($"No signers configured for {role}");

        metadata.Signatures.Clear();

        foreach (var signerId in signerIds)
        {
            var signer = _signers[signerId];

            // Get signed bytes using the GetSignedBytes method for consistency
            var signedBytes = metadata.GetSignedBytes();
            var signature = signer.SignBytes(signedBytes);

            metadata.Signatures.Add(signature);
        }
    }
}

/// <summary>
/// Represents a target file in the repository builder
/// </summary>
public record TargetFileInfo(string Path, byte[] Content, Dictionary<string, string>? Custom = null);

/// <summary>
/// Internal helper for role configuration
/// </summary>
internal record RoleConfiguration(RoleKeys RoleKeys, Dictionary<string, Key> Keys);

/// <summary>
/// Represents a complete TUF repository
/// </summary>
public record TufRepository(
    Metadata<Root> Root,
    Metadata<Timestamp> Timestamp,
    Metadata<Snapshot> Snapshot,
    Metadata<Targets> Targets,
    Dictionary<string, TargetFileInfo> TargetFiles
)
{
    /// <summary>
    /// Write the repository to a directory structure
    /// </summary>
    public void WriteToDirectory(string basePath)
    {
        var metadataDir = Path.Combine(basePath, "metadata");
        var targetsDir = Path.Combine(basePath, "targets");

        Directory.CreateDirectory(metadataDir);
        Directory.CreateDirectory(targetsDir);

        // Write metadata files
        WriteMetadataFile(Path.Combine(metadataDir, "root.json"), Root);
        WriteMetadataFile(Path.Combine(metadataDir, "timestamp.json"), Timestamp);
        WriteMetadataFile(Path.Combine(metadataDir, "snapshot.json"), Snapshot);
        WriteMetadataFile(Path.Combine(metadataDir, "targets.json"), Targets);

        // Write target files
        foreach (var targetFile in TargetFiles.Values)
        {
            var targetPath = Path.Combine(targetsDir, targetFile.Path.Replace('/', Path.DirectorySeparatorChar));
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
                Directory.CreateDirectory(targetDir);

            File.WriteAllBytes(targetPath, targetFile.Content);
        }
    }

    private static void WriteMetadataFile<T>(string filePath, Metadata<T> metadata)
        where T : class, ISerializeProvider<T>, IDeserializeProvider<T>
    {
        var jsonBytes = CanonicalJson.Serializer.Serialize<Metadata<T>, MetadataProxy.Ser<T>>(metadata);
        var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
        File.WriteAllText(filePath, json);
    }
}