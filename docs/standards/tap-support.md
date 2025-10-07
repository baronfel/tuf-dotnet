# TUF Augmentation Proposal (TAP) Support

TUF Augmentation Proposals (TAPs) extend the core TUF specification with additional features and improvements. This document details which TAPs are supported by TUF .NET and provides implementation guidance.

## TAP Support Overview

| TAP | Title | Status | Implementation | Version Added |
|-----|-------|--------|---------------|---------------|
| [TAP 3](#tap-3) | Multi-role delegations | ✅ Full | Core delegation system | v1.0.0 |
| [TAP 4](#tap-4) | Multiple repository consensus | ✅ Full | `MultiRepositoryClient` | v1.1.0 |
| [TAP 5](#tap-5) | Setting URLs for roles in root metadata | ✅ Full | Root metadata URLs | v1.0.0 |
| [TAP 8](#tap-8) | Key rotation and explicit key revocation | ✅ Full | Root key rotation | v1.0.0 |
| [TAP 9](#tap-9) | Mandatory metadata signatures | ✅ Full | Signature verification | v1.0.0 |
| [TAP 10](#tap-10) | Remove native support for compressed metadata | ✅ Full | No compression support | v1.0.0 |
| [TAP 12](#tap-12) | Improving client implementation | ✅ Full | Client architecture | v1.0.0 |
| [TAP 15](#tap-15) | Succinct roles | 🔄 Planned | Design phase | Planned v2.0 |
| [TAP 19](#tap-19) | Content addressable storage | 🔄 Planned | Research phase | TBD |

## Implemented TAPs

### TAP 3: Multi-role Delegations {#tap-3}

**Status**: ✅ Fully Implemented  
**Specification**: [TAP 3](https://github.com/theupdateframework/taps/blob/master/tap3.md)

TAP 3 enables a single delegated role to be signed by multiple keys with configurable thresholds.

#### Implementation Details
```csharp
// Multi-signature delegation support
public class DelegatedRole
{
    public string Name { get; init; }
    public string[] KeyIds { get; init; }      // Multiple signing keys
    public int Threshold { get; init; }        // Minimum signatures required
    public string[]? Paths { get; init; }
    public string[]? PathHashPrefixes { get; init; }
    public bool Terminating { get; init; }
}
```

#### Usage Example
```json
{
  "delegations": {
    "keys": {
      "key1": { "keytype": "ed25519", "scheme": "ed25519", "keyval": {...} },
      "key2": { "keytype": "ed25519", "scheme": "ed25519", "keyval": {...} },
      "key3": { "keytype": "rsa", "scheme": "rsassa-pss-sha256", "keyval": {...} }
    },
    "roles": [
      {
        "name": "secure-apps",
        "keyids": ["key1", "key2", "key3"],
        "threshold": 2,
        "paths": ["secure/*"],
        "terminating": true
      }
    ]
  }
}
```

#### Security Benefits
- **Higher security**: Multiple keys required for sensitive delegations
- **Key compromise resilience**: Single key compromise doesn't break security
- **Flexible policies**: Different threshold requirements for different roles

### TAP 4: Multiple Repository Consensus {#tap-4}

**Status**: ✅ Fully Implemented  
**Specification**: [TAP 4](https://github.com/theupdateframework/taps/blob/master/tap4.md)

TAP 4 allows clients to use multiple TUF repositories and make consensus decisions across them.

#### Implementation Details
```csharp
public class MultiRepositoryClient
{
    public async Task RefreshAsync()                    // Refresh all repositories
    public Dictionary<string, TargetFile> GetAvailableTargets()  // Consensus targets
    public ConsensusResult<T> GetConsensus<T>(Func<Repository, T> selector)
}

public enum ConsensusPolicy
{
    Unanimous,      // All repositories must agree
    Majority,       // More than half must agree
    Any,           // At least one repository agrees
    Threshold      // Configurable threshold
}
```

#### Configuration Example
```csharp
var config = new MultiRepositoryConfig
{
    Repositories = new[]
    {
        new RepositoryConfig 
        { 
            Name = "primary",
            MetadataUrl = new Uri("https://repo1.example.com/metadata/"),
            Weight = 1.0,
            Priority = 1
        },
        new RepositoryConfig 
        { 
            Name = "mirror",
            MetadataUrl = new Uri("https://repo2.example.com/metadata/"),
            Weight = 0.8,
            Priority = 2
        }
    },
    ConsensusPolicy = ConsensusPolicy.Majority,
    MinimumAgreement = 0.6
};

var client = new MultiRepositoryClient(config);
```

#### Use Cases
- **High availability**: Continue operation if some repositories are down
- **Geographic distribution**: Use closest/fastest repository
- **Security enhancement**: Require consensus for sensitive operations
- **Vendor diversification**: Reduce single point of failure

### TAP 5: Setting URLs for Roles in Root Metadata {#tap-5}

**Status**: ✅ Fully Implemented  
**Specification**: [TAP 5](https://github.com/theupdateframework/taps/blob/master/tap5.md)

TAP 5 allows root metadata to specify custom URLs for different metadata roles.

#### Implementation Details
```csharp
public record Root
{
    public Dictionary<string, RoleDefinition> Roles { get; init; } = new();
    // ... other properties
}

public record RoleDefinition
{
    public string[] KeyIds { get; init; } = Array.Empty<string>();
    public int Threshold { get; init; }
    public string? Url { get; init; }     // TAP 5: Custom URL for role
    public Dictionary<string, object> UnrecognizedFields { get; init; } = new();
}
```

#### Usage Example
```json
{
  "_type": "root",
  "version": 1,
  "roles": {
    "timestamp": {
      "keyids": ["timestamp-key"],
      "threshold": 1,
      "url": "https://fast-cdn.example.com/timestamp.json"
    },
    "snapshot": {
      "keyids": ["snapshot-key"], 
      "threshold": 1,
      "url": "https://metadata.example.com/snapshot.json"
    },
    "targets": {
      "keyids": ["targets-key"],
      "threshold": 1,
      "url": "https://secure.example.com/targets.json"
    }
  }
}
```

#### Benefits  
- **Performance optimization**: Use CDNs for frequently accessed metadata
- **Security zones**: Separate URLs for different security levels
- **Load distribution**: Distribute load across multiple endpoints

### TAP 8: Key Rotation and Explicit Key Revocation {#tap-8}

**Status**: ✅ Fully Implemented  
**Specification**: [TAP 8](https://github.com/theupdateframework/taps/blob/master/tap8.md)

TAP 8 provides clear procedures for key rotation and revocation.

#### Implementation Details
```csharp
public class TrustedMetadata
{
    public TrustedMetadata UpdateRoot(Metadata<Root> newRoot)
    {
        // TAP 8: Implement key rotation validation
        ValidateKeyRotation(currentRoot, newRoot);
        return new CompleteTrustedMetadata(newRoot, /* ... */);
    }
    
    private void ValidateKeyRotation(Root currentRoot, Root newRoot)
    {
        // Validate rotation follows TAP 8 rules
        // - New root must be signed by sufficient keys from current root
        // - Version must increment
        // - Proper key addition/removal procedures
    }
}
```

#### Key Rotation Process
1. **Create new root metadata** with updated keys
2. **Sign with sufficient current keys** (meets current threshold)
3. **Increment version number**
4. **Distribute new root metadata**
5. **Clients validate** rotation against current trusted root

#### Security Guarantees
- **Smooth transitions**: No service disruption during key rotation
- **Attack prevention**: Prevents unauthorized key changes
- **Audit trail**: Clear history of key changes

### TAP 9: Mandatory Metadata Signatures {#tap-9}

**Status**: ✅ Fully Implemented  
**Specification**: [TAP 9](https://github.com/theupdateframework/taps/blob/master/tap9.md)

TAP 9 clarifies that all metadata must be signed (no unsigned metadata allowed).

#### Implementation Details
```csharp
public class Metadata<T>
{
    public T Signed { get; init; }
    public Signature[] Signatures { get; init; }    // TAP 9: Always required
    
    public void VerifySignatures(Dictionary<string, Key> keys, int threshold)
    {
        if (Signatures.Length == 0)
            throw new SignatureVerificationException("TAP 9: Metadata must be signed");
            
        // Verify all signatures...
    }
}
```

#### Validation Rules
- **All metadata must have signatures**: No exceptions for any metadata type
- **Signature verification is mandatory**: Cannot skip verification
- **Threshold enforcement**: Must meet minimum signature requirements

### TAP 10: Remove Native Support for Compressed Metadata {#tap-10}

**Status**: ✅ Fully Implemented  
**Specification**: [TAP 10](https://github.com/theupdateframework/taps/blob/master/tap10.md)

TAP 10 removes native compression support from TUF specification.

#### Implementation Details
TUF .NET never implemented native compression support, making it compliant by default:

```csharp
// No compression/decompression in metadata handling
public class Updater
{
    private async Task<Metadata<T>> FetchMetadata<T>(string path)
    {
        var json = await httpClient.GetStringAsync(path);  // Plain JSON only
        return CanonicalJsonSerializer.Deserialize<Metadata<T>>(json);
    }
}
```

#### Rationale
- **Simplicity**: Reduces implementation complexity
- **Security**: Eliminates compression-based attacks (zip bombs, etc.)
- **Clarity**: Clear separation of concerns (transport vs. application layer)

### TAP 12: Improving Client Implementation {#tap-12}

**Status**: ✅ Fully Implemented  
**Specification**: [TAP 12](https://github.com/theupdateframework/taps/blob/master/tap12.md)

TAP 12 provides guidance for robust client implementations.

#### Implementation Details

##### Robust Error Handling
```csharp
public async Task RefreshAsync()
{
    try
    {
        await RefreshTimestamp();
        await RefreshSnapshot();
        await RefreshTargets();
    }
    catch (TufNetworkException) when (retryCount < maxRetries)
    {
        // TAP 12: Implement retry logic for transient errors
        await Task.Delay(GetBackoffDelay(retryCount));
        retryCount++;
        await RefreshAsync();
    }
    catch (TufSignatureException)
    {
        // TAP 12: Don't retry signature failures - potential attack
        throw;
    }
}
```

##### State Management
```csharp
public abstract record TrustedMetadata
{
    // TAP 12: Clear state machine for metadata updates
    public abstract TrustedMetadata UpdateTimestamp(Metadata<Timestamp> timestamp);
    public abstract TrustedMetadata UpdateSnapshot(Metadata<Snapshot> snapshot);
    public abstract TrustedMetadata UpdateTargets(Metadata<Targets> targets);
}
```

##### Security Validation
```csharp
private void ValidateMetadataConsistency(Snapshot snapshot, Targets targets)
{
    // TAP 12: Comprehensive consistency checking
    foreach (var (path, info) in snapshot.Meta)
    {
        if (path.EndsWith("targets.json"))
        {
            ValidateVersion(info.Version, targets.Version);
            ValidateHashes(info.Hashes, CalculateHash(targets));
        }
    }
}
```

## Planned TAPs

### TAP 15: Succinct Roles {#tap-15}

**Status**: 🔄 Planned for v2.0  
**Specification**: [TAP 15](https://github.com/theupdateframework/taps/blob/master/tap15.md)

TAP 15 provides a more scalable approach to delegations for large repositories.

#### Design Considerations
- **Scalability**: Support for millions of delegated targets
- **Performance**: Efficient lookup and verification
- **Backward compatibility**: Coexist with traditional delegations
- **Memory efficiency**: Minimize memory usage for large delegation sets

#### Planned Implementation
```csharp
// Planned API for TAP 15
public record SuccinctRole
{
    public string Name { get; init; }
    public string[] KeyIds { get; init; }
    public int Threshold { get; init; }
    public int BitLength { get; init; }        // TAP 15: Succinct role bit length
    public string[] Prefixes { get; init; }    // TAP 15: Hash prefixes
}
```

### TAP 19: Content Addressable Storage {#tap-19}

**Status**: 🔄 Research Phase  
**Specification**: [TAP 19](https://github.com/theupdateframework/taps/blob/master/tap19.md)

TAP 19 enables content-addressable storage for TUF repositories.

#### Research Areas
- **Storage backends**: Integration with CAS systems (IPFS, Git LFS, etc.)
- **Performance impact**: Overhead of content addressing
- **Caching strategies**: Efficient content retrieval
- **Migration paths**: Transitioning from traditional to CAS repositories

## TAP Implementation Guidelines

### Adding New TAP Support

1. **Research Phase**
   - Study TAP specification thoroughly
   - Analyze impact on existing codebase
   - Design API changes and extensions

2. **Design Phase**
   - Create design document
   - Get community feedback
   - Plan backward compatibility

3. **Implementation Phase**
   - Implement core functionality
   - Add comprehensive tests
   - Update documentation

4. **Testing Phase**
   - Unit tests for new functionality
   - Integration tests with existing features
   - Interoperability tests with other implementations

5. **Documentation Phase**
   - Update API documentation
   - Create usage examples
   - Update this TAP support document

### TAP Compatibility Testing

```csharp
[TestFixture]
public class TapCompatibilityTests
{
    [Test]
    public void Tap4_MultiRepositoryConsensus()
    {
        // Test TAP 4 implementation
        var client = new MultiRepositoryClient(multiRepoConfig);
        Assert.That(client, Is.Not.Null);
        // Additional TAP 4 specific tests
    }
    
    [Test]
    public void Tap5_CustomRoleUrls()
    {
        // Test TAP 5 implementation
        var root = CreateRootWithCustomUrls();
        Assert.That(root.Roles["timestamp"].Url, Is.Not.Null);
    }
}
```

## Community Involvement

### Contributing to TAP Development
- **TUF Community**: Participate in TAP discussions
- **Implementation Feedback**: Share implementation experiences
- **Specification Review**: Review TAP specifications for clarity
- **Interoperability Testing**: Test with other implementations

### Reporting TAP Issues
```markdown
## TAP Implementation Issue

**TAP Number**: TAP 4
**TUF .NET Version**: v1.2.0
**Issue Category**: Implementation Bug

**Description**: 
Multi-repository consensus fails when one repository is unavailable

**Expected Behavior**:
Should continue with remaining repositories per TAP 4 specification

**Actual Behavior**: 
Throws exception and stops processing
```

### TAP Proposal Process
1. **Create TAP proposal** following TUF community guidelines
2. **Implement prototype** in TUF .NET (if applicable)
3. **Test with other implementations**
4. **Gather community feedback**
5. **Refine specification** based on implementation experience

## Resources and References

### TAP Documents
- [TAP Repository](https://github.com/theupdateframework/taps) - All TAP specifications
- [TAP Guidelines](https://github.com/theupdateframework/taps/blob/master/tap-template.md) - How to write TAPs

### Implementation References
- [python-tuf TAPs](https://github.com/theupdateframework/python-tuf/tree/develop/tuf) - Python implementation
- [go-tuf TAPs](https://github.com/theupdateframework/go-tuf) - Go implementation
- [rust-tuf TAPs](https://github.com/awslabs/tough) - Rust implementation

### Community Resources
- [TUF Mailing List](https://groups.google.com/forum/#!forum/theupdateframework)
- [TUF Slack](https://theupdateframework.slack.com/)
- [TUF Community Calls](https://github.com/theupdateframework/community) - Regular community meetings

### Contributing
- [TUF .NET Issues](https://github.com/baronfel/tuf-dotnet/issues) - Report TAP-related issues
- [TUF .NET Discussions](https://github.com/baronfel/tuf-dotnet/discussions) - Discuss TAP implementations
- [Contributing Guide](../../CONTRIBUTING.md) - How to contribute to TUF .NET