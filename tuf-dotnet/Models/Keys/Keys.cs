using TUF.Models.Keys.Schemes;
using TUF.Models.Keys.Types;
using TUF.Models.Keys.Values;
using TUF.Models.Primitives;

namespace TUF.Models.Keys;

public abstract record Key(string KeyType, string Scheme)
{
    public abstract object KeyVal { get; }
}

public record Key<TKey, TKeyScheme, TKeyValInner>(IKeyValue<TKey, TKeyValInner> TypedKeyVal) : Key(TKey.Name, TKeyScheme.Name)
    where TKey : IKeyType<TKey>
    where TKeyScheme : IKeyScheme<TKeyScheme>
{
    public override object KeyVal => TypedKeyVal;
}

public static class WellKnown
{
    public sealed record Rsa(RsaKeyValue Public) : Key<Types.Rsa, Schemes.RSASSA_PSS_SHA256, PEMString>(Public);
    public sealed record Ed25519(Ed25519KeyValue Public) : Key<Types.Ed25519, Schemes.Ed25519, HexString>(Public);
    public sealed record Ecdsa(EcdsaKeyValue Public) : Key<Types.Ecdsa, Schemes.ECDSA_SHA2_NISTP256, PEMString>(Public);
}