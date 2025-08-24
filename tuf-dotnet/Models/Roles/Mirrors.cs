using TUF.Models.Primitives;

namespace TUF.Models.Roles.Mirrors;

public record Mirror(AbsoluteUri UrlBase, RelativeUri MetaPath, RelativeUri TargetsPath, PathPattern[] MetaContent, PathPattern[] TargetsContent, Dictionary<string, object> Custom);
public record MirrorRole(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires, Mirror[] Mirrors) : RoleBase(SpecVersion, Version, Expires);
