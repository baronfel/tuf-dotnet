using Serde;

namespace TUF.Models;

/// <summary>
/// Represents the signed portion of TUF timestamp metadata.
/// Timestamp metadata provides the entry point for clients to discover the latest repository state.
/// </summary>
/// <remarks>
/// From TUF specification section 5.2: "The timestamp role signs a metadata file that indicates 
/// the latest version of the snapshot metadata. This is the first file that clients download 
/// when they are updating their metadata."
/// 
/// Timestamp metadata serves several critical security functions:
/// 1. Provides freshness guarantee - clients can detect if they're being served stale metadata
/// 2. References the current snapshot - acts as entry point to the metadata tree
/// 3. Has short expiration - forces frequent updates to prevent freeze attacks
/// 4. Small size - can be downloaded quickly to check for updates
/// </remarks>
[GenerateSerde]
public partial record Timestamp
{
    /// <summary>
    /// The metadata type identifier. Always "timestamp" for timestamp metadata.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "timestamp";
    
    /// <summary>
    /// The version of the TUF specification this metadata conforms to.
    /// Should follow semantic versioning (e.g., "1.0.0").
    /// </summary>
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    /// <summary>
    /// Version number of this timestamp metadata instance.
    /// Must be incremented for each new version.
    /// </summary>
    /// <remarks>
    /// Timestamp version numbers increase monotonically and clients use this to
    /// detect rollback attacks. Unlike other metadata, timestamp versions may
    /// increase frequently (potentially on every repository change).
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    /// <summary>
    /// Expiration timestamp in ISO 8601 format (YYYY-MM-DDTHH:MM:SSZ).
    /// Clients must reject metadata after this time.
    /// </summary>
    /// <remarks>
    /// Timestamp metadata typically has short expiration times (e.g., 1 day) to ensure
    /// clients receive fresh metadata frequently. This helps detect freeze attacks
    /// where an attacker serves old metadata indefinitely.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "expires")]
    public string Expires { get; init; } = "";
    
    /// <summary>
    /// Metadata information for files referenced by this timestamp.
    /// Typically contains only "snapshot.json" with its version and hashes.
    /// </summary>
    /// <remarks>
    /// The 'meta' field tells clients which snapshot metadata to download and how to
    /// verify its integrity. In consistent snapshot mode, this may reference
    /// version-prefixed snapshot files (e.g., "42.snapshot.json").
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "meta")]
    public Dictionary<string, FileMetadata> Meta { get; init; } = new();
}