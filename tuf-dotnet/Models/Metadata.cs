using System.Reflection.Metadata;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using CanonicalJson;

using TUF.Models.Keys;
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
    extension<T, TInner>(T metadata)
        where T : Metadata<TInner>, IAOTSerializable<T>
        where TInner : IRole<TInner>, IAOTSerializable<TInner>
    {
        public Signature Sign(ISigner signer, bool replaceExisting = true)
        {
            var signature = signer.SignBytes(metadata.SignedBytes);

            if (replaceExisting)
            {
                metadata.Signatures.Clear();
            }

            var keyId = signer.Key.Id;

            metadata.Signatures[keyId] = signature;
            return signature;
        }
    }

    extension<T, TInner>(T metadata)
        where T: Metadata<TInner>, IAOTSerializable<T>
        where TInner : IRole<TInner>, IAOTSerializable<TInner>
    {
        void ValidateKeys<TOther, TOtherInner>(Dictionary<KeyId, Key> allKeys, RoleKeys roleKeys, TOther otherMetadata)
            where TOther : Metadata<TOtherInner>
            where TOtherInner : IRole<TOtherInner>, IAOTSerializable<TOtherInner>
        {
            if (roleKeys.KeyIds.Count == 0)
            {
                throw new Exception("No delegation found");
            }

            if (roleKeys.Threshold < 1)
            {
                throw new Exception("Invalid threshold");
            }

            var verifiedSignatures = 0;

            foreach (var keyId in roleKeys.KeyIds)
            {
                if (!allKeys.TryGetValue(keyId, out var key))
                {
                    throw new Exception($"Key {keyId} not found in keys");
                }
                // try to find matching signature in other metadata
                if (!otherMetadata.Signatures.TryGetValue(key.Id, out var signature))
                {
                    throw new Exception($"No signature found for key {keyId}");
                }

                if (key.VerifySignature(signature.Value, otherMetadata.SignedBytes))
                {
                    verifiedSignatures++;
                }
                else
                {
                    throw new Exception($"Signature verification failed for key {keyId}");
                }
            }

            if (verifiedSignatures < roleKeys.Threshold)
            {
                throw new Exception($"Insufficient valid signatures: {verifiedSignatures} of {roleKeys.Threshold} required");
            }
        }
    }

    extension<T>(T rootMetadata)
        where T: Metadata<Root>, IAOTSerializable<T>
    {
        void VerifyRootRole<TOther, TOtherInner>(string roleType, TOther otherMetadata) where TOther: Metadata<TOtherInner>, IAOTSerializable<TOther> where TOtherInner: IRole<TOtherInner>, IAOTSerializable<TOtherInner>
        {
            // try to match the given role with one of our known roles, and get the keyids and threshold from that delegation
            var roles = rootMetadata.Signed.Roles;
            RoleKeys? roleKeys = roleType switch
            {
                "root" => roles.Root,
                "timestamp" => roles.Timestamp,
                "snapshot" => roles.Snapshot,
                "targets" => roles.Targets,
                "mirrors" => roles.Mirrors,
                _ => null
            };

            if (roleKeys is null)
            {
                throw new Exception($"No delegation found for {roleType}");
            }

            ValidateKeys<T, Root, TOther, TOtherInner>(rootMetadata, rootMetadata.Signed.Keys, roleKeys, otherMetadata);
        }
    }

    extension<T>(T targetsMetadata)
        where T: Metadata<TargetsRole>, IAOTSerializable<T>
    {
        void VerifyDelegatedRole<TOther, TOtherInner>(string roleType, TOther otherMetadata) where TOther: Metadata<TOtherInner> where TOtherInner: IRole<TOtherInner>, IAOTSerializable<TOtherInner>
        {
            if (targetsMetadata.Signed.Delegations is null or not { Roles: { Count: > 0 } })
            {
                throw new Exception("No delegations defined in targets metadata");
            }

            if (!targetsMetadata.Signed.Delegations.Roles.TryGetValue(new(roleType), out var delegation))
            {
                throw new Exception($"No delegation found for {roleType}");
            }

            ValidateKeys<T, TargetsRole, TOther, TOtherInner>(targetsMetadata, targetsMetadata.Signed.Delegations.Keys, delegation.RoleKeys, otherMetadata);
        }
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