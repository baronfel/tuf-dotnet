using TUF.Models.Primitives;
using TUF.Serialization;

namespace TUF.Models.Roles;

public interface IRole<T> : IAOTSerializable<T> where T : IRole<T>
{
    SemanticVersion SpecVersion { get; }
    uint Version { get; }
    DateTimeOffset Expires { get; }
    string Type => T.TypeLabel;

    static abstract string TypeLabel { get; }
}