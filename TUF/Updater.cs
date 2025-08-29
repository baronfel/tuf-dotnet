using System.Runtime.InteropServices;

using TUF.Models;
using TUF.Models.Primitives;

namespace TUF;

public class UpdaterConfig
{
    public uint MaxRootRotations { get; init; } = 256;
    public uint MaxDelegations { get; init; } = 32;
    public uint RootMaxLength { get; init; } = 512000;
    public uint TimestampMaxLength { get; init; } = 16384;
    public uint SnapshotMaxLength { get; init; } = 2000000;
    public uint TargetsMaxLength { get; init; } = 5000000;

    public byte[] LocalTrustedRoot { get; init; }
    public required string LocalMetadataDir { get; init; }
    public required string LocalTargetsDir { get; init; }
    public Uri RemoteMetadataUrl { get; init; }
    public Uri RemoteTargetsUrl { get; init; }
    public bool PrefixTargetsWithHash { get; init; } = true;
    public bool DisableLocalCache { get; init; } = false;
    public required HttpClient Client { get; init; }

    public UpdaterConfig(byte[] initialRootBytes, Uri remoteMetadataUrl)
    {
        LocalTrustedRoot = initialRootBytes ?? throw new ArgumentNullException(nameof(initialRootBytes));
        RemoteMetadataUrl = remoteMetadataUrl ?? throw new ArgumentNullException(nameof(remoteMetadataUrl));
        RemoteTargetsUrl = new Uri(remoteMetadataUrl, "targets");
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
    }

    public async Task Refresh()
    {
        await OnlineRefresh();
    }

    public Dictionary<RelativePath, Models.Roles.Targets.TargetMetadata> GetTopLevelTargets()
    {
        if (_trusted is not CoreTrustedMetadata c)
        {
            throw new InvalidOperationException("Trusted metadata not loaded");
        }
        return c.Targets.TopLevelTargets.Signed.Targets;
    }
    
    public TrustedMetadata GetTrustedMetadataSet() => _trusted;

    public async Task<TUF.Models.Roles.Targets.TargetMetadata> GetTargetInfo(string targetPath)
    {
        if (_trusted is not CoreTrustedMetadata c)
        {
            await Refresh();
        }

        return await preOrderDepthFirstWalk(targetPath);
    }

    public async Task<(string FilePath, byte[] Data)> DownloadTarget(Models.Roles.Targets.TargetMetadata targetFile, string? destinationFilePath, Uri? baseUrl)
    {
        var localDestinationPath = destinationFilePath ?? GenerateTargetFilePath(targetFile);
        var targetBaseUrl = baseUrl ?? _config.RemoteTargetsUrl;
        var targetRemotePath = targetFile.Path.RelPath;
        if (_trusted.Root.Signed.ConsistentSnapshot is true && _config.PrefixTargetsWithHash)
        {
            var hash = targetFile.Hashes.FirstOrDefault()?.HexEncodedValue;
            var targetFileDir = Path.GetDirectoryName(targetFile.Path.RelPath);
            var targetFileName = Path.GetFileName(targetFile.Path.RelPath)!;
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
        var data = await DownloadFile(downloadUri, targetFile.Length);
        targetFile.VerifyLengthHashes(data);

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

    public async Task<(string FilePath, byte[] Data)?> FindCachedTarget(Models.Roles.Targets.TargetMetadata targetFile, string? filePath)
    {
        if (_config.DisableLocalCache)
        {
            return null;
        }

        var localPath = filePath ?? GenerateTargetFilePath(targetFile);
        var fileBytes = await File.ReadAllBytesAsync(localPath);
        try
        {
            targetFile.VerifyLengthHashes(fileBytes);
        }
        catch
        {
            return null;
        }

        return (localPath, fileBytes);
    }

    private string GenerateTargetFilePath(Models.Roles.Targets.TargetMetadata targetFile) => Path.Combine(_config.LocalTargetsDir, targetFile.Path.RelPath);

    private async Task<TUF.Models.Roles.Targets.TargetMetadata> preOrderDepthFirstWalk(string targetFilePath)
    {
        Stack<(string Role, string Parent)> toVisit = new();
        toVisit.Push((Models.Roles.Targets.TargetsRole.TypeLabel, Models.Roles.Root.Root.TypeLabel));
        HashSet<string> visited = new();
        while (toVisit.Count < _config.MaxDelegations && toVisit.Count > 0)
        {
            (var role, var parent) = toVisit.Pop();
            if (visited.Contains(role))
            {
                continue;
            }
            await LoadTargets(role, parent);
            CoreTrustedMetadata rtts = _trusted as CoreTrustedMetadata ?? throw new Exception("trusted metadata not loaded");
            var targets = rtts.Targets[role]!;
            if (targets.Signed.Targets.TryGetValue(new(targetFilePath), out var targetMeta))
            {
                // if we have the file, return it
                return targetMeta;
            }
            // if we didn't find the file in this role
            visited.Add(role); // add the visited role
            // add any child delegations to the list to traverse
            if (targets.Signed.Delegations is Models.Roles.Targets.Delegations delegations)
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
        throw new Exception("Target not found");
    }

    private async Task OnlineRefresh()
    {
        await LoadRoot();
        await LoadTimestamp();
        await LoadSnapshot();
        await LoadTargets(Models.Roles.Targets.TargetsRole.TypeLabel, Models.Roles.Root.Root.TypeLabel);
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
                data = await DownloadMetadata(Models.Roles.Root.Root.TypeLabel, _config.RootMaxLength, v);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                break;
            }
        }
        if (data is not null)
        {
            _trusted = _trusted.UpdateRoot(data);
            await PersistMetadata(Models.Roles.Root.Root.TypeLabel, data);
        }
    }

    private async Task LoadTimestamp()
    {
        var data = await DownloadMetadata(Models.Roles.Timestamp.Timestamp.TypeLabel, _config.TimestampMaxLength, null);
        _trusted = _trusted.UpdateTimeStamp(data);
        await PersistMetadata(Models.Roles.Timestamp.Timestamp.TypeLabel, data);
    }

    private async Task LoadSnapshot()
    {
        if (_trusted is RootAndTimestampTrustedMetadata rts)
        {
            var snapshotMeta = rts.Timestamp.Signed.SnapshotFileMetadata;
            var length = snapshotMeta.Length ?? _config.SnapshotMaxLength;
            int? version = rts.Root.Signed.ConsistentSnapshot is true ? (int)snapshotMeta.Version : null;
            var data = await DownloadMetadata(Models.Roles.Snapshot.Snapshot.TypeLabel, _config.SnapshotMaxLength, version);
            _trusted = _trusted.UpdateSnapshot(data, isTrusted: false);
            await PersistMetadata(Models.Roles.Snapshot.Snapshot.TypeLabel, data);
        }
        else {
            throw new Exception("trusted timestamp not loaded");
        }
    }

    private async Task LoadTargets(string roleName, string parentName)
    {
        if (_trusted is RootAndTimestampAndSnapshotTrustedMetadata rts)
        {
            if (!rts.Snapshot.Signed.Meta.TryGetValue(new(roleName + ".json"), out var roleFileMetadata))
            {
                throw new Exception($"Role file metadata for {roleName} not found");
            }

            var length = roleFileMetadata.Length ?? _config.TargetsMaxLength;
            int? version = rts.Root.Signed.ConsistentSnapshot is true ? (int)roleFileMetadata.Version : null;
            var data = await DownloadMetadata(roleName, length, version);
            _trusted = _trusted.UpdateDelegatedTargets(data, roleName, parentName);
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