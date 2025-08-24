using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TUnit;
using TUF.Models;
using TUF.Models.Primitives;
using TUF.Models.Keys;
using TUF.Models.Roles;
using TUF.Models.DigestAlgorithms;
using RootRoleNs = TUF.Models.Roles.Root;
using SnapshotRoleNs = TUF.Models.Roles.Snapshot;
using TargetsRoleNs = TUF.Models.Roles.Targets;
using TimestampRoleNs = TUF.Models.Roles.Timestamp;
using MirrorsRoleNs = TUF.Models.Roles.Mirrors;
using TUF.Serialization;

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
        var signed = new RootRoleNs.Root(spec, null, 1, expires, new Dictionary<KeyId, IKey>(), roles);
        var meta = new Metadata<RootRoleNs.Root>(signed, new Dictionary<KeyId, Signature>(), null);

        var bytes = MetadataSerializer.SerializeToUTF8Bytes(meta);
        var round = MetadataSerializer.Deserialize<RootRoleNs.Root>(bytes);

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
        var meta = new Metadata<SnapshotRoleNs.Snapshot>(signed, new Dictionary<KeyId, Signature>(), null);

        var bytes = MetadataSerializer.SerializeToUTF8Bytes(meta);
        var round = MetadataSerializer.Deserialize<SnapshotRoleNs.Snapshot>(bytes);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Signed.Version).IsEqualTo(meta.Signed.Version);
    }

    [Test]
    public async Task TargetsRole_Roundtrip()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(7);
        var targets = new Dictionary<RelativePath, TargetsRoleNs.TargetMetadata> { [new RelativePath("file.txt")] = new TargetsRoleNs.TargetMetadata(0, new List<DigestValue>(), null) };
        var signed = new TargetsRoleNs.TargetsRole(spec, 1, expires, targets);
        var meta = new Metadata<TargetsRoleNs.TargetsRole>(signed, new Dictionary<KeyId, Signature>(), null);

        var bytes = MetadataSerializer.SerializeToUTF8Bytes(meta);
        var round = MetadataSerializer.Deserialize<TargetsRoleNs.TargetsRole>(bytes);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Signed.Version).IsEqualTo(meta.Signed.Version);
    }

    [Test]
    public async Task TimestampRole_Roundtrip()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(1);
        var signed = new TimestampRoleNs.TimestampRole(spec, 1, expires, new FileMetadata(1, null, null));
        var meta = new Metadata<TimestampRoleNs.TimestampRole>(signed, new Dictionary<KeyId, Signature>(), null);

        var bytes = MetadataSerializer.SerializeToUTF8Bytes(meta);
        var round = MetadataSerializer.Deserialize<TimestampRoleNs.TimestampRole>(bytes);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Signed.Version).IsEqualTo(meta.Signed.Version);
    }

    [Test]
    public async Task MirrorsRole_Roundtrip()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(7);
        var mirror = new MirrorsRoleNs.Mirror(new AbsoluteUri("https://example.com/"), new RelativeUri("meta.json"), new RelativeUri("targets"), Array.Empty<PathPattern>(), Array.Empty<PathPattern>(), new Dictionary<string, object>());
        var signed = new MirrorsRoleNs.MirrorRole(spec, 1, expires, new[] { mirror });
        var meta = new Metadata<MirrorsRoleNs.MirrorRole>(signed, new Dictionary<KeyId, Signature>(), null);

        var bytes = MetadataSerializer.SerializeToUTF8Bytes(meta);
        var round = MetadataSerializer.Deserialize<MirrorsRoleNs.MirrorRole>(bytes);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Signed.Version).IsEqualTo(meta.Signed.Version);
    }
}
