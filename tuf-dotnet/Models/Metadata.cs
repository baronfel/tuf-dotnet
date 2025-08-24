using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using CanonicalJson;

using TUF.Models.Primitives;
using TUF.Models.Roles;
using TUF.Models.Roles.Mirrors;
using TUF.Models.Roles.Root;
using TUF.Models.Roles.Snapshot;
using TUF.Models.Roles.Targets;
using TUF.Models.Roles.Timestamp;
using TUF.Serialization;
using TUF.Signing;

namespace TUF.Models;

public abstract class Metadata<TSigned>(TSigned signed, Dictionary<KeyId, Signature> signatures)
    where TSigned : IRole<TSigned>, IAOTSerializable<TSigned>
{   
    [JsonPropertyName("signed")]
    public TSigned Signed => signed;

    [JsonPropertyName("signatures")]
    public Dictionary<KeyId, Signature> Signatures => signatures;

    [JsonExtensionData]
    public Dictionary<string, object>? UnrecognizedFields { get; set; }

    public byte[] SignedBytes => CanonicalJsonSerializer.Serialize(Signed, TSigned.JsonTypeInfo);

    public bool IsExpired(DateTimeOffset reference)
    {
        return reference > Signed.Expires;
    }
}

public static class MetadataExtensions
{
    public static SignatureResult Sign<T, TInner>(this T metadata, ISigner signer, bool replaceExisting = true)
        where T : Metadata<TInner>, IAOTSerializable<T>
        where TInner : IRole<TInner>, IAOTSerializable<TInner>
    {
        var sig = signer.Sign<T, TInner>(metadata);

        if (replaceExisting)
        {
            metadata.Signatures.Clear();
        }

        metadata.Signatures[sig.keyId] = sig.Signature;
        return sig;
    }
}

public sealed class RootMetadata(
        Root signed,
        Dictionary<KeyId, Signature> signatures) : Metadata<Root>(signed, signatures), IAOTSerializable<RootMetadata>
{
    public static JsonTypeInfo<RootMetadata> JsonTypeInfo => MetadataJsonContext.Default.RootMetadata;
}

public sealed class SnapshotMetadata(Snapshot signed, Dictionary<KeyId, Signature> signatures) : Metadata<Snapshot>(signed, signatures), IAOTSerializable<SnapshotMetadata>
{
    public static JsonTypeInfo<SnapshotMetadata> JsonTypeInfo => MetadataJsonContext.Default.SnapshotMetadata;
}
public sealed class TargetsMetadata(TargetsRole signed, Dictionary<KeyId, Signature> signatures) : Metadata<TargetsRole>(signed, signatures), IAOTSerializable<TargetsMetadata>
{
    public static JsonTypeInfo<TargetsMetadata> JsonTypeInfo => MetadataJsonContext.Default.TargetsMetadata;
}
public sealed class TimestampMetadata(Timestamp signed, Dictionary<KeyId, Signature> signatures) : Metadata<Timestamp>(signed, signatures), IAOTSerializable<TimestampMetadata>
{
    public static JsonTypeInfo<TimestampMetadata> JsonTypeInfo => MetadataJsonContext.Default.TimestampMetadata;
}
public sealed class MirrorMetadata(Mirror signed, Dictionary<KeyId, Signature> signatures) : Metadata<Mirror>(signed, signatures), IAOTSerializable<MirrorMetadata>
{
    public static JsonTypeInfo<MirrorMetadata> JsonTypeInfo => MetadataJsonContext.Default.MirrorMetadata;
}