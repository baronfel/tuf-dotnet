using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using TUF.Caching;
using TUF.Exceptions;
using TUF.Models;
using TUF.Resilience;

namespace TUF;

public class UpdaterConfig
{
    public uint MaxRootRotations { get; init; } = 256;
    public uint MaxDelegations { get; init; } = 32;
    public uint RootMaxLength { get; init; } = 512000;
    public uint TimestampMaxLength { get; init; } = 16384;
    public int SnapshotMaxLength { get; init; } = 2000000;
    public int TargetsMaxLength { get; init; } = 5000000;

    public byte[] LocalTrustedRoot { get; init; }
    public required string LocalMetadataDir { get; init; }
    public required string LocalTargetsDir { get; init; }
    public Uri RemoteMetadataUrl { get; init; }
    public Uri RemoteTargetsUrl { get; init; }
    public bool PrefixTargetsWithHash { get; init; } = true;
    public bool DisableLocalCache { get; init; } = false;
    public required HttpClient Client { get; init; }

    /// <summary>
    /// Cache implementation for metadata and targets (default: InMemoryTufCache)
    /// </summary>
    public ITufCache Cache { get; init; } = new InMemoryTufCache();

    /// <summary>
    /// Retry policy for handling transient failures (default: ExponentialBackoffRetryPolicy)
    /// </summary>
    public IRetryPolicy RetryPolicy { get; init; } = new ExponentialBackoffRetryPolicy();

    /// <summary>
    /// Logger for TUF operations (optional)
    /// </summary>
    public ILogger? Logger { get; init; }

    /// <summary>
    /// Whether to validate configuration on construction (default: true)
    /// </summary>
    public bool ValidateConfiguration { get; init; } = true;

    public UpdaterConfig(byte[] initialRootBytes, Uri remoteMetadataUrl)
    {
        LocalTrustedRoot = initialRootBytes ?? throw new ArgumentNullException(nameof(initialRootBytes));
        RemoteMetadataUrl = remoteMetadataUrl ?? throw new ArgumentNullException(nameof(remoteMetadataUrl));
        RemoteTargetsUrl = new Uri(remoteMetadataUrl, "targets");
        
        if (ValidateConfiguration)
        {
            ValidateConfigurationInternal();
        }
    }

    /// <summary>
    /// Validate configuration properties and throw descriptive exceptions for invalid settings
    /// </summary>
    private void ValidateConfigurationInternal()
    {
        if (LocalTrustedRoot.Length == 0)
        {
            throw new TufConfigurationException("LocalTrustedRoot cannot be empty", nameof(LocalTrustedRoot));
        }

        if (string.IsNullOrWhiteSpace(LocalMetadataDir))
        {
            throw new TufConfigurationException("LocalMetadataDir cannot be null or empty", nameof(LocalMetadataDir));
        }

        if (string.IsNullOrWhiteSpace(LocalTargetsDir))
        {
            throw new TufConfigurationException("LocalTargetsDir cannot be null or empty", nameof(LocalTargetsDir));
        }

        if (!RemoteMetadataUrl.IsAbsoluteUri)
        {
            throw new TufConfigurationException("RemoteMetadataUrl must be an absolute URI", nameof(RemoteMetadataUrl));
        }

        if (!RemoteTargetsUrl.IsAbsoluteUri)
        {
            throw new TufConfigurationException("RemoteTargetsUrl must be an absolute URI", nameof(RemoteTargetsUrl));
        }

        if (MaxRootRotations == 0)
        {
            throw new TufConfigurationException("MaxRootRotations must be greater than 0", nameof(MaxRootRotations));
        }

        if (MaxDelegations == 0)
        {
            throw new TufConfigurationException("MaxDelegations must be greater than 0", nameof(MaxDelegations));
        }

        if (RootMaxLength == 0)
        {
            throw new TufConfigurationException("RootMaxLength must be greater than 0", nameof(RootMaxLength));
        }

        if (TimestampMaxLength == 0)
        {
            throw new TufConfigurationException("TimestampMaxLength must be greater than 0", nameof(TimestampMaxLength));
        }

        if (SnapshotMaxLength <= 0)
        {
            throw new TufConfigurationException("SnapshotMaxLength must be greater than 0", nameof(SnapshotMaxLength));
        }

        if (TargetsMaxLength <= 0)
        {
            throw new TufConfigurationException("TargetsMaxLength must be greater than 0", nameof(TargetsMaxLength));
        }
    }
}

public class Updater
{
    private TrustedMetadata _trusted;
    private readonly UpdaterConfig _config;

    public Updater(UpdaterConfig config)
    {
        _config = config;
        _trusted = TrustedMetadata.CreateFromRootData(config.LocalTrustedRoot);
        
        // Log configuration warning if caching is disabled but cache is configured
        if (_config.DisableLocalCache && _config.Cache is not NullTufCache)
        {
            _config.Logger?.LogWarning("Local caching is disabled but cache is configured. Consider using NullTufCache.Instance for better performance.");
        }
    }

    public async Task RefreshAsync()
    {
        await OnlineRefresh();
    }

    public Dictionary<string, TargetFile> GetTopLevelTargets()
    {
        if (_trusted is not CompleteTrustedMetadata c)
        {
            throw new InvalidOperationException("Trusted metadata not loaded");
        }
        return c.TopLevelTargets.Signed.TargetMap;
    }

    public TrustedMetadata GetTrustedMetadataSet() => _trusted;

    public async Task<(string RemotePath, TargetFile File)?> GetTargetInfo(string targetPath)
    {
        if (_trusted is not CompleteTrustedMetadata c)
        {
            await RefreshAsync();
        }

        return await PreOrderDepthFirstWalk(targetPath);
    }

    public async Task<(string FilePath, byte[] Data)> DownloadTarget(TargetFile targetFile, string targetPath, string? destinationFilePath = null, Uri? baseUrl = null)
    {
        var localDestinationPath = destinationFilePath ?? Path.Combine(_config.LocalTargetsDir, targetPath);
        var targetBaseUrl = baseUrl ?? _config.RemoteTargetsUrl;
        var targetRemotePath = targetPath;
        var expectedHash = targetFile.Hashes.FirstOrDefault().Value;

        _config.Logger?.LogTargetResolution(targetPath);

        // Try cache first
        var cachedData = await _config.Cache.GetTargetAsync(targetPath, expectedHash);
        if (cachedData != null)
        {
            _config.Logger?.LogDebug("Target cache hit for {TargetPath}", targetPath);
            return (localDestinationPath, cachedData);
        }

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

        // Cache the downloaded target
        await _config.Cache.SetTargetAsync(targetPath, data, expectedHash);
        _config.Logger?.LogTargetDownloadSuccess(targetPath, data.Length);

        // Also save to local file cache if enabled
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
        return await _config.RetryPolicy.ExecuteAsync(async cancellationToken =>
        {
            _config.Logger?.LogHttpRequest(uri.Host, "GET", uri.PathAndQuery);

            HttpResponseMessage? response = null;
            try
            {
                response = await _config.Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    _config.Logger?.LogRepositoryNetworkError(uri, (int)response.StatusCode, error, new HttpRequestException(error));
                    throw new RepositoryNetworkException(error, uri, response.StatusCode);
                }

                var length = response.Content.Headers.ContentLength ?? 0;
                if (maxLength.HasValue && length > maxLength.Value)
                {
                    var filePath = uri.ToString().Replace(_config.RemoteTargetsUrl.ToString(), "", StringComparison.OrdinalIgnoreCase);
                    var message = $"File {filePath} length {length} exceeds maximum allowed length {maxLength.Value}";
                    _config.Logger?.LogFileSizeExceeded(filePath, length, maxLength.Value);
                    throw new FileSizeException(filePath, length, maxLength.Value);
                }

                var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                _config.Logger?.LogDebug("Downloaded file from {Uri} ({Size} bytes)", uri, data.Length);
                return data;
            }
            catch (HttpRequestException ex)
            {
                var message = $"HTTP request failed for {uri}: {ex.Message}";
                _config.Logger?.LogRepositoryNetworkError(uri, null, message, ex);
                throw new RepositoryNetworkException(message, uri, innerException: ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is not OperationCanceledException)
            {
                var message = $"HTTP request timed out for {uri}";
                _config.Logger?.LogRepositoryTimeout(uri, (int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                throw new RepositoryNetworkException(message, uri, innerException: ex);
            }
            finally
            {
                response?.Dispose();
            }
        });
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