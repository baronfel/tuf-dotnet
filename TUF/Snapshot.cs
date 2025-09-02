using Serde;

namespace TUF.Models;

/// <summary>
/// Represents the signed portion of TUF snapshot metadata.
/// Snapshot metadata provides a complete view of all available metadata versions at a point in time.
/// </summary>
/// <remarks>
/// From TUF specification section 5.3: "The snapshot role signs a metadata file that lists 
/// the version numbers of all targets metadata files."
/// 
/// Snapshot metadata serves several important purposes:
/// 1. Prevents mix-and-match attacks by ensuring consistent metadata versions
/// 2. Provides integrity verification for all targets metadata
/// 3. Enables atomic repository updates by referencing specific metadata versions
/// 4. Supports delegation discovery by listing all available targets metadata
/// 
/// The snapshot creates a "consistent view" of the repository at a specific point in time,
/// preventing attackers from serving clients inconsistent combinations of metadata files.
/// </remarks>
[GenerateSerde]
public partial record Snapshot
{
    /// <summary>
    /// The metadata type identifier. Always "snapshot" for snapshot metadata.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "snapshot";
    
    /// <summary>
    /// The version of the TUF specification this metadata conforms to.
    /// Should follow semantic versioning (e.g., "1.0.0").
    /// </summary>
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    /// <summary>
    /// Version number of this snapshot metadata instance.
    /// Must be incremented for each new version.
    /// </summary>
    /// <remarks>
    /// Snapshot versions increase when any targets metadata changes. Clients use this
    /// to detect if they need to update their local targets metadata cache.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    /// <summary>
    /// Expiration timestamp in ISO 8601 format (YYYY-MM-DDTHH:MM:SSZ).
    /// Clients must reject metadata after this time.
    /// </summary>
    /// <remarks>
    /// Snapshot metadata typically has moderate expiration times (e.g., 1 week)
    /// balancing security against operational overhead. It expires more frequently
    /// than root/targets but less frequently than timestamp metadata.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "expires")]
    public DateTimeOffset Expires { get; init; }
    
    /// <summary>
    /// Metadata information for all targets metadata files in the repository.
    /// Key is the metadata filename, value contains version and integrity information.
    /// </summary>
    /// <remarks>
    /// This field lists ALL targets metadata files, including:
    /// - Top-level targets.json
    /// - All delegated targets metadata (e.g., "role1.json", "role2.json")
    /// 
    /// For each metadata file, the snapshot provides:
    /// - Version number (for rollback protection)
    /// - Optional length (for truncation attack prevention)  
    /// - Optional hashes (for integrity verification)
    /// 
    /// In consistent snapshot mode, filenames may be version-prefixed.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "meta")]
    public Dictionary<string, FileMetadata> Meta { get; init; } = new();
}