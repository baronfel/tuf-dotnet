using Serde;
using System.Text.Json.Serialization;

namespace TUF.Models.Simple;

// Basic primitive types
[GenerateSerde]
public partial record KeyId(string Value);

[GenerateSerde] 
public partial record Signature(string Value);

// Key structures
[GenerateSerde]
public partial record KeyValue
{
    [property: SerdeMemberOptions(Rename = "public")]
    public string Public { get; init; } = "";
}

[GenerateSerde]
public partial record Key
{
    [property: SerdeMemberOptions(Rename = "keytype")]
    public string KeyType { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "scheme")]
    public string Scheme { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "keyval")]
    public KeyValue KeyVal { get; init; } = new();
}

// Role assignment
[GenerateSerde]
public partial record RoleKeys
{
    [property: SerdeMemberOptions(Rename = "keyids")]
    public List<string> KeyIds { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "threshold")]
    public int Threshold { get; init; } = 1;
}

[GenerateSerde]
public partial record Roles
{
    [property: SerdeMemberOptions(Rename = "root")]
    public RoleKeys Root { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "timestamp")]
    public RoleKeys Timestamp { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "snapshot")]
    public RoleKeys Snapshot { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "targets")]
    public RoleKeys Targets { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "mirrors")]
    public RoleKeys? Mirrors { get; init; }
}

// Root metadata
[GenerateSerde]
public partial record Root
{
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "root";
    
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "consistent_snapshot")]
    public bool? ConsistentSnapshot { get; init; }
    
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "expires")]
    public string Expires { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "keys")]
    public Dictionary<string, Key> Keys { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "roles")]
    public Roles Roles { get; init; } = new();
}

// Snapshot/Timestamp file metadata
[GenerateSerde]
public partial record FileMetadata
{
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "length")]
    public int? Length { get; init; }
    
    [property: SerdeMemberOptions(Rename = "hashes")]
    public Dictionary<string, string>? Hashes { get; init; }
}

// Timestamp metadata
[GenerateSerde]
public partial record Timestamp
{
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "timestamp";
    
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "expires")]
    public string Expires { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "meta")]
    public Dictionary<string, FileMetadata> Meta { get; init; } = new();
}

// Snapshot metadata
[GenerateSerde]
public partial record Snapshot
{
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "snapshot";
    
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "expires")]
    public string Expires { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "meta")]
    public Dictionary<string, FileMetadata> Meta { get; init; } = new();
}

// Target file info
[GenerateSerde]
public partial record TargetFile
{
    [property: SerdeMemberOptions(Rename = "length")]
    public int Length { get; init; }
    
    [property: SerdeMemberOptions(Rename = "hashes")]
    public Dictionary<string, string> Hashes { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "custom")]
    public Dictionary<string, string>? Custom { get; init; }
}

// Delegation role
[GenerateSerde]
public partial record DelegatedRole
{
    [property: SerdeMemberOptions(Rename = "name")]
    public string Name { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "keyids")]
    public List<string> KeyIds { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "threshold")]
    public int Threshold { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "paths")]
    public List<string>? Paths { get; init; }
    
    [property: SerdeMemberOptions(Rename = "path_hash_prefixes")]
    public List<string>? PathHashPrefixes { get; init; }
    
    [property: SerdeMemberOptions(Rename = "terminating")]
    public bool Terminating { get; init; } = false;
}

// Delegations
[GenerateSerde]
public partial record Delegations
{
    [property: SerdeMemberOptions(Rename = "keys")]
    public Dictionary<string, Key> Keys { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "roles")]
    public List<DelegatedRole> Roles { get; init; } = new();
}

// Targets metadata
[GenerateSerde]
public partial record Targets
{
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "targets";
    
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "expires")]
    public string Expires { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "targets")]
    public Dictionary<string, TargetFile> TargetMap { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "delegations")]
    public Delegations? Delegations { get; init; }
}

// Signature object
[GenerateSerde]
public partial record SignatureObject
{
    [property: SerdeMemberOptions(Rename = "keyid")]
    public string KeyId { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "sig")]
    public string Sig { get; init; } = "";
}

// Generic metadata wrapper with proper Serde constraints
[GenerateSerde]
public partial record Metadata<T> where T : ISerializeProvider<T>, IDeserializeProvider<T>
{
    [property: SerdeMemberOptions(Rename = "signed")]
    public T Signed { get; init; } = default!;
    
    [property: SerdeMemberOptions(Rename = "signatures")]
    public List<SignatureObject> Signatures { get; init; } = new();
}

// Mirrors (TAP 5)
[GenerateSerde]
public partial record Mirror
{
    [property: SerdeMemberOptions(Rename = "url")]
    public string Url { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "custom")]
    public Dictionary<string, string>? Custom { get; init; }
}

[GenerateSerde]
public partial record Mirrors
{
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "mirrors";
    
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "expires")]
    public string Expires { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "mirrors")]
    public List<Mirror> MirrorList { get; init; } = new();
}

// Extension methods for metadata validation and verification
public static class SimplifiedMetadataExtensions
{
    extension<T>(Metadata<T> sourceMetadata)
        where T : ISerializeProvider<T>, IDeserializeProvider<T>
    {
        /// <summary>
        /// Validates signatures against a set of keys and role requirements
        /// </summary>
        public void ValidateKeys<TOther>(Dictionary<string, Key> allKeys, RoleKeys roleKeys, Metadata<TOther> otherMetadata)
            where TOther : ISerializeProvider<TOther>, IDeserializeProvider<TOther>
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
            var signedBytes = GetSignedBytes(otherMetadata);

            foreach (var keyId in roleKeys.KeyIds)
            {
                if (!allKeys.TryGetValue(keyId, out var key))
                {
                    throw new Exception($"Key {keyId} not found in keys");
                }

                // try to find matching signature in other metadata
                var matchingSignature = otherMetadata.Signatures.FirstOrDefault(s => s.KeyId == keyId);
                if (matchingSignature == null)
                {
                    throw new Exception($"No signature found for key {keyId}");
                }

                if (VerifyKeySignature(key, matchingSignature.Sig, signedBytes))
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

        /// <summary>
        /// Gets the canonical JSON bytes of the signed portion for signature verification
        /// </summary>
        private byte[] GetSignedBytes()
        {
            // For now, we'll serialize the signed portion using Serde
            // In a full implementation, this should use canonical JSON
            var signedJson = Serde.Json.JsonSerializer.Serialize(sourceMetadata.Signed);
            return System.Text.Encoding.UTF8.GetBytes(signedJson);
        }

        
    }

    extension(Metadata<Root> rootMetadata)
    {
        public void VerifyRole<TOther>(string roleType, Metadata<TOther> otherMetadata)
            where TOther : ISerializeProvider<TOther>, IDeserializeProvider<TOther>
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

            rootMetadata.ValidateKeys(rootMetadata.Signed.Keys, roleKeys, otherMetadata);
        }
    }

    extension(Metadata<Targets> targetsMetadata)
    {
        /// <summary>
        /// Verifies a delegated role signature against targets metadata
        /// </summary>
        public void VerifyDelegatedRole(string roleType, Metadata<Targets> otherMetadata)
        {
            if (targetsMetadata.Signed.Delegations is null || targetsMetadata.Signed.Delegations.Roles.Count == 0)
            {
                throw new Exception("No delegations defined in targets metadata");
            }

            var delegation = targetsMetadata.Signed.Delegations.Roles.FirstOrDefault(r => r.Name == roleType);
            if (delegation == null)
            {
                throw new Exception($"No delegation found for {roleType}");
            }

            var roleKeys = new RoleKeys
            {
                KeyIds = delegation.KeyIds,
                Threshold = delegation.Threshold
            };

            targetsMetadata.ValidateKeys(targetsMetadata.Signed.Delegations.Keys, roleKeys, otherMetadata);
        }
    }


    /// <summary>
    /// Verifies a signature using the appropriate key type
    /// </summary>
    private static bool VerifyKeySignature(Key key, string signature, byte[] signedBytes)
    {
        try
        {
            var signatureBytes = Convert.FromHexString(signature);

            return key.KeyType switch
            {
                "ed25519" => VerifyEd25519Signature(key.KeyVal.Public, signatureBytes, signedBytes),
                "rsa" => VerifyRsaSignature(key.KeyVal.Public, signatureBytes, signedBytes),
                "ecdsa" => VerifyEcdsaSignature(key.KeyVal.Public, signatureBytes, signedBytes),
                _ => throw new NotSupportedException($"Key type {key.KeyType} is not supported")
            };
        }
        catch
        {
            return false;
        }
    }

    private static bool VerifyEd25519Signature(string publicKey, byte[] signature, byte[] data)
    {
        try
        {
            var publicKeyBytes = Convert.FromHexString(publicKey);
            var pubKey = NSec.Cryptography.PublicKey.Import(NSec.Cryptography.SignatureAlgorithm.Ed25519,
                publicKeyBytes, NSec.Cryptography.KeyBlobFormat.RawPublicKey);
            return NSec.Cryptography.SignatureAlgorithm.Ed25519.Verify(pubKey, data, signature);
        }
        catch
        {
            return false;
        }
    }

    private static bool VerifyRsaSignature(string publicKey, byte[] signature, byte[] data)
    {
        try
        {
            using var rsa = System.Security.Cryptography.RSA.Create();
            rsa.ImportFromPem(publicKey);
            var hash = System.Security.Cryptography.SHA256.HashData(data);
            return rsa.VerifyHash(hash, signature, System.Security.Cryptography.HashAlgorithmName.SHA256, 
                System.Security.Cryptography.RSASignaturePadding.Pss);
        }
        catch
        {
            return false;
        }
    }

    private static bool VerifyEcdsaSignature(string publicKey, byte[] signature, byte[] data)
    {
        try
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
            ecdsa.ImportFromPem(publicKey);
            return ecdsa.VerifyHash(data, signature);
        }
        catch
        {
            return false;
        }
    }
}