using System.Text.Json.Serialization;

using TUF.Models.Keys.Types;
using TUF.Models.Primitives;

namespace TUF.Models.Keys.Values;

public interface IKeyValue<TKeyType, TValueType> where TKeyType : IKeyType<TKeyType>
{
    abstract TValueType Public { get; init; }
}

public sealed record RsaKeyValue([property: JsonPropertyName("public")] PEMString Public) : IKeyValue<Rsa, PEMString>;
public sealed record Ed25519KeyValue([property: JsonPropertyName("public")] HexString Public) : IKeyValue<Ed25519, HexString>;
public sealed record EcdsaKeyValue([property: JsonPropertyName("public")] PEMString Public) : IKeyValue<Ecdsa, PEMString>;