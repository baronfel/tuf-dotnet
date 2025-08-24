using System.Text.Json.Serialization.Metadata;
using TUF.Models.Primitives;
using TUF.Serialization;

namespace TUF.Models.Roles.Snapshot;

public record struct HashAlgorithm(string algo);

public record Snapshot(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires, Dictionary<RelativePath, FileMetadata> Meta) :
    RoleBase<Snapshot>(SpecVersion, Version, Expires),
    IAOTSerializable<Snapshot>
{
    public static JsonTypeInfo<Snapshot> JsonTypeInfo => MetadataJsonContext.Default.Snapshot;
}
