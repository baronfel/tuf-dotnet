using System.Security.Cryptography;

using Serde;

using TUF.Models.Keys.Values;
using TUF.Models.Primitives;

namespace TUF.Models.Keys;

public abstract record KeyBase
{
    private KeyBase() {}

    protected abstract KeyId ComputeId();

    public abstract string Type { get; }
    public abstract string Scheme { get; }

    public KeyId Id => ComputeId();

    public abstract bool VerifySignature(byte[] signatureBytes, byte[] payloadBytes);

    public record Rsa(RsaKeyValue Public) : KeyBase
    {   
        public override string Type => Types.Rsa.Name;
        public override string Scheme => Schemes.RSASSA_PSS_SHA256.Name;

        protected override KeyId ComputeId() => new(this.ToDigest());

        public override bool VerifySignature(byte[] signatureBytes, byte[] payloadBytes)
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(Public.Public.PemEncodedValue);
            // Hash the payload data first, then verify the hash
            var hash = SHA256.HashData(payloadBytes);
            return rsa.VerifyHash(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
    }
    
    public record Ed25519(Ed25519KeyValue Public) : KeyBase
    {
        public override string Type => Types.Ed25519.Name;
        public override string Scheme => Schemes.Ed25519.Name;

        override protected KeyId ComputeId() => new(this.ToDigest());

        public override bool VerifySignature(byte[] signatureBytes, byte[] payloadBytes)
        {
            try
            {
                var publicKeyBytes = Convert.FromHexString(Public.Public.HexEncodedValue);
                var publicKey = NSec.Cryptography.PublicKey.Import(NSec.Cryptography.SignatureAlgorithm.Ed25519,
                    publicKeyBytes, NSec.Cryptography.KeyBlobFormat.RawPublicKey);
                return NSec.Cryptography.SignatureAlgorithm.Ed25519.Verify(publicKey, payloadBytes, signatureBytes);
            }
            catch
            {
                return false;
            }
        }
    }

    public record Ecdsa(EcdsaKeyValue Public) : KeyBase
    {
        public override string Type => Types.Ecdsa.Name;
        public override string Scheme => Schemes.ECDSA_SHA2_NISTP256.Name;

        override protected KeyId ComputeId() => new(this.ToDigest());

        public override bool VerifySignature(byte[] signatureBytes, byte[] payloadBytes)
        {
            var edcsa = System.Security.Cryptography.ECDsa.Create(ECCurve.NamedCurves.nistP256);
            edcsa.ImportFromPem(Public.Public.PemEncodedValue);
            return edcsa.VerifyHash(payloadBytes, signatureBytes);
        }
    }
}

public static class KeyExtensions
{
    extension<T>(T item) where T : ISerializeProvider<T>
    {
        /// <summary>
        /// Computes the hexdigest of an item by serializing it to utf8 bytes via the 
        /// canonical json format, then getting the sha256hash of that, then hex-tolowering the result.
        /// </summary>
        public HexDigest ToDigest()
        {
            var bytes = CanonicalJson.CanonicalJsonSerializer.Serialize(item);
            return new HexDigest(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }
}