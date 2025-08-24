using System.Text.Json.Serialization.Metadata;
using TUF.Models.Primitives;
using TUF.Serialization;

namespace TUF.Models.Roles.Mirrors;

public record MirrorDefinition(AbsoluteUri UrlBase, RelativeUri MetaPath, RelativeUri TargetsPath, PathPattern[] MetaContent, PathPattern[] TargetsContent, Dictionary<string, object> Custom);
public record Mirror(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires, MirrorDefinition[] Mirrors) :
    RoleBase<Mirror>(SpecVersion, Version, Expires),
    IAOTSerializable<Mirror>
{
    public static JsonTypeInfo<Mirror> JsonTypeInfo => MetadataJsonContext.Default.Mirror;
}
