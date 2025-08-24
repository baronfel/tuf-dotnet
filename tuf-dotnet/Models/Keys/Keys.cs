using TUF.Models.Keys.Schemes;
using TUF.Models.Keys.Types;
using TUF.Models.Keys.Values;
using TUF.Models.Primitives;

namespace TUF.Models.Keys;

public interface IKey
{
    string KeyType { get; }
    string Scheme { get; }
    object KeyVal { get; }
}


public record Key(string KeyType, string Scheme, object KeyVal) : IKey
{
    public static Key From<T>(Key<T> key) => new(key.KeyType, key.Scheme, key.KeyVal!);
}

public record Key<T>(string KeyType, string Scheme, T KeyVal) : IKey
{
    object IKey.KeyVal => KeyVal!;
}

public record Key<TKey, TKeyScheme, TKeyValInner>(IKeyValue<TKey, TKeyValInner> KeyVal) : Key<IKeyValue<TKey, TKeyValInner>>(TKey.Name, TKeyScheme.Name, KeyVal)
    where TKey : IKeyType<TKey>
    where TKeyScheme : IKeyScheme<TKeyScheme>
{
}

public static class WellKnown
{
    public sealed record Rsa(RsaKeyValue Public) : Key<Types.Rsa, Schemes.RSASSA_PSS_SHA256, PEMString>(Public);
    public sealed record Ed25519(Ed25519KeyValue Public) : Key<Types.Ed25519, Schemes.Ed25519, HexString>(Public);
    public sealed record Ecdsa(EcdsaKeyValue Public) : Key<Types.Ecdsa, Schemes.ECDSA_SHA2_NISTP256, PEMString>(Public);
}