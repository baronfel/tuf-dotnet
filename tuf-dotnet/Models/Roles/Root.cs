using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using TUF.Models.Primitives;
using TUF.Serialization;

namespace TUF.Models.Roles.Root;

public record RoleKeys(
    [property: JsonPropertyName("keyids")]
    List<KeyId> KeyIds,
    [property: JsonPropertyName("threshold")]
    uint Threshold
);

public record RootRoles(
    [property: JsonPropertyName("root")]
    RoleKeys Root,
    [property: JsonPropertyName("timestamp")]
    RoleKeys Timestamp,
    [property: JsonPropertyName("snapshot")]
    RoleKeys Snapshot,
    [property: JsonPropertyName("targets")]
    RoleKeys Targets,
    [property: JsonPropertyName("mirrors")]
    RoleKeys? Mirrors
);

public record Root(
    [property: JsonPropertyName("spec_version")]
    SemanticVersion SpecVersion,
    [property: JsonPropertyName("consistent_snapshot")]
    bool? ConsistentSnapshot,
    [property: JsonPropertyName("version")]
    uint Version,
    [property: JsonPropertyName("expires")]
    DateTimeOffset Expires,
    [property: JsonPropertyName("keys")]
    Dictionary<KeyId, Keys.Key> Keys,
    [property: JsonPropertyName("roles")]
    RootRoles Roles
) :
    IRole<Root>,
    IAOTSerializable<Root>
{
    public static JsonTypeInfo<Root> JsonTypeInfo => MetadataJsonContext.Default.Root;
}