using Serde;
using System.Text.Json.Serialization;

namespace TUF.Models.Simple;

// Basic primitive types
[GenerateSerde]
public partial record KeyId(string Value);

[GenerateSerde] 
public partial record Signature(string Value);

// Key structures
[GenerateSerde]
public partial record KeyValue
{
    [property: SerdeMemberOptions(Rename = "public")]
    public string Public { get; init; } = "";
}

[GenerateSerde]
public partial record Key
{
    [property: SerdeMemberOptions(Rename = "keytype")]
    public string KeyType { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "scheme")]
    public string Scheme { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "keyval")]
    public KeyValue KeyVal { get; init; } = new();
}

// Role assignment
[GenerateSerde]
public partial record RoleKeys
{
    [property: SerdeMemberOptions(Rename = "keyids")]
    public List<string> KeyIds { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "threshold")]
    public int Threshold { get; init; } = 1;
}

[GenerateSerde]
public partial record Roles
{
    [property: SerdeMemberOptions(Rename = "root")]
    public RoleKeys Root { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "timestamp")]
    public RoleKeys Timestamp { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "snapshot")]
    public RoleKeys Snapshot { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "targets")]
    public RoleKeys Targets { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "mirrors")]
    public RoleKeys? Mirrors { get; init; }
}

// Root metadata
[GenerateSerde]
public partial record Root
{
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "root";
    
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "consistent_snapshot")]
    public bool? ConsistentSnapshot { get; init; }
    
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "expires")]
    public string Expires { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "keys")]
    public Dictionary<string, Key> Keys { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "roles")]
    public Roles Roles { get; init; } = new();
}

// Snapshot/Timestamp file metadata
[GenerateSerde]
public partial record FileMetadata
{
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "length")]
    public int? Length { get; init; }
    
    [property: SerdeMemberOptions(Rename = "hashes")]
    public Dictionary<string, string>? Hashes { get; init; }
}

// Timestamp metadata
[GenerateSerde]
public partial record Timestamp
{
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "timestamp";
    
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "expires")]
    public string Expires { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "meta")]
    public Dictionary<string, FileMetadata> Meta { get; init; } = new();
}

// Snapshot metadata
[GenerateSerde]
public partial record Snapshot
{
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "snapshot";
    
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "expires")]
    public string Expires { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "meta")]
    public Dictionary<string, FileMetadata> Meta { get; init; } = new();
}

// Target file info
[GenerateSerde]
public partial record TargetFile
{
    [property: SerdeMemberOptions(Rename = "length")]
    public int Length { get; init; }
    
    [property: SerdeMemberOptions(Rename = "hashes")]
    public Dictionary<string, string> Hashes { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "custom")]
    public Dictionary<string, string>? Custom { get; init; }
}

// Delegation role
[GenerateSerde]
public partial record DelegatedRole
{
    [property: SerdeMemberOptions(Rename = "name")]
    public string Name { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "keyids")]
    public List<string> KeyIds { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "threshold")]
    public int Threshold { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "paths")]
    public List<string>? Paths { get; init; }
    
    [property: SerdeMemberOptions(Rename = "path_hash_prefixes")]
    public List<string>? PathHashPrefixes { get; init; }
    
    [property: SerdeMemberOptions(Rename = "terminating")]
    public bool Terminating { get; init; } = false;
}

// Delegations
[GenerateSerde]
public partial record Delegations
{
    [property: SerdeMemberOptions(Rename = "keys")]
    public Dictionary<string, Key> Keys { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "roles")]
    public List<DelegatedRole> Roles { get; init; } = new();
}

// Targets metadata
[GenerateSerde]
public partial record Targets
{
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "targets";
    
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "expires")]
    public string Expires { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "targets")]
    public Dictionary<string, TargetFile> TargetMap { get; init; } = new();
    
    [property: SerdeMemberOptions(Rename = "delegations")]
    public Delegations? Delegations { get; init; }
}

// Signature object
[GenerateSerde]
public partial record SignatureObject
{
    [property: SerdeMemberOptions(Rename = "keyid")]
    public string KeyId { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "sig")]
    public string Sig { get; init; } = "";
}

// Generic metadata wrapper with proper Serde constraints
[GenerateSerde]
public partial record Metadata<T> where T : ISerializeProvider<T>, IDeserializeProvider<T>
{
    [property: SerdeMemberOptions(Rename = "signed")]
    public T Signed { get; init; } = default!;
    
    [property: SerdeMemberOptions(Rename = "signatures")]
    public List<SignatureObject> Signatures { get; init; } = new();
}

// Mirrors (TAP 5)
[GenerateSerde]
public partial record Mirror
{
    [property: SerdeMemberOptions(Rename = "url")]
    public string Url { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "custom")]
    public Dictionary<string, string>? Custom { get; init; }
}

[GenerateSerde]
public partial record Mirrors
{
    [property: SerdeMemberOptions(Rename = "_type")]
    public string Type { get; init; } = "mirrors";
    
    [property: SerdeMemberOptions(Rename = "spec_version")]
    public string SpecVersion { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "version")]
    public int Version { get; init; } = 1;
    
    [property: SerdeMemberOptions(Rename = "expires")]
    public string Expires { get; init; } = "";
    
    [property: SerdeMemberOptions(Rename = "mirrors")]
    public List<Mirror> MirrorList { get; init; } = new();
}

[GenerateSerde]
public partial record MirrorsMetadata : Metadata<Mirrors>;