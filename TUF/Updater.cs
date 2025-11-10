using System.Runtime.InteropServices;

using TUF.Models;

namespace TUF;

/// <summary>
/// Configuration for TUF client operations including repository URLs, local directories, and security parameters.
/// </summary>
/// <remarks>
/// The UpdaterConfig class controls all aspects of TUF client behavior including:
/// - Security limits (root rotations, delegation depth, file sizes)
/// - Network configuration (repository URLs, HTTP client)
/// - Local storage (metadata and targets directories)
/// - Behavior options (consistent snapshots, caching)
/// 
/// All security limits have safe defaults based on TUF specification recommendations.
/// </remarks>
public class UpdaterConfig
{
    /// <summary>
    /// Maximum number of root metadata rotations allowed during a single update.
    /// Prevents infinite root key rotation attacks.
    /// </summary>
    /// <value>Default: 256</value>
    public uint MaxRootRotations { get; init; } = 256;
    /// <summary>
    /// Maximum depth of delegation tree traversal during target search.
    /// Prevents denial-of-service attacks via deep delegation chains.
    /// </summary>
    /// <value>Default: 32</value>
    public uint MaxDelegations { get; init; } = 32;
    /// <summary>
    /// Maximum size in bytes for root metadata files.
    /// Prevents resource exhaustion attacks via oversized metadata.
    /// </summary>
    /// <value>Default: 512,000 bytes (500KB)</value>
    public uint RootMaxLength { get; init; } = 512000;
    /// <summary>
    /// Maximum size in bytes for timestamp metadata files.
    /// Prevents resource exhaustion attacks via oversized metadata.
    /// </summary>
    /// <value>Default: 16,384 bytes (16KB)</value>
    public uint TimestampMaxLength { get; init; } = 16384;
    /// <summary>
    /// Maximum size in bytes for snapshot metadata files.
    /// Prevents resource exhaustion attacks via oversized metadata.
    /// </summary>
    /// <value>Default: 2,000,000 bytes (2MB)</value>
    public int SnapshotMaxLength { get; init; } = 2000000;
    /// <summary>
    /// Maximum size in bytes for targets metadata files.
    /// Prevents resource exhaustion attacks via oversized metadata.
    /// </summary>
    /// <value>Default: 5,000,000 bytes (5MB)</value>
    public int TargetsMaxLength { get; init; } = 5000000;

    /// <summary>
    /// Initial trusted root metadata bytes that establish the foundation of trust.
    /// This must be obtained through a secure, out-of-band mechanism.
    /// </summary>
    /// <remarks>
    /// The trusted root is the foundation of all TUF security. It must be distributed
    /// and updated through secure channels independent of the TUF repository itself.
    /// </remarks>
    public byte[] LocalTrustedRoot { get; init; }
    /// <summary>
    /// Local directory path where TUF metadata files are cached.
    /// Directory will be created if it doesn't exist.
    /// </summary>
    public required string LocalMetadataDir { get; init; }
    /// <summary>
    /// Local directory path where downloaded target files are stored.
    /// Directory will be created if it doesn't exist.
    /// </summary>
    public required string LocalTargetsDir { get; init; }
    /// <summary>
    /// Base URL for the remote TUF metadata repository.
    /// Should point to the directory containing root.json, timestamp.json, etc.
    /// </summary>
    public Uri RemoteMetadataUrl { get; init; }
    /// <summary>
    /// Base URL for the remote TUF targets repository.
    /// Should point to the directory containing the actual downloadable files.
    /// </summary>
    public Uri RemoteTargetsUrl { get; init; }
    /// <summary>
    /// When true and consistent snapshots are enabled, target files are prefixed with their hash.
    /// This improves CDN caching and enables atomic updates.
    /// </summary>
    /// <value>Default: true</value>
    public bool PrefixTargetsWithHash { get; init; } = true;
    /// <summary>
    /// When true, disables local file caching for both metadata and targets.
    /// Useful for testing or when persistent storage is not desired.
    /// </summary>
    /// <value>Default: false</value>
    public bool DisableLocalCache { get; init; } = false;
    /// <summary>
    /// HTTP client instance used for all network operations.
    /// Should be configured with appropriate timeouts and retry policies.
    /// </summary>
    public required HttpClient Client { get; init; }

    /// <summary>
    /// Creates a new UpdaterConfig with the minimum required parameters.
    /// </summary>
    /// <param name="initialRootBytes">Initial trusted root metadata bytes</param>
    /// <param name="remoteMetadataUrl">Base URL for metadata repository</param>
    /// <remarks>
    /// This constructor automatically sets RemoteTargetsUrl to {remoteMetadataUrl}/targets.
    /// You'll still need to set LocalMetadataDir, LocalTargetsDir, and Client properties.
    /// </remarks>
    public UpdaterConfig(byte[] initialRootBytes, Uri remoteMetadataUrl)
    {
        LocalTrustedRoot = initialRootBytes ?? throw new ArgumentNullException(nameof(initialRootBytes));
        RemoteMetadataUrl = remoteMetadataUrl ?? throw new ArgumentNullException(nameof(remoteMetadataUrl));
        RemoteTargetsUrl = new Uri(remoteMetadataUrl, "targets");
    }
}

/// <summary>
/// The primary TUF client for performing secure software updates.
/// Implements the complete TUF client workflow including metadata refresh, target discovery, and secure download verification.
/// </summary>
/// <remarks>
/// The Updater class is the main entry point for TUF client operations. It provides:
/// - Secure metadata refresh following TUF specification security rules
/// - Target file discovery through delegation tree traversal
/// - Cryptographically verified file downloads
/// - Local caching for improved performance
/// 
/// Usage pattern:
/// 1. Create UpdaterConfig with repository URLs and trusted root
/// 2. Instantiate Updater with config
/// 3. Call RefreshAsync() to update metadata
/// 4. Use GetTargetInfo() to find files
/// 5. Use DownloadTarget() to securely download files
/// </remarks>
public class Updater
{
    private TrustedMetadata _trusted;
    private readonly UpdaterConfig _config;

    /// <summary>
    /// Creates a new TUF updater client with the specified configuration.
    /// </summary>
    /// <param name="config">Configuration containing repository URLs, directories, and security parameters</param>
    /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
    /// <exception cref="TUF.Exceptions.TufConfigurationException">Thrown when configuration validation fails</exception>
    public Updater(UpdaterConfig config)
    {
        _config = config;
        _trusted = TrustedMetadata.CreateFromRootData(config.LocalTrustedRoot);
    }

    /// <summary>
    /// Performs a complete TUF metadata refresh workflow, updating all metadata from the remote repository.
    /// </summary>
    /// <returns>A task that completes when metadata refresh is successful</returns>
    /// <remarks>
    /// This method implements the TUF specification's secure metadata update workflow:
    /// 1. Updates root metadata (following root rotation rules)
    /// 2. Updates timestamp metadata (for freshness)
    /// 3. Updates snapshot metadata (for consistency)
    /// 4. Updates targets metadata (for file catalog)
    /// 
    /// All metadata updates include:
    /// - Signature verification against trusted keys
    /// - Version checking to prevent rollback attacks
    /// - Expiration time validation
    /// - File size limits to prevent resource exhaustion
    /// </remarks>
    /// <exception cref="TUF.Exceptions.RepositoryNetworkException">Network or HTTP transport errors</exception>
    /// <exception cref="TUF.Exceptions.SignatureVerificationException">Signature verification failures</exception>
    /// <exception cref="TUF.Exceptions.RollbackAttackException">Rollback attack detection</exception>
    /// <exception cref="TUF.Exceptions.ExpiredMetadataException">Expired metadata detected</exception>
    /// <exception cref="TUF.Exceptions.InsufficientSignaturesException">Insufficient signatures for role threshold</exception>
    public async Task RefreshAsync()
    {
        await OnlineRefresh();
    }

    /// <summary>
    /// Returns all top-level targets (non-delegated) from the current trusted targets metadata.
    /// </summary>
    /// <returns>Dictionary mapping target paths to target file information</returns>
    /// <exception cref="InvalidOperationException">Thrown if trusted metadata is not fully loaded (call RefreshAsync() first)</exception>
    /// <remarks>
    /// This method only returns targets directly listed in the main targets.json file,
    /// not targets that are managed by delegated roles. For comprehensive target search
    /// including delegations, use GetTargetInfo() instead.
    /// </remarks>
    public Dictionary<string, TargetFile> GetTopLevelTargets()
    {
        if (_trusted is not CompleteTrustedMetadata c)
        {
            throw new InvalidOperationException("Trusted metadata not loaded");
        }
        return c.TopLevelTargets.Signed.TargetMap;
    }

    /// <summary>
    /// Returns the current trusted metadata set for inspection or advanced operations.
    /// </summary>
    /// <returns>The current trusted metadata state</returns>
    /// <remarks>
    /// Use cases include:
    /// - Custom delegation tree traversal
    /// - Metadata inspection and debugging
    /// - Advanced security policy implementation
    /// - Integration with external validation tools
    /// </remarks>
    public TrustedMetadata GetTrustedMetadataSet() => _trusted;

    /// <summary>
    /// Finds target information through the complete delegation tree, following TUF delegation rules.
    /// </summary>
    /// <param name="targetPath">The target file path to search for</param>
    /// <returns>Target information (remote path and metadata) if found, null if not found</returns>
    /// <remarks>
    /// This method:
    /// - Automatically calls RefreshAsync() if metadata is not loaded
    /// - Performs depth-first delegation tree traversal
    /// - Respects delegation termination rules
    /// - Handles path pattern matching and hash bin delegations
    /// - Enforces maximum delegation depth for security
    /// </remarks>
    /// <exception cref="TUF.Exceptions.MaxDelegationDepthExceededException">Maximum delegation depth exceeded</exception>
    /// <exception cref="TUF.Exceptions.TufException">Other TUF-related errors during metadata loading</exception>
    public async Task<(string RemotePath, TargetFile File)?> GetTargetInfo(string targetPath)
    {
        if (_trusted is not CompleteTrustedMetadata c)
        {
            await RefreshAsync();
        }

        return await PreOrderDepthFirstWalk(targetPath);
    }

    /// <summary>
    /// Downloads and verifies a target file with full TUF security validation.
    /// </summary>
    /// <param name="targetFile">Target file metadata (typically from GetTargetInfo())</param>
    /// <param name="targetPath">Target path in repository</param>
    /// <param name="destinationFilePath">Optional local destination path (defaults to LocalTargetsDir + targetPath)</param>
    /// <param name="baseUrl">Optional base URL override (defaults to RemoteTargetsUrl)</param>
    /// <returns>A tuple containing the local file path and downloaded data</returns>
    /// <remarks>
    /// Security features:
    /// - Verifies file length matches metadata exactly
    /// - Validates all cryptographic hashes in target metadata
    /// - Supports consistent snapshot URL transformation
    /// - Prevents download and storage of tampered files
    /// - Atomic file operations to prevent partial writes
    /// </remarks>
    /// <exception cref="TUF.Exceptions.RepositoryNetworkException">Download network errors</exception>
    /// <exception cref="TUF.Exceptions.TargetIntegrityException">Hash or length verification failures</exception>
    /// <exception cref="TUF.Exceptions.TargetNotFoundException">Target file not found on server</exception>
    public async Task<(string FilePath, byte[] Data)> DownloadTarget(TargetFile targetFile, string targetPath, string? destinationFilePath = null, Uri? baseUrl = null)
    {
        var localDestinationPath = destinationFilePath ?? Path.Combine(_config.LocalTargetsDir, targetPath);
        var targetBaseUrl = baseUrl ?? _config.RemoteTargetsUrl;
        var targetRemotePath = targetPath;

        if (_trusted.Root.Signed.ConsistentSnapshot == true && _config.PrefixTargetsWithHash)
        {
            var hash = targetFile.Hashes.FirstOrDefault().Value;
            var targetFileDir = Path.GetDirectoryName(targetPath);
            var targetFileName = Path.GetFileName(targetPath)!;
            if (targetFileDir is not null)
            {
                targetRemotePath = Path.Combine(targetFileDir, $"{hash}.{targetFileName}");
            }
            else
            {
                targetRemotePath = $"{hash}.{targetFileName}";
            }
        }

        var downloadUri = new Uri(targetBaseUrl, targetRemotePath);
        var data = await DownloadFile(downloadUri, (uint)targetFile.Length);
        VerifyTargetFile(targetFile, data);

        if (!_config.DisableLocalCache)
        {
            await File.WriteAllBytesAsync(localDestinationPath, data);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(localDestinationPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }
        }
        return (localDestinationPath, data);
    }

    /// <summary>
    /// Attempts to find and verify a cached copy of the target file locally.
    /// </summary>
    /// <param name="targetFile">Target file metadata for verification</param>
    /// <param name="targetPath">Target path in repository</param>
    /// <param name="filePath">Optional specific file path to check (defaults to LocalTargetsDir + targetPath)</param>
    /// <returns>Cached file information if found and verified, null otherwise</returns>
    /// <remarks>
    /// This method performs the same security verification as DownloadTarget() on cached files.
    /// If verification fails, the cached file is considered invalid and null is returned.
    /// Caching can be disabled via DisableLocalCache configuration option.
    /// </remarks>
    public async Task<(string FilePath, byte[] Data)?> FindCachedTarget(TargetFile targetFile, string targetPath, string? filePath)
    {
        if (_config.DisableLocalCache)
        {
            return null;
        }

        var localPath = filePath ?? Path.Combine(_config.LocalTargetsDir, targetPath);
        if (!File.Exists(localPath))
        {
            return null;
        }

        var fileBytes = await File.ReadAllBytesAsync(localPath);
        try
        {
            VerifyTargetFile(targetFile, fileBytes);
        }
        catch
        {
            return null;
        }

        return (localPath, fileBytes);
    }

    private void VerifyTargetFile(TargetFile targetFile, byte[] data)
    {
        // Verify length
        if (data.Length != targetFile.Length)
        {
            throw new Exception($"Target file length mismatch. Expected: {targetFile.Length}, Actual: {data.Length}");
        }

        // Verify at least one hash
        bool validHash = false;
        foreach (var hash in targetFile.Hashes)
        {
            var computedHash = hash.Key.ToLowerInvariant() switch
            {
                "sha256" => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant(),
                "sha512" => Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(data)).ToLowerInvariant(),
                _ => null
            };

            if (computedHash != null && computedHash == hash.Value.ToLowerInvariant())
            {
                validHash = true;
                break;
            }
        }

        if (!validHash)
        {
            throw new Exception("No valid hash found for target file");
        }
    }

    private async Task<(string Path, TargetFile File)?> PreOrderDepthFirstWalk(string targetFilePath)
    {
        if (_trusted is not CompleteTrustedMetadata complete)
        {
            throw new Exception("Complete trusted metadata not loaded");
        }

        Stack<(string Role, string Parent)> toVisit = new();
        toVisit.Push(("targets", "root"));
        HashSet<string> visited = new();
        while (toVisit.Count < _config.MaxDelegations && toVisit.Count > 0)
        {
            (var role, var parent) = toVisit.Pop();
            if (visited.Contains(role))
            {
                continue;
            }
            await LoadTargets(role, parent);
            CompleteTrustedMetadata rtts = _trusted as CompleteTrustedMetadata ?? throw new Exception("trusted metadata not loaded");
            var targets = rtts.Targets[role];
            if (targets is not null && targets.Signed.TargetMap.TryGetValue(targetFilePath, out var targetMeta))
            {
                // if we have the file, return it
                return (targetFilePath, targetMeta);
            }
            // if we didn't find the file in this role
            visited.Add(role); // add the visited role
            // add any child delegations to the list to traverse
            if (targets is not null && targets.Signed.Delegations is { } delegations)
            {
                var roles = delegations.GetRolesForTarget(targetFilePath);
                foreach (var r in roles)
                {
                    toVisit.Push((r.Name, role));
                    if (r.Terminating)
                    {
                        break;
                    }
                }
            }
        }
        if (toVisit.Count > 0)
        {
            // we terminated due to length
            throw new Exception("Maximum delegation depth reached");
        }
        // otherwise didn't find target
        return null;
    }

    private async Task OnlineRefresh()
    {
        await LoadRoot();
        await LoadTimestamp();
        await LoadSnapshot();
        await LoadTargets("targets", "root");
    }

    private async Task LoadRoot()
    {
        var lowerBound = _trusted.Root.Signed.Version + 1;
        var upperBound = lowerBound + _config.MaxRootRotations;
        byte[] data = null!;
        foreach (var v in (int)lowerBound..(int)upperBound)
        {
            try
            {
                data = await DownloadMetadata("root", _config.RootMaxLength, v);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                break;
            }
        }
        if (data is not null)
        {
            _trusted = _trusted.UpdateRoot(data);
            await PersistMetadata("root", data);
        }
    }

    private async Task LoadTimestamp()
    {
        var data = await DownloadMetadata("timestamp", _config.TimestampMaxLength, null);
        _trusted = _trusted.UpdateTimestamp(data);
        await PersistMetadata("timestamp", data);
    }

    private async Task LoadSnapshot()
    {
        if (_trusted is TrustedMetadataWithTimestamp rts)
        {
            var snapshotMeta = rts.Timestamp.Signed.Meta["snapshot.json"];
            var length = (uint)(snapshotMeta.Length ?? _config.SnapshotMaxLength);
            int? version = rts.Root.Signed.ConsistentSnapshot is true ? (int)snapshotMeta.Version : null;
            var data = await DownloadMetadata("snapshot", length, version);
            _trusted = _trusted.UpdateSnapshot(data, isTrusted: false);
            await PersistMetadata("snapshot", data);
        }
        else
        {
            throw new Exception("trusted timestamp not loaded");
        }
    }

    private async Task LoadTargets(string roleName, string parentName)
    {
        if (_trusted is TrustedMetadataWithSnapshot rts)
        {
            if (!rts.Snapshot.Signed.Meta.TryGetValue($"{roleName}.json", out var roleFileMetadata))
            {
                throw new Exception($"Role file metadata for {roleName} not found");
            }

            var length = roleFileMetadata.Length ?? _config.TargetsMaxLength;
            int? version = rts.Root.Signed.ConsistentSnapshot is true ? (int)roleFileMetadata.Version : null;
            var data = await DownloadMetadata(roleName, (uint)length, version);
            _trusted = rts.UpdateDelegatedTargets(data, roleName, parentName);
            await PersistMetadata(roleName, data);
        }
        else
        {
            throw new Exception("trusted snapshot not loaded");
        }
    }

    private async Task PersistMetadata(string roleName, byte[] data)
    {
        if (_config.DisableLocalCache) return;
        var finalFile = Path.Combine(_config.LocalMetadataDir, roleName + ".json");
        var tempFile = Path.Combine(_config.LocalMetadataDir, "tuf_tmp");
        try
        {
            File.Create(tempFile).Close();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(tempFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }
        }
        catch
        {
            File.Delete(tempFile);
        }
        try
        {
            await File.WriteAllBytesAsync(tempFile, data);
        }
        catch
        {
            File.Delete(tempFile);
        }
        File.Move(tempFile, finalFile, true);
        if (!(await File.ReadAllBytesAsync(finalFile)).SequenceEqual(data))
        {
            throw new Exception("File contents do not match");
        }
    }

    private async Task<byte[]> DownloadMetadata(string type, uint maxlength, int? version)
    {
        var uri = new Uri(_config.RemoteMetadataUrl, version is int v && v != 0 ? $"{type}.{version}.json" : $"{type}.json");
        return await DownloadFile(uri, maxlength);
    }

    private async Task<byte[]> DownloadFile(Uri uri, uint? maxLength)
    {
        var headers = await _config.Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        var length = headers.Content.Headers.ContentLength ?? 0;
        if (length > maxLength)
        {
            var filePath = uri.ToString().TrimStart(_config.RemoteTargetsUrl.ToString());
            throw new Exception($"File {filePath} length {length} exceeds maximum allowed length {maxLength}");
        }
        return await headers.Content.ReadAsByteArrayAsync();
    }
}

public static class RangeExtensions
{
    public static IEnumerator<int> GetEnumerator(this Range range)
    {
        int start = range.Start.IsFromEnd ? -range.Start.Value : range.Start.Value;
        int end = range.End.IsFromEnd ? -range.End.Value : range.End.Value;

        for (int i = start; i <= end; i++)
        {
            yield return i;
        }
    }
}