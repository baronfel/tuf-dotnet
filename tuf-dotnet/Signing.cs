namespace TUF.Signing;

using TUF.Models;
using TUF.Models.Primitives;
using TUF.Models.Roles;
using TUF.Serialization;

public interface ISigner
{
    public SignatureResult Sign<T, TInner>(T metadata)
        where T : Metadata<TInner>, IAOTSerializable<T>
        where TInner : IRole<TInner>, IAOTSerializable<TInner>;
}