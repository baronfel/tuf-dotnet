using TUF.Models.Keys.Types;
using TUF.Models.Primitives;

namespace TUF.Models.Keys.Values;

public interface IKeyValue<TKeyType, TValueType> where TKeyType : IKeyType<TKeyType>
{
    abstract TValueType Public { get; }
}

public sealed record RsaKeyValue(PEMString Public) : IKeyValue<Rsa, PEMString>;
public sealed record Ed25519KeyValue(HexString Public) : IKeyValue<Ed25519, HexString>;
public sealed record EcdsaKeyValue(PEMString Public) : IKeyValue<Ecdsa, PEMString>;