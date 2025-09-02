# TUF .NET Security Model

This document provides a comprehensive overview of the security model implemented in TUF .NET, including threat mitigation strategies, security guarantees, and implementation details.

## Overview

The TUF (The Update Framework) security model is based on the principle of **minimizing trust** and **distributing risk** across multiple cryptographic keys and metadata roles. TUF .NET implements the complete TUF security specification with additional .NET-specific security considerations.

## Core Security Principles

### 1. Role Separation
TUF distributes security responsibilities across four primary roles, each with specific keys and responsibilities:

#### Root Role
- **Trust anchor** - Foundation of all security decisions
- **Offline keys** - Stored securely offline, used infrequently
- **Key rotation** - Can rotate keys for all other roles
- **Limited scope** - Only signs metadata about other roles, not target files

#### Timestamp Role  
- **Freshness guarantee** - Prevents freeze attacks
- **Online keys** - May be stored online for automated signing
- **Frequent updates** - Signatures updated regularly (hourly/daily)
- **Short expiration** - Metadata expires quickly to ensure freshness

#### Snapshot Role
- **Consistency guarantee** - Prevents mix-and-match attacks
- **Version tracking** - Records versions of all other metadata
- **Automated signing** - Can be signed automatically
- **Integrity protection** - Ensures metadata consistency across updates

#### Targets Role
- **Authorization** - Specifies which files are legitimate
- **Delegation support** - Can delegate signing authority to other keys
- **File metadata** - Contains hashes, sizes, and custom metadata
- **Flexible expiration** - Can have longer expiration times

### 2. Threshold Cryptography
Each role can require multiple signatures (threshold signing):

```csharp
// Example: Require 2 of 3 root signatures
var rootRole = new Role
{
    KeyIds = ["key1", "key2", "key3"],
    Threshold = 2
};
```

**Security Benefits:**
- **Key compromise resilience** - Multiple keys must be compromised
- **Operational flexibility** - Operations can continue if some keys are unavailable
- **Gradual key rotation** - Keys can be rotated individually

### 3. Key Compromise Recovery
TUF provides mechanisms to recover from key compromise:

#### Root Key Compromise
- New root metadata is signed with new keys
- Old root metadata references new keys
- Clients can verify the transition using existing trusted keys

#### Other Role Key Compromise
- Root role can rotate compromised keys
- New metadata is signed with new keys
- Compromised keys are immediately revoked

## Attack Prevention

### Repository Compromise Attacks

**Attack**: Attacker gains control of the update repository server.

**TUF Mitigation**:
- **Offline root keys** prevent attacker from creating new trusted metadata
- **Role separation** limits what can be compromised with server access
- **Signature verification** ensures clients reject unsigned content

**TUF .NET Implementation**:
```csharp
public async Task RefreshAsync()
{
    // Always verify signatures before trusting any metadata
    var timestamp = await FetchAndVerifyTimestamp();
    var snapshot = await FetchAndVerifySnapshot(timestamp);
    var targets = await FetchAndVerifyTargets(snapshot);
}
```

### Man-in-the-Middle Attacks

**Attack**: Network attacker intercepts and modifies update traffic.

**TUF Mitigation**:
- **Digital signatures** ensure metadata authenticity
- **Hash verification** ensures target file integrity
- **HTTPS transport** provides additional network-level protection

**TUF .NET Implementation**:
```csharp
private void VerifyTargetFile(TargetFile expectedTarget, byte[] data)
{
    // Verify file length
    if (data.Length != expectedTarget.Length)
        throw new TufIntegrityException("File length mismatch");
    
    // Verify all hashes
    foreach (var (algorithm, expectedHash) in expectedTarget.Hashes)
    {
        var actualHash = ComputeHash(algorithm, data);
        if (!actualHash.SequenceEqual(expectedHash))
            throw new TufIntegrityException($"{algorithm} hash mismatch");
    }
}
```

### Rollback Attacks

**Attack**: Attacker serves old, vulnerable versions of software.

**TUF Mitigation**:
- **Version numbers** must increase monotonically
- **Timestamp metadata** provides freshness guarantees
- **Client-side version tracking** detects rollback attempts

**TUF .NET Implementation**:
```csharp
private void ValidateMetadataVersion<T>(Metadata<T> newMetadata, Metadata<T>? currentMetadata)
    where T : ITufMetadata<T>
{
    if (currentMetadata != null && newMetadata.Signed.Version <= currentMetadata.Signed.Version)
    {
        throw new TufRollbackException(
            $"Metadata version decreased from {currentMetadata.Signed.Version} to {newMetadata.Signed.Version}");
    }
}
```

### Key Compromise Attacks

**Attack**: Attacker obtains signing keys through various means.

**TUF Mitigation**:
- **Threshold signatures** require multiple key compromise
- **Role separation** limits impact of any single key compromise
- **Key rotation** enables recovery from compromise

**TUF .NET Implementation**:
```csharp
private bool VerifyRoleSignatures(Metadata<T> metadata, Role role, Dictionary<string, TufKey> keys)
{
    var validSignatures = 0;
    
    foreach (var signature in metadata.Signatures)
    {
        if (keys.TryGetValue(signature.KeyId, out var key) && 
            role.KeyIds.Contains(signature.KeyId))
        {
            if (VerifySignature(signature, metadata.Signed, key))
                validSignatures++;
        }
    }
    
    return validSignatures >= role.Threshold;
}
```

### Freeze Attacks

**Attack**: Attacker prevents clients from receiving metadata updates.

**TUF Mitigation**:
- **Timestamp metadata expiration** forces regular updates
- **Short expiration times** limit attack window
- **Client-side expiration checking** detects stale metadata

**TUF .NET Implementation**:
```csharp
private void ValidateExpiration<T>(Metadata<T> metadata) where T : ITufMetadata<T>
{
    if (DateTime.UtcNow > metadata.Signed.Expires)
    {
        throw new TufExpiredException(
            $"Metadata expired on {metadata.Signed.Expires:O}");
    }
}
```

### Mix-and-Match Attacks

**Attack**: Attacker combines legitimate metadata from different time periods.

**TUF Mitigation**:
- **Snapshot metadata** records versions of all other metadata
- **Consistency checking** ensures all metadata versions are compatible
- **Hash verification** prevents unauthorized metadata modification

**TUF .NET Implementation**:
```csharp
private void ValidateSnapshotConsistency(Snapshot snapshot, Metadata<Targets> targets)
{
    if (!snapshot.Meta.TryGetValue("targets.json", out var targetsMeta))
        throw new TufConsistencyException("Targets metadata not found in snapshot");
        
    if (targetsMeta.Version != targets.Signed.Version)
    {
        throw new TufConsistencyException(
            $"Targets version mismatch: snapshot={targetsMeta.Version}, targets={targets.Signed.Version}");
    }
}
```

## Cryptographic Security

### Supported Algorithms

TUF .NET supports multiple cryptographic algorithms with different security/performance tradeoffs:

#### Digital Signatures
- **Ed25519** - Preferred for new implementations (fast, small signatures)
- **RSA-PSS** - Compatibility with existing infrastructure
- **ECDSA** - Balance of security and compatibility

#### Hash Functions
- **SHA-256** - Primary hash algorithm
- **SHA-512** - Additional security for high-value targets
- **Multiple hashes** - Cryptographic agility and redundancy

### Key Security Requirements

#### Key Generation
```csharp
// Ed25519: Cryptographically secure random key generation
var signer = Ed25519Signer.Generate(); // Uses secure RNG

// RSA: Minimum 2048-bit keys, preferably 3072-bit or 4096-bit
var rsaSigner = RsaSigner.Generate(keySize: 3072);
```

#### Key Storage
- **Root keys** - Hardware Security Modules (HSMs) or offline storage
- **Online keys** - Encrypted storage with access controls
- **Key backup** - Secure backup procedures for key recovery

#### Key Rotation
- **Regular rotation** - Keys should be rotated periodically
- **Compromise response** - Immediate rotation if compromise suspected
- **Overlap periods** - Support for gradual key transitions

## Implementation Security Features

### Memory Safety
TUF .NET implements several memory safety features:

```csharp
// Sensitive data is cleared after use
private void ClearSensitiveData(Span<byte> sensitiveData)
{
    CryptographicOperations.ZeroMemory(sensitiveData);
}

// Temporary cryptographic data uses stack allocation
public bool VerifySignature(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
{
    Span<byte> hash = stackalloc byte[32]; // SHA-256 hash size
    if (!SHA256.TryHashData(data, hash, out _))
        return false;
    
    var result = crypto.Verify(hash, signature);
    CryptographicOperations.ZeroMemory(hash);
    return result;
}
```

### Timing Attack Resistance
Cryptographic operations use constant-time algorithms:

```csharp
// Constant-time signature verification
public bool VerifySignature(ReadOnlySpan<byte> message, ReadOnReadOnlySpan<byte> signature)
{
    return CryptographicOperations.FixedTimeEquals(
        expectedSignature, 
        ComputeSignature(message)
    );
}
```

### Input Validation
All external inputs are validated:

```csharp
private void ValidateMetadata<T>(Metadata<T> metadata) where T : ITufMetadata<T>
{
    ArgumentNullException.ThrowIfNull(metadata);
    ArgumentNullException.ThrowIfNull(metadata.Signed);
    
    if (metadata.Signatures.Count == 0)
        throw new TufSignatureException("No signatures found");
        
    if (string.IsNullOrEmpty(metadata.Signed.SpecVersion))
        throw new TufValidationException("Missing spec version");
}
```

## Security Configuration

### Default Security Settings
TUF .NET provides secure defaults:

```csharp
public class UpdaterConfig
{
    // Conservative limits prevent resource exhaustion
    public uint MaxRootRotations { get; init; } = 256;
    public uint MaxDelegations { get; init; } = 32;
    public uint RootMaxLength { get; init; } = 512000;
    public uint TimestampMaxLength { get; init; } = 16384;
    
    // Security-first defaults
    public bool PrefixTargetsWithHash { get; init; } = true;
    public bool DisableLocalCache { get; init; } = false;
}
```

### Security Hardening Options
Additional security can be configured:

```csharp
var config = new UpdaterConfig
{
    // Stricter limits for high-security environments
    MaxRootRotations = 64,
    MaxDelegations = 8,
    
    // Shorter expiration tolerance
    TimestampMaxAge = TimeSpan.FromHours(1),
    
    // Enhanced verification
    RequireAllHashes = true,
    VerifyDelegationPaths = true
};
```

## Compliance and Auditing

### TUF Specification Compliance
TUF .NET implements the complete TUF specification:
- **Version 1.0.0** - Full specification compliance
- **Conformance testing** - Passes official TUF conformance test suite
- **Interoperability** - Compatible with python-tuf, go-tuf, and other implementations

### Audit Trail
All security-relevant operations are logged:

```csharp
public async Task RefreshAsync()
{
    _logger.LogInformation("Starting TUF metadata refresh");
    
    try
    {
        await ValidateAndUpdateMetadata();
        _logger.LogInformation("TUF metadata refresh completed successfully");
    }
    catch (TufException ex)
    {
        _logger.LogError(ex, "TUF metadata refresh failed: {Message}", ex.Message);
        throw;
    }
}
```

### Security Metrics
TUF .NET can be configured to collect security metrics:
- Signature verification timing
- Key rotation frequency  
- Attack detection events
- Error rates and types

## Threat Model Summary

| Attack Type | TUF Mitigation | Implementation Status |
|-------------|----------------|----------------------|
| Repository Compromise | Offline root keys, role separation | ✅ Complete |
| Man-in-the-Middle | Digital signatures, hash verification | ✅ Complete |
| Rollback Attacks | Version checking, timestamp metadata | ✅ Complete |
| Key Compromise | Threshold signatures, key rotation | ✅ Complete |
| Freeze Attacks | Metadata expiration, freshness checking | ✅ Complete |
| Mix-and-Match | Snapshot consistency, version tracking | ✅ Complete |
| Denial of Service | Resource limits, input validation | ✅ Complete |
| Side Channel | Constant-time operations, memory clearing | ✅ Complete |

## Security Best Practices

### For Repository Operators
1. **Use offline root keys** - Store root keys in HSMs or offline systems
2. **Implement key rotation** - Rotate keys regularly and after any suspected compromise
3. **Monitor for attacks** - Implement logging and monitoring for attack detection
4. **Test incident response** - Regularly test key rotation and incident response procedures

### For Client Developers
1. **Verify trusted root** - Always verify the initial trusted root through secure channels
2. **Handle all exceptions** - Implement proper error handling for all TUF exceptions
3. **Use secure storage** - Store metadata and targets in secure locations
4. **Enable logging** - Configure logging for security monitoring and debugging

### For System Administrators
1. **Secure infrastructure** - Properly secure repository hosting infrastructure
2. **Monitor operations** - Implement comprehensive monitoring and alerting
3. **Regular updates** - Keep TUF .NET and dependencies updated
4. **Security assessments** - Perform regular security assessments and penetration testing