using System.Diagnostics.CodeAnalysis;

using Serde;

namespace TUF.Models;

/// <summary>
/// Represents trusted TUF metadata in various states of completion.
/// Provides secure, progressive loading of metadata following TUF specification order and verification requirements.
/// </summary>
/// <remarks>
/// TUF metadata must be loaded and verified in a specific order to maintain security:
/// 1. Root metadata (establishes trust)
/// 2. Timestamp metadata (provides freshness)
/// 3. Snapshot metadata (provides consistency)
/// 4. Targets metadata (provides file catalog)
/// 
/// Each stage validates the next using cryptographic signatures and role delegation,
/// ensuring the entire metadata chain is trustworthy.
/// </remarks>
public class TrustedMetadata
{
    /// <summary>
    /// Root metadata containing the foundation of trust for the repository.
    /// Contains key assignments and role definitions for all other metadata types.
    /// </summary>
    public Metadata<Root> Root { get; private set; }

    /// <summary>
    /// Reference time used for expiration checks.
    /// All metadata expiration times are evaluated against this timestamp.
    /// </summary>
    public DateTimeOffset RefTime { get; init; }

    /// <summary>
    /// Creates a new trusted metadata instance starting with root metadata.
    /// </summary>
    /// <param name="root">Root metadata that has been independently verified</param>
    /// <param name="refTime">Reference time for expiration checks</param>
    protected TrustedMetadata(Metadata<Root> root, DateTimeOffset refTime)
    {
        Root = root;
        RefTime = refTime;
    }

    /// <summary>
    /// Creates trusted metadata from root metadata bytes.
    /// This is the entry point for establishing trust in a TUF repository.
    /// </summary>
    /// <param name="rootData">JSON bytes of root metadata</param>
    /// <returns>Trusted metadata initialized with verified root</returns>
    /// <exception cref="Exception">Thrown if root metadata is invalid or verification fails</exception>
    /// <remarks>
    /// Root metadata is special because it is self-signed - it must be independently
    /// verified through out-of-band means (secure distribution, key fingerprint verification, etc.)
    /// before being trusted.
    /// </remarks>
    public static TrustedMetadata CreateFromRootData(byte[] rootData)
    {
        var refTime = DateTimeOffset.UtcNow;

        // Deserialize root metadata with caching
        var rootMetadata = PerformanceCache.GetOrComputeDeserialization(
            rootData,
            () => Serde.Json.JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(rootData, MetadataProxy.De<Root>.Instance));
        if (rootMetadata == null)
        {
            throw new Exception("Failed to deserialize root metadata");
        }

        // Root metadata is self-signed, so we verify it against itself
        rootMetadata.VerifyRole("root", rootMetadata);

        // Check if root is expired
        if (IsExpired(rootMetadata.Signed.Expires, refTime))
        {
            throw new Exception("Root metadata is expired");
        }

        return new TrustedMetadata(rootMetadata, refTime);
    }

    /// <summary>
    /// Updates root metadata to a new version.
    /// Verifies the new root using the current root's trust relationships.
    /// </summary>
    /// <param name="rootData">JSON bytes of new root metadata</param>
    /// <returns>This trusted metadata instance with updated root</returns>
    /// <exception cref="Exception">Thrown if verification fails or version is invalid</exception>
    /// <remarks>
    /// Root updates require careful verification:
    /// 1. New root must be signed by keys authorized in current root
    /// 2. Version must increment by exactly 1
    /// 3. New root must self-verify (ensuring key rotation is valid)
    /// </remarks>
    public virtual TrustedMetadata UpdateRoot(byte[] rootData)
    {
        var newRoot = PerformanceCache.GetOrComputeDeserialization(
            rootData,
            () => Serde.Json.JsonSerializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(rootData, MetadataProxy.De<Root>.Instance));
        if (newRoot == null)
        {
            throw new Exception("Failed to deserialize new root metadata");
        }

        // Verify new root against current root's delegation
        Root.VerifyRole("root", newRoot);

        // Verify version increment (must be exactly +1 for root)
        if (newRoot.Signed.Version != Root.Signed.Version + 1)
        {
            throw new Exception($"Invalid root version: expected {Root.Signed.Version + 1}, got {newRoot.Signed.Version}");
        }

        // New root must self-verify to ensure key rotation is valid
        newRoot.VerifyRole("root", newRoot);

        // Check expiration
        if (IsExpired(newRoot.Signed.Expires, RefTime))
        {
            throw new Exception("New root metadata is expired");
        }

        Root = newRoot;
        return this;
    }

    /// <summary>
    /// Updates timestamp metadata. This advances the trusted metadata to include timestamp information.
    /// </summary>
    /// <param name="timestampData">JSON bytes of timestamp metadata</param>
    /// <returns>New trusted metadata instance including timestamp</returns>
    /// <exception cref="Exception">Thrown if verification fails or root is expired</exception>
    public virtual TrustedMetadataWithTimestamp UpdateTimestamp(byte[] timestampData)
    {
        // Root must not be expired before we can update timestamp
        if (IsExpired(Root.Signed.Expires, RefTime))
        {
            throw new Exception("Cannot update timestamp with expired root metadata");
        }

        var timestamp = PerformanceCache.GetOrComputeDeserialization(
            timestampData,
            () => Serde.Json.JsonSerializer.Deserialize<Metadata<Timestamp>, MetadataProxy.De<Timestamp>>(timestampData, MetadataProxy.De<Timestamp>.Instance));
        if (timestamp == null)
        {
            throw new Exception("Failed to deserialize timestamp metadata");
        }

        // Verify timestamp against root's timestamp role
        Root.VerifyRole("timestamp", timestamp);

        // Check expiration
        if (IsExpired(timestamp.Signed.Expires, RefTime))
        {
            throw new Exception("Timestamp metadata is expired");
        }

        return new TrustedMetadataWithTimestamp(Root, timestamp, RefTime);
    }

    /// <summary>
    /// Helper method to check if metadata has expired.
    /// </summary>
    /// <param name="expiresString">ISO 8601 expiration timestamp</param>
    /// <param name="refTime">Reference time to compare against</param>
    /// <returns>True if expired, false otherwise</returns>
    protected static bool IsExpired(DateTimeOffset expires, DateTimeOffset refTime)
    {
        return refTime >= expires;
    }

    // Virtual methods that are only valid in later stages
    public virtual TrustedMetadataWithTimestamp UpdateSnapshot(byte[] snapshotData, bool isTrusted)
    {
        throw new InvalidOperationException("Cannot update snapshot before timestamp");
    }

    public virtual TrustedMetadataWithTimestamp UpdateDelegatedTargets(byte[] delegatedTargetsData, string roleName, string delegatorRoleName)
    {
        throw new InvalidOperationException("Cannot update delegated targets before snapshot");
    }
}

/// <summary>
/// Trusted metadata that includes timestamp information.
/// Provides snapshot file metadata and enables loading snapshot metadata.
/// </summary>
[method: SetsRequiredMembers]
public class TrustedMetadataWithTimestamp(Metadata<Root> root, Metadata<Timestamp> timestamp, DateTimeOffset refTime)
    : TrustedMetadata(root, refTime)
{
    /// <summary>
    /// Timestamp metadata providing freshness guarantees and snapshot file metadata.
    /// </summary>
    public Metadata<Timestamp> Timestamp { get; private set; } = timestamp;

    /// <summary>
    /// Gets the snapshot file metadata from the timestamp.
    /// This describes the expected snapshot file (version, length, hashes).
    /// </summary>
    protected FileMetadata SnapshotMeta => Timestamp.Signed.Meta["snapshot.json"];

    /// <summary>
    /// Updates timestamp metadata with version and freshness validation.
    /// </summary>
    /// <param name="timestampData">JSON bytes of new timestamp metadata</param>
    /// <returns>New or existing trusted metadata instance</returns>
    /// <exception cref="Exception">Thrown if verification fails or versions are invalid</exception>
    public override TrustedMetadataWithTimestamp UpdateTimestamp(byte[] timestampData)
    {
        if (IsExpired(Root.Signed.Expires, RefTime))
        {
            throw new Exception("Cannot update timestamp with expired root metadata");
        }

        var newTimestamp = PerformanceCache.GetOrComputeDeserialization(
            timestampData,
            () => Serde.Json.JsonSerializer.Deserialize<Metadata<Timestamp>, MetadataProxy.De<Timestamp>>(timestampData, MetadataProxy.De<Timestamp>.Instance));
        if (newTimestamp == null)
        {
            throw new Exception("Failed to deserialize new timestamp metadata");
        }

        // Verify against root
        Root.VerifyRole("timestamp", newTimestamp);

        // Check version progression
        if (newTimestamp.Signed.Version < Timestamp.Signed.Version)
        {
            throw new Exception("New timestamp version is older than current timestamp version");
        }

        // If same version, keep existing
        if (newTimestamp.Signed.Version == Timestamp.Signed.Version)
        {
            return this;
        }

        // Check expiration
        if (IsExpired(newTimestamp.Signed.Expires, RefTime))
        {
            throw new Exception("New timestamp metadata is expired");
        }

        // Prevent snapshot rollback
        var newSnapshotMeta = newTimestamp.Signed.Meta["snapshot.json"];
        if (newSnapshotMeta.Version < SnapshotMeta.Version)
        {
            throw new Exception("New snapshot version is older than current snapshot version");
        }

        return new TrustedMetadataWithTimestamp(Root, newTimestamp, RefTime);
    }

    /// <summary>
    /// Updates snapshot metadata. Advances to the next stage of trusted metadata.
    /// </summary>
    /// <param name="snapshotData">JSON bytes of snapshot metadata</param>
    /// <param name="isTrusted">Whether the data comes from a trusted source (bypasses length/hash verification)</param>
    /// <returns>Trusted metadata including snapshot information</returns>
    public override TrustedMetadataWithSnapshot UpdateSnapshot(byte[] snapshotData, bool isTrusted)
    {
        // Verify integrity if not from trusted source
        if (!isTrusted)
        {
            VerifyFileIntegrity(snapshotData, SnapshotMeta);
        }

        var snapshot = PerformanceCache.GetOrComputeDeserialization(
            snapshotData,
            () => Serde.Json.JsonSerializer.Deserialize<Metadata<Snapshot>, MetadataProxy.De<Snapshot>>(snapshotData, MetadataProxy.De<Snapshot>.Instance));
        if (snapshot == null)
        {
            throw new Exception("Failed to deserialize snapshot metadata");
        }

        // Verify against root's snapshot role
        Root.VerifyRole("snapshot", snapshot);

        // Version must match what timestamp expects
        if (snapshot.Signed.Version != SnapshotMeta.Version)
        {
            throw new Exception("Snapshot version doesn't match timestamp metadata");
        }

        // Check expiration
        if (IsExpired(snapshot.Signed.Expires, RefTime))
        {
            throw new Exception("Snapshot metadata is expired");
        }

        return new TrustedMetadataWithSnapshot(Root, Timestamp, snapshot, RefTime);
    }

    /// <summary>
    /// Verifies file integrity using length and cryptographic hashes.
    /// </summary>
    /// <param name="data">File data to verify</param>
    /// <param name="fileMeta">Expected file metadata (length, hashes)</param>
    /// <exception cref="Exception">Thrown if verification fails</exception>
    public static void VerifyFileIntegrity(byte[] data, FileMetadata fileMeta)
    {
        // Verify length
        if (data.Length != fileMeta.Length)
        {
            throw new Exception($"File length mismatch: expected {fileMeta.Length}, got {data.Length}");
        }

        // Verify at least one hash
        bool hashVerified = false;
        if (fileMeta.Hashes is null)
        {
            throw new Exception("File metadata is missing hashes");
        }
        foreach (var (algorithm, expectedHash) in fileMeta.Hashes)
        {
            string? actualHash = algorithm.ToLowerInvariant() switch
            {
                "sha256" => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant(),
                "sha512" => Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(data)).ToLowerInvariant(),
                _ => null // Skip unsupported hash algorithms
            };

            if (actualHash != null && actualHash == expectedHash.ToLowerInvariant())
            {
                hashVerified = true;
                break;
            }
        }

        if (!hashVerified)
        {
            throw new Exception("No hash verification succeeded");
        }
    }
}

/// <summary>
/// Trusted metadata that includes snapshot information.
/// Provides consistent view of all target metadata and enables loading target files.
/// </summary>
[method: SetsRequiredMembers]
public class TrustedMetadataWithSnapshot(Metadata<Root> root, Metadata<Timestamp> timestamp, Metadata<Snapshot> snapshot, DateTimeOffset refTime)
    : TrustedMetadataWithTimestamp(root, timestamp, refTime)
{
    /// <summary>
    /// Snapshot metadata providing a consistent view of all available targets metadata.
    /// </summary>
    public Metadata<Snapshot> Snapshot { get; private set; } = snapshot;

    /// <summary>
    /// Updates snapshot metadata with consistency validation.
    /// </summary>
    /// <param name="snapshotData">JSON bytes of new snapshot metadata</param>
    /// <param name="isTrusted">Whether the data comes from a trusted source</param>
    /// <returns>New trusted metadata instance with updated snapshot</returns>
    public override TrustedMetadataWithSnapshot UpdateSnapshot(byte[] snapshotData, bool isTrusted)
    {
        var newTrustedMetadata = (TrustedMetadataWithSnapshot)base.UpdateSnapshot(snapshotData, isTrusted);

        // Ensure no target metadata versions go backwards
        foreach (var (fileName, currentMeta) in Snapshot.Signed.Meta)
        {
            if (newTrustedMetadata.Snapshot.Signed.Meta.TryGetValue(fileName, out var newMeta))
            {
                if (newMeta.Version < currentMeta.Version)
                {
                    throw new Exception($"New snapshot version for {fileName} is older than current version");
                }
            }
            else
            {
                throw new Exception($"New snapshot is missing metadata for {fileName}");
            }
        }

        return newTrustedMetadata;
    }

    /// <summary>
    /// Updates delegated targets metadata.
    /// </summary>
    /// <param name="delegatedTargetsData">JSON bytes of delegated targets metadata</param>
    /// <param name="roleName">Name of the delegated role</param>
    /// <param name="delegatorRoleName">Name of the role that delegated authority</param>
    /// <returns>Complete trusted metadata including the delegated targets</returns>
    public override CompleteTrustedMetadata UpdateDelegatedTargets(byte[] delegatedTargetsData, string roleName, string delegatorRoleName)
    {
        // Find the file metadata in snapshot
        var fileName = roleName + ".json";
        if (!Snapshot.Signed.Meta.TryGetValue(fileName, out var fileMeta))
        {
            throw new Exception($"File metadata for {roleName} not found in snapshot");
        }

        // Verify file integrity
        VerifyFileIntegrity(delegatedTargetsData, fileMeta);

        // Deserialize targets metadata with caching
        var targets = PerformanceCache.GetOrComputeDeserialization(
            delegatedTargetsData,
            () => Serde.Json.JsonSerializer.Deserialize<Metadata<Targets>, MetadataProxy.De<Targets>>(delegatedTargetsData, MetadataProxy.De<Targets>.Instance));
        if (targets == null)
        {
            throw new Exception("Failed to deserialize targets metadata");
        }

        // Verify signatures based on delegator
        if (delegatorRoleName == "root")
        {
            // Verify against root's targets role
            Root.VerifyRole("targets", targets);
        }
        else
        {
            // This would require having the delegator targets metadata loaded
            // For now, we'll throw as this requires more complex state management
            throw new NotImplementedException("Delegated role verification not yet implemented");
        }

        // Version must match snapshot expectation
        if (targets.Signed.Version != fileMeta.Version)
        {
            throw new Exception("Targets version doesn't match snapshot metadata");
        }

        // Check expiration
        if (IsExpired(targets.Signed.Expires, RefTime))
        {
            throw new Exception("Targets metadata is expired");
        }

        // Create the complete trusted metadata
        var targetsMap = new Dictionary<string, Metadata<Targets>> { [roleName] = targets };
        return new CompleteTrustedMetadata(Root, Timestamp, Snapshot, targetsMap, RefTime);
    }
}

/// <summary>
/// Complete trusted metadata containing all loaded TUF metadata.
/// Provides access to target files and supports ongoing metadata updates.
/// </summary>
[method: SetsRequiredMembers]
public class CompleteTrustedMetadata(
    Metadata<Root> root,
    Metadata<Timestamp> timestamp,
    Metadata<Snapshot> snapshot,
    Dictionary<string, Metadata<Targets>> targets,
    DateTimeOffset refTime)
    : TrustedMetadataWithSnapshot(root, timestamp, snapshot, refTime)
{
    /// <summary>
    /// Map of all loaded targets metadata by role name.
    /// Always includes at least the top-level "targets" role.
    /// </summary>
    public IReadOnlyDictionary<string, Metadata<Targets>> Targets { get; } = targets;

    /// <summary>
    /// Gets the top-level targets metadata.
    /// </summary>
    public Metadata<Targets> TopLevelTargets => Targets["targets"];

    /// <summary>
    /// Updates delegated targets metadata and adds it to the loaded set.
    /// </summary>
    /// <param name="delegatedTargetsData">JSON bytes of delegated targets metadata</param>
    /// <param name="roleName">Name of the delegated role</param>
    /// <param name="delegatorRoleName">Name of the role that delegated authority</param>
    /// <returns>This trusted metadata instance with the new targets added</returns>
    public override CompleteTrustedMetadata UpdateDelegatedTargets(byte[] delegatedTargetsData, string roleName, string delegatorRoleName)
    {
        // Find the file metadata in snapshot
        var fileName = roleName + ".json";
        if (!Snapshot.Signed.Meta.TryGetValue(fileName, out var fileMeta))
        {
            throw new Exception($"File metadata for {roleName} not found in snapshot");
        }

        // Verify file integrity
        VerifyFileIntegrity(delegatedTargetsData, fileMeta);

        // Deserialize targets metadata with caching
        var targets = PerformanceCache.GetOrComputeDeserialization(
            delegatedTargetsData,
            () => Serde.Json.JsonSerializer.Deserialize<Metadata<Targets>, MetadataProxy.De<Targets>>(delegatedTargetsData, MetadataProxy.De<Targets>.Instance));
        if (targets == null)
        {
            throw new Exception("Failed to deserialize targets metadata");
        }

        // Verify signatures based on delegator
        if (delegatorRoleName == "root")
        {
            Root.VerifyRole("targets", targets);
        }
        else if (Targets.TryGetValue(delegatorRoleName, out var delegatorTargets))
        {
            delegatorTargets.VerifyDelegatedRole(roleName, targets);
        }
        else
        {
            throw new Exception($"Delegator role {delegatorRoleName} not loaded");
        }

        // Version must match snapshot expectation
        if (targets.Signed.Version != fileMeta.Version)
        {
            throw new Exception("Targets version doesn't match snapshot metadata");
        }

        // Check expiration
        if (IsExpired(targets.Signed.Expires, RefTime))
        {
            throw new Exception("Targets metadata is expired");
        }

        // Add to loaded targets
        var updatedTargets = new Dictionary<string, Metadata<Targets>>(Targets)
        {
            [roleName] = targets
        };

        return new CompleteTrustedMetadata(Root, Timestamp, Snapshot, updatedTargets, RefTime);
    }

    /// <summary>
    /// Finds target file information across all loaded targets metadata.
    /// </summary>
    /// <param name="targetPath">Path to the target file</param>
    /// <returns>Target file information if found, null otherwise</returns>
    public TargetFile? FindTarget(string targetPath)
    {
        foreach (var targetsMetadata in Targets.Values)
        {
            if (targetsMetadata.Signed.TargetMap.TryGetValue(targetPath, out var target))
            {
                return target;
            }
        }
        return null;
    }
}