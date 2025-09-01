using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Serde;

using Tuf.DotNet.Serialization.Converters;

using TUF.Models.Primitives;
using TUF.Models.Roles.Root;
using TUF.Serialization;
using TUF.Serialization.Converters;

namespace TUF.Models.Roles.Targets;



public record struct DelegatedRoleName(string roleName);

/// <summary>
/// Marks types that contain their own key for dictionary-creation purposes.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TKey"></typeparam>
public interface IKeyHolder<T, TKey> where T : IKeyHolder<T, TKey>
    where TKey : notnull
{
    static abstract TKey GetKey(T instance);
}


public record DelegationData(
    [property: JsonPropertyName("name")]
    DelegatedRoleName Name,
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
) : IAOTSerializable<DelegationData>, IKeyHolder<DelegationData, DelegatedRoleName>
{
    public static JsonTypeInfo<DelegationData> JsonTypeInfo(MetadataJsonContext context) => context.DelegationData;

    public RoleKeys RoleKeys => new(KeyIDs.ToList(), Threshold);

    public static DelegatedRoleName GetKey(DelegationData instance) => instance.Name;

    public bool IsDelegatedPath(string targetFile)
    {
        if (Paths is null || Paths.Length == 0)
        {
            return true;
        }
        return Paths.Any(p => p.IsMatch(targetFile));
    }
}


public record TargetMetadata(
    [property: JsonPropertyName("length")]
    uint Length,
    [property: JsonPropertyName("hashes")]
    List<DigestAlgorithms.DigestValue> Hashes,
    [property: JsonPropertyName("custom")]
    Dictionary<string, object>? Custom,
    [property: JsonPropertyName("path")]
    RelativePath Path
) : IKeyHolder<TargetMetadata, RelativePath>,
    IVerifyHashes,
    IVerifyLength
{
    uint? IVerifyLength.Length => Length;

    public static RelativePath GetKey(TargetMetadata instance) => instance.Path;
}


public record struct RoleResult(string Name, bool Terminating);


public record Delegations(
    [property: JsonPropertyName("keys")]
    Dictionary<KeyId, Keys.KeyBase> Keys,
    [property: JsonPropertyName("roles")]
    [property: JsonConverter(typeof(ArrayToDictionaryConverter<DelegatedRoleName, DelegationData>))]
    Dictionary<DelegatedRoleName, DelegationData>? Roles
)
{
    public List<RoleResult> GetRolesForTarget(string targetFile)
    {
        if (Roles is null)
        {
            return [];
        }
        return Roles.Where(r => r.Value.IsDelegatedPath(targetFile)).Select(r => new RoleResult(r.Key.roleName, r.Value.Terminating)).ToList();
    }
}

// style nit: Can't call the type and the member Target, so in order to keep the member name aligned with TUF specifications, the member is called Targets
//            and we 'sacrifice' the type name.

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
    public static string TypeLabel => "targets";
}