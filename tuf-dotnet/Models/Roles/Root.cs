using System.Text.Json.Serialization.Metadata;

using TUF.Models.Primitives;
using TUF.Serialization;

namespace TUF.Models.Roles.Root;

public record RoleKeys(List<KeyId> KeyIds, uint Threshold);

public record RootRoles(RoleKeys Root, RoleKeys Timestamp, RoleKeys Snapshot, RoleKeys Targets, RoleKeys? Mirrors);

public record Root(SemanticVersion SpecVersion, bool? ConsistentSnapshot, uint Version, DateTimeOffset Expires, Dictionary<KeyId, Keys.Key> Keys, RootRoles Roles) :
    RoleBase<Root>(SpecVersion, Version, Expires),
    IAOTSerializable<Root>
{
    public static JsonTypeInfo<Root> JsonTypeInfo => MetadataJsonContext.Default.Root;
}