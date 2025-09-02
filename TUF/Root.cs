using System.Globalization;

using Serde;

namespace TUF.Models;

/// <summary>
/// Represents the signed portion of TUF root metadata.
/// Root metadata establishes the trust relationships and key assignments for all other roles.
/// </summary>
/// <remarks>
/// From TUF specification section 5.1: "The root role's metadata lists the public keys 
/// for the timestamp, snapshot, targets, and root roles, and the threshold number of 
/// signatures required to validate metadata from each role."
/// 
/// Root metadata is the foundation of trust in TUF - it must be distributed and updated
/// through a secure, out-of-band mechanism. All other metadata validity depends on 
/// having valid root metadata.
/// </remarks>
[GenerateSerde]
public partial record Root
{
    /// <summary>
    /// The metadata type identifier. Always "root" for root metadata.
    /// </summary>
    /// <remarks>
    /// This field allows parsers to identify the metadata type and apply appropriate
    /// validation rules without examining the filename or other context.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "root";
    
    /// <summary>
    /// The version of the TUF specification this metadata conforms to.
    /// Should follow semantic versioning (e.g., "1.0.0").
    /// </summary>
    /// <remarks>
    /// Clients MUST verify that the spec_version is a version they understand.
    /// This prevents clients from accepting metadata in formats they cannot properly validate.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    /// <summary>
    /// Indicates whether the repository uses consistent snapshots.
    /// When true, all files are prefixed with their version number.
    /// </summary>
    /// <remarks>
    /// Consistent snapshots allow multiple metadata versions to coexist, enabling
    /// atomic updates and preventing race conditions during repository updates.
    /// When false, metadata files use fixed names (snapshot.json, targets.json, etc.).
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "consistent_snapshot")]
    public bool? ConsistentSnapshot { get; init; }
    
    /// <summary>
    /// Version number of this root metadata instance.
    /// Must be incremented for each new version of root metadata.
    /// </summary>
    /// <remarks>
    /// Clients use this to detect and prevent rollback attacks. They MUST reject
    /// root metadata with a version number lower than previously seen.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    /// <summary>
    /// Expiration timestamp in ISO 8601 format (YYYY-MM-DDTHH:MM:SSZ).
    /// Clients must reject metadata after this time.
    /// </summary>
    /// <remarks>
    /// Expiration times prevent attackers from using old metadata indefinitely.
    /// Root metadata typically has longer expiration times (e.g., 1 year) since
    /// it's updated less frequently and requires out-of-band distribution.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "expires", Proxy = typeof(CanonicalJson.Proxies.CanonicalDateTimeOffsetProxy))]
    public DateTimeOffset Expires { get; init; }
    
    /// <summary>
    /// Dictionary of all keys used by any role in this TUF repository.
    /// Key is the key ID, value is the complete key specification.
    /// </summary>
    /// <remarks>
    /// This is the authoritative source for all cryptographic keys in the repository.
    /// Keys can be shared across multiple roles, but each key is defined only once here.
    /// The root role delegates specific keys to other roles through the 'roles' field.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "keys")]
    public Dictionary<string, Key> Keys { get; init; } = new();
    
    /// <summary>
    /// Role assignments that delegate specific keys to each TUF role.
    /// Defines which keys can sign for which roles and the signature thresholds.
    /// </summary>
    /// <remarks>
    /// This establishes the complete trust delegation hierarchy for the repository.
    /// Changes to role assignments require a new version of root metadata signed
    /// by the current root keys.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "roles")]
    public required Roles Roles { get; init; }
}