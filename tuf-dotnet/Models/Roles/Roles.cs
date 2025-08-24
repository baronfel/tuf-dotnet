using TUF.Models.Primitives;

namespace TUF.Models.Roles;

public interface IRole<T>
{
    SemanticVersion SpecVersion { get; }
    uint Version { get; }
    DateTimeOffset Expires { get; }
}