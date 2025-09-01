using Serde;

using TUF.Models.Keys.Types;
using TUF.Models.Primitives;

namespace TUF.Models.Keys.Values;

public interface IKeyValue<TKeyType, TValueType> where TKeyType : IKeyType<TKeyType>
{
    abstract TValueType Public { get; }
}



public record RsaKeyValue(PEMString Public) : IKeyValue<Rsa, PEMString>;

public record Ed25519KeyValue(HexString Public) : IKeyValue<Ed25519, HexString>;

public record EcdsaKeyValue(PEMString Public) : IKeyValue<Ecdsa, PEMString>;