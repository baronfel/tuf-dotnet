using TUF.Models.Primitives;

namespace TUF.Models.Roles;

public interface IRole
{
    SemanticVersion SpecVersion { get; }
    uint Version { get; }
    DateTimeOffset Expires { get; }
}

public record RoleBase(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires) : IRole;

