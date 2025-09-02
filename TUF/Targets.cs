using Microsoft.Extensions.FileSystemGlobbing;

using Serde;

namespace TUF.Models;

/// <summary>
/// Represents a target file with its metadata including size and cryptographic hashes.
/// Target files are the actual content that clients want to download securely.
/// </summary>
/// <remarks>
/// From TUF specification: "A target is a file that clients want to download from 
/// the repository. Target files are opaque to TUF."
/// 
/// Each target file includes cryptographic metadata that allows clients to:
/// 1. Verify file integrity using cryptographic hashes
/// 2. Detect truncation attacks using the length field
/// 3. Store custom metadata for application-specific purposes
/// </remarks>
[GenerateSerde]
public partial record TargetFile
{
    /// <summary>
    /// Length of the target file in bytes.
    /// Clients MUST verify downloaded files match this length exactly.
    /// </summary>
    /// <remarks>
    /// The length field prevents truncation attacks where an attacker serves
    /// a partial file that still has valid hashes for the truncated portion.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "length")]
    public int Length { get; init; }
    
    /// <summary>
    /// Dictionary of cryptographic hashes for integrity verification.
    /// Key is the hash algorithm name (e.g., "sha256"), value is the hex-encoded hash.
    /// </summary>
    /// <remarks>
    /// Clients MUST verify at least one hash algorithm they trust. Common algorithms:
    /// - "sha256": SHA-256 (most common, good security/performance balance)
    /// - "sha512": SHA-512 (higher security, more computation)
    /// - "blake2b-256": BLAKE2b (modern alternative to SHA-256)
    /// 
    /// Multiple hash algorithms provide crypto-agility and defense against
    /// hash function vulnerabilities.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "hashes")]
    public Dictionary<string, string> Hashes { get; init; } = new();
    
    /// <summary>
    /// Optional custom metadata for application-specific use.
    /// TUF specification allows arbitrary key-value pairs here.
    /// </summary>
    /// <remarks>
    /// Applications can use this field to store additional metadata like:
    /// - File permissions or ownership information
    /// - Content-type or MIME type
    /// - Application-specific version information
    /// - Signature metadata for executable files
    /// 
    /// Custom metadata is protected by TUF signatures but is opaque to TUF itself.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "custom")]
    public Dictionary<string, string>? Custom { get; init; }
}

/// <summary>
/// Represents a delegated role definition within targets metadata.
/// Delegations allow the targets role to distribute signing authority to other roles.
/// </summary>
/// <remarks>
/// From TUF specification section 5.6: "The targets role can delegate trust to other 
/// roles to sign for specific target files. This delegation is done through the 
/// delegations field in targets metadata."
/// 
/// Delegation provides several benefits:
/// 1. Scalability - different teams can manage different parts of the repository
/// 2. Security isolation - compromise of one delegated role doesn't affect others
/// 3. Flexible organization - roles can be organized by file path, team, or function
/// </remarks>
[GenerateSerde]
public partial record DelegatedRole
{
    /// <summary>
    /// Name of the delegated role. Must be unique within the delegation scope.
    /// </summary>
    /// <remarks>
    /// Role names are used to identify the metadata file for this role
    /// (typically "{name}.json"). Names should be descriptive and follow
    /// your organization's naming conventions.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "name")]
    public string Name { get; init; } = "";

    /// <summary>
    /// List of key identifiers authorized to sign for this delegated role.
    /// Keys must be defined in the delegation's key dictionary.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "keyids")]
    public List<string> KeyIds { get; init; } = new();

    /// <summary>
    /// Minimum number of signatures required from the authorized keys.
    /// Provides multi-signature security for delegated roles.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "threshold")]
    public int Threshold { get; init; } = 1;

    /// <summary>
    /// Optional list of path patterns this role is responsible for.
    /// Uses glob patterns to specify which target files this role can sign for.
    /// </summary>
    /// <remarks>
    /// Path patterns support shell-style wildcards:
    /// - "*" matches any characters except path separators
    /// - "?" matches any single character except path separators  
    /// - "**" matches any characters including path separators
    /// 
    /// Examples: "bin/*", "lib/**/*.so", "config/app.conf"
    /// 
    /// Either 'paths' or 'path_hash_prefixes' must be specified, but not both.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "paths")]
    public List<string>? Paths { get; init; }

    /// <summary>
    /// Optional list of path hash prefixes this role is responsible for.
    /// Alternative to 'paths' that uses hash-based path distribution.
    /// </summary>
    /// <remarks>
    /// Path hash prefixes provide deterministic load balancing across delegated roles.
    /// The path is hashed, and the role handles files whose hash starts with the prefix.
    /// 
    /// This method is useful when you want to distribute files evenly across roles
    /// without manual path assignment. Prefixes are typically 2-4 hex characters.
    /// 
    /// Either 'paths' or 'path_hash_prefixes' must be specified, but not both.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "path_hash_prefixes")]
    public List<string>? PathHashPrefixes { get; init; }

    /// <summary>
    /// Whether this role terminates the delegation search.
    /// If true, no further delegated roles are consulted for paths this role handles.
    /// </summary>
    /// <remarks>
    /// When false (default), if this role cannot provide metadata for a target,
    /// TUF continues searching other delegated roles. When true, the search stops
    /// at this role regardless of whether it can provide the metadata.
    /// 
    /// Terminating roles provide stronger security guarantees but less flexibility.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "terminating")]
    public bool Terminating { get; init; } = false;

    public bool IsDelegatedPath(string targetFile)
    {
        if (Paths is null || Paths.Count == 0)
        {
            return false;
        }
        return Paths.Any(p => PathIsMatch(p, targetFile));
    }

    public static bool PathIsMatch(string path, string targetFile)
    {
        var matcher = new Matcher(StringComparison.Ordinal).AddInclude(path);
        return matcher.Match(targetFile).HasMatches;
    }
}

/// <summary>
/// Represents the delegation configuration within targets metadata.
/// Contains the keys and role definitions for all delegated roles.
/// </summary>
/// <remarks>
/// The delegations section allows targets metadata to delegate signing authority
/// for specific files or path patterns to other roles. This enables distributed
/// management while maintaining security through cryptographic verification.
/// </remarks>
[GenerateSerde]
public partial record Delegations
{
    /// <summary>
    /// Dictionary of keys available to delegated roles.
    /// Key is the key ID, value is the complete key specification.
    /// </summary>
    /// <remarks>
    /// These keys are separate from the main repository keys and are used
    /// exclusively for delegated roles. Keys can be shared across multiple
    /// delegated roles within the same delegation scope.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "keys")]
    public Dictionary<string, Key> Keys { get; init; } = new();

    /// <summary>
    /// List of delegated role definitions in priority order.
    /// Earlier roles in the list are consulted before later roles.
    /// </summary>
    /// <remarks>
    /// Role order matters for path resolution. When searching for a target file,
    /// TUF consults roles in the order they appear in this list until:
    /// 1. A role provides valid metadata for the target, OR
    /// 2. A terminating role is reached (whether it has the target or not)
    /// 
    /// Careful ordering ensures predictable behavior and optimal performance.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "roles")]

    public List<DelegatedRole> Roles { get; init; } = new();
    
    public List<(string Name, bool Terminating)> GetRolesForTarget(string targetFile)
    {
        if (Roles is null)
        {
            return [];
        }
        var roles = new List<(string Name, bool Terminating)>(Roles.Count);
        foreach (var role in Roles)
        {
            if (role.IsDelegatedPath(targetFile))
            {
                roles.Add((role.Name, role.Terminating));
            }
            if (role.Terminating)
            {
                break;
            }
        }
        return roles;
    }
}

/// <summary>
/// Represents the signed portion of TUF targets metadata.
/// Targets metadata lists available target files and their cryptographic metadata.
/// </summary>
/// <remarks>
/// From TUF specification section 5.4: "The targets role signs a metadata file 
/// that lists hashes and sizes of target files."
/// 
/// Targets metadata serves as the authoritative catalog of available files:
/// 1. Lists all target files with their cryptographic hashes and sizes
/// 2. Enables integrity verification of downloaded content
/// 3. Supports delegation to distribute signing responsibilities
/// 4. Can include custom metadata for application-specific needs
/// 
/// Targets metadata can delegate signing authority through the delegations field,
/// allowing for scalable and distributed repository management while maintaining
/// cryptographic security guarantees.
/// </remarks>
[GenerateSerde]
public partial record Targets
{
    /// <summary>
    /// The metadata type identifier. Always "targets" for targets metadata.
    /// </summary>
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "targets";
    
    /// <summary>
    /// The version of the TUF specification this metadata conforms to.
    /// Should follow semantic versioning (e.g., "1.0.0").
    /// </summary>
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    /// <summary>
    /// Version number of this targets metadata instance.
    /// Must be incremented for each new version.
    /// </summary>
    /// <remarks>
    /// Targets versions increase when target files are added, removed, or updated.
    /// The snapshot metadata references this version to ensure consistency.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    /// <summary>
    /// Expiration timestamp in ISO 8601 format (YYYY-MM-DDTHH:MM:SSZ).
    /// Clients must reject metadata after this time.
    /// </summary>
    /// <remarks>
    /// Targets metadata typically has longer expiration times (e.g., 1 month to 1 year)
    /// since target file lists change less frequently than snapshot/timestamp metadata.
    /// The expiration time balances security against operational burden.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "expires")]
    public DateTimeOffset Expires { get; init; }
    
    /// <summary>
    /// Dictionary of target files managed by this metadata.
    /// Key is the target file path, value contains cryptographic metadata.
    /// </summary>
    /// <remarks>
    /// Target file paths should use forward slashes as path separators and
    /// be relative paths (no leading slash). This ensures cross-platform
    /// compatibility and prevents directory traversal attacks.
    /// 
    /// Each target file includes length and hash information that clients
    /// MUST verify after downloading to ensure integrity and authenticity.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "targets")]
    public Dictionary<string, TargetFile> TargetMap { get; init; } = new();
    
    /// <summary>
    /// Optional delegation configuration for distributing signing authority.
    /// If present, allows this targets role to delegate specific paths to other roles.
    /// </summary>
    /// <remarks>
    /// Delegations enable scalable repository management by allowing different
    /// teams or systems to manage different parts of the target file tree.
    /// Each delegation specifies which keys can sign for which paths/patterns.
    /// 
    /// When resolving a target file, clients first check this metadata's targets,
    /// then consult delegated roles in the order specified in the delegations.
    /// </remarks>
    [property: SerdeMemberOptions(Rename = "delegations")]
    public Delegations? Delegations { get; init; }
}