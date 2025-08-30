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

namespace TUF.Tests;

public class SerializationTests
{
    [Test]
    public async Task RootRoleRoundtrip()
    {
        var spec = new SemanticVersion("1.0");
        var expires = DateTimeOffset.UtcNow.AddDays(30);
        var rootRoles = new RootRoleNs.RoleKeys(new List<KeyId>(), 1);
        var roles = new RootRoleNs.RootRoles(rootRoles, rootRoles, rootRoles, rootRoles, null);
        var signed = new RootRoleNs.Root(spec, null, 1, expires, new Dictionary<KeyId, IKey>(), roles);
        var meta = new RootMetadata(signed, new Dictionary<KeyId, Signature>());

        var bytes = MetadataSerializer.SerializeToUTF8Bytes(meta);
        var round = MetadataSerializer.Deserialize<RootMetadata>(bytes);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Signed.Version).IsEqualTo(meta.Signed.Version);
    }

    [Test]
    public async Task GoldenRootMetadata()
    {
        var golden = """
{
 "signatures": [
  {
   "keyid": "767194bc72565e86affb884a0f8f614c9bda7d843bf4b09128cb7272fe957d69",
   "sig": "3045022100fd6960dfbffc58507245803cd2930625dfabc7d126fd898da6dfc6259c9f2faf0220173e756e7c195dbad5b00c80443e6ace2810d1b007bb7001dd85ed0f1be2f938"
  }
 ],
 "signed": {
  "_type": "root",
  "consistent_snapshot": true,
  "expires": "2025-09-29T02:43:28Z",
  "keys": {
   "5c4d6edf3bdd156946bc6f1f5d9ebf39a83e1d89daac11a119101aba634a6ccb": {
    "keytype": "ecdsa",
    "keyval": {
     "public": "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEepGd26fJXuJFPDW0zlcEKjHheZjR\n5CE9cI9Rd0UQ6hohIAbifafu7TBad8q4HGPHAxJZyO3JyHhrAwD6yaGUbA==\n-----END PUBLIC KEY-----\n"
    },
    "scheme": "ecdsa-sha2-nistp256"
   },
   "767194bc72565e86affb884a0f8f614c9bda7d843bf4b09128cb7272fe957d69": {
    "keytype": "ecdsa",
    "keyval": {
     "public": "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEtvVVPV...cDQgAEK2pN0hcEitsU5ZKnTVqGoLn0ljvO\nNAO2ditxpiLWUUkXoGDG5X7j0ZQmWEHpK0k3LRPQLggf4A9jiUqxM6K5Dw==\n-----END PUBLIC KEY-----\n"
    },
    "scheme": "ecdsa-sha2-nistp256"
   },
   "a05136c6ff6cbb5d275f4b03ce03369fb34e5ff5a3b044fdd246e093bd97f511": {
    "keytype": "ecdsa",
    "keyval": {
     "public": "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEI0/dhnPZtVKoAlpfH68rc8ybhux6\nJedfCrX63vW2LLVNqQuoQ73q/xhZsTV+ubEGTMU77+TLMQz929TMzqjANQ==\n-----END PUBLIC KEY-----\n"
    },
    "scheme": "ecdsa-sha2-nistp256"
   }
  },
  "roles": {
   "root": {
    "keyids": [
     "767194bc72565e86affb884a0f8f614c9bda7d843bf4b09128cb7272fe957d69"
    ],
    "threshold": 1
   },
   "snapshot": {
    "keyids": [
     "a05136c6ff6cbb5d275f4b03ce03369fb34e5ff5a3b044fdd246e093bd97f511"
    ],
    "threshold": 1
   },
   "targets": {
    "keyids": [
     "936fba625bad647370da662f6b41c2044b4503de20407d536d35eec26cdc3dd9"
    ],
    "threshold": 1
   },
   "timestamp": {
    "keyids": [
     "5c4d6edf3bdd156946bc6f1f5d9ebf39a83e1d89daac11a119101aba634a6ccb"
    ],
    "threshold": 1
   }
  },
  "spec_version": "1.0.31",
  "version": 1
 }
}
""";
        var root = MetadataSerializer.Deserialize<RootMetadata>(Encoding.UTF8.GetBytes(golden));
        Dictionary<KeyId, IKey> expectedKeys = new()
        {
            [new(new("5c4d6edf3bdd156946bc6f1f5d9ebf39a83e1d89daac11a119101aba634a6ccb"))] = new Models.Keys.WellKnown.Ecdsa(new(new("-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEepGd26fJXuJFPDW0zlcEKjHheZjR\n5CE9cI9Rd0UQ6hohIAbifafu7TBad8q4HGPHAxJZyO3JyHhrAwD6yaGUbA==\n-----END PUBLIC KEY-----\n"))),
            [new(new("767194bc72565e86affb884a0f8f614c9bda7d843bf4b09128cb7272fe957d69"))] = new Models.Keys.WellKnown.Ecdsa(new(new("-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEtvVVPV...cDQgAEK2pN0hcEitsU5ZKnTVqGoLn0ljvO\nNAO2ditxpiLWUUkXoGDG5X7j0ZQmWEHpK0k3LRPQLggf4A9jiUqxM6K5Dw==\n-----END PUBLIC KEY-----\n"))),
            [new(new("a05136c6ff6cbb5d275f4b03ce03369fb34e5ff5a3b044fdd246e093bd97f511"))] = new Models.Keys.WellKnown.Ecdsa(new(new("-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEI0/dhnPZtVKoAlpfH68rc8ybhux6\nJedfCrX63vW2LLVNqQuoQ73q/xhZsTV+ubEGTMU77+TLMQz929TMzqjANQ==\n-----END PUBLIC KEY-----\n"))),
        };
        RootRoles expectedRootRoles = new(
            new RoleKeys([
                new(new("767194bc72565e86affb884a0f8f614c9bda7d843bf4b09128cb7272fe957d69"))
            ], 1),
            new RoleKeys([
                new(new("5c4d6edf3bdd156946bc6f1f5d9ebf39a83e1d89daac11a119101aba634a6ccb"))
            ], 1),
            new RoleKeys([
                new(new("a05136c6ff6cbb5d275f4b03ce03369fb34e5ff5a3b044fdd246e093bd97f511"))
            ], 1),
            new RoleKeys([
                new(new("936fba625bad647370da662f6b41c2044b4503de20407d536d35eec26cdc3dd9"))
            ], 1)
        );
        Dictionary<KeyId, Signature> expectedSigs = new()
        {
            [new(new("767194bc72565e86affb884a0f8f614c9bda7d843bf4b09128cb7272fe957d69"))] = new(new(new("767194bc72565e86affb884a0f8f614c9bda7d843bf4b09128cb7272fe957d69")), new("3045022100fd6960dfbffc58507245803cd2930625dfabc7d126fd898da6dfc6259c9f2faf0220173e756e7c195dbad5b00c80443e6ace2810d1b007bb7001dd85ed0f1be2f938"))
        };
        RootMetadata expectedRoot = new(new(new("1.0.31"), true, 1, DateTimeOffset.Parse("2025-09-29T02:43:28Z"), expectedKeys, expectedRootRoles), expectedSigs);
        await Assert.That(root).IsEquivalentTo(expectedRoot);
    }

    [Test]
    public async Task SnapshotRoleRoundtrip()
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
    public async Task TargetsRoleRoundtrip()
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
    public async Task TimestampRoleRoundtrip()
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
    public async Task MirrorsRoleRoundtrip()
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