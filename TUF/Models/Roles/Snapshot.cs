using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Serde;

using TUF.Models.Primitives;
using TUF.Serialization;
using TUF.Serialization.Converters;

namespace TUF.Models.Roles.Snapshot;

[GenerateSerde]
public partial record struct HashAlgorithm(string algo);

[GenerateSerde]
public partial record Snapshot(
    [property: JsonPropertyName("spec_version")]
    SemanticVersion SpecVersion,
    [property: JsonPropertyName("version")]
    uint Version,
    [property: JsonPropertyName("expires")]
    DateTimeOffset Expires,
    [property: JsonPropertyName("meta")]
    Dictionary<RelativePath, FileMetadata> Meta
) :
    IRole<Snapshot>
{
    public static string TypeLabel => "snapshot";
}