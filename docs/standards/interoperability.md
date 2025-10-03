# Cross-Language Interoperability

This document describes the interoperability testing and compatibility matrix for TUF .NET with other TUF implementations.

## Overview

Cross-language interoperability is critical for TUF adoption, as different parts of a software supply chain may use different TUF implementations. TUF .NET is designed to be fully compatible with repositories created by other implementations and vice versa.

## Compatibility Matrix

### Repository Compatibility

| Repository Created By | Can Be Read By .NET TUF | Can Be Read By Python TUF | Can Be Read By Go TUF | Notes |
|----------------------|-------------------------|---------------------------|---------------------|--------|
| **TUF .NET**         | ✅ Yes                   | ✅ Yes                     | ✅ Yes               | Full compatibility |
| **Python-TUF**      | ✅ Yes                   | ✅ Yes                     | ✅ Yes               | Reference implementation |
| **Go-TUF v2**       | ✅ Yes                   | ✅ Yes                     | ✅ Yes               | Production ready |
| **TUF-JS**           | ✅ Yes                   | ✅ Yes                     | ✅ Yes               | GitHub-maintained |

### Signature Algorithm Compatibility

| Algorithm | TUF .NET | Python-TUF | Go-TUF | Notes |
|-----------|----------|------------|---------|-------|
| **Ed25519** | ✅ Supported | ✅ Supported | ✅ Supported | Recommended algorithm |
| **ECDSA P-256** | ✅ Supported | ✅ Supported | ✅ Supported | Wide compatibility |
| **RSA-PSS** | ✅ Supported | ✅ Supported | ✅ Supported | Legacy support |
| **RSA-PKCS1v15** | ❌ Not supported | ⚠️ Deprecated | ⚠️ Deprecated | Security concerns |

### Metadata Format Compatibility

| Feature | TUF .NET | Python-TUF | Go-TUF | Compatibility Notes |
|---------|----------|------------|---------|-------------------|
| **Canonical JSON** | ✅ Full support | ✅ Reference | ✅ Full support | RFC 7159 compliant |
| **TUF Spec v1.0.31** | ✅ Implemented | ✅ Reference | ✅ Implemented | Latest stable spec |
| **Consistent Snapshots** | ✅ Supported | ✅ Supported | ✅ Supported | TAP 2 |
| **Delegations** | ✅ Supported | ✅ Supported | ✅ Supported | Full hierarchy support |
| **Multi-Repository** | ✅ Supported | ❌ Not supported | ⚠️ Limited | TAP 4 implementation |

## Testing Approach

### Automated Testing

The TUF .NET implementation includes comprehensive interoperability tests:

1. **Repository Cross-Reading Tests**
   - .NET client reads Python-generated repositories
   - .NET client reads Go-generated repositories
   - Python client reads .NET-generated repositories
   - Go client reads .NET-generated repositories

2. **Signature Verification Tests**
   - Cross-verification of Ed25519 signatures
   - Cross-verification of ECDSA signatures
   - Cross-verification of RSA signatures

3. **Metadata Format Tests**
   - JSON serialization/deserialization compatibility
   - Canonical JSON format verification
   - Key ID calculation consistency

### Test Infrastructure

```bash
# Run interoperability tests
dotnet test TUF.InteroperabilityTests

# Generate Python test fixtures
python3 test-fixtures/scripts/generate_python_fixtures.py ./fixtures/python-repo

# Generate Go test fixtures  
go run test-fixtures/scripts/generate_go_fixtures.go ./fixtures/go-repo

# Test external client compatibility
python3 test-fixtures/scripts/test_python_can_read_dotnet.py ./dotnet-repo
go run test-fixtures/scripts/test_go_can_read_dotnet.go ./dotnet-repo
```

## Key Interoperability Features

### 1. Canonical JSON Implementation

TUF .NET uses a canonical JSON implementation that produces byte-identical output to other TUF implementations:

```csharp
// Canonical JSON serialization
var canonicalBytes = CanonicalJson.Serializer.Serialize(metadata);

// Key ID calculation (must match across implementations)
var keyId = key.GetKeyId(); // SHA-256 of canonical JSON
```

### 2. Signature Algorithms

All signature algorithms use standard cryptographic libraries and formats:

```csharp
// Ed25519 (recommended)
var ed25519Signer = Ed25519Signer.Generate();
var signature = ed25519Signer.SignBytes(data);

// ECDSA P-256
var ecdsaSigner = EcdsaSigner.Generate();
var signature = ecdsaSigner.SignBytes(data);

// RSA-PSS
var rsaSigner = RsaSigner.Generate(2048);
var signature = rsaSigner.SignBytes(data);
```

### 3. Metadata Structure

TUF .NET follows the TUF specification exactly for metadata structure:

```csharp
// Root metadata compatible with all implementations
var root = new Root
{
    Type = "root",
    Version = 1,
    SpecVersion = "1.0.31",
    Expires = DateTimeOffset.UtcNow.AddYears(1),
    Keys = keyDict,
    Roles = rolesDict
};
```

## Known Compatibility Issues

### Minor Differences

1. **Timestamp Precision**
   - TUF .NET: Microsecond precision in ISO 8601 format
   - Python-TUF: May vary based on system clock precision
   - **Impact**: None - all implementations handle various precisions

2. **JSON Property Ordering**
   - TUF .NET: Lexicographic ordering (per canonical JSON spec)
   - Some implementations: May use different ordering
   - **Impact**: None - signatures are calculated on canonical JSON

### Resolved Issues

1. **Key ID Calculation** ✅ Resolved
   - **Issue**: Different canonical JSON implementations produced different key IDs
   - **Resolution**: Updated to RFC 7159 compliant canonical JSON
   - **Status**: All implementations now produce identical key IDs

2. **Date Format Handling** ✅ Resolved
   - **Issue**: Different date format parsing between implementations
   - **Resolution**: Strict ISO 8601 format with timezone handling
   - **Status**: Full compatibility across implementations

## Testing with Real-World Repositories

### PyPI Repository (Python Package Index)

```csharp
// Test against PyPI's TUF repository
var config = new UpdaterConfig
{
    RemoteMetadataUrl = new Uri("https://pypi.org/simple-repository-metadata/"),
    RemoteTargetsUrl = new Uri("https://files.pythonhosted.org/"),
    // ... other config
};

var updater = new Updater(config);
await updater.RefreshAsync(); // Should work with PyPI's Python-generated repo
```

### Docker/Notary Repositories

```csharp
// Test against Docker's Notary TUF repositories  
var config = new UpdaterConfig
{
    RemoteMetadataUrl = new Uri("https://notary.docker.io/v2/docker.io/library/ubuntu/metadata/"),
    // ... other config
};
```

## Contributing to Interoperability

### Adding New Test Cases

1. Create test repositories using other implementations
2. Add verification tests in `CrossLanguageInteroperabilityTests.cs`
3. Update the compatibility matrix in this document
4. Run tests against multiple TUF implementations

### Reporting Compatibility Issues

When reporting interoperability issues, please include:

1. **TUF .NET version** and target framework
2. **Other implementation** name and version
3. **Test repository** or minimal reproduction case
4. **Expected vs actual behavior**
5. **Error messages** and stack traces
6. **Metadata samples** (if relevant)

### Best Practices for Compatibility

1. **Follow TUF Specification** - Stick closely to the official TUF specification
2. **Use Standard Algorithms** - Prefer widely-supported cryptographic algorithms
3. **Test Cross-Implementation** - Always test with other TUF implementations
4. **Validate Canonical JSON** - Ensure byte-identical canonical JSON output
5. **Check Key ID Calculation** - Verify key IDs match across implementations

## Future Interoperability Work

### Planned Improvements

1. **TUF Specification Updates**
   - Track and implement new TUF specification versions
   - Participate in TUF specification discussions
   - Contribute .NET-specific insights to the specification

2. **Extended Algorithm Support**
   - Additional signature algorithms as they become standardized
   - New hash algorithms for improved security
   - Quantum-resistant algorithms (when specified)

3. **Performance Benchmarking**
   - Cross-implementation performance comparisons
   - Optimization based on real-world usage patterns
   - Memory and CPU usage analysis

### Community Contributions

We welcome contributions to improve interoperability:

- Submit test cases with other TUF implementations
- Report compatibility issues you discover
- Contribute fixes for interoperability problems
- Help maintain the compatibility matrix

## Resources

- **[TUF Specification](https://theupdateframework.github.io/specification/)** - Official specification
- **[TUF Implementations](https://theupdateframework.io/implementations/)** - List of all implementations  
- **[Python-TUF](https://github.com/theupdateframework/python-tuf)** - Reference implementation
- **[Go-TUF](https://github.com/theupdateframework/go-tuf)** - Go implementation
- **[TUF-JS](https://github.com/theupdateframework/tuf-js)** - JavaScript implementation
- **[TUF Conformance](https://github.com/theupdateframework/tuf-conformance)** - Official conformance tests