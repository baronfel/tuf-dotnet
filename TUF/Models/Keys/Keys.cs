using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using TUF.Models.Keys.Schemes;
using TUF.Models.Keys.Types;
using TUF.Models.Keys.Values;
using TUF.Models.Primitives;
using TUF.Serialization;
using TUF.Serialization.Converters;

namespace TUF.Models.Keys;

[JsonConverter(typeof(KeyConverter))]
public interface IKey
{
    string Type { get; }
    string Scheme { get; }
    object Value { get; }

    [JsonIgnore]
    public KeyId Id { get; }

    /// <summary>
    /// Using the parameters of a given Key<type, scheme, value> to create the verification algorithm, 
    /// verify that the given signature matches the hashing of the payload using this Key.
    /// </summary>
    /// <param name="signatureBytes"></param>
    /// <param name="payloadBytes"></param>
    public bool VerifySignature(string signatureBytes, byte[] payloadBytes);
}

[JsonConverter(typeof(KeyConverter))]
public abstract record Key<TKey, TKeyScheme, TKeyValue, TKeyValInner>(TKeyValue TypedKeyVal) :
    IKey
    where TKey : IKeyType<TKey>
    where TKeyScheme : IKeyScheme<TKeyScheme>
    where TKeyValue : IKeyValue<TKey, TKeyValInner>
{
    [JsonPropertyName("keytype")]
    public string Type => TKey.Name;

    [JsonPropertyName("keyscheme")]  
    public string Scheme => TKeyScheme.Name;

    [JsonPropertyName("keyvalue")]
    public object Value => TypedKeyVal;

    [JsonIgnore]
    public KeyId Id
    {
        get => field == default ? field = ComputeId() : field;
    }

    public abstract bool VerifySignature(string signatureBytes, byte[] payloadBytes);

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
    [method: SetsRequiredMembers]
    public sealed record Rsa(RsaKeyValue Public) : Key<Types.Rsa, Schemes.RSASSA_PSS_SHA256, RsaKeyValue, PEMString>(Public), IAOTSerializable<Rsa>
    {
        protected override KeyId ComputeId() => new(this.ToDigest());

        public override bool VerifySignature(string signatureBytes, byte[] payloadBytes)
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(Public.Public.PemEncodedValue);
            // Hash the payload data first, then verify the hash
            var hash = SHA256.HashData(payloadBytes);
            return rsa.VerifyHash(hash, Encoding.UTF8.GetBytes(signatureBytes), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }

        public static JsonTypeInfo<Rsa> JsonTypeInfo(MetadataJsonContext context) => context.Rsa;
    }

    [method: SetsRequiredMembers]
    public sealed record Ed25519(Ed25519KeyValue Public) : Key<Types.Ed25519, Schemes.Ed25519, Ed25519KeyValue, HexString>(Public), IAOTSerializable<Ed25519>
    {
        override protected KeyId ComputeId() => new(this.ToDigest());

        public override bool VerifySignature(string signatureBytes, byte[] payloadBytes)
        {
            try
            {
                var publicKeyBytes = Convert.FromHexString(Public.Public.HexEncodedValue);
                var publicKey = NSec.Cryptography.PublicKey.Import(NSec.Cryptography.SignatureAlgorithm.Ed25519,
                    publicKeyBytes, NSec.Cryptography.KeyBlobFormat.RawPublicKey);
                return NSec.Cryptography.SignatureAlgorithm.Ed25519.Verify(publicKey, payloadBytes, Encoding.UTF8.GetBytes(signatureBytes));
            }
            catch
            {
                return false;
            }
        }
        public static JsonTypeInfo<Ed25519> JsonTypeInfo(MetadataJsonContext context) => context.Ed25519;
    }

    [method: SetsRequiredMembers]
    public sealed record Ecdsa(EcdsaKeyValue Public) : Key<Types.Ecdsa, Schemes.ECDSA_SHA2_NISTP256, EcdsaKeyValue, PEMString>(Public), IAOTSerializable<Ecdsa>
    {
        override protected KeyId ComputeId() => new(this.ToDigest());

        public override bool VerifySignature(string signatureBytes, byte[] payloadBytes)
        {
            var edcsa = System.Security.Cryptography.ECDsa.Create(ECCurve.NamedCurves.nistP256);
            edcsa.ImportFromPem(Public.Public.PemEncodedValue);
            return edcsa.VerifyHash(payloadBytes, Encoding.UTF8.GetBytes(signatureBytes));
        }
        public static JsonTypeInfo<Ecdsa> JsonTypeInfo(MetadataJsonContext context) => context.Ecdsa;
    }
}