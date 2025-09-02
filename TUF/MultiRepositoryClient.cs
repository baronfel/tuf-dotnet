using System.Diagnostics.CodeAnalysis;

using Serde.Json;

using TUF.Models.Roles.Targets;
using TUF.MultiRepository;

namespace TUF;

/// <summary>
/// Multi-repository TUF client implementing TAP 4 (Multiple repository consensus on entrusted targets).
/// Enables secure target retrieval from multiple independent TUF repositories with consensus validation.
/// </summary>
public class MultiRepositoryClient
{
    private readonly MultiRepositoryConfig _config;
    private readonly Dictionary<string, Updater> _repositoryClients;
    private MultiRepositoryMap? _map;

    public MultiRepositoryClient(MultiRepositoryConfig config)
    {
        _config = config;
        _repositoryClients = new Dictionary<string, Updater>();
    }

    /// <summary>
    /// Initializes the multi-repository client by loading the map.json configuration
    /// and setting up individual TUF clients for each repository.
    /// </summary>
    [RequiresUnreferencedCode("JSON deserialization may require types that cannot be statically analyzed")]
    [RequiresDynamicCode("JSON deserialization may require runtime code generation")]
    public async Task InitializeAsync()
    {
        // Load the map.json file
        var mapJson = await File.ReadAllTextAsync(_config.MapFilePath);
        _map = JsonSerializer.Deserialize<MultiRepositoryMap>(mapJson)
            ?? throw new InvalidOperationException("Failed to parse map.json file");

        // Ensure metadata and targets directories exist
        Directory.CreateDirectory(_config.MetadataDir);
        Directory.CreateDirectory(_config.TargetsDir);

        // Initialize TUF clients for each repository
        foreach (var (repoName, repoInfo) in _map.Repositories)
        {
            var repoMetadataDir = Path.Combine(_config.MetadataDir, repoName);
            var repoTargetsDir = Path.Combine(_config.TargetsDir, repoName);

            Directory.CreateDirectory(repoMetadataDir);
            Directory.CreateDirectory(repoTargetsDir);

            // Load the trusted root for this repository
            var trustedRootPath = repoInfo.TrustedRootPath;
            if (!Path.IsPathRooted(trustedRootPath))
            {
                // Make relative paths relative to the map.json file location
                var mapDir = Path.GetDirectoryName(_config.MapFilePath) ?? ".";
                trustedRootPath = Path.Combine(mapDir, trustedRootPath);
            }

            var trustedRootBytes = await File.ReadAllBytesAsync(trustedRootPath);
            var config = new UpdaterConfig(trustedRootBytes, new Uri(repoInfo.MetadataUrl))
            {
                LocalMetadataDir = repoMetadataDir,
                LocalTargetsDir = repoTargetsDir,
                RemoteTargetsUrl = new Uri(repoInfo.TargetsUrl),
                Client = _config.HttpClient
            };

            var updater = new Updater(config);
            _repositoryClients[repoName] = updater;
        }
    }

    /// <summary>
    /// Refreshes metadata for all configured repositories.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (_map == null)
        {
            throw new InvalidOperationException("Client not initialized. Call InitializeAsync() first.");
        }

        var refreshTasks = _repositoryClients.Values.Select(client => client.Refresh());
        await Task.WhenAll(refreshTasks);
    }

    /// <summary>
    /// Finds target information across multiple repositories using TAP 4 consensus mechanism.
    /// </summary>
    /// <param name="targetPath">Path of the target file to search for</param>
    /// <returns>Result containing target information and consensus details</returns>
    public async Task<MultiRepositoryTargetResult> GetTargetInfoAsync(string targetPath)
    {
        if (_map == null)
        {
            throw new InvalidOperationException("Client not initialized. Call InitializeAsync() first.");
        }

        // Implement TAP 4 search algorithm
        foreach (var mapping in _map.Mapping)
        {
            // Check if this mapping applies to the target path
            if (mapping.Paths.Any(pattern => pattern.IsMatch(targetPath)))
            {
                var result = await CheckRepositoriesForTarget(targetPath, mapping);

                // If terminating mapping or target found, return result
                if (mapping.Terminating || result.IsValid)
                {
                    return result;
                }
            }
        }

        // Target not found in any matching mapping
        return new MultiRepositoryTargetResult(
            targetPath,
            null,
            0,
            0,
            Array.Empty<string>()
        );
    }

    /// <summary>
    /// Downloads a target file using multi-repository consensus validation.
    /// </summary>
    /// <param name="targetPath">Path of the target file to download</param>
    /// <param name="destinationPath">Local path where the file should be saved</param>
    public async Task<bool> DownloadTargetAsync(string targetPath, string destinationPath)
    {
        var targetResult = await GetTargetInfoAsync(targetPath);

        if (!targetResult.IsValid || targetResult.TargetInfo == null)
        {
            return false;
        }

        // Try to download from each repository that has this target
        var targetInfo = targetResult.TargetInfo;
        foreach (var repoName in targetResult.RepositoriesChecked)
        {
            try
            {
                var client = _repositoryClients[repoName];
                await client.DownloadTarget(targetInfo, destinationPath, null);
                return true;
            }
            catch
            {
                // Continue to next repository if download fails
                continue;
            }
        }

        return false;
    }

    private async Task<MultiRepositoryTargetResult> CheckRepositoriesForTarget(
        string targetPath,
        Mapping mapping)
    {
        var targetCandidates = new Dictionary<string, TargetMetadata>();
        var checkedRepositories = new List<string>();

        // Check each repository in this mapping
        foreach (var repoName in mapping.Repositories)
        {
            if (!_repositoryClients.TryGetValue(repoName, out var client))
            {
                continue;
            }

            checkedRepositories.Add(repoName);

            try
            {
                var targetInfo = await client.GetTargetInfo(targetPath);
                if (targetInfo != null)
                {
                    // Use a key based on metadata that should be identical across repositories
                    var candidateKey = $"{targetInfo.Length}:{string.Join(",", targetInfo.Hashes.Select(h => h.ToString()))}";
                    targetCandidates[candidateKey] = targetInfo;
                }
            }
            catch
            {
                // Repository doesn't have the target or failed to retrieve it
                continue;
            }
        }

        // Find the target metadata that appears in the most repositories
        var bestCandidate = targetCandidates
            .GroupBy(kvp => kvp.Key)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        var agreementCount = bestCandidate?.Count() ?? 0;
        var targetMetadata = agreementCount > 0 ? bestCandidate!.First().Value : null;

        return new MultiRepositoryTargetResult(
            targetPath,
            targetMetadata,
            agreementCount,
            mapping.Threshold,
            checkedRepositories.ToArray()
        );
    }
}