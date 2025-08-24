using System.Text.Json.Serialization.Metadata;
using TUF.Models.Primitives;
using TUF.Serialization;

namespace TUF.Models.Roles;

public interface IRole<T>
{
    SemanticVersion SpecVersion { get; }
    uint Version { get; }
    DateTimeOffset Expires { get; }
}

public abstract record RoleBase<T>(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires) : IRole<T>;
