using TUF.Models.Primitives;

namespace TUF.Models.Roles.Timestamp;

/// <summary>
/// For the timestamp role specifically, the only metadata record is for the snapshot.json file.
/// </summary>
public class SnapshotMetadata(FileMetadata snapshotFileMetadata) : Dictionary<RelativePath, FileMetadata>([new(new("snapshot.json"), snapshotFileMetadata)])
{
}

public record TimestampRole(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires, FileMetadata Meta) : RoleBase(SpecVersion, Version, Expires);