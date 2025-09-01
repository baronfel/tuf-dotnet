using Serde;

using TUF.Models.Primitives;

namespace TUF.Models.Roles;

public interface IRole<T> where T : IRole<T>
{
    SemanticVersion SpecVersion { get; }
    uint Version { get; }
    DateTimeOffset Expires { get; }
    string Type => T.TypeLabel;

    static abstract string TypeLabel { get; }
}