using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Serde;

using TUF.Models.Primitives;
using TUF.Serialization;
using TUF.Serialization.Converters;

namespace TUF.Models.Roles.Timestamp;

/// <summary>
/// For the timestamp role specifically, the only metadata record is for the snapshot.json file.
/// </summary>
[JsonConverter(typeof(Tuf.DotNet.Serialization.Converters.SnapshotFileMetadataJsonConverter))]
public class SnapshotFileMetadata(FileMetadata snapshotFileMetadata) :
    Dictionary<RelativePath, FileMetadata>([new(new("snapshot.json"), snapshotFileMetadata)]);


public record Timestamp(
    [property: JsonPropertyName("spec_version")]
    SemanticVersion SpecVersion,
    [property: JsonPropertyName("version")]
    uint Version,
    [property: JsonPropertyName("expires")]
    DateTimeOffset Expires,
    [property: JsonPropertyName("meta")]
    SnapshotFileMetadata Meta) :
    IRole<Timestamp>
{
    public FileMetadata SnapshotFileMetadata => Meta.Values.First();
    public static JsonTypeInfo<Timestamp> JsonTypeInfo(MetadataJsonContext context) => context.Timestamp;

    public static string TypeLabel => "timestamp";
}