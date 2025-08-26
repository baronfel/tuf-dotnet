using System.Text.Json.Serialization;

using TUF.Models.Roles.Targets;

namespace TUF.Serialization;

[JsonSerializable(typeof(TUF.Models.RootMetadata))]
[JsonSerializable(typeof(TUF.Models.SnapshotMetadata))]
[JsonSerializable(typeof(TUF.Models.TargetsMetadata))]
[JsonSerializable(typeof(TUF.Models.TimestampMetadata))]
[JsonSerializable(typeof(TUF.Models.MirrorMetadata))]
[JsonSerializable(typeof(TUF.Models.Roles.Root.Root))]
[JsonSerializable(typeof(TUF.Models.Roles.Snapshot.Snapshot))]
[JsonSerializable(typeof(TUF.Models.Roles.Targets.TargetMetadata))]
[JsonSerializable(typeof(TUF.Models.Roles.Targets.TargetsRole))]
[JsonSerializable(typeof(TUF.Models.Roles.Timestamp.Timestamp))]
[JsonSerializable(typeof(TUF.Models.Roles.Mirrors.Mirror))]
[JsonSerializable(typeof(TUF.Models.Roles.Mirrors.Mirror))]
[JsonSerializable(typeof(TUF.Models.Keys.Key))]
[JsonSerializable(typeof(TUF.Models.Keys.WellKnown.Rsa))]
[JsonSerializable(typeof(TUF.Models.Keys.WellKnown.Ed25519))]
[JsonSerializable(typeof(TUF.Models.Keys.WellKnown.Ecdsa))]
[JsonSerializable(typeof(TUF.Models.Keys.Values.RsaKeyValue))]
[JsonSerializable(typeof(TUF.Models.Keys.Values.Ed25519KeyValue))]
[JsonSerializable(typeof(TUF.Models.Keys.Values.EcdsaKeyValue))]
[JsonSerializable(typeof(TUF.Models.Primitives.PEMString))]
[JsonSerializable(typeof(TUF.Models.Primitives.HexString))]
[JsonSerializable(typeof(TUF.Models.Primitives.HexDigest))]
[JsonSerializable(typeof(TUF.Models.Primitives.RelativePath))]
[JsonSerializable(typeof(Dictionary<TUF.Models.Primitives.RelativePath, TUF.Models.Primitives.FileMetadata>))]
[JsonSerializable(typeof(TUF.Models.Primitives.FileMetadata))]
[JsonSerializable(typeof(DelegationData))]
[JsonSerializable(typeof(DelegatedRoleName))]
public partial class MetadataJsonContext : JsonSerializerContext
{
}