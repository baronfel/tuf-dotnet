using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using TUF.Models.Primitives;
using TUF.Models.Roles.Root;
using TUF.Serialization;
using TUF.Serialization.Converters;

namespace TUF.Models.Roles.Targets;

public record struct DelegatedRoleName(string roleName);

public record DelegationData(
    [property: JsonPropertyName("keyids")]
    KeyId[] KeyIDs,
    [property: JsonPropertyName("threshold")]
    uint Threshold,
    [property: JsonPropertyName("paths")]
    PathPattern[] Paths,
    [property: JsonPropertyName("terminating")]
    bool Terminating,
    [property: JsonPropertyName("path_hash_prefixes")]
    HexDigest[]? PathHashPrefixes = null
)
{
    public RoleKeys RoleKeys => new(KeyIDs.ToList(), Threshold);
}

public record TargetMetadata(
    [property: JsonPropertyName("length")]
    uint Length,
    [property: JsonPropertyName("hashes")]
    List<DigestAlgorithms.DigestValue> Hashes,
    [property: JsonPropertyName("custom")]
    Dictionary<string, object>? Custom
);

public record Delegations(
    [property: JsonPropertyName("keys")]
    Dictionary<KeyId, Keys.Key> Keys,
    [property: JsonPropertyName("roles")]
    Dictionary<DelegatedRoleName, DelegationData>? Roles
);

// style nit: Can't call the type and the member Target, so in order to keep the member name aligned with TUF specifications, the member is called Targets
//            and we 'sacrifice' the type name.
[JsonConverter(typeof(RoleTypeJsonConverter<TargetsRole>))]
public record TargetsRole(
    [property: JsonPropertyName("spec_version")]
    SemanticVersion SpecVersion,
    [property: JsonPropertyName("version")]
    uint Version,
    [property: JsonPropertyName("expires")]
    DateTimeOffset Expires,
    [property: JsonPropertyName("targets")]
    Dictionary<RelativePath, TargetMetadata> Targets,
    [property: JsonPropertyName("delegations")]
    Delegations? Delegations = null
) :
    IRole<TargetsRole>
{
    public static JsonTypeInfo<TargetsRole> JsonTypeInfo => MetadataJsonContext.Default.TargetsRole;

    public static string TypeLabel => "targets";
}