using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using TUF.Models.Primitives;
using TUF.Serialization;
using TUF.Serialization.Converters;

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
)
{
    public RootRoles() : this(
        Root: new RoleKeys(new List<KeyId>(), 1),
        Timestamp: new RoleKeys(new List<KeyId>(), 1),
        Snapshot: new RoleKeys(new List<KeyId>(), 1),
        Targets: new RoleKeys(new List<KeyId>(), 1),
        Mirrors: null
    )
    {

    }
}

[method: JsonConstructor]
public record Root(
    [property: JsonPropertyName("spec_version")]
    SemanticVersion SpecVersion,
    [property: JsonPropertyName("consistent_snapshot")]
    bool? ConsistentSnapshot,
    uint Version,
    [property: JsonPropertyName("expires")]
    DateTimeOffset Expires,
    [property: JsonPropertyName("keys")]
    Dictionary<KeyId, Keys.Key> Keys,
    [property: JsonPropertyName("roles")]
    RootRoles Roles
) :
    IRole<Root>
{
    public Root(DateTimeOffset? expiry) : this(
        SpecVersion: Constants.ImplementedSpecVersion,
        ConsistentSnapshot: true,
        Version: 1,
        Expires: expiry ?? DateTimeOffset.UtcNow,
        Keys: new Dictionary<KeyId, Keys.Key>(),
        Roles: new RootRoles()
    )
    {
    }

    public static JsonTypeInfo<Root> JsonTypeInfo(MetadataJsonContext context) => context.Root;

    public static string TypeLabel => "root";
}