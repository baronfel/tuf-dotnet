using System.Diagnostics.CodeAnalysis;

using TUF.Models;
using TUF.Models.Primitives;
using TUF.Models.Roles.Root;
using TUF.Models.Roles.Snapshot;
using TUF.Models.Roles.Targets;
using TUF.Models.Roles.Timestamp;
using TUF.Serialization;

namespace TUF;

public class TrustedMetadata(RootMetadata root)
{
    public RootMetadata Root { get; private set; } = root;
    public required DateTimeOffset RefTime { get; init; }

    public static TrustedMetadata CreateFromRootData(byte[] rootData)
    {
        var refTime = DateTimeOffset.UtcNow;
        var newRoot = MetadataSerializer.Deserialize<RootMetadata>(rootData);
        if (newRoot is null)
        {
            throw new Exception("Failed to deserialize root metadata");
        }
        MetadataExtensions.VerifyRootRole<RootMetadata, RootMetadata, Root>(newRoot, Models.Roles.Root.Root.TypeLabel, newRoot);
        return new TrustedMetadata(newRoot)
        {
            RefTime = refTime
        };
    }

    public TrustedMetadata UpdateRoot(byte[] rootData)
    {
        var newRoot = MetadataSerializer.Deserialize<RootMetadata>(rootData);
        if (newRoot is null)
        {
            throw new Exception("Failed to deserialize root metadata");
        }
        MetadataExtensions.VerifyRootRole<RootMetadata, RootMetadata, Root>(Root, Models.Roles.Root.Root.TypeLabel, newRoot);
        if (newRoot.Signed.Version != Root.Signed.Version + 1)
        {
            throw new Exception($"Invalid root metadata version: {newRoot.Signed.Version}");
        }
        MetadataExtensions.VerifyRootRole<RootMetadata, RootMetadata, Root>(newRoot, Models.Roles.Root.Root.TypeLabel, newRoot);
        Root = newRoot;
        return this;
    }

    protected RootAndTimestampTrustedMetadata UpdateTimestampInternal(byte[] timestampData)
    {
        if (Root.IsExpired(RefTime))
        {
            throw new Exception("Cannot update timestamp with expired root metadata");
        }
        var newTimestamp = MetadataSerializer.Deserialize<TimestampMetadata>(timestampData);
        if (newTimestamp is null)
        {
            throw new Exception("Failed to deserialize timestamp metadata");
        }
        MetadataExtensions.VerifyRootRole<RootMetadata, TimestampMetadata, Timestamp>(Root, Models.Roles.Timestamp.Timestamp.TypeLabel, newTimestamp);
        var newTrustedMetadata = new RootAndTimestampTrustedMetadata(Root, newTimestamp)
        {
            RefTime = RefTime
        };
        return newTrustedMetadata;
    }

    public virtual RootAndTimestampTrustedMetadata UpdateTimeStamp(byte[] timestampData)
    {
        var newTrustedMetadata = UpdateTimestampInternal(timestampData);
        newTrustedMetadata.CheckFinalTimestamp();
        return newTrustedMetadata;
    }

    public virtual TrustedMetadata UpdateSnapshot(byte[] snapshotData, bool isTrusted)
    {
        throw new NotImplementedException("Cannot update snapshot before timestamp");
    }
    public virtual TrustedMetadata UpdateTargets(byte[] targetsData)
    {
        return UpdateDelegatedTargets(targetsData, Models.Roles.Targets.TargetsRole.TypeLabel, Models.Roles.Root.Root.TypeLabel);
    }
    public virtual TrustedMetadata UpdateDelegatedTargets(byte[] delegatedTargetsData, string roleName, string delegatorRoleName)
    {
        throw new NotImplementedException("Cannot load targets before snapshot");
    }
}

[method: SetsRequiredMembers]
public class RootAndTimestampTrustedMetadata(RootMetadata root, TimestampMetadata timestamp) : TrustedMetadata(root)
{
    public TimestampMetadata Timestamp { get; private set; } = timestamp;

    protected FileMetadata SnapshotMeta => Timestamp.Signed.SnapshotFileMetadata;

    public override RootAndTimestampTrustedMetadata UpdateTimeStamp(byte[] timestampData)
    {
        var newTrustedMetadata = UpdateTimestampInternal(timestampData);
        // check that new timestamp isn't older than current timestamp
        if (newTrustedMetadata.Timestamp.Signed.Version < Timestamp.Signed.Version)
        {
            throw new Exception("New timestamp version is older than current timestamp version");
        }
        // keep using existing if versions are equal
        else if (newTrustedMetadata.Timestamp.Signed.Version == Timestamp.Signed.Version)
        {
            return this;
        }
        // prevent rolling back snapshot version
        if (newTrustedMetadata.SnapshotMeta.Version < SnapshotMeta.Version)
        {
            throw new Exception("New snapshot version is older than current snapshot version");
        }
        newTrustedMetadata.CheckFinalTimestamp();
        return newTrustedMetadata;
    }

    protected RootAndTimestampAndSnapshotTrustedMetadata UpdateSnapshotInternal(byte[] snapshotData, bool isTrusted)
    {
        CheckFinalTimestamp();
        if (!isTrusted)
        {
            SnapshotMeta.VerifyLengthHashes(snapshotData);
        }
        var newSnapshot = MetadataSerializer.Deserialize<SnapshotMetadata>(snapshotData);
        if (newSnapshot is null)
        {
            throw new Exception("Failed to deserialize snapshot metadata");
        }
        MetadataExtensions.VerifyRootRole<RootMetadata, SnapshotMetadata, Snapshot>(Root, Snapshot.TypeLabel, newSnapshot);
        var newThing = new RootAndTimestampAndSnapshotTrustedMetadata(Root, Timestamp, newSnapshot)
        {
            RefTime = RefTime
        };
        return newThing;
    }

    public override RootAndTimestampAndSnapshotTrustedMetadata UpdateSnapshot(byte[] snapshotData, bool isTrusted)
    {
        var newThing = UpdateSnapshotInternal(snapshotData, isTrusted);
        newThing.CheckFinalSnapshot();
        return newThing;
    }

    internal void CheckFinalTimestamp()
    {
        if (Timestamp.IsExpired(RefTime))
        {
            throw new Exception("Final timestamp is expired");
        }
    }
}

[method: SetsRequiredMembers]
public class RootAndTimestampAndSnapshotTrustedMetadata(RootMetadata root, TimestampMetadata timestamp, SnapshotMetadata snapshot) : RootAndTimestampTrustedMetadata(root, timestamp)
{
    public SnapshotMetadata Snapshot { get; private set; } = snapshot;

    public override RootAndTimestampAndSnapshotTrustedMetadata UpdateSnapshot(byte[] snapshotData, bool isTrusted)
    {
        var newThing = UpdateSnapshotInternal(snapshotData, isTrusted);
        foreach (var fileEntry in Snapshot.Signed.Meta)
        {
            if (!newThing.Snapshot.Signed.Meta.TryGetValue(fileEntry.Key, out var newMeta))
            {
                throw new Exception($"New snapshot metadata for {fileEntry.Key} is missing");
            }
            if (newMeta.Version < fileEntry.Value.Version)
            {
                throw new Exception($"New snapshot version for {fileEntry.Key} is older than current snapshot version");
            }
        }
        newThing.CheckFinalSnapshot();
        return newThing;
    }

    protected CoreTrustedMetadata UpdateDelegatedTargetsInternal(byte[] delegatedTargetsData, string roleName,
        Action verifyDelegator,
        Action<TargetsMetadata> verify,
        Func<TargetsMetadata, CoreTrustedMetadata> acceptVerified)
    {
        CheckFinalSnapshot();
        verifyDelegator.Invoke();
        if (!Snapshot.Signed.Meta.TryGetValue(new RelativePath(roleName + ".json"), out var fileMeta))
        {
            throw new Exception($"File metadata for {roleName} not found in snapshot");
        }
        fileMeta.VerifyLengthHashes(delegatedTargetsData);
        var newTargets = MetadataSerializer.Deserialize<TargetsMetadata>(delegatedTargetsData);
        if (newTargets is null)
        {
            throw new Exception("Failed to deserialize delegated targets");
        }
        verify.Invoke(newTargets);
        if (newTargets.Signed.Version != fileMeta.Version)
        {
            throw new Exception("Target metadata out of sync with snapshot metadata");
        }
        if (newTargets.IsExpired(RefTime))
        {
            throw new Exception("Target metadata is expired");
        }
        return acceptVerified.Invoke(newTargets);
    }

    public override CoreTrustedMetadata UpdateDelegatedTargets(byte[] delegatedTargetsData, string roleName, string delegatorRoleName)
    {
        return UpdateDelegatedTargetsInternal(delegatedTargetsData, roleName,
            () =>
            {
                if (delegatorRoleName != Models.Roles.Root.Root.TypeLabel)
                {
                    throw new NotImplementedException("Cannot load targets before delegator");
                }
            },
            (newTargets) => MetadataExtensions.VerifyRootRole<RootMetadata, TargetsMetadata, TargetsRole>(Root, delegatorRoleName, newTargets),
            (newTargets) => new CoreTrustedMetadata(Root, Timestamp, Snapshot, newTargets)
            {
                RefTime = RefTime
            });
    }

    public void CheckFinalSnapshot()
    {
        if (Snapshot.IsExpired(RefTime))
        {
            throw new Exception("Final snapshot is expired");
        }
        if (Snapshot.Signed.Version != SnapshotMeta.Version)
        {
            throw new Exception("Final snapshot version is out of sync with timestamp snapshot metadata");
        }
    }
}

[method: SetsRequiredMembers]
public class CoreTrustedMetadata(RootMetadata root, TimestampMetadata timestamp, SnapshotMetadata snapshot, TargetsMetadata initialTarget) : RootAndTimestampAndSnapshotTrustedMetadata(root, timestamp, snapshot)
{
    public TargetsMap Targets { get; private set; } = new(initialTarget);

    public override CoreTrustedMetadata UpdateDelegatedTargets(byte[] delegatedTargetsData, string roleName, string delegatorRoleName)
    {
        return UpdateDelegatedTargetsInternal(delegatedTargetsData, roleName,
            () =>
            {
                if (delegatorRoleName != Models.Roles.Root.Root.TypeLabel)
                {
                    // verify that we have a target of this name
                    if (!Targets.TryGetValue(delegatorRoleName, out var delegatorTargets))
                    {
                        throw new Exception($"Delegator role {delegatorRoleName} not found in loaded targets");
                    }
                }
            },
            (newTargets) =>
            {
                if (delegatorRoleName == Models.Roles.Root.Root.TypeLabel)
                {
                    MetadataExtensions.VerifyRootRole<RootMetadata, TargetsMetadata, TargetsRole>(Root, delegatorRoleName, newTargets);
                }
                else
                {
                    Targets[delegatorRoleName].VerifyDelegatedRole(roleName, newTargets);
                }
            },
            (newTargets) =>
            {
                Targets[roleName] = newTargets;
                return this;
            }
            );
    }
}

public class TargetsMap(TargetsMetadata topLevelMetadata) : Dictionary<string, TargetsMetadata>([new KeyValuePair<string, TargetsMetadata>(Models.Roles.Targets.TargetsRole.TypeLabel, topLevelMetadata)])
{
    public TargetsMetadata TopLevelTargets => topLevelMetadata;
}