# Metadata Processing Architecture

This document details how TUF .NET processes, validates, and manages TUF metadata throughout the client workflow.

## Overview

Metadata processing is the core security function of TUF .NET, responsible for:
- **Loading and parsing** metadata from various sources
- **Cryptographic verification** of signatures and integrity
- **State management** of trusted metadata transitions
- **Validation** of security policies and constraints
- **Efficient access** to metadata for client operations

## Metadata Processing Pipeline

```
Raw JSON Data
      │
      ▼
┌─────────────────┐
│ JSON Parsing    │ ◄── CanonicalJsonDeserializer
│ & Deserialization │
└─────────────────┘
      │
      ▼
┌─────────────────┐
│ Metadata<T>     │ ◄── Generic metadata wrapper
│ Object Creation │
└─────────────────┘
      │
      ▼
┌─────────────────┐
│ Signature       │ ◄── Cryptographic verification
│ Verification    │
└─────────────────┘
      │
      ▼
┌─────────────────┐
│ Policy          │ ◄── Version, expiration, thresholds
│ Validation      │
└─────────────────┘
      │
      ▼
┌─────────────────┐
│ State           │ ◄── TrustedMetadata state machine
│ Transition      │
└─────────────────┘
      │
      ▼
┌─────────────────┐
│ Trusted         │ ◄── Ready for client use
│ Metadata        │
└─────────────────┘
```

## Core Components

### 1. Metadata Models

All TUF metadata types are implemented as immutable record types:

```csharp
// Base metadata structure
public record Metadata<T>
{
    public T Signed { get; init; }
    public Signature[] Signatures { get; init; }
    
    public void VerifySignatures(Dictionary<string, Key> keys, int threshold)
    {
        // Cryptographic verification logic
    }
}

// Specific metadata types
public record Root : IMetadata
{
    public string Type => "root";
    public string SpecVersion { get; init; }
    public int Version { get; init; }
    public DateTime Expires { get; init; }
    public Dictionary<string, Key> Keys { get; init; }
    public Dictionary<string, RoleDefinition> Roles { get; init; }
    public bool ConsistentSnapshot { get; init; }
}
```

### 2. JSON Processing

#### Canonical JSON Deserialization
```csharp
public class CanonicalJsonDeserializer
{
    public T Deserialize<T>(string json)
    {
        // Parse JSON while preserving unrecognized fields
        var jsonDocument = JsonDocument.Parse(json);
        return DeserializeFromElement<T>(jsonDocument.RootElement);
    }
    
    private T DeserializeFromElement<T>(JsonElement element)
    {
        // Type-safe deserialization with validation
        // Handles unrecognized fields for forward compatibility
        // Strict validation of required fields
    }
}
```

#### Unrecognized Field Handling
```csharp
// Forward compatibility support
public record Root
{
    // Standard TUF fields...
    public Dictionary<string, object> UnrecognizedFields { get; init; } = new();
}

// Preserved during serialization/deserialization
var serialized = CanonicalJsonSerializer.Serialize(root);
var deserialized = CanonicalJsonDeserializer.Deserialize<Root>(serialized);
Assert.That(deserialized.UnrecognizedFields, Is.EqualTo(root.UnrecognizedFields));
```

### 3. Signature Verification

#### Multi-Algorithm Support
```csharp
public static class Signing
{
    public static bool VerifySignature(Key key, ReadOnlyMemory<byte> data, Signature signature)
    {
        return key.KeyType switch
        {
            "ed25519" => VerifyEd25519(key, data, signature),
            "rsa" when key.Scheme == "rsassa-pss-sha256" => VerifyRsaPss(key, data, signature),
            "ecdsa" when key.Scheme == "ecdsa-sha2-nistp256" => VerifyEcdsa(key, data, signature),
            _ => throw new UnsupportedAlgorithmException($"Unsupported key type: {key.KeyType}")
        };
    }
}
```

#### Signature Validation Process
```csharp
public void VerifySignatures(Dictionary<string, Key> trustedKeys, int threshold)
{
    var validSignatures = 0;
    var canonicalBytes = CanonicalJsonSerializer.SerializeToBytes(Signed);
    
    foreach (var signature in Signatures)
    {
        if (trustedKeys.TryGetValue(signature.KeyId, out var key))
        {
            if (Signing.VerifySignature(key, canonicalBytes, signature))
            {
                validSignatures++;
            }
        }
    }
    
    if (validSignatures < threshold)
    {
        throw new InsufficientSignaturesException(
            $"Required {threshold} signatures, but only {validSignatures} valid signatures found");
    }
}
```

### 4. State Management

#### TrustedMetadata State Machine
```csharp
public abstract record TrustedMetadata
{
    // State transitions follow TUF security rules
    public abstract TrustedMetadata UpdateRoot(Metadata<Root> newRoot);
    public abstract TrustedMetadata UpdateTimestamp(Metadata<Timestamp> timestamp);
    public abstract TrustedMetadata UpdateSnapshot(Metadata<Snapshot> snapshot);
    public abstract TrustedMetadata UpdateTargets(Metadata<Targets> targets);
}

// Bootstrap state - only root metadata loaded
public record PartialTrustedMetadata(Metadata<Root> Root) : TrustedMetadata
{
    public override TrustedMetadata UpdateTimestamp(Metadata<Timestamp> timestamp)
    {
        ValidateTimestamp(timestamp);
        return new TimestampTrustedMetadata(Root, timestamp);
    }
}

// Complete state - all metadata loaded and verified
public record CompleteTrustedMetadata(
    Metadata<Root> Root,
    Metadata<Timestamp> Timestamp, 
    Metadata<Snapshot> Snapshot,
    Metadata<Targets> TopLevelTargets,
    Dictionary<string, Metadata<Targets>> DelegatedTargets
) : TrustedMetadata;
```

## Metadata Validation Rules

### 1. Signature Validation

#### Root Metadata Signatures
```csharp
private void ValidateRootSignatures(Metadata<Root> newRoot, Metadata<Root> currentRoot)
{
    // Root metadata must be signed by keys from current root
    var currentKeys = ExtractKeysForRole(currentRoot.Signed.Roles["root"], currentRoot.Signed.Keys);
    var threshold = currentRoot.Signed.Roles["root"].Threshold;
    
    newRoot.VerifySignatures(currentKeys, threshold);
}
```

#### Delegated Role Signatures
```csharp
private void ValidateDelegatedRoleSignatures(Metadata<Targets> delegatedTargets, DelegatedRole role, Dictionary<string, Key> delegationKeys)
{
    var roleKeys = role.KeyIds.ToDictionary(id => id, id => delegationKeys[id]);
    delegatedTargets.VerifySignatures(roleKeys, role.Threshold);
}
```

### 2. Version Validation

#### Rollback Attack Prevention
```csharp
private void ValidateVersion(int newVersion, int currentVersion, string metadataType)
{
    if (newVersion < currentVersion)
    {
        throw new RollbackAttackException(
            $"Rollback attack detected for {metadataType}: " +
            $"new version {newVersion} < current version {currentVersion}");
    }
    
    if (newVersion == currentVersion)
    {
        // Same version is allowed for idempotent operations
        return;
    }
    
    // Version increment is valid
}
```

### 3. Expiration Validation

#### Metadata Freshness Checking
```csharp
private void ValidateExpiration(DateTime expires, string metadataType)
{
    var now = DateTime.UtcNow;
    
    if (expires <= now)
    {
        throw new ExpiredMetadataException(
            $"{metadataType} metadata expired at {expires:yyyy-MM-dd HH:mm:ss} UTC " +
            $"(current time: {now:yyyy-MM-dd HH:mm:ss} UTC)");
    }
    
    // Optional: Warn if expiring soon
    var warningThreshold = now.AddHours(24);
    if (expires <= warningThreshold)
    {
        logger.LogWarning("{MetadataType} metadata expires soon: {Expires}", 
            metadataType, expires);
    }
}
```

### 4. Consistency Validation

#### Snapshot Consistency Checking
```csharp
private void ValidateSnapshotConsistency(Metadata<Snapshot> snapshot, Metadata<Targets> targets)
{
    var targetsPath = "targets.json";
    
    if (!snapshot.Signed.Meta.TryGetValue(targetsPath, out var targetsInfo))
    {
        throw new InconsistentMetadataException(
            $"Snapshot does not reference {targetsPath}");
    }
    
    // Verify version matches
    if (targetsInfo.Version != targets.Signed.Version)
    {
        throw new InconsistentMetadataException(
            $"Version mismatch: snapshot reports {targetsInfo.Version}, " +
            $"targets has {targets.Signed.Version}");
    }
    
    // Verify hash matches
    var targetsBytes = CanonicalJsonSerializer.SerializeToBytes(targets);
    var actualHashes = ComputeHashes(targetsBytes);
    
    foreach (var (algorithm, expectedHash) in targetsInfo.Hashes)
    {
        if (!actualHashes.TryGetValue(algorithm, out var actualHash) || 
            !actualHash.Equals(expectedHash, StringComparison.Ordinal))
        {
            throw new InconsistentMetadataException(
                $"Hash mismatch for {targetsPath}: expected {expectedHash}, " +
                $"actual {actualHash}");
        }
    }
}
```

## Delegation Processing

### 1. Delegation Tree Traversal

#### Path-Based Delegation Resolution
```csharp
public async Task<(string RemotePath, TargetFile File)?> GetTargetInfoFromDelegatedRole(
    string targetPath, 
    string roleName,
    Dictionary<string, Metadata<Targets>> delegatedTargets)
{
    if (!delegatedTargets.TryGetValue(roleName, out var delegatedMetadata))
    {
        await LoadDelegatedRole(roleName);
        delegatedMetadata = delegatedTargets[roleName];
    }
    
    // Check if target is in this delegated role
    var targets = delegatedMetadata.Signed.Targets;
    if (targets.TryGetValue(targetPath, out var targetFile))
    {
        return (targetPath, targetFile);
    }
    
    // Recurse into nested delegations
    if (delegatedMetadata.Signed.Delegations != null)
    {
        return await TraverseDelegations(targetPath, delegatedMetadata.Signed.Delegations);
    }
    
    return null;
}
```

#### Hash-Based Delegation Resolution  
```csharp
private string GetDelegatedRoleForHash(string targetPath, List<DelegatedRole> roles)
{
    var hashBytes = ComputeTargetHash(targetPath);
    var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
    
    foreach (var role in roles)
    {
        if (role.PathHashPrefixes != null)
        {
            foreach (var prefix in role.PathHashPrefixes)
            {
                if (hashHex.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return role.Name;
                }
            }
        }
    }
    
    throw new DelegationException($"No delegated role found for target hash: {hashHex}");
}
```

### 2. Delegation Depth Limiting
```csharp
public class DelegationTraversalContext
{
    private readonly int maxDepth;
    private int currentDepth;
    
    public void EnterDelegation(string roleName)
    {
        if (++currentDepth > maxDepth)
        {
            throw new MaxDelegationDepthExceededException(
                $"Maximum delegation depth {maxDepth} exceeded at role {roleName}");
        }
    }
    
    public void ExitDelegation()
    {
        currentDepth--;
    }
}
```

## Performance Optimizations

### 1. Metadata Caching

#### Memory Caching
```csharp
public class MetadataCache
{
    private readonly Dictionary<string, (Metadata<Targets>, DateTime)> delegatedRoleCache = new();
    private readonly TimeSpan cacheExpiry = TimeSpan.FromMinutes(5);
    
    public bool TryGetCachedRole(string roleName, out Metadata<Targets> metadata)
    {
        if (delegatedRoleCache.TryGetValue(roleName, out var cached))
        {
            var (cachedMetadata, cacheTime) = cached;
            if (DateTime.UtcNow - cacheTime < cacheExpiry)
            {
                metadata = cachedMetadata;
                return true;
            }
            
            // Remove expired entry
            delegatedRoleCache.Remove(roleName);
        }
        
        metadata = null;
        return false;
    }
}
```

#### File System Caching
```csharp
public class FileSystemMetadataCache
{
    private readonly string cacheDirectory;
    
    public async Task<Metadata<T>?> TryLoadFromCache<T>(string metadataPath) where T : IMetadata
    {
        var cachePath = GetCachePath(metadataPath);
        
        if (!File.Exists(cachePath))
            return null;
            
        try
        {
            var json = await File.ReadAllTextAsync(cachePath);
            return CanonicalJsonDeserializer.Deserialize<Metadata<T>>(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load cached metadata from {Path}", cachePath);
            File.Delete(cachePath); // Remove corrupted cache
            return null;
        }
    }
}
```

### 2. Signature Verification Optimization

#### Signature Result Caching
```csharp
public class SignatureVerificationCache
{
    private readonly LRUCache<string, bool> verificationResults;
    
    public bool VerifySignatureWithCache(Key key, ReadOnlyMemory<byte> data, Signature signature)
    {
        var cacheKey = ComputeCacheKey(key, data, signature);
        
        if (verificationResults.TryGetValue(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }
        
        var result = Signing.VerifySignature(key, data, signature);
        verificationResults.Add(cacheKey, result);
        
        return result;
    }
}
```

#### Parallel Signature Verification
```csharp
public async Task<bool> VerifySignaturesParallel(Dictionary<string, Key> trustedKeys, int threshold)
{
    var canonicalBytes = CanonicalJsonSerializer.SerializeToBytes(Signed);
    var validSignatures = 0;
    
    var verificationTasks = Signatures.Select(async signature =>
    {
        if (trustedKeys.TryGetValue(signature.KeyId, out var key))
        {
            // CPU-bound verification can benefit from Task.Run
            return await Task.Run(() => Signing.VerifySignature(key, canonicalBytes, signature));
        }
        return false;
    });
    
    var results = await Task.WhenAll(verificationTasks);
    validSignatures = results.Count(r => r);
    
    return validSignatures >= threshold;
}
```

### 3. JSON Processing Optimization

#### Memory-Efficient Parsing
```csharp
public T DeserializeFromStream<T>(Stream jsonStream)
{
    using var reader = new Utf8JsonReader(ReadJsonBytesFromStream(jsonStream));
    return DeserializeFromReader<T>(ref reader);
}

private ReadOnlySpan<byte> ReadJsonBytesFromStream(Stream stream)
{
    // Use ArrayPool for memory efficiency
    var buffer = ArrayPool<byte>.Shared.Rent((int)stream.Length);
    try
    {
        var bytesRead = stream.Read(buffer, 0, (int)stream.Length);
        return buffer.AsSpan(0, bytesRead);
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
```

## Error Handling and Recovery

### 1. Metadata Corruption Detection

#### JSON Validation
```csharp
private void ValidateJsonStructure(string json, string metadataType)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        
        // Validate required top-level fields
        if (!root.TryGetProperty("_type", out var typeElement) ||
            typeElement.GetString() != metadataType)
        {
            throw new MetadataDeserializationException(
                $"Invalid or missing _type field for {metadataType} metadata");
        }
        
        if (!root.TryGetProperty("version", out var versionElement) ||
            !versionElement.TryGetInt32(out var version) || version < 1)
        {
            throw new MetadataDeserializationException(
                $"Invalid or missing version field for {metadataType} metadata");
        }
    }
    catch (JsonException ex)
    {
        throw new MetadataDeserializationException(
            $"Malformed JSON in {metadataType} metadata: {ex.Message}", ex);
    }
}
```

### 2. Graceful Degradation

#### Partial Metadata Loading
```csharp
public async Task<PartialUpdateResult> TryPartialRefresh()
{
    var results = new PartialUpdateResult();
    
    try
    {
        await UpdateTimestamp();
        results.TimestampUpdated = true;
    }
    catch (Exception ex)
    {
        results.Errors.Add(new MetadataUpdateError("timestamp", ex));
    }
    
    try
    {
        await UpdateSnapshot();
        results.SnapshotUpdated = true;
    }
    catch (Exception ex)
    {
        results.Errors.Add(new MetadataUpdateError("snapshot", ex));
    }
    
    return results;
}
```

### 3. Recovery Strategies

#### Metadata Rollback
```csharp
public class MetadataRecoveryManager
{
    private readonly Stack<TrustedMetadata> metadataHistory = new();
    
    public void SaveCheckpoint(TrustedMetadata metadata)
    {
        metadataHistory.Push(metadata);
        
        // Limit history size
        if (metadataHistory.Count > 10)
        {
            var items = metadataHistory.ToArray();
            metadataHistory.Clear();
            for (int i = 0; i < 10; i++)
            {
                metadataHistory.Push(items[i]);
            }
        }
    }
    
    public TrustedMetadata? TryRollback()
    {
        return metadataHistory.Count > 0 ? metadataHistory.Pop() : null;
    }
}
```

## Monitoring and Diagnostics

### 1. Metadata Processing Metrics

#### Performance Metrics
```csharp
public class MetadataProcessingMetrics
{
    private static readonly Counter MetadataProcessedCounter = 
        Metrics.CreateCounter("tuf_metadata_processed_total", "Total metadata processed");
        
    private static readonly Histogram ProcessingDurationHistogram = 
        Metrics.CreateHistogram("tuf_metadata_processing_duration_seconds", "Metadata processing duration");
        
    private static readonly Gauge CacheHitRateGauge = 
        Metrics.CreateGauge("tuf_metadata_cache_hit_rate", "Metadata cache hit rate");
        
    public void RecordMetadataProcessed(string metadataType, TimeSpan duration)
    {
        MetadataProcessedCounter.WithLabels(metadataType).Inc();
        ProcessingDurationHistogram.WithLabels(metadataType).Observe(duration.TotalSeconds);
    }
}
```

#### Health Checks
```csharp
public class MetadataHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var checks = new List<string>();
        
        // Check metadata freshness
        if (trustedMetadata is CompleteTrustedMetadata complete)
        {
            var now = DateTime.UtcNow;
            var timestampExpiry = complete.Timestamp.Signed.Expires;
            
            if (timestampExpiry <= now.AddMinutes(5))
            {
                checks.Add("Timestamp metadata expires soon");
            }
        }
        
        var isHealthy = checks.Count == 0;
        var status = isHealthy ? HealthStatus.Healthy : HealthStatus.Degraded;
        
        return Task.FromResult(new HealthCheckResult(status, string.Join("; ", checks)));
    }
}
```

### 2. Diagnostic Logging

#### Structured Logging
```csharp
public void LogMetadataOperation(string operation, string metadataType, object additionalData = null)
{
    using var scope = logger.BeginScope("MetadataOperation");
    
    logger.LogInformation("Metadata operation: {Operation} for {MetadataType} {@AdditionalData}",
        operation, metadataType, additionalData);
}
```

## Future Enhancements

### 1. Streaming Metadata Processing
- **Large metadata handling**: Stream processing for very large targets metadata
- **Progressive loading**: Load delegation trees on-demand
- **Memory optimization**: Process metadata without full memory loading

### 2. Advanced Caching Strategies
- **Distributed caching**: Redis/memcached integration for multi-instance deployments
- **Smart invalidation**: More intelligent cache invalidation strategies  
- **Compressed caching**: Store cached metadata in compressed format

### 3. Performance Improvements
- **SIMD optimization**: Use SIMD instructions for hash computation
- **Native compilation**: Optimize hot paths for Native AOT
- **Async optimization**: Further async/await optimizations

## Related Documentation

- [Architecture Overview](overview.md) - High-level system architecture
- [Cryptographic Design](cryptography.md) - Cryptographic implementation details
- [Performance Considerations](performance.md) - Performance optimization strategies
- [Core Concepts](../guides/core-concepts.md) - TUF concepts and terminology
- [API Documentation](../api/) - Detailed API reference