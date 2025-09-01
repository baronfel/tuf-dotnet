using Serde;

namespace TUF.Models.Simple;

/// <summary>
/// Represents the key assignment and threshold for a TUF role.
/// Specifies which keys can sign for a role and how many signatures are required.
/// </summary>
/// <remarks>
/// From TUF specification section 4.4: "A role contains a list of key identifiers 
/// and a threshold of keys required to sign metadata for that role."
/// </remarks>
[GenerateSerde]
public partial record RoleKeys
{
    /// <summary>
    /// List of key identifiers that are authorized to sign for this role.
    /// At least 'threshold' number of these keys must sign valid metadata.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "keyids")]
    public List<string> KeyIds { get; init; } = new();
    
    /// <summary>
    /// Minimum number of signatures required from the authorized keys.
    /// Must be at least 1 and no more than the number of key IDs.
    /// </summary>
    /// <remarks>
    /// The threshold provides security against key compromise - even if some keys
    /// are compromised, the attacker needs to compromise at least 'threshold' keys
    /// to forge metadata for this role.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "threshold")]
    public int Threshold { get; init; } = 1;
}

/// <summary>
/// Represents the complete role assignments for all top-level TUF roles.
/// This structure is found in root metadata and defines the trust relationships.
/// </summary>
/// <remarks>
/// From TUF specification section 5.1: "The root role delegates trust to four 
/// other roles: timestamp, snapshot, targets, and optionally mirrors."
/// </remarks>
[GenerateSerde]
public partial record Roles
{
    /// <summary>
    /// Key assignment for the root role.
    /// The root role signs root metadata and delegates trust to other roles.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "root")]
    public RoleKeys Root { get; init; } = new();
    
    /// <summary>
    /// Key assignment for the timestamp role.
    /// The timestamp role signs timestamp metadata, which references the latest snapshot.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "timestamp")]
    public RoleKeys Timestamp { get; init; } = new();
    
    /// <summary>
    /// Key assignment for the snapshot role.
    /// The snapshot role signs snapshot metadata, which lists all current metadata versions.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "snapshot")]
    public RoleKeys Snapshot { get; init; } = new();
    
    /// <summary>
    /// Key assignment for the targets role.
    /// The targets role signs targets metadata, which lists available target files.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "targets")]
    public RoleKeys Targets { get; init; } = new();
    
    /// <summary>
    /// Optional key assignment for the mirrors role (TAP 5).
    /// The mirrors role provides information about available repository mirrors.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "mirrors")]
    public RoleKeys? Mirrors { get; init; }
}

/// <summary>
/// Represents file metadata with version and integrity information.
/// Used in snapshot and timestamp metadata to reference other metadata files.
/// </summary>
/// <remarks>
/// This structure provides both version tracking and integrity verification
/// for metadata files, preventing rollback and corruption attacks.
/// </remarks>
[GenerateSerde]
public partial record FileMetadata
{
    /// <summary>
    /// Version number of the referenced metadata file.
    /// Used to prevent rollback attacks by ensuring newer versions are used.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    /// <summary>
    /// Length of the referenced file in bytes.
    /// Optional field that can be used to detect truncation attacks.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "length")]
    public int? Length { get; init; }
    
    /// <summary>
    /// Dictionary of cryptographic hashes for integrity verification.
    /// Key is the hash algorithm name (e.g., "sha256"), value is the hex-encoded hash.
    /// </summary>
    /// <remarks>
    /// From TUF specification: "The client MUST verify the length and hashes of 
    /// downloaded files against their expected values."
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "hashes")]
    public Dictionary<string, string>? Hashes { get; init; }
}