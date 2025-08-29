using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using TUF.Models.Keys.Schemes;
using TUF.Models.Keys.Types;
using TUF.Models.Keys.Values;
using TUF.Models.Primitives;
using TUF.Serialization;

namespace TUF.Models.Keys;

public abstract record Key(
    [property: JsonPropertyName("keytype")] string KeyType,
    [property: JsonPropertyName("scheme")] string Scheme)
{
    [JsonPropertyName("keyval")]
    public abstract object KeyVal { get; }

    [JsonIgnore]
    public abstract KeyId Id { get; }

    /// <summary>
    /// Using the parameters of a given Key<type, scheme, value> to create the verification algorithm, 
    /// verify that the given signature matches the hashing of the payload using this Key.
    /// </summary>
    /// <param name="signatureBytes"></param>
    /// <param name="payloadBytes"></param>
    public abstract bool VerifySignature(byte[] signatureBytes, byte[] payloadBytes);
}

public abstract record Key<TKey, TKeyScheme, TKeyValInner>(IKeyValue<TKey, TKeyValInner> TypedKeyVal) :
    Key(TKey.Name, TKeyScheme.Name)
    where TKey : IKeyType<TKey>
    where TKeyScheme : IKeyScheme<TKeyScheme>
{

    [JsonPropertyName("keyval")]
    public override object KeyVal => TypedKeyVal;

    [JsonIgnore]
    public override KeyId Id
    {
        get => field == default ? field = ComputeId() : field;
    }

    /// <summary>
    /// KeyIds are computed in part by serializing the type in canonicalJson, and we only want to do that in a strongly typed way.
    /// Having this extensibility point allows the concrete variants of this type to forward along their type information for the 
    /// IAOTSerializable<T> interface. 
    /// </summary>
    protected abstract KeyId ComputeId();
}

public static class KeyExtensions
{
    extension<T>(T item) where T : IAOTSerializable<T>
    {
        /// <summary>
        /// Computes the hexdigest of an item by serializing it to utf8 bytes via the 
        /// canonical json format, then getting the sha256hash of that, then hex-tolowering the result.
        /// </summary>
        public HexDigest ToDigest()
        {
            var bytes = CanonicalJson.CanonicalJsonSerializer.Serialize(item, T.JsonTypeInfo(MetadataJsonContext.Default));
            return new HexDigest(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }
}

public static class WellKnown
{
    public sealed record Rsa(RsaKeyValue Public) : Key<Types.Rsa, Schemes.RSASSA_PSS_SHA256, PEMString>(Public), IAOTSerializable<Rsa>
    {
        protected override KeyId ComputeId() => new(this.ToDigest());

        public override bool VerifySignature(byte[] signatureBytes, byte[] payloadBytes)
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(Public.Public.PemEncodedValue);
            // we use these parameters because they match the signature scheme that's pinned for this particular implementation
            return rsa.VerifyHash(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }

        public static JsonTypeInfo<Rsa> JsonTypeInfo(MetadataJsonContext context) => context.Rsa;
    }
    public sealed record Ed25519(Ed25519KeyValue Public) : Key<Types.Ed25519, Schemes.Ed25519, HexString>(Public), IAOTSerializable<Ed25519>
    {
        override protected KeyId ComputeId() => new(this.ToDigest());

        public override bool VerifySignature(byte[] signatureBytes, byte[] payloadBytes)
        {
            return false;
        }
        public static JsonTypeInfo<Ed25519> JsonTypeInfo(MetadataJsonContext context) => context.Ed25519;
    }
    public sealed record Ecdsa(EcdsaKeyValue Public) : Key<Types.Ecdsa, Schemes.ECDSA_SHA2_NISTP256, PEMString>(Public), IAOTSerializable<Ecdsa>
    {
        override protected KeyId ComputeId() => new(this.ToDigest());

        public override bool VerifySignature(byte[] signatureBytes, byte[] payloadBytes)
        {
            var edcsa = System.Security.Cryptography.ECDsa.Create(ECCurve.NamedCurves.nistP256);
            edcsa.ImportFromPem(Public.Public.PemEncodedValue);
            return edcsa.VerifyHash(payloadBytes, signatureBytes);
        }
        public static JsonTypeInfo<Ecdsa> JsonTypeInfo(MetadataJsonContext context) => context.Ecdsa;
    }
}