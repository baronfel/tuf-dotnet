using System.Net;
using System.Text;

using TUF.Models;
using TUF.Models.DigestAlgorithms;
using TUF.Models.Keys;
using TUF.Models.Primitives;
using TUF.Models.Roles.Root;
using TUF.Models.Roles.Targets;
using TUF.Models.Roles.Timestamp;
using TUF.Models.Roles.Snapshot;
using TUF.Serialization;

namespace TUF.Tests;

public class UpdaterTests
{
    private static byte[] CreateValidRootMetadata()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddYears(1);
        var rootRoles = new RoleKeys(new List<KeyId>(), 1);
        var roles = new RootRoles(rootRoles, rootRoles, rootRoles, rootRoles, null);
        var signed = new Root(spec, null, 1, expires, new Dictionary<KeyId, Key>(), roles);
        var meta = new RootMetadata(signed, new Dictionary<KeyId, Signature>());
        
        return MetadataSerializer.SerializeToUTF8Bytes(meta);
    }

    private static byte[] CreateValidTimestampMetadata()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(1);
        var snapshotMeta = new SnapshotFileMetadata(new FileMetadata(1, null, null));
        var signed = new Models.Roles.Timestamp.Timestamp(spec, 1, expires, snapshotMeta);
        var meta = new TimestampMetadata(signed, new Dictionary<KeyId, Signature>());
        
        return MetadataSerializer.SerializeToUTF8Bytes(meta);
    }

    private static byte[] CreateValidSnapshotMetadata()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(7);
        var metaMap = new Dictionary<RelativePath, FileMetadata> 
        { 
            [new RelativePath("targets.json")] = new FileMetadata(1, null, null) 
        };
        var signed = new Models.Roles.Snapshot.Snapshot(spec, 1, expires, metaMap);
        var meta = new SnapshotMetadata(signed, new Dictionary<KeyId, Signature>());
        
        return MetadataSerializer.SerializeToUTF8Bytes(meta);
    }

    private static byte[] CreateValidTargetsMetadata()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(7);
        var targets = new Dictionary<RelativePath, TargetMetadata>
        {
            [new RelativePath("test-file.txt")] = new TargetMetadata(
                100, 
                new List<DigestValue> { new DigestValue<SHA256>("abc123") }, 
                null, 
                new RelativePath("test-file.txt")
            )
        };
        var signed = new TargetsRole(spec, 1, expires, targets);
        var meta = new TargetsMetadata(signed, new Dictionary<KeyId, Signature>());
        
        return MetadataSerializer.SerializeToUTF8Bytes(meta);
    }

    [Test]
    public void UpdaterConfig_Constructor_WithNullRootBytes_ThrowsArgumentNullException()
    {
        var remoteUrl = new Uri("https://example.com/metadata/");
        
        Assert.Throws<ArgumentNullException>(() => new UpdaterConfig(null!, remoteUrl)
        {
            LocalMetadataDir = "/tmp/metadata",
            LocalTargetsDir = "/tmp/targets",
            Client = new HttpClient()
        });
    }

    [Test]
    public void UpdaterConfig_Constructor_WithNullRemoteUrl_ThrowsArgumentNullException()
    {
        var rootBytes = CreateValidRootMetadata();
        
        Assert.Throws<ArgumentNullException>(() => new UpdaterConfig(rootBytes, null!)
        {
            LocalMetadataDir = "/tmp/metadata",
            LocalTargetsDir = "/tmp/targets",
            Client = new HttpClient()
        });
    }

    [Test]
    public async Task UpdaterConfig_Constructor_WithValidParameters_SetsProperties()
    {
        var rootBytes = CreateValidRootMetadata();
        var remoteUrl = new Uri("https://example.com/metadata/");
        
        var config = new UpdaterConfig(rootBytes, remoteUrl)
        {
            LocalMetadataDir = "/tmp/metadata",
            LocalTargetsDir = "/tmp/targets",
            Client = new HttpClient()
        };

        await Assert.That(config.LocalTrustedRoot).IsEqualTo(rootBytes);
        await Assert.That(config.RemoteMetadataUrl).IsEqualTo(remoteUrl);
        await Assert.That(config.RemoteTargetsUrl).IsEqualTo(new Uri(remoteUrl, "targets"));
        await Assert.That(config.MaxRootRotations).IsEqualTo(256u);
        await Assert.That(config.PrefixTargetsWithHash).IsTrue();
    }

    [Test]
    public void Updater_Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Updater(null!));
    }

    [Test]
    public async Task Updater_Constructor_WithValidConfig_InitializesTrustedMetadata()
    {
        var rootBytes = CreateValidRootMetadata();
        var config = new UpdaterConfig(rootBytes, new Uri("https://example.com/metadata/"))
        {
            LocalMetadataDir = "/tmp/metadata",
            LocalTargetsDir = "/tmp/targets", 
            Client = new HttpClient()
        };

        var updater = new Updater(config);
        var trustedMetadata = updater.GetTrustedMetadataSet();
        
        await Assert.That(trustedMetadata).IsNotNull();
        await Assert.That(trustedMetadata.Root).IsNotNull();
    }

    [Test]
    public async Task GetTopLevelTargets_WithoutRefresh_ThrowsInvalidOperationException()
    {
        var rootBytes = CreateValidRootMetadata();
        var config = new UpdaterConfig(rootBytes, new Uri("https://example.com/metadata/"))
        {
            LocalMetadataDir = "/tmp/metadata",
            LocalTargetsDir = "/tmp/targets",
            Client = new HttpClient()
        };

        var updater = new Updater(config);
        
        await Assert.That(() => updater.GetTopLevelTargets())
            .Throws<InvalidOperationException>()
            .WithMessage("Trusted metadata not loaded");
    }

    [Test]
    public async Task GetTargetInfo_WithoutRefresh_CallsRefresh()
    {
        var rootBytes = CreateValidRootMetadata();
        var mockHandler = new MockHttpMessageHandler();
        
        // Setup mock responses for refresh workflow
        mockHandler.AddResponse("https://example.com/metadata/timestamp.json", CreateValidTimestampMetadata());
        mockHandler.AddResponse("https://example.com/metadata/snapshot.json", CreateValidSnapshotMetadata());
        mockHandler.AddResponse("https://example.com/metadata/targets.json", CreateValidTargetsMetadata());
        
        var config = new UpdaterConfig(rootBytes, new Uri("https://example.com/metadata/"))
        {
            LocalMetadataDir = Path.GetTempPath(),
            LocalTargetsDir = Path.GetTempPath(),
            Client = new HttpClient(mockHandler)
        };

        var updater = new Updater(config);
        
        // This should trigger refresh and then search for the target
        await Assert.That(async () => await updater.GetTargetInfo("test-file.txt"))
            .Throws<Exception>();
    }

    [Test]
    public async Task DownloadTarget_ValidTarget_ReturnsFileData()
    {
        var rootBytes = CreateValidRootMetadata();
        var testFileContent = Encoding.UTF8.GetBytes("test content for file");
        var hash = System.Security.Cryptography.SHA256.HashData(testFileContent);
        var hashHex = Convert.ToHexString(hash).ToLowerInvariant();
        
        var targetMeta = new TargetMetadata(
            (uint)testFileContent.Length,
            new List<DigestValue> { new DigestValue<SHA256>(hashHex) },
            null,
            new RelativePath("test-file.txt")
        );

        var mockHandler = new MockHttpMessageHandler();
        mockHandler.AddResponse("https://example.com/targets/test-file.txt", testFileContent);
        
        var config = new UpdaterConfig(rootBytes, new Uri("https://example.com/metadata/"))
        {
            LocalMetadataDir = Path.GetTempPath(),
            LocalTargetsDir = Path.GetTempPath(),
            Client = new HttpClient(mockHandler),
            DisableLocalCache = true
        };

        var updater = new Updater(config);
        
        var (filePath, data) = await updater.DownloadTarget(targetMeta, null, new Uri("https://example.com/targets/"));
        
        await Assert.That(data).IsEqualTo(testFileContent);
        await Assert.That(filePath).IsNotNull();
    }

    [Test]
    public async Task DownloadTarget_InvalidHash_ThrowsException()
    {
        var rootBytes = CreateValidRootMetadata();
        var testFileContent = Encoding.UTF8.GetBytes("test content for file");
        var incorrectHash = "deadbeef";
        
        var targetMeta = new TargetMetadata(
            (uint)testFileContent.Length,
            new List<DigestValue> { new DigestValue<SHA256>(incorrectHash) },
            null,
            new RelativePath("test-file.txt")
        );

        var mockHandler = new MockHttpMessageHandler();
        mockHandler.AddResponse("https://example.com/targets/test-file.txt", testFileContent);
        
        var config = new UpdaterConfig(rootBytes, new Uri("https://example.com/metadata/"))
        {
            LocalMetadataDir = Path.GetTempPath(),
            LocalTargetsDir = Path.GetTempPath(),
            Client = new HttpClient(mockHandler),
            DisableLocalCache = true
        };

        var updater = new Updater(config);
        
        await Assert.That(async () => await updater.DownloadTarget(targetMeta, null, new Uri("https://example.com/targets/")))
            .Throws<Exception>();
    }

    [Test]
    public async Task FindCachedTarget_DisabledCache_ReturnsNull()
    {
        var rootBytes = CreateValidRootMetadata();
        var targetMeta = new TargetMetadata(
            100,
            new List<DigestValue> { new DigestValue<SHA256>("abc123") },
            null,
            new RelativePath("test-file.txt")
        );

        var config = new UpdaterConfig(rootBytes, new Uri("https://example.com/metadata/"))
        {
            LocalMetadataDir = Path.GetTempPath(),
            LocalTargetsDir = Path.GetTempPath(),
            Client = new HttpClient(),
            DisableLocalCache = true
        };

        var updater = new Updater(config);
        
        var result = await updater.FindCachedTarget(targetMeta, null);
        
        await Assert.That(result).IsNull();
    }
}

internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _responses = new();

    public void AddResponse(string url, byte[] content)
    {
        _responses[url] = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString();
        
        if (url != null && _responses.TryGetValue(url, out var content))
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            };
            return Task.FromResult(response);
        }

        var notFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
        return Task.FromResult(notFoundResponse);
    }
}