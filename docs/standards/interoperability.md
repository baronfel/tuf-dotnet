# Interoperability with Other TUF Implementations

TUF .NET is designed to be fully interoperable with other TUF implementations in Python, Go, Rust, and JavaScript. This document details compatibility matrices, testing procedures, and integration patterns.

## Overview

Interoperability ensures that:
- **TUF .NET clients** can consume repositories created by other implementations
- **Other TUF clients** can consume repositories created by TUF .NET
- **Metadata formats** are consistent across implementations
- **Cryptographic signatures** verify correctly across implementations

## Cross-Implementation Compatibility Matrix

### Repository Compatibility

| Creator Implementation | TUF .NET Client | python-tuf Client | go-tuf Client | rust-tuf Client | tuf-js Client |
|------------------------|-----------------|-------------------|---------------|----------------|---------------|
| **TUF .NET** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| **python-tuf** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| **go-tuf** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| **rust-tuf** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| **tuf-js** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full |

### Feature Compatibility

| Feature | TUF .NET | python-tuf | go-tuf | rust-tuf | tuf-js |
|---------|----------|------------|--------|----------|--------|
| **Ed25519 signatures** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **RSA-PSS signatures** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **ECDSA signatures** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Delegations** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Consistent snapshots** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Multi-repository (TAP 4)** | ✅ | ❌ | ✅ | ❌ | ❌ |
| **Succinct roles (TAP 15)** | 🔄 Planned | ❌ | ❌ | ❌ | ❌ |

## Interoperability Testing

### Automated Testing Framework

TUF .NET includes comprehensive interoperability tests in `TUF.InteroperabilityTests`:

```csharp
[Test]
public async Task CanReadPythonGeneratedRepository()
{
    // Test TUF .NET client reading python-tuf repository
    var updater = new Updater(config);
    await updater.RefreshAsync();
    var targets = updater.GetTopLevelTargets();
    Assert.That(targets, Is.Not.Empty);
}

[Test] 
public async Task PythonCanReadDotNetRepository()
{
    // Test python-tuf client reading TUF .NET repository
    var result = await RunPythonTufClient(repositoryPath);
    Assert.That(result.ExitCode, Is.EqualTo(0));
}
```

### Test Infrastructure

#### Repository Generation Scripts
Each implementation generates test repositories:
- **Python**: `scripts/generate_python_repo.py`
- **Go**: `scripts/generate_go_repo.go`
- **Rust**: `scripts/generate_rust_repo.rs`
- **JavaScript**: `scripts/generate_js_repo.js`

#### Cross-Validation Scripts
Validation scripts test each implementation against all others:
- **Python validator**: `scripts/validate_with_python.py`
- **Go validator**: `scripts/validate_with_go.go`
- **Rust validator**: `scripts/validate_with_rust.rs`

### Real-World Test Results

#### Go TUF Validation Example
```bash
$ go run validate_with_go.go ./dotnet-generated-repo
Testing Go TUF with .NET repository
✅ Successfully loaded root.json (1566 bytes)
✅ Successfully parsed root metadata using Go JSON
   - Type: root, Version: 1, Keys: 4, Signatures: 1
✅ Found 4 metadata files, 1 target files
🎉 SUCCESS: Go can read .NET TUF repository
```

#### Python TUF Validation Example
```bash
$ python validate_with_python.py ./dotnet-generated-repo
Loading TUF .NET generated repository...
✅ Root metadata loaded and verified
✅ Timestamp metadata verified  
✅ Snapshot metadata verified
✅ Targets metadata verified
✅ Target file integrity verified
🎉 SUCCESS: Python TUF can fully validate .NET repository
```

## JSON Serialization Compatibility

### Canonical JSON Compliance
All implementations must produce identical canonical JSON:

```csharp
// TUF .NET canonical JSON
{
  "_type": "root",
  "consistent_snapshot": true,
  "expires": "2025-12-31T23:59:59Z",
  "keys": {
    "key1": { "keytype": "ed25519", ... }
  },
  "roles": {
    "root": { "keyids": ["key1"], "threshold": 1 }
  },
  "spec_version": "1.0.32",
  "version": 1
}
```

### Cross-Implementation JSON Validation
```csharp
[Test]
public void CanonicalJsonMatchesOtherImplementations()
{
    var dotnetJson = CanonicalJsonSerializer.Serialize(rootMetadata);
    var pythonJson = LoadExpectedPythonJson();
    var goJson = LoadExpectedGoJson();
    
    Assert.That(dotnetJson, Is.EqualTo(pythonJson));
    Assert.That(dotnetJson, Is.EqualTo(goJson));
}
```

## Signature Interoperability

### Cross-Implementation Signature Verification

#### Ed25519 Signature Testing
```csharp
[Test]
public void Ed25519SignatureVerifiesAcrossImplementations()
{
    // Signature created by python-tuf
    var pythonSignature = LoadPythonEd25519Signature();
    var publicKey = LoadEd25519PublicKey();
    var data = LoadSignedData();
    
    // Verify using TUF .NET
    var isValid = Signing.VerifySignature(publicKey, data, pythonSignature);
    Assert.That(isValid, Is.True);
}
```

#### RSA Signature Testing  
```csharp
[Test]
public void RsaSignatureVerifiesAcrossImplementations()
{
    // Signature created by go-tuf
    var goSignature = LoadGoRsaSignature();
    var publicKey = LoadRsaPublicKey();
    var data = LoadSignedData();
    
    // Verify using TUF .NET
    var isValid = Signing.VerifySignature(publicKey, data, goSignature);
    Assert.That(isValid, Is.True);
}
```

### Key Format Compatibility

#### Ed25519 Key Format
```json
{
  "keytype": "ed25519",
  "scheme": "ed25519",
  "keyval": {
    "public": "01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b"
  }
}
```
✅ **Compatible** with all implementations

#### RSA Key Format
```json
{
  "keytype": "rsa", 
  "scheme": "rsassa-pss-sha256",
  "keyval": {
    "public": "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEF...\n-----END PUBLIC KEY-----"
  }
}
```
✅ **Compatible** with all implementations

#### ECDSA Key Format
```json
{
  "keytype": "ecdsa",
  "scheme": "ecdsa-sha2-nistp256", 
  "keyval": {
    "public": "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZI...\n-----END PUBLIC KEY-----"
  }
}
```
✅ **Compatible** with all implementations

## Delegation Interoperability

### Path-Based Delegations
```json
{
  "delegations": {
    "keys": { /* delegation keys */ },
    "roles": [
      {
        "name": "apps",
        "keyids": ["delegation-key-1"],
        "threshold": 1,
        "paths": ["apps/*", "tools/*"],
        "terminating": false
      }
    ]
  }
}
```

### Hash-Based Delegations
```json
{
  "delegations": {
    "keys": { /* delegation keys */ },
    "roles": [
      {
        "name": "bin-0",
        "keyids": ["delegation-key-1"], 
        "threshold": 1,
        "path_hash_prefixes": ["00", "01", "02", "03"],
        "terminating": false
      }
    ]
  }
}
```

### Cross-Implementation Delegation Testing
```csharp
[Test]
public async Task DelegationWorksAcrossImplementations()
{
    // Repository with delegations created by python-tuf
    var updater = new Updater(pythonRepoConfig);
    await updater.RefreshAsync();
    
    // Find delegated target
    var targetInfo = await updater.GetTargetInfo("apps/myapp.exe");
    Assert.That(targetInfo.HasValue, Is.True);
    
    // Verify target is found through delegation
    var (path, target) = targetInfo.Value;
    Assert.That(path, Is.EqualTo("apps/myapp.exe"));
}
```

## Multi-Repository Interoperability

TUF .NET's multi-repository support (TAP 4) is compatible with go-tuf's implementation:

### Configuration Compatibility
```json
{
  "repositories": [
    {
      "name": "primary", 
      "metadata_url": "https://repo1.example.com/metadata/",
      "targets_url": "https://repo1.example.com/targets/",
      "weight": 1.0
    },
    {
      "name": "mirror",
      "metadata_url": "https://repo2.example.com/metadata/", 
      "targets_url": "https://repo2.example.com/targets/",
      "weight": 0.5
    }
  ],
  "consensus_policy": "majority"
}
```

### Cross-Implementation Multi-Repository Testing
```csharp
[Test]
public async Task MultiRepositoryWorksWithGoTuf()
{
    // Multi-repository setup compatible with go-tuf
    var client = new MultiRepositoryClient(multiRepoConfig);
    await client.RefreshAsync();
    
    var targets = client.GetAvailableTargets();
    Assert.That(targets, Is.Not.Empty);
}
```

## Known Compatibility Issues

### Resolved Issues
| Issue | Affected Implementations | Resolution | Version Fixed |
|-------|-------------------------|------------|---------------|
| **Consistent snapshot URL format** | python-tuf, TUF .NET | Standardized URL construction | v1.2.0 |
| **Delegation path normalization** | go-tuf, TUF .NET | Consistent path handling | v1.1.5 |
| **RSA signature padding** | rust-tuf, TUF .NET | PKCS#1 v2.1 PSS standard | v1.0.8 |

### Current Limitations
| Limitation | Impact | Workaround | Timeline |
|------------|---------|------------|----------|
| **TAP 15 (Succinct Roles)** | Future compatibility | Not yet implemented | Planned v2.0 |
| **Custom signature schemes** | Plugin signatures | Interface extension | On demand |

## Testing Procedures

### Local Interoperability Testing

#### Setup Test Environment
```bash
# Install all TUF implementations
pip install python-tuf
go install github.com/theupdateframework/go-tuf/cmd/tuf-client
cargo install tough-cli
npm install -g @tufjs/cli

# Run interoperability test suite
dotnet test TUF.InteroperabilityTests
```

#### Generate Cross-Implementation Repositories
```bash
# Generate repositories with each implementation
./scripts/generate-all-repos.sh

# Validate each repository with all clients
./scripts/validate-all-combinations.sh
```

### Continuous Integration Testing
GitHub Actions workflow validates interoperability:

```yaml
name: Interoperability Testing
on: [push, pull_request]

jobs:
  cross-implementation:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        implementation: [python-tuf, go-tuf, rust-tough]
    steps:
      - uses: actions/checkout@v4
      - name: Setup ${{ matrix.implementation }}
        run: ./scripts/setup-${{ matrix.implementation }}.sh
      - name: Test interoperability
        run: dotnet test TUF.InteroperabilityTests --filter Category=CrossImplementation
```

### Manual Testing Procedures

#### Repository Round-Trip Testing
1. **Create repository** with TUF .NET
2. **Validate repository** with python-tuf client  
3. **Add new target** with go-tuf tools
4. **Refresh metadata** with TUF .NET client
5. **Verify signatures** across all implementations

#### Signature Verification Testing
1. **Generate signatures** with each implementation
2. **Cross-verify signatures** with all other implementations
3. **Test signature failure scenarios**
4. **Validate error handling** consistency

## Best Practices for Interoperability

### Repository Creation
```csharp
// Use standard configuration for maximum compatibility
var config = new RepositoryBuilderConfig
{
    // Use widely supported algorithms
    SigningAlgorithm = "ed25519",
    HashAlgorithm = "sha256",
    
    // Enable consistent snapshots for CDN compatibility
    ConsistentSnapshot = true,
    
    // Use conservative metadata size limits
    MetadataMaxLength = 1024 * 1024, // 1MB
    
    // Standard expiration periods
    RootExpiration = TimeSpan.FromDays(365),
    TimestampExpiration = TimeSpan.FromHours(1),
    SnapshotExpiration = TimeSpan.FromDays(7),
    TargetsExpiration = TimeSpan.FromDays(30)
};
```

### Client Configuration  
```csharp
// Configure client for maximum compatibility
var clientConfig = new UpdaterConfig
{
    // Conservative timeout values
    HttpTimeout = TimeSpan.FromSeconds(30),
    
    // Standard security limits  
    MaxRootRotations = 32,
    MaxDelegations = 32,
    
    // Enable caching for performance
    DisableLocalCache = false,
    
    // Use standard URL patterns
    PrefixTargetsWithHash = true
};
```

### Testing Recommendations
- **Always test** new features with other implementations
- **Use canonical examples** from TUF specification
- **Validate edge cases** across implementations
- **Document compatibility** issues and workarounds
- **Contribute back** fixes to upstream implementations

## Reporting Compatibility Issues

### Issue Classification
| Priority | Description | Response Time |
|----------|-------------|---------------|
| **P0 - Critical** | Repository corruption, security bypass | 24 hours |
| **P1 - High** | Feature incompatibility, signature failures | 1 week |
| **P2 - Medium** | Performance issues, edge cases | 2 weeks |
| **P3 - Low** | Documentation, minor inconsistencies | 1 month |

### Issue Reporting Template
```markdown
## Interoperability Issue Report

**Affected Implementations:** TUF .NET v1.2.3, python-tuf v4.0.0
**Category:** Signature verification
**Priority:** P1 - High

**Description:**
RSA signatures created by python-tuf fail verification in TUF .NET

**Reproduction:**
1. Generate RSA signature with python-tuf
2. Attempt verification with TUF .NET
3. Observe verification failure

**Expected Behavior:**
Signature should verify successfully

**Actual Behavior:**
SignatureVerificationException thrown

**Test Case:**
[Provide minimal reproduction case]
```

### Contact Information
- **GitHub Issues**: [tuf-dotnet Issues](https://github.com/baronfel/tuf-dotnet/issues)
- **TUF Community**: [TUF Mailing List](https://groups.google.com/forum/#!forum/theupdateframework)
- **TUF Slack**: [theupdateframework.slack.com](https://theupdateframework.slack.com/)

## Resources and References

- [TUF Specification](https://theupdateframework.github.io/specification/latest/)
- [TUF Conformance Tests](https://github.com/theupdateframework/tuf-conformance)  
- [python-tuf](https://github.com/theupdateframework/python-tuf)
- [go-tuf](https://github.com/theupdateframework/go-tuf)
- [rust-tuf (tough)](https://github.com/awslabs/tough)
- [tuf-js](https://github.com/theupdateframework/tuf-js)
- [TAP 4: Multiple Repository Consensus](https://github.com/theupdateframework/taps/blob/master/tap4.md)