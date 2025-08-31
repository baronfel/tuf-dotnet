using Serde;

using TUF.Models.Keys.Types;
using TUF.Models.Primitives;

namespace TUF.Models.Keys.Values;

public interface IKeyValue<TKeyType, TValueType> where TKeyType : IKeyType<TKeyType>
{
    abstract TValueType Public { get; }
}


[GenerateSerde]
public partial record RsaKeyValue(PEMString Public) : IKeyValue<Rsa, PEMString>;
[GenerateSerde]
public partial record Ed25519KeyValue(HexString Public) : IKeyValue<Ed25519, HexString>;
[GenerateSerde]
public partial record EcdsaKeyValue(PEMString Public) : IKeyValue<Ecdsa, PEMString>;