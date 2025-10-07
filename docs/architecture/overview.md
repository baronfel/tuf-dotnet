# TUF .NET Architecture Overview

This document provides a high-level overview of the TUF .NET architecture, design decisions, and how the components work together to provide secure software updates.

## Design Principles

### Security First
- **Defense in depth**: Multiple layers of validation and verification
- **Fail-safe defaults**: Secure behavior by default, explicit opt-in for less secure options
- **Clear security boundaries**: Well-defined trust boundaries between components
- **Cryptographic agility**: Support for multiple algorithms with secure defaults

### Performance Optimized
- **AOT compilation support**: All code is Native AOT compatible
- **Memory efficient**: Minimal allocations using `Span<T>` and `Memory<T>`
- **Streaming operations**: Large files processed without loading into memory
- **Caching strategies**: Intelligent metadata and HTTP caching

### .NET Integration
- **Idiomatic APIs**: Follows .NET conventions and patterns
- **Async/await throughout**: Full asynchronous operation support
- **Configuration patterns**: Uses .NET configuration and dependency injection patterns
- **Logging integration**: Built-in support for `Microsoft.Extensions.Logging`

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Client Application                       │
├─────────────────────────────────────────────────────────────────┤
│                    TUF .NET Public API                         │
├─────────────────┬─────────────────┬─────────────────────────────┤
│     Updater     │ Repository     │    Multi-Repository        │
│   (Client API)  │   Builder      │      Client               │
├─────────────────┼─────────────────┼─────────────────────────────┤
│           TrustedMetadata         │       Signing APIs         │
│        (Core Security)           │   (Cryptographic Ops)      │
├─────────────────────────────────────┬─────────────────────────────┤
│           Metadata Models          │      Canonical JSON        │
│     (Root, Targets, etc.)         │       Serialization        │
├─────────────────────────────────────┴─────────────────────────────┤
│                    HTTP Transport Layer                          │
└─────────────────────────────────────────────────────────────────┘
```

## Core Components

### 1. Metadata Models (`TUF/` namespace)
The foundation layer providing type-safe representations of TUF metadata:

- **`Root`**: Root metadata containing trusted keys and role definitions
- **`Timestamp`**: Timestamp metadata for freshness guarantee
- **`Snapshot`**: Snapshot metadata for consistency guarantee  
- **`Targets`**: Targets metadata containing file information and delegations
- **`Metadata<T>`**: Generic wrapper providing signature verification and serialization

**Key Design Decisions:**
- Generic `Metadata<T>` wrapper ensures consistent signature handling
- Immutable record types for thread safety
- Serde.NET for canonical JSON serialization control

### 2. Trusted Metadata Management (`TrustedMetadata.cs`)
Central security component managing the trusted metadata state:

```csharp
public abstract record TrustedMetadata
{
    public static PartialTrustedMetadata BootstrapTrusted(byte[] trustedRootBytes)
    public abstract TrustedMetadata UpdateRoot(Metadata<Root> newRoot)
    public abstract TrustedMetadata UpdateTimestamp(Metadata<Timestamp> timestamp)
    // ... other update methods
}
```

**Security Features:**
- **State machine pattern**: Enforces correct metadata update order
- **Signature verification**: All metadata changes verified against current trusted keys
- **Version validation**: Prevents rollback attacks
- **Expiration checks**: Automatic expiry validation

### 3. Client API (`Updater.cs`)
High-level client API implementing the complete TUF workflow:

```csharp
public class Updater
{
    public async Task RefreshAsync()                    // Complete metadata refresh
    public async Task<(string, TargetFile)?> GetTargetInfo(string path)
    public async Task<(string, byte[])> DownloadTarget(TargetFile target, string path)
    public Dictionary<string, TargetFile> GetTopLevelTargets()
}
```

**Workflow Implementation:**
1. **Bootstrap**: Initialize from trusted root metadata
2. **Refresh**: Download and verify timestamp → snapshot → targets
3. **Search**: Find target through delegation tree
4. **Download**: Secure download with integrity verification

### 4. Repository Management (`Repository/RepositoryBuilder.cs`)
APIs for creating and managing TUF repositories:

- Repository creation and initialization
- Metadata generation and signing
- Target file management
- Key rotation workflows

### 5. Multi-Repository Support (`MultiRepositoryClient.cs`)
Implementation of TAP 4 (multi-repository consensus):

- **Consensus mechanism**: Aggregate decisions across multiple repositories
- **Policy enforcement**: Configurable consensus policies
- **Failure isolation**: Continue with partial repository failures

### 6. Cryptographic Layer (`Signing.cs`)
Secure cryptographic operations:

```csharp
public interface ISigner
{
    Task<Signature> SignAsync(ReadOnlyMemory<byte> data);
}

public static class Signing
{
    public static bool VerifySignature(Key key, ReadOnlyMemory<byte> data, Signature signature)
    // ... other verification methods
}
```

**Algorithm Support:**
- **Ed25519**: EdDSA signatures (preferred)
- **RSA-PSS**: RSA with PSS padding 
- **ECDSA**: P-256 curve
- **Extensible**: Interface-based design for new algorithms

### 7. Canonical JSON (`CanonicalJson/`)
Deterministic JSON serialization for signature consistency:

- **Lexicographic ordering**: Consistent property ordering
- **Minimal escaping**: Only required characters escaped
- **Deterministic output**: Same input always produces identical JSON

## Data Flow Architecture

### Metadata Refresh Flow

```
Client App
    │
    ▼
┌─────────────┐    HTTP     ┌─────────────────┐
│   Updater   │◄───────────►│ TUF Repository  │
└─────────────┘             └─────────────────┘
    │
    ▼
┌─────────────┐    verify    ┌─────────────────┐
│TrustedMetadata◄─────────────┤ Signing Module  │
└─────────────┘              └─────────────────┘
    │                               │
    ▼                               ▼
┌─────────────┐              ┌─────────────────┐
│Local Cache  │              │ Canonical JSON  │
└─────────────┘              └─────────────────┘
```

### Target Download Flow

```
Client App
    │ GetTargetInfo
    ▼
┌─────────────┐
│   Updater   │
└─────────────┘
    │ search delegation tree
    ▼
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   Targets   │───►│ Delegation  │───►│   Target    │
│  Metadata   │    │    Tree     │    │    Found    │
└─────────────┘    └─────────────┘    └─────────────┘
    │                                        │
    ▼                                        ▼
┌─────────────┐    HTTP Download      ┌─────────────┐
│TUF Repository◄──────────────────────┤   Updater   │
└─────────────┘                       └─────────────┘
    │                                        │
    ▼                    Hash Verify         ▼
┌─────────────┐◄─────────────────────────────────────┐
│ Target File │                              │
└─────────────┘                              ▼
                                      ┌─────────────┐
                                      │Client App   │
                                      └─────────────┘
```

## Security Architecture

### Trust Model

1. **Root of Trust**: Initial root metadata establishes trusted keys
2. **Delegation Chain**: Trust flows from root → timestamp → snapshot → targets
3. **Threshold Signatures**: Multiple signatures required for critical roles
4. **Key Rotation**: Secure key rotation without breaking trust chain

### Attack Mitigation

| Attack Vector | Mitigation Strategy | Implementation |
|---------------|--------------------|-----------------| 
| **Rollback** | Version checking | `TrustedMetadata` enforces increasing versions |
| **Freeze** | Expiration times | All metadata has expiration validation |
| **Mix-and-match** | Consistent snapshots | Snapshot metadata ensures consistency |
| **Wrong signer** | Signature verification | All signatures verified against trusted keys |
| **Threshold violation** | Signature counting | Minimum signature thresholds enforced |
| **Slow retrieval** | Download timeouts | HTTP client timeouts and cancellation |

### Cryptographic Boundaries

```
┌─────────────────────────────────────────────────────────────┐
│                    Trusted Boundary                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │    Root     │  │ Trusted     │  │   Signature         │  │
│  │  Metadata   │  │ Metadata    │  │  Verification       │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                    Network Boundary                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │   HTTP      │  │   JSON      │  │    Downloaded       │  │
│  │ Transport   │  │ Metadata    │  │     Files           │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## Performance Architecture

### Memory Management
- **Streaming I/O**: Large files processed in chunks
- **Object pooling**: Reuse of buffers and objects
- **Span<T> usage**: Stack allocation for temporary data
- **Minimal boxing**: Value types kept on stack

### Caching Strategy
- **Metadata caching**: Local filesystem caching of metadata
- **HTTP caching**: Respect HTTP cache headers
- **Signature verification caching**: Cache verification results
- **Target file caching**: Optional local target caching

### Concurrency Model
- **Thread-safe reads**: Multiple concurrent read operations
- **Single-writer**: Metadata updates are single-threaded
- **Async I/O**: Non-blocking network operations
- **Cancellation support**: Graceful operation cancellation

## Integration Points

### .NET Ecosystem Integration

#### Dependency Injection
```csharp
// Service registration
services.AddSingleton<UpdaterConfig>(config);
services.AddSingleton<Updater>();

// Usage
public MyService(Updater updater) { }
```

#### Configuration System
```csharp
// appsettings.json integration
services.Configure<UpdaterOptions>(configuration.GetSection("TUF"));
```

#### Logging Integration
```csharp
// Structured logging throughout
logger.LogInformation("Metadata refreshed successfully. Version: {Version}", version);
```

### External Dependencies

#### Required Dependencies
- **Serde.NET**: JSON serialization control
- **NSec.Cryptography**: Ed25519 cryptographic operations
- **Microsoft.Extensions.FileSystemGlobbing**: File pattern matching

#### Optional Dependencies
- **Microsoft.Extensions.Logging**: Logging integration
- **Microsoft.Extensions.DependencyInjection**: DI integration
- **Microsoft.Extensions.Configuration**: Configuration binding

## Extension Points

### Custom Implementations
- **`ISigner`**: Custom signing implementations (HSM, cloud KMS)
- **HTTP handlers**: Custom transport (proxies, authentication)
- **File storage**: Custom target storage backends
- **Metadata storage**: Custom metadata persistence

### Plugin Architecture
The design supports extension through:
- Interface implementations
- Configuration injection
- Event-based notifications
- Custom validation policies

## Deployment Considerations

### Native AOT Support
- All code paths are AOT-compatible
- No reflection or dynamic code generation
- Minimal startup time and memory footprint

### Container Deployment
- Minimal base images supported
- Health check endpoints
- Environment variable configuration
- Volume mount patterns

### Cloud Integration
- Support for cloud key management (Azure Key Vault, AWS KMS)
- Blob storage backends (Azure Blob, S3)
- CDN integration patterns
- Auto-scaling considerations

## Next Steps

For deeper understanding of specific components:
- [Metadata Processing](metadata-processing.md) - How metadata is processed and validated
- [Cryptographic Design](cryptography.md) - Cryptographic implementation details  
- [Performance Considerations](performance.md) - Performance optimization strategies

For implementation guidance:
- [Building Clients](../guides/building-clients.md) - Client implementation guide
- [Creating Repositories](../guides/creating-repositories.md) - Repository setup
- [Integration Patterns](../guides/integration-patterns.md) - Common integration scenarios