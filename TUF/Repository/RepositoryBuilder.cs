using TUF.Models;
using TUF.Models.Keys;
using TUF.Models.Primitives;
using TUF.Models.Roles;
using TUF.Models.Roles.Root;
using TUF.Models.Roles.Targets;
using TUF.Models.Roles.Snapshot;
using TUF.Models.Roles.Timestamp;
using TUF.Models.DigestAlgorithms;
using TUF.Signing;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using TUF.Serialization;

namespace TUF.Repository;

/// <summary>
/// High-level API for creating and managing TUF repositories
/// </summary>
public class RepositoryBuilder
{
    private readonly Dictionary<string, ISigner> _signers = new();
    private readonly Dictionary<string, List<string>> _roleSigners = new();
    private readonly List<TargetFile> _targets = new();
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
    public RepositoryBuilder AddTarget(string path, byte[] content, Dictionary<string, object>? custom = null)
    {
        _targets.Add(new TargetFile(path, content, custom));
        return this;
    }

    /// <summary>
    /// Add a target file from a file path
    /// </summary>
    public RepositoryBuilder AddTargetFromFile(string targetPath, string filePath, Dictionary<string, object>? custom = null)
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
        var rootRole = new Root(_defaultExpiry)
        {
            ConsistentSnapshot = _consistentSnapshots,
            Keys = rootKeys.Keys.Concat(timestampKeys.Keys)
                           .Concat(snapshotKeys.Keys)
                           .Concat(targetsKeys.Keys)
                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Roles = new RootRoles(
                Root: rootKeys.RoleKeys,
                Timestamp: timestampKeys.RoleKeys,
                Snapshot: snapshotKeys.RoleKeys,
                Targets: targetsKeys.RoleKeys,
                Mirrors: null
            )
        };

        var rootMetadata = new RootMetadata(rootRole, new Dictionary<KeyId, Signature>());
        SignMetadata<RootMetadata, Root>(rootMetadata, "root");

        // Build targets metadata
        var targetMetadataDict = CreateTargetMetadata();
        var targetsRole = new TargetsRole(
            SpecVersion: Constants.ImplementedSpecVersion,
            Version: 1,
            Expires: _defaultExpiry,
            Targets: targetMetadataDict
        );

        var targetsMetadata = new TargetsMetadata(targetsRole, new Dictionary<KeyId, Signature>());
        SignMetadata<TargetsMetadata, TargetsRole>(targetsMetadata, "targets");

        // Build snapshot metadata
        var snapshotRole = new Models.Roles.Snapshot.Snapshot(
            SpecVersion: Constants.ImplementedSpecVersion,
            Version: 1,
            Expires: _defaultExpiry,
            Meta: CreateSnapshotMeta(targetsMetadata)
        );

        var snapshotMetadata = new SnapshotMetadata(snapshotRole, new Dictionary<KeyId, Signature>());
        SignMetadata<SnapshotMetadata, Models.Roles.Snapshot.Snapshot>(snapshotMetadata, "snapshot");

        // Build timestamp metadata
        var timestampRole = new Models.Roles.Timestamp.Timestamp(
            SpecVersion: Constants.ImplementedSpecVersion,
            Version: 1,
            Expires: _defaultExpiry,
            Meta: CreateTimestampMeta(snapshotMetadata)
        );

        var timestampMetadata = new TimestampMetadata(timestampRole, new Dictionary<KeyId, Signature>());
        SignMetadata<TimestampMetadata, Models.Roles.Timestamp.Timestamp>(timestampMetadata, "timestamp");

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

        var keys = new Dictionary<KeyId, Models.Keys.Key>();
        var keyIds = new List<KeyId>();

        foreach (var signerId in signerIds)
        {
            var signer = _signers[signerId];
            keys[signer.Key.Id] = signer.Key;
            keyIds.Add(signer.Key.Id);
        }

        return new RoleConfiguration(
            new RoleKeys(keyIds, (uint)Math.Max(1, keyIds.Count)), // Default threshold = all keys
            keys
        );
    }

    private Dictionary<RelativePath, TargetMetadata> CreateTargetMetadata()
    {
        var result = new Dictionary<RelativePath, TargetMetadata>();

        foreach (var target in _targets)
        {
            var sha256Hash = System.Security.Cryptography.SHA256.HashData(target.Content);
            var hashes = new List<TUF.Models.DigestAlgorithms.DigestValue>
            {
                new TUF.Models.DigestAlgorithms.DigestValue<TUF.Models.DigestAlgorithms.SHA256>(Convert.ToHexString(sha256Hash).ToLowerInvariant())
            };

            var metadata = new TargetMetadata(
                Length: (uint)target.Content.Length,
                Hashes: hashes,
                Custom: target.Custom,
                Path: new RelativePath(target.Path)
            );

            result[new RelativePath(target.Path)] = metadata;
        }

        return result;
    }

    private Dictionary<RelativePath, FileMetadata> CreateSnapshotMeta(TargetsMetadata targetsMetadata)
    {
        var targetsBytes = JsonSerializer.SerializeToUtf8Bytes(targetsMetadata, TargetsMetadata.JsonTypeInfo(MetadataJsonContext.Default));
        
        var sha256Hash = System.Security.Cryptography.SHA256.HashData(targetsBytes);
        
        return new Dictionary<RelativePath, FileMetadata>
        {
            [new RelativePath("targets.json")] = new FileMetadata(
                Version: 1,
                Length: (uint)targetsBytes.Length,
                Hashes: new List<TUF.Models.DigestAlgorithms.DigestValue> { 
                    new TUF.Models.DigestAlgorithms.DigestValue<TUF.Models.DigestAlgorithms.SHA256>(Convert.ToHexString(sha256Hash).ToLowerInvariant())
                }
            )
        };
    }

    private Models.Roles.Timestamp.SnapshotFileMetadata CreateTimestampMeta(SnapshotMetadata snapshotMetadata)
    {
        var snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(snapshotMetadata, SnapshotMetadata.JsonTypeInfo(MetadataJsonContext.Default));
        
        var sha256Hash = System.Security.Cryptography.SHA256.HashData(snapshotBytes);
        
        var fileMetadata = new FileMetadata(
            Version: 1,
            Length: (uint)snapshotBytes.Length,
            Hashes: new List<TUF.Models.DigestAlgorithms.DigestValue> { 
                new TUF.Models.DigestAlgorithms.DigestValue<TUF.Models.DigestAlgorithms.SHA256>(Convert.ToHexString(sha256Hash).ToLowerInvariant())
            }
        );
        
        return new Models.Roles.Timestamp.SnapshotFileMetadata(fileMetadata);
    }

    private void SignMetadata<T, TInner>(T metadata, string role) 
        where T : IMetadata<T, TInner> 
        where TInner : IRole<TInner>
    {
        if (!_roleSigners.TryGetValue(role, out var signerIds))
            throw new InvalidOperationException($"No signers configured for {role}");

        bool first = true;
        foreach (var signerId in signerIds)
        {
            var signer = _signers[signerId];
            
            // Use the extension method explicitly
            var signature = signer.SignBytes(metadata.SignedBytes);
            
            if (first)
            {
                metadata.Signatures.Clear();
            }
            
            metadata.Signatures[signer.Key.Id] = signature;
            first = false;
        }
    }
}

/// <summary>
/// Represents a target file in the repository
/// </summary>
public record TargetFile(string Path, byte[] Content, Dictionary<string, object>? Custom = null);

/// <summary>
/// Internal helper for role configuration
/// </summary>
internal record RoleConfiguration(RoleKeys RoleKeys, Dictionary<KeyId, Models.Keys.Key> Keys);

/// <summary>
/// Represents a complete TUF repository
/// </summary>
public record TufRepository(
    RootMetadata Root,
    TimestampMetadata Timestamp,
    SnapshotMetadata Snapshot,
    TargetsMetadata Targets,
    Dictionary<string, TargetFile> TargetFiles
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

    private static void WriteMetadataFile<T>(string filePath, T metadata) where T : class
    {
        JsonTypeInfo<T> jsonTypeInfo = metadata switch
        {
            RootMetadata rm => (JsonTypeInfo<T>)(object)RootMetadata.JsonTypeInfo(MetadataJsonContext.Default),
            TimestampMetadata tm => (JsonTypeInfo<T>)(object)TimestampMetadata.JsonTypeInfo(MetadataJsonContext.Default),
            SnapshotMetadata sm => (JsonTypeInfo<T>)(object)SnapshotMetadata.JsonTypeInfo(MetadataJsonContext.Default),
            TargetsMetadata tarm => (JsonTypeInfo<T>)(object)TargetsMetadata.JsonTypeInfo(MetadataJsonContext.Default),
            _ => throw new ArgumentException($"Unknown metadata type: {typeof(T)}")
        };

        var json = JsonSerializer.Serialize(metadata, jsonTypeInfo);
        File.WriteAllText(filePath, json);
    }
}