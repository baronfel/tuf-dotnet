using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using TUF.Models.Primitives;
using TUF.Serialization;
using TUF.Serialization.Converters;

namespace TUF.Models.Roles.Mirrors;

public record MirrorDefinition(
    [property: JsonPropertyName("urlbase")] AbsoluteUri UrlBase,
    [property: JsonPropertyName("metapath")] RelativeUri MetaPath,
    [property: JsonPropertyName("targetspath")] RelativeUri TargetsPath,
    [property: JsonPropertyName("metacontent")] PathPattern[] MetaContent,
    [property: JsonPropertyName("targetscontent")] PathPattern[] TargetsContent,
    [property: JsonPropertyName("custom")] Dictionary<string, object> Custom);

public record Mirror(
    [property: JsonPropertyName("spec_version")]
    SemanticVersion SpecVersion,
    [property: JsonPropertyName("version")]
    uint Version,
    [property: JsonPropertyName("expires")]
    DateTimeOffset Expires,
    [property: JsonPropertyName("mirrors")]
    MirrorDefinition[] Mirrors
) :
    IRole<Mirror>
{
    public static JsonTypeInfo<Mirror> JsonTypeInfo(MetadataJsonContext context) => context.Mirror;

    public static string TypeLabel => "mirror";
}