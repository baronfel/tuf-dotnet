using System.Text;

using TUF.Models;
using TUF.Models.DigestAlgorithms;
using TUF.Models.Keys;
using TUF.Models.Primitives;
using TUF.Models.Roles.Root;
using TUF.Serialization;

using MirrorsRoleNs = TUF.Models.Roles.Mirrors;
using RootRoleNs = TUF.Models.Roles.Root;
using SnapshotRoleNs = TUF.Models.Roles.Snapshot;
using TargetsRoleNs = TUF.Models.Roles.Targets;
using TimestampRoleNs = TUF.Models.Roles.Timestamp;

namespace tuf_dotnet.Tests;

public class MetadataRoundtripTests
{
    [Test]
    public async Task RootRole_Roundtrip()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(30);
        var rootRoles = new RootRoleNs.RoleKeys(new List<KeyId>(), 1);
        var roles = new RootRoleNs.RootRoles(rootRoles, rootRoles, rootRoles, rootRoles, null);
        var signed = new RootRoleNs.Root(spec, null, 1, expires, new Dictionary<KeyId, Key>(), roles);
        var meta = new RootMetadata(signed, new Dictionary<KeyId, Signature>());

        var bytes = MetadataSerializer.SerializeToUTF8Bytes(meta);
        var round = MetadataSerializer.Deserialize<RootMetadata>(bytes);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Signed.Version).IsEqualTo(meta.Signed.Version);
    }

    [Test]
    public async Task SnapshotRole_Roundtrip()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(7);
        var metaMap = new Dictionary<RelativePath, FileMetadata> { [new RelativePath("snapshot.json")] = new FileMetadata(1, null, null) };
        var signed = new SnapshotRoleNs.Snapshot(spec, 1, expires, metaMap);
        var meta = new SnapshotMetadata(signed, new Dictionary<KeyId, Signature>());

        var bytes = MetadataSerializer.SerializeToUTF8Bytes(meta);
        var round = MetadataSerializer.Deserialize<SnapshotMetadata>(bytes);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Signed.Version).IsEqualTo(meta.Signed.Version);
    }

    [Test]
    public async Task TargetsRole_Roundtrip()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(7);
        var targets = new Dictionary<RelativePath, TargetsRoleNs.TargetMetadata> { [new RelativePath("file.txt")] = new TargetsRoleNs.TargetMetadata(0, new List<DigestValue>(), null, new RelativePath("thing.json")) };
        var signed = new TargetsRoleNs.TargetsRole(spec, 1, expires, targets);
        var meta = new TargetsMetadata(signed, new Dictionary<KeyId, Signature>());

        var bytes = MetadataSerializer.SerializeToUTF8Bytes(meta);
        var str = Encoding.UTF8.GetString(bytes);
        Console.WriteLine(str);
        var round = MetadataSerializer.Deserialize<TargetsMetadata>(bytes);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Signed.Version).IsEqualTo(meta.Signed.Version);
    }

    [Test]
    public async Task TimestampRole_Roundtrip()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(1);
        var signed = new TimestampRoleNs.Timestamp(spec, 1, expires, new(new FileMetadata(1, null, null)));
        var meta = new TimestampMetadata(signed, new Dictionary<KeyId, Signature>());

        var bytes = MetadataSerializer.SerializeToUTF8Bytes(meta);
        var round = MetadataSerializer.Deserialize<TimestampMetadata>(bytes);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Signed.Version).IsEqualTo(meta.Signed.Version);
    }

    [Test]
    public async Task MirrorsRole_Roundtrip()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(7);
        var mirror = new MirrorsRoleNs.MirrorDefinition(
                AbsoluteUri.From("https://example.com/"),
                RelativeUri.From("./meta.json"),
                RelativeUri.From("./targets"),
                Array.Empty<PathPattern>(),
                Array.Empty<PathPattern>(),
                new Dictionary<string, object>()
        );
        var signed = new MirrorsRoleNs.Mirror(spec, 1, expires, new[] { mirror });
        var meta = new MirrorMetadata(signed, new Dictionary<KeyId, Signature>());

        var bytes = MetadataSerializer.SerializeToUTF8Bytes(meta);
        var round = MetadataSerializer.Deserialize<MirrorMetadata>(bytes);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Signed.Version).IsEqualTo(meta.Signed.Version);
    }
}