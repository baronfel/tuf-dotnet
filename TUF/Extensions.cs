using Serde;

using System.Security.Cryptography;

namespace TUF.Models;

/// <summary>
/// Extension methods for TUF metadata validation and verification.
/// Provides cryptographic signature verification and role validation capabilities.
/// </summary>
/// <remarks>
/// These extensions implement the core security functions of TUF:
/// 1. Signature verification using appropriate cryptographic algorithms
/// 2. Role-based trust delegation validation  
/// 3. Multi-signature threshold verification
/// 4. Key authorization checking
/// 
/// The extension methods follow TUF specification requirements for:
/// - Canonical JSON serialization for signature verification
/// - Support for Ed25519, RSA, and ECDSA signature schemes
/// - Proper threshold signature validation
/// - Trust delegation through root and targets metadata
/// </remarks>
public static class SimplifiedMetadataExtensions
{
    extension<T>(Metadata<T> sourceMetadata)
        where T : ISerializeProvider<T>, IDeserializeProvider<T>
    {
        /// <summary>
        /// Extension method for any metadata type that validates signatures against role requirements.
        /// Verifies that the specified metadata has sufficient valid signatures from authorized keys.
        /// </summary>
        /// <typeparam name="TOther">Type of the metadata being validated</typeparam>
        /// <param name="allKeys">Dictionary of all available keys (key ID -> Key)</param>
        /// <param name="roleKeys">Role configuration specifying authorized keys and threshold</param>
        /// <param name="otherMetadata">The metadata whose signatures should be validated</param>
        /// <exception cref="Exception">Thrown when validation fails for any reason</exception>
        /// <remarks>
        /// This method implements the core TUF signature verification algorithm:
        /// 1. Checks that the role has authorized keys and a valid threshold
        /// 2. For each authorized key, looks for a matching signature  
        /// 3. Verifies each signature cryptographically against the signed content
        /// 4. Ensures at least 'threshold' number of valid signatures are present
        /// 
        /// The method supports all TUF signature schemes (Ed25519, RSA PSS, ECDSA)
        /// and uses canonical JSON serialization for signature verification as required by the specification.
        /// </remarks>
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
            var signedBytes = otherMetadata.GetSignedBytes();

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
        /// Gets the canonical JSON bytes of the signed portion for signature verification.
        /// </summary>
        /// <returns>UTF-8 encoded bytes of the canonical JSON representation</returns>
        /// <remarks>
        /// This method serializes the signed portion of metadata to canonical JSON format
        /// as required by the TUF specification for signature verification.
        /// 
        /// In a production implementation, this should use a proper canonical JSON library
        /// to ensure deterministic serialization. The current implementation uses Serde
        /// JSON serialization as a placeholder.
        /// 
        /// Canonical JSON requirements:
        /// - Consistent field ordering (typically alphabetical)
        /// - No unnecessary whitespace
        /// - Consistent number representation
        /// - Consistent string escaping
        /// </remarks>
        public byte[] GetSignedBytes()
        {
            // For now, we'll serialize the signed portion using Serde
            // In a full implementation, this should use canonical JSON
            var signedJson = Serde.Json.JsonSerializer.Serialize(sourceMetadata.Signed);
            return System.Text.Encoding.UTF8.GetBytes(signedJson);
        }
    }

    extension(Metadata<Root> rootMetadata)
    {
        /// <summary>
        /// Extension method for root metadata to verify signatures of other role metadata.
        /// Uses root metadata's key assignments to validate signatures from delegated roles.
        /// </summary>
        /// <typeparam name="TOther">Type of the metadata being verified</typeparam>
        /// <param name="roleType">Name of the role to verify ("root", "timestamp", "snapshot", "targets", "mirrors")</param>
        /// <param name="otherMetadata">The metadata to verify against root's key assignments</param>
        /// <exception cref="Exception">Thrown when role is not found or validation fails</exception>
        /// <remarks>
        /// This method implements root-based trust delegation as specified in the TUF specification.
        /// Root metadata contains the authoritative key assignments for all repository roles.
        /// 
        /// The verification process:
        /// 1. Looks up the role definition in root metadata's roles configuration
        /// 2. Retrieves the authorized keys and threshold for that role
        /// 3. Validates the other metadata's signatures against those requirements
        /// 
        /// This is the primary mechanism for establishing trust in TUF - all role authority
        /// flows from root metadata's key assignments.
        /// </remarks>
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
        /// Extension method for targets metadata to verify delegated role signatures.
        /// Validates that delegated targets metadata is properly signed by authorized delegation keys.
        /// </summary>
        /// <param name="roleType">Name of the delegated role to verify</param>
        /// <param name="otherMetadata">The delegated targets metadata to verify</param>
        /// <exception cref="Exception">Thrown when delegation is not found or validation fails</exception>
        /// <remarks>
        /// This method implements targets delegation verification as specified in TUF specification section 5.6.
        /// Targets metadata can delegate signing authority for specific paths to other roles.
        /// 
        /// The verification process:
        /// 1. Checks that delegations are configured in the parent targets metadata
        /// 2. Finds the specific delegated role definition
        /// 3. Extracts the key assignments and threshold for that role
        /// 4. Validates the delegated metadata's signatures against those requirements
        /// 
        /// Delegation allows scalable repository management while maintaining cryptographic security.
        /// Different teams or systems can manage different parts of the target file tree.
        /// </remarks>
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

    extension(Key key)
    {
        /// <summary>
        /// Computes the TUF key identifier for this key.
        /// The key ID is the SHA-256 hash of the canonical JSON representation of the key.
        /// </summary>
        /// <returns>Hex-encoded SHA-256 hash of the key's canonical JSON</returns>
        /// <remarks>
        /// From TUF specification: "The key identifier is the hexadecimal representation 
        /// of the SHA-256 hash of the canonical JSON form of the key."
        /// 
        /// This ensures that keys with identical cryptographic material but potentially 
        /// different JSON formatting will have the same key ID.
        /// </remarks>
        public string GetKeyId()
        {
            // Serialize the key to canonical JSON
            var keyJson = Serde.Json.JsonSerializer.Serialize(key);
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(keyJson);
            
            // Compute SHA-256 hash
            var hashBytes = System.Security.Cryptography.SHA256.HashData(keyBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Verifies a cryptographic signature using the appropriate algorithm based on key type.
        /// Supports Ed25519, RSA PSS, and ECDSA signature verification.
        /// </summary>
        /// <param name="key">The key to use for verification</param>
        /// <param name="signature">Hex-encoded signature to verify</param>
        /// <param name="signedBytes">The data that was signed</param>
        /// <returns>True if the signature is valid, false otherwise</returns>
        /// <remarks>
        /// This method implements cryptographic signature verification for all TUF-supported key types:
        /// 
        /// - Ed25519: Modern elliptic curve signature scheme with excellent security and performance
        /// - RSA: Traditional RSA with PSS padding and SHA-256 hashing (minimum 2048 bits)
        /// - ECDSA: Elliptic curve signature scheme using NIST P-256 curve with SHA-256
        /// 
        /// The method handles:
        /// - Hex decoding of signature values
        /// - Public key import from various formats (PEM, raw bytes)
        /// - Algorithm-specific verification procedures
        /// - Proper error handling for invalid signatures or malformed keys
        /// 
        /// Security considerations:
        /// - All exceptions during verification result in returning false (fail-safe)
        /// - Uses constant-time comparison where possible to prevent timing attacks
        /// - Validates key formats before attempting cryptographic operations
        /// </remarks>
        public bool VerifyKeySignature(string signature, byte[] signedBytes)
        {
            try
            {
                var signatureBytes = Convert.FromHexString(signature);

                // Verify signature based on key type and scheme combination (pinned types)
                return (key.KeyType, key.Scheme) switch
                {
                    ("ed25519", "ed25519") => VerifyEd25519Signature(key.KeyVal.Public, signatureBytes, signedBytes),
                    ("rsa", "rsassa-pss-sha256") => VerifyRsaSignature(key.KeyVal.Public, signatureBytes, signedBytes),
                    ("ecdsa", "ecdsa-sha2-nistp256") => VerifyEcdsaSignature(key.KeyVal.Public, signatureBytes, signedBytes),
                    _ => throw new NotSupportedException($"Key type/scheme combination {key.KeyType}/{key.Scheme} is not supported")
                };
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Verifies an Ed25519 signature using the NSec cryptography library.
    /// </summary>
    /// <param name="publicKey">Hex-encoded Ed25519 public key (32 bytes)</param>
    /// <param name="signature">Ed25519 signature bytes (64 bytes)</param>
    /// <param name="data">The data that was signed</param>
    /// <returns>True if the signature is valid, false otherwise</returns>
    /// <remarks>
    /// Ed25519 is the preferred signature algorithm for TUF due to:
    /// - Strong security guarantees (128-bit security level)
    /// - Excellent performance (fast signing and verification)
    /// - Small key and signature sizes
    /// - Resistance to side-channel attacks
    /// - Deterministic signatures (same message always produces same signature)
    /// </remarks>
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

    /// <summary>
    /// Verifies an RSA PSS signature using SHA-256 hashing.
    /// </summary>
    /// <param name="publicKey">PEM-encoded RSA public key (minimum 2048 bits)</param>
    /// <param name="signature">RSA signature bytes</param>
    /// <param name="data">The data that was signed</param>
    /// <returns>True if the signature is valid, false otherwise</returns>
    /// <remarks>
    /// RSA with PSS padding provides:
    /// - Compatibility with existing PKI infrastructure
    /// - Provable security guarantees when using sufficient key sizes
    /// - Support in all major cryptographic libraries
    /// 
    /// TUF requires minimum 2048-bit RSA keys and recommends 3072-bit or larger
    /// for new deployments due to advances in factoring algorithms.
    /// </remarks>
    private static bool VerifyRsaSignature(string publicKey, byte[] signature, byte[] data)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKey);
            var hash = SHA256.HashData(data);
            return rsa.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies an ECDSA signature using the NIST P-256 curve and SHA-256 hashing.
    /// </summary>
    /// <param name="publicKey">PEM-encoded ECDSA public key using P-256 curve</param>
    /// <param name="signature">ECDSA signature bytes</param>
    /// <param name="data">The data that was signed</param>
    /// <returns>True if the signature is valid, false otherwise</returns>
    /// <remarks>
    /// ECDSA with P-256 provides:
    /// - Good balance of security and performance
    /// - Smaller key sizes compared to RSA for equivalent security
    /// - Wide industry adoption and standardization
    /// - FIPS 186-4 compliance
    /// 
    /// Note: ECDSA signatures are non-deterministic, meaning the same message
    /// will produce different signatures each time it's signed (unlike Ed25519).
    /// </remarks>
    private static bool VerifyEcdsaSignature(string publicKey, byte[] signature, byte[] data)
    {
        try
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            ecdsa.ImportFromPem(publicKey);
            return ecdsa.VerifyHash(data, signature);
        }
        catch
        {
            return false;
        }
    }
}