using System.Data.SqlTypes;
using tuf_dotnet.Serialization;

namespace tuf_dotnet.Models;

public static class KeyTypes
{
    public interface IKeyType<T> where T : IKeyType<T>
    {
        static abstract string Name { get; }
    }

    public sealed record Rsa : IKeyType<Rsa>
    {
        public static string Name => "rsa";
    }

    public sealed record Ed25519 : IKeyType<Ed25519>
    {
        public static string Name => "ed25519";
    }

    public sealed record Ecdsa : IKeyType<Ecdsa>
    {
        public static string Name => "ecdsa";
    }
}


public static class DigestAlgorithms
{
    public interface IDigestAlgorithm<T> where T : IDigestAlgorithm<T>
    {
        static abstract string Name { get; }
    }

    public sealed record SHA256 : IDigestAlgorithm<SHA256>
    {
        public static string Name => "sha256";
    }

    public sealed record SHA512 : IDigestAlgorithm<SHA512>
    {
        public static string Name => "sha512";
    }

    public record DigestValue(string Algorithm, string HexEncodedValue);
    public sealed record DigestValue<T>(string HexEncodedValue) : DigestValue(T.Name, HexEncodedValue) where T : IDigestAlgorithm<T>;
}

public static class KeySchemes
{
    public interface IKeyScheme
    {
        string Name { get; }
    }

    public interface IKeyScheme<T> where T : IKeyScheme<T>
    {
        static abstract string Name { get; }
    }

    public record KeyScheme(string Name) : IKeyScheme
    {
    }

    public record KeyScheme<T>() : KeyScheme(T.Name) where T : IKeyScheme<T>
    {
    }
    
    public sealed record RSASSA_PSS_SHA256() : KeyScheme<RSASSA_PSS_SHA256>(), IKeyScheme<RSASSA_PSS_SHA256>
    {
        public static new string Name => "rsassa-pss-sha256";
    }

    public sealed record Ed25519() : KeyScheme<Ed25519>(), IKeyScheme<Ed25519>
    {
        public static new string Name => "ed25519";
    }

    public sealed record ECDSA_SHA2_NISTP256() : KeyScheme<ECDSA_SHA2_NISTP256>(), IKeyScheme<ECDSA_SHA2_NISTP256>
    {
        public static new string Name => "ecdsa-sha2-nistp256";
    }
}

public static class KeyValues
{

    public interface IKeyValue<TKeyType, TValueType> where TKeyType : KeyTypes.IKeyType<TKeyType>
    {
        abstract TValueType Public { get; }
    }

    /// <summary>
    /// PEM format and a string. All RSA keys MUST be at least 2048 bits.
    /// </summary>
    public record struct PEMString(string PemEncodedValue);

    /// <summary>
    /// 64-bit hex-encoded string
    /// </summary>
    public record struct HexString(string HexEncodedValue);

    public sealed record RsaKeyValue(PEMString Public) : IKeyValue<KeyTypes.Rsa, PEMString>;
    public sealed record Ed25519KeyValue(HexString Public) : IKeyValue<KeyTypes.Ed25519, HexString>;
    public sealed record EcdsaKeyValue(PEMString Public) : IKeyValue<KeyTypes.Ecdsa, PEMString>;
}

public static class Keys
{
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

    public record Key<TKey, TKeyScheme, TKeyValInner>(KeyValues.IKeyValue<TKey, TKeyValInner> KeyVal) : Key<KeyValues.IKeyValue<TKey, TKeyValInner>>(TKey.Name, TKeyScheme.Name, KeyVal)
        where TKey : KeyTypes.IKeyType<TKey>
        where TKeyScheme : KeySchemes.IKeyScheme<TKeyScheme>
    {
    }

    public record struct KeyId(string hexDigestSha256hash);

    public static class WellKnown
    {
        public sealed record Rsa(KeyValues.RsaKeyValue Public) : Key<KeyTypes.Rsa, KeySchemes.RSASSA_PSS_SHA256, KeyValues.PEMString>(Public);
        public sealed record Ed25519(KeyValues.Ed25519KeyValue Public) : Key<KeyTypes.Ed25519, KeySchemes.Ed25519, KeyValues.HexString>(Public);
        public sealed record Ecdsa(KeyValues.EcdsaKeyValue Public) : Key<KeyTypes.Ecdsa, KeySchemes.ECDSA_SHA2_NISTP256, KeyValues.PEMString>(Public);
    }
}

public static class Roles
{
    public record SemanticVersion(string SemVer);

    public static class Root
    {
        public record RoleKeys(List<Keys.KeyId> KeyIds, uint Threshold);
        public record RootRoles(RoleKeys Root, RoleKeys Timestamp, RoleKeys Snapshot, RoleKeys Targets, RoleKeys? Mirrors);

        public record RootRole(SemanticVersion SpecVersion, bool? ConsistentSnapshot, uint Version, DateTimeOffset Expires, Dictionary<Keys.KeyId, Keys.IKey> Keys, RootRoles Roles);
    }
    public record struct RelativePath(string RelPath);

    public record FileMetadata(uint Version, uint? Length, List<DigestAlgorithms.DigestValue>? Hashes);

    public static class Snapshot
    {
        public record struct HashAlgorithm(string algo);
        public record SnapshotRole(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires, Dictionary<RelativePath, FileMetadata> Meta);
    }

    public static class Targets
    {
        public interface ITargetMetadata
        {
            uint Length { get; }
            List<DigestAlgorithms.DigestValue> Hashes { get; }
        }

        // todo: model delegations
        public record TargetRole(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires, Dictionary<RelativePath, ITargetMetadata> Targets);
    }
    
    public static class Timestamp
    {   
        /// <summary>
        /// For the timestamp role specifically, the only metadata record is for the snapshot.json file.
        /// </summary>
        public class SnapshotMetadata(FileMetadata snapshotFileMetadata) : Dictionary<RelativePath, FileMetadata>([new(new("snapshot.json"), snapshotFileMetadata)])
        {
        }
        public record TimestampRole(SemanticVersion SpecVersion, uint Version, DateTimeOffset Expires, FileMetadata Meta);
    }
}


/// <summary>
/// A hex-encoded signature of the canonical form of a metadata object
/// </summary>
public record struct Signature();

public record struct KeyId(string hexDigestSha256hash);

public record SignatureResult(KeyId keyId, Signature Signature);

public interface ISigner
{
    public SignatureResult Sign<T>(Metadata<T> metadata);
}

public record Metadata<T>(
    T Signed,
    Dictionary<KeyId, Signature> Signatures,
    Dictionary<string, object>? UnrecognizedFields)
{
    public byte[] SignedBytes => CanonicalJsonSerializer.Serialize(Signed);

    /// <summary>
    /// Signs the metadata using the provided signer.
    /// </summary>
    /// <param name="signer"></param>
    /// <param name="replaceExisting"></param>
    /// <returns></returns>
    public SignatureResult Sign(ISigner signer, bool replaceExisting = true)
    {
        var sig = signer.Sign(this);

        if (replaceExisting)
        {
            Signatures.Clear();
        }

        Signatures[sig.keyId] = sig.Signature;
        return sig;
    }
}

