using System.Text.Json.Serialization.Metadata;
using TUF.Models.Primitives;
using TUF.Serialization;

namespace TUF.Models.Roles.Timestamp;

/// <summary>
/// For the timestamp role specifically, the only metadata record is for the snapshot.json file.
/// </summary>
public class SnapshotMetadata(FileMetadata snapshotFileMetadata) : Dictionary<RelativePath, FileMetadata>([new(new("snapshot.json"), snapshotFileMetadata)])
{
}

public record Timestamp(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires, FileMetadata Meta) :
    RoleBase<Timestamp>(SpecVersion, Version, Expires),
    IAOTSerializable<Timestamp>
{
    public static JsonTypeInfo<Timestamp> JsonTypeInfo => MetadataJsonContext.Default.Timestamp;
}
