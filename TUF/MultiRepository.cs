using System.Text.Json.Serialization;

using TUF.Models.Primitives;
using TUF.Serialization;

namespace TUF.MultiRepository;

/// <summary>
/// Represents a mapping from target paths to repositories as defined in TAP 4.
/// </summary>
/// <param name="Paths">Array of path patterns that this mapping applies to</param>
/// <param name="Repositories">List of repository names to search for matching targets</param>
/// <param name="Threshold">Minimum number of repositories that must agree on a target's metadata</param>
/// <param name="Terminating">If true, stops searching subsequent mappings after this one matches</param>
public record Mapping(
    [property: JsonPropertyName("paths")]
    PathPattern[] Paths,
    [property: JsonPropertyName("repositories")]
    string[] Repositories,
    [property: JsonPropertyName("threshold")]
    int Threshold,
    [property: JsonPropertyName("terminating")]
    bool Terminating = false
);

/// <summary>
/// Represents repository configuration information for multi-repository setup.
/// </summary>
/// <param name="Name">Unique identifier for this repository</param>
/// <param name="MetadataUrl">Base URL for TUF metadata</param>
/// <param name="TargetsUrl">Base URL for target files (may be same as metadata URL)</param>
/// <param name="TrustedRootPath">Path to the trusted root.json for this repository</param>
public record RepositoryInfo(
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("metadata_url")]
    string MetadataUrl,
    [property: JsonPropertyName("targets_url")]
    string TargetsUrl,
    [property: JsonPropertyName("trusted_root")]
    string TrustedRootPath
);

/// <summary>
/// Represents the map.json file structure as defined in TAP 4.
/// Contains repository definitions and mapping rules for multi-repository consensus.
/// </summary>
/// <param name="Repositories">Dictionary of repository configurations</param>
/// <param name="Mapping">Ordered list of mapping rules for target resolution</param>
public record MultiRepositoryMap(
    [property: JsonPropertyName("repositories")]
    Dictionary<string, RepositoryInfo> Repositories,
    [property: JsonPropertyName("mapping")]
    Mapping[] Mapping
);

/// <summary>
/// Configuration for multi-repository TUF client operations.
/// </summary>
public record MultiRepositoryConfig
{
    /// <summary>
    /// Path to the map.json file containing repository and mapping configuration
    /// </summary>
    public required string MapFilePath { get; init; }

    /// <summary>
    /// Directory to store metadata for each repository
    /// </summary>
    public required string MetadataDir { get; init; }

    /// <summary>
    /// Directory to store cached target files
    /// </summary>
    public required string TargetsDir { get; init; }

    /// <summary>
    /// HTTP client to use for all repository communications
    /// </summary>
    public HttpClient HttpClient { get; init; } = new HttpClient();
}

/// <summary>
/// Result of a multi-repository target search operation.
/// </summary>
/// <param name="TargetPath">The requested target file path</param>
/// <param name="TargetInfo">Target metadata if found and validated</param>
/// <param name="AgreementCount">Number of repositories that agreed on this target's metadata</param>
/// <param name="RequiredThreshold">Minimum number of repositories required for consensus</param>
/// <param name="RepositoriesChecked">List of repository names that were consulted</param>
public record MultiRepositoryTargetResult(
    string TargetPath,
    Models.Roles.Targets.TargetMetadata? TargetInfo,
    int AgreementCount,
    int RequiredThreshold,
    string[] RepositoriesChecked
)
{
    /// <summary>
    /// Indicates whether the target was found and meets the consensus threshold
    /// </summary>
    public bool IsValid => TargetInfo != null && AgreementCount >= RequiredThreshold;
}