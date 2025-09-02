using Serde;

namespace TUF.Models;

/// <summary>
/// Represents a single repository mirror definition.
/// Mirrors provide alternative download locations for repository content.
/// </summary>
/// <remarks>
/// From TAP 5 (TUF Enhancement Proposal 5): "Mirrors provide redundancy and 
/// can improve download performance by allowing clients to choose geographically 
/// closer or faster mirrors."
/// 
/// Mirror selection can be based on:
/// 1. Geographic location (latency optimization)
/// 2. Network conditions (bandwidth, reliability)  
/// 3. Content availability (some mirrors may have subset of files)
/// 4. Client policies (organization preferences, security requirements)
/// </remarks>
[GenerateSerde]
public partial record Mirror
{
    /// <summary>
    /// Base URL for this mirror endpoint.
    /// Should include the protocol (https://) and end without a trailing slash.
    /// </summary>
    /// <remarks>
    /// Mirror URLs should use HTTPS to prevent man-in-the-middle attacks during
    /// initial mirror discovery. The URL serves as the base for constructing
    /// paths to specific repository files.
    /// 
    /// Example: "https://mirror.example.com/repository"
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "url")]
    public string Url { get; init; } = "";
    
    /// <summary>
    /// Optional custom metadata for application-specific mirror properties.
    /// Can include geographic, performance, or policy information.
    /// </summary>
    /// <remarks>
    /// Custom metadata might include:
    /// - "location": Geographic region or country code
    /// - "bandwidth": Expected bandwidth characteristics
    /// - "priority": Suggested priority for mirror selection
    /// - "organization": Mirror operator information
    /// - "content_types": Types of content available on this mirror
    /// 
    /// This information helps clients make intelligent mirror selection decisions
    /// based on their specific requirements and constraints.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "custom")]
    public Dictionary<string, string>? Custom { get; init; }
}

/// <summary>
/// Represents the signed portion of TUF mirrors metadata (TAP 5).
/// Mirrors metadata provides a list of alternative repository locations for improved availability and performance.
/// </summary>
/// <remarks>
/// From TAP 5: "The purpose of the mirrors role is to provide a list of mirrors 
/// from which clients can download repository metadata and targets."
/// 
/// Mirrors metadata provides several benefits:
/// 1. Redundancy - if the primary repository is unavailable, clients can use mirrors
/// 2. Performance - clients can select geographically closer or faster mirrors
/// 3. Load distribution - distributes bandwidth load across multiple servers
/// 4. Censorship resistance - provides alternative access paths
/// 
/// Important security considerations:
/// - Mirror metadata is signed like other TUF metadata, ensuring authenticity
/// - Mirrors only affect WHERE files are downloaded, not WHAT files are trusted
/// - TUF's cryptographic verification still applies to all downloaded content
/// - Compromised mirrors cannot serve malicious content due to hash verification
/// 
/// The mirrors role is optional in TUF implementations. When present, it's typically
/// signed by dedicated mirror management keys separate from other repository roles.
/// </remarks>
[GenerateSerde]
public partial record Mirrors
{
    /// <summary>
    /// The metadata type identifier. Always "mirrors" for mirrors metadata.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "mirrors";
    
    /// <summary>
    /// The version of the TUF specification this metadata conforms to.
    /// Should follow semantic versioning (e.g., "1.0.0").
    /// </summary>
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    /// <summary>
    /// Version number of this mirrors metadata instance.
    /// Must be incremented when the mirror list changes.
    /// </summary>
    /// <remarks>
    /// Mirror versions increase when mirrors are added, removed, or their
    /// properties are updated. Clients can use this to detect when to
    /// refresh their mirror information.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    /// <summary>
    /// Expiration timestamp in ISO 8601 format (YYYY-MM-DDTHH:MM:SSZ).
    /// Clients must reject metadata after this time.
    /// </summary>
    /// <remarks>
    /// Mirrors metadata typically has moderate expiration times (e.g., 1 week to 1 month)
    /// since mirror lists change infrequently. The expiration ensures clients
    /// eventually discover new mirrors or remove defunct ones.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "expires", Proxy = typeof(CanonicalJson.Proxies.CanonicalDateTimeOffsetProxy))]
    public DateTimeOffset Expires { get; init; }
    
    /// <summary>
    /// List of available repository mirrors.
    /// Clients can use any combination of these mirrors for downloading content.
    /// </summary>
    /// <remarks>
    /// The mirror list provides clients with multiple options for accessing
    /// repository content. Clients may implement various selection strategies:
    /// 
    /// 1. Primary/fallback: Try mirrors in order until one works
    /// 2. Performance-based: Test mirrors and select the fastest
    /// 3. Geographic: Prefer mirrors in the same region
    /// 4. Load balancing: Distribute requests across multiple mirrors
    /// 5. Custom logic: Use mirror metadata for application-specific selection
    /// 
    /// All mirrors should provide the same content, but clients must still
    /// verify cryptographic hashes regardless of which mirror they use.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "mirrors")]
    public List<Mirror> MirrorList { get; init; } = new();
}