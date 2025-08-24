using TUF.Models.Primitives;

namespace TUF.Models.Roles.Snapshot;

public record struct HashAlgorithm(string algo);
public record Snapshot(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires, Dictionary<RelativePath, FileMetadata> Meta) : RoleBase(SpecVersion, Version, Expires);