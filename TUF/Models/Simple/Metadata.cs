using Serde;

namespace TUF.Models.Simple;

/// <summary>
/// Generic wrapper for TUF metadata that combines signed content with cryptographic signatures.
/// This represents the complete structure of any TUF metadata file (root, timestamp, snapshot, targets, mirrors).
/// </summary>
/// <typeparam name="T">The type of signed metadata content (Root, Timestamp, Snapshot, Targets, or Mirrors)</typeparam>
/// <remarks>
/// From TUF specification section 4.1: "All TUF metadata files have the same high-level format:
/// a 'signed' object containing the actual metadata, and a 'signatures' array containing
/// cryptographic signatures of the signed object."
/// 
/// This generic structure provides:
/// 1. Type safety - ensures signed content matches the expected metadata type
/// 2. Signature verification - standardized signature validation across all metadata types  
/// 3. Serialization consistency - uniform JSON structure for all TUF metadata
/// 4. Extensibility - easy to add new metadata types while reusing common signature logic
/// 
/// The two-level structure (signed + signatures) enables:
/// - Canonical serialization of the signed portion for signature verification
/// - Multiple signatures from different keys for the same metadata
/// - Clean separation between content and authentication
/// - Consistent signature verification logic across all metadata types
/// </remarks>
[GenerateSerde]
public partial record Metadata<T> where T : ISerializeProvider<T>, IDeserializeProvider<T>
{
    /// <summary>
    /// The signed metadata content that is cryptographically protected.
    /// This object is serialized in canonical form for signature generation and verification.
    /// </summary>
    /// <remarks>
    /// The signed object contains all the actual metadata information (keys, roles, targets, etc.).
    /// When verifying signatures, this object is serialized to canonical JSON format and
    /// the resulting bytes are what the signatures protect.
    /// 
    /// Changes to any field in the signed object will invalidate existing signatures,
    /// requiring new signatures from authorized keys.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "signed")]
    public T Signed { get; init; } = default!;
    
    /// <summary>
    /// Array of cryptographic signatures that authenticate the signed metadata.
    /// Each signature is created by a key authorized for the metadata's role.
    /// </summary>
    /// <remarks>
    /// The signatures array contains one or more signature objects, each pairing:
    /// 1. A key ID identifying which key created the signature
    /// 2. The actual signature value (hex-encoded cryptographic signature)
    /// 
    /// For metadata to be valid:
    /// - At least 'threshold' number of signatures must be present (defined in role configuration)
    /// - Each signature must be from a key authorized for this role
    /// - Each signature must be a valid cryptographic signature of the canonical signed content
    /// - All signing keys must be properly trusted through the TUF trust chain
    /// 
    /// Multiple signatures provide:
    /// - Security against key compromise (requires threshold number of keys)
    /// - Key rotation capabilities (old and new keys can both sign during transitions)
    /// - Distributed signing scenarios (different entities can contribute signatures)
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "signatures")]
    public List<SignatureObject> Signatures { get; init; } = new();
}

// Type aliases for specific metadata types with descriptive names and documentation

/// <summary>
/// Complete root metadata including signatures.
/// Contains the foundation of trust for the entire TUF repository.
/// </summary>
/// <remarks>
/// Root metadata establishes the trust relationships and key assignments for all other roles.
/// This is the most critical metadata type as compromise of root keys allows an attacker
/// to control the entire repository. Root metadata updates require careful out-of-band verification.
/// </remarks>
[GenerateSerde]
public partial record RootMetadata : Metadata<Root>;

/// <summary>
/// Complete timestamp metadata including signatures.
/// Provides the entry point for clients to discover current repository state.
/// </summary>
/// <remarks>
/// Timestamp metadata is typically the first file clients download when checking for updates.
/// It has short expiration times and points to the current snapshot metadata, ensuring
/// clients can detect if they're being served stale repository information.
/// </remarks>
[GenerateSerde]
public partial record TimestampMetadata : Metadata<Timestamp>;

/// <summary>
/// Complete snapshot metadata including signatures.
/// Provides a consistent view of all available metadata versions.
/// </summary>
/// <remarks>
/// Snapshot metadata prevents mix-and-match attacks by ensuring clients see a consistent
/// set of metadata versions. It lists all targets metadata files and their versions,
/// creating an atomic view of the repository at a specific point in time.
/// </remarks>
[GenerateSerde]
public partial record SnapshotMetadata : Metadata<Snapshot>;

/// <summary>
/// Complete targets metadata including signatures.
/// Contains the catalog of available target files with cryptographic verification information.
/// </summary>
/// <remarks>
/// Targets metadata is the primary catalog of files available for download. It includes
/// cryptographic hashes and file sizes that clients use to verify downloaded content.
/// Can delegate signing authority to other roles for scalable repository management.
/// </remarks>
[GenerateSerde]
public partial record TargetsMetadata : Metadata<Targets>;

/// <summary>
/// Complete mirrors metadata including signatures (TAP 5).
/// Provides alternative download locations for improved availability and performance.
/// </summary>
/// <remarks>
/// Mirrors metadata is optional and provides redundancy and performance benefits.
/// Even when using mirrors, all TUF cryptographic verification still applies,
/// so compromised mirrors cannot serve malicious content to clients.
/// </remarks>
[GenerateSerde]
public partial record MirrorsMetadata : Metadata<Mirrors>;