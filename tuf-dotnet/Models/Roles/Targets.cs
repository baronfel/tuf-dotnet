using TUF.Models.Primitives;

namespace TUF.Models.Roles.Targets;

public record struct DelegatedRoleName(string roleName);
public record DelegationData(KeyId[] KeyIDs, uint Threshold, HexDigest[]? PathHashPrefixes, PathPattern[]? Paths, bool Terminating);
public record TargetMetadata(uint Length, List<DigestAlgorithms.DigestValue> Hashes, Dictionary<string, object>? Custom);
public record Delegations(Dictionary<KeyId, Keys.Key> Keys, Dictionary<DelegatedRoleName, DelegationData>? Roles);

// style nit: Can't call the type and the member Target, so in order to keep the member name aligned with TUF specifications, the member is called Targets
//            and we 'sacrifice' the type name.
public record TargetsRole(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires, Dictionary<RelativePath, TargetMetadata> Targets) : RoleBase(SpecVersion, Version, Expires);