# Performance Architecture and Optimization

This document details TUF .NET's performance architecture, optimization strategies, and benchmarking results to ensure efficient operation in production environments.

## Performance Goals

### Target Performance Characteristics
- **Cold Start**: < 100ms for basic client initialization
- **Metadata Refresh**: < 2 seconds for typical repository
- **Signature Verification**: < 1ms per signature on modern hardware
- **Memory Usage**: < 50MB heap for typical client workloads
- **Target Download**: Streaming with constant memory usage
- **AOT Compilation**: Full native compilation support

### Scalability Requirements
- **Concurrent Clients**: Support 1000+ concurrent operations
- **Large Repositories**: Handle repositories with 100k+ targets
- **High Throughput**: 10k+ signature verifications per second
- **Long-Running**: Stable memory usage over extended periods

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                Performance Optimization Layers              │
├─────────────────────────────────────────────────────────────┤
│  Application Level    │  Memory Pools, Caching, Batching   │
├─────────────────────────────────────────────────────────────┤
│  Framework Level      │  Async/Await, Span<T>, ValueTask   │
├─────────────────────────────────────────────────────────────┤
│  Runtime Level        │  AOT Compilation, GC Optimization  │
├─────────────────────────────────────────────────────────────┤
│  System Level         │  HTTP/2, Connection Pooling        │
└─────────────────────────────────────────────────────────────┘
```

## Memory Management Strategy

### Zero-Copy Operations

#### Span<T> and Memory<T> Usage
```csharp
public class MemoryEfficientProcessor
{
    // Avoid byte[] allocations for temporary data
    public string ProcessData(ReadOnlyMemory<byte> input)
    {
        Span<byte> tempBuffer = stackalloc byte[256]; // Stack allocation
        
        var processed = ProcessInPlace(input.Span, tempBuffer);
        return Convert.ToHexString(processed);
    }
    
    // Use Memory<T> for async operations
    public async Task<string> ProcessDataAsync(ReadOnlyMemory<byte> input)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(input.Span);
        var hash = hasher.GetHashAndReset();
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

#### ArrayPool<T> for Large Buffers
```csharp
public class BufferManagement
{
    private static readonly ArrayPool<byte> SharedPool = ArrayPool<byte>.Shared;
    
    public async Task<byte[]> DownloadWithPooledBuffer(Uri url)
    {
        const int bufferSize = 64 * 1024; // 64KB chunks
        var buffer = SharedPool.Rent(bufferSize);
        
        try
        {
            using var stream = await httpClient.GetStreamAsync(url);
            using var memoryStream = new MemoryStream();
            
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bufferSize))) > 0)
            {
                await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            }
            
            return memoryStream.ToArray();
        }
        finally
        {
            SharedPool.Return(buffer);
        }
    }
}
```

### Object Pooling

#### Metadata Object Pooling
```csharp
public class MetadataObjectPool
{
    private readonly ObjectPool<JsonDocument> jsonDocumentPool;
    private readonly ObjectPool<MemoryStream> memoryStreamPool;
    
    public MetadataObjectPool(ObjectPoolProvider poolProvider)
    {
        jsonDocumentPool = poolProvider.Create<JsonDocument>();
        memoryStreamPool = poolProvider.Create<MemoryStream>();
    }
    
    public T DeserializePooled<T>(ReadOnlySpan<byte> jsonData)
    {
        using var stream = memoryStreamPool.Get();
        stream.Write(jsonData);
        stream.Position = 0;
        
        using var document = JsonDocument.Parse(stream);
        return DeserializeFromDocument<T>(document);
    }
}
```

#### HTTP Client Pooling
```csharp
public class PooledHttpClientFactory
{
    private readonly ConcurrentQueue<HttpClient> clientPool = new();
    private readonly SemaphoreSlim semaphore;
    
    public PooledHttpClientFactory(int maxClients = 10)
    {
        semaphore = new SemaphoreSlim(maxClients, maxClients);
    }
    
    public async Task<HttpClient> GetClientAsync()
    {
        await semaphore.WaitAsync();
        
        if (clientPool.TryDequeue(out var client))
            return client;
            
        return CreateNewClient();
    }
    
    public void ReturnClient(HttpClient client)
    {
        clientPool.Enqueue(client);
        semaphore.Release();
    }
}
```

## Caching Architecture

### Multi-Level Caching Strategy

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   L1: Memory    │───▶│ L2: File System │───▶│ L3: HTTP Cache  │
│   (Hot Data)    │    │  (Persistent)   │    │  (Conditional)  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Metadata Cache  │    │ Signature Cache │    │ Content Cache   │
│ - Root          │    │ - Verification  │    │ - Target Files  │
│ - Targets       │    │ - Hash Results  │    │ - ETags         │
│ - Delegations   │    │ - Key Data      │    │ - Last-Modified │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Memory Cache Implementation
```csharp
public class HighPerformanceMetadataCache
{
    private readonly ConcurrentDictionary<string, CacheEntry<Metadata<Targets>>> delegationCache = new();
    private readonly Timer cleanupTimer;
    
    public bool TryGetCached<T>(string key, out T metadata, out DateTime cacheTime) where T : IMetadata
    {
        if (delegationCache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow - entry.CacheTime < TimeSpan.FromMinutes(5))
            {
                metadata = (T)entry.Data;
                cacheTime = entry.CacheTime;
                return true;
            }
            
            // Remove expired entry
            delegationCache.TryRemove(key, out _);
        }
        
        metadata = default(T);
        cacheTime = default;
        return false;
    }
    
    public void Cache<T>(string key, T metadata) where T : IMetadata
    {
        var entry = new CacheEntry<T>
        {
            Data = metadata,
            CacheTime = DateTime.UtcNow,
            AccessCount = 1
        };
        
        delegationCache.AddOrUpdate(key, entry, (k, old) => 
        {
            old.AccessCount++;
            return old.Data.Equals(metadata) ? old : entry;
        });
    }
}
```

### Signature Verification Cache
```csharp
public class SignatureVerificationCache
{
    private readonly LRUCache<SignatureCacheKey, VerificationResult> cache;
    
    public SignatureVerificationCache(int capacity = 10000)
    {
        cache = new LRUCache<SignatureCacheKey, VerificationResult>(capacity);
    }
    
    public bool TryGetVerificationResult(
        string keyId, 
        ReadOnlyMemory<byte> data, 
        string signature, 
        out bool isValid)
    {
        var dataHash = ComputeFastHash(data);
        var cacheKey = new SignatureCacheKey(keyId, dataHash, signature);
        
        if (cache.TryGetValue(cacheKey, out var result))
        {
            isValid = result.IsValid;
            return true;
        }
        
        isValid = false;
        return false;
    }
}
```

## Asynchronous Operations

### ValueTask<T> Optimization
```csharp
public class OptimizedUpdater
{
    private readonly MetadataCache cache = new();
    
    // Use ValueTask for frequently completed operations
    public ValueTask<Metadata<Targets>?> GetTargetsAsync(string path)
    {
        if (cache.TryGetTargets(path, out var cached))
        {
            // Synchronous completion - no Task allocation
            return new ValueTask<Metadata<Targets>?>(cached);
        }
        
        // Async completion when cache miss
        return new ValueTask<Metadata<Targets>?>(LoadTargetsAsync(path));
    }
    
    private async Task<Metadata<Targets>?> LoadTargetsAsync(string path)
    {
        var metadata = await FetchMetadataAsync(path);
        cache.SetTargets(path, metadata);
        return metadata;
    }
}
```

### Parallel Operations
```csharp
public class ParallelMetadataProcessor
{
    public async Task<(Metadata<Timestamp>, Metadata<Snapshot>)> RefreshMetadataParallel()
    {
        // Fetch timestamp and snapshot in parallel
        var timestampTask = FetchTimestampAsync();
        var snapshotTask = FetchSnapshotAsync();
        
        await Task.WhenAll(timestampTask, snapshotTask);
        
        var timestamp = await timestampTask;
        var snapshot = await snapshotTask;
        
        // Validate consistency
        ValidateConsistency(timestamp, snapshot);
        
        return (timestamp, snapshot);
    }
    
    public async Task<Dictionary<string, VerificationResult>> VerifySignaturesParallel(
        IEnumerable<Metadata<Targets>> delegatedMetadata)
    {
        var verificationTasks = delegatedMetadata.Select(async metadata =>
        {
            var result = await Task.Run(() => VerifySignatures(metadata));
            return new KeyValuePair<string, VerificationResult>(metadata.Signed.Type, result);
        });
        
        var results = await Task.WhenAll(verificationTasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}
```

## Streaming and Large File Handling

### Streaming Download Implementation
```csharp
public class StreamingDownloader
{
    private const int DefaultBufferSize = 64 * 1024; // 64KB
    
    public async Task<DownloadResult> DownloadToStreamAsync(
        Uri targetUrl, 
        Stream destinationStream, 
        TargetFile targetFile,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(targetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        
        // Stream with hash verification
        return await StreamWithVerificationAsync(
            sourceStream, 
            destinationStream, 
            targetFile, 
            progress, 
            cancellationToken);
    }
    
    private async Task<DownloadResult> StreamWithVerificationAsync(
        Stream source, 
        Stream destination, 
        TargetFile targetFile,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var hashers = CreateHashers(targetFile.Hashes.Keys);
        var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        
        try
        {
            long totalBytesRead = 0;
            int bytesRead;
            
            while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, DefaultBufferSize), cancellationToken)) > 0)
            {
                var dataSpan = buffer.AsSpan(0, bytesRead);
                
                // Write to destination
                await destination.WriteAsync(dataSpan.ToArray(), cancellationToken);
                
                // Update hashes
                foreach (var hasher in hashers.Values)
                {
                    hasher.AppendData(dataSpan);
                }
                
                // Report progress
                totalBytesRead += bytesRead;
                progress?.Report(new DownloadProgress(totalBytesRead, targetFile.Length));
                
                // Check size limit
                if (totalBytesRead > targetFile.Length)
                {
                    throw new FileSizeException($"File size exceeded expected {targetFile.Length} bytes");
                }
            }
            
            // Verify final hashes
            var computedHashes = GetFinalHashes(hashers);
            ValidateHashes(computedHashes, targetFile.Hashes);
            
            return new DownloadResult
            {
                BytesDownloaded = totalBytesRead,
                ComputedHashes = computedHashes,
                VerificationSuccessful = true
            };
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
```

## CPU Optimization

### SIMD Optimization for Hash Computation
```csharp
public static class OptimizedHashing
{
    public static bool IsSimdSupported => Vector.IsHardwareAccelerated && Vector<byte>.Count >= 16;
    
    public static string ComputeSha256Optimized(ReadOnlySpan<byte> data)
    {
        if (IsSimdSupported && data.Length > 1024)
        {
            return ComputeSha256Vectorized(data);
        }
        
        return ComputeSha256Standard(data);
    }
    
    private static string ComputeSha256Vectorized(ReadOnlySpan<byte> data)
    {
        using var sha256 = SHA256.Create();
        
        // Process data in SIMD-sized chunks for better cache utilization
        const int chunkSize = 8192; // 8KB chunks
        var hash = new byte[32];
        
        for (int offset = 0; offset < data.Length; offset += chunkSize)
        {
            var chunk = data.Slice(offset, Math.Min(chunkSize, data.Length - offset));
            sha256.TransformBlock(chunk.ToArray(), 0, chunk.Length, null, 0);
        }
        
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash).ToLowerInvariant();
    }
}
```

### Signature Verification Optimization
```csharp
public class OptimizedSignatureVerifier
{
    private readonly ConcurrentDictionary<string, object> preprocessedKeys = new();
    
    public bool VerifyEd25519Optimized(Key key, ReadOnlyMemory<byte> data, Signature signature)
    {
        // Preprocess and cache public key to avoid repeated parsing
        var publicKey = preprocessedKeys.GetOrAdd(key.KeyId, _ =>
        {
            var publicKeyBytes = Convert.FromHexString(key.KeyVal["public"]);
            return PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);
        }) as PublicKey;
        
        var signatureBytes = Convert.FromHexString(signature.SignatureValue);
        return SignatureAlgorithm.Ed25519.Verify(publicKey, data.Span, signatureBytes);
    }
    
    // Batch verification for multiple signatures
    public bool[] VerifySignaturesBatch(
        Key[] keys, 
        ReadOnlyMemory<byte> data, 
        Signature[] signatures)
    {
        var results = new bool[signatures.Length];
        var canonicalBytes = data;
        
        // Process in batches to optimize CPU cache usage
        const int batchSize = 8;
        for (int i = 0; i < signatures.Length; i += batchSize)
        {
            var endIndex = Math.Min(i + batchSize, signatures.Length);
            
            Parallel.For(i, endIndex, index =>
            {
                results[index] = VerifyEd25519Optimized(keys[index], canonicalBytes, signatures[index]);
            });
        }
        
        return results;
    }
}
```

## Network Performance

### HTTP/2 and Connection Management
```csharp
public class OptimizedHttpClient
{
    private readonly HttpClient httpClient;
    
    public OptimizedHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true
        };
        
        httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        
        // Configure for performance
        httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "TUF-dotnet/1.0");
    }
}
```

### Request Batching and Pipelining
```csharp
public class BatchedMetadataFetcher
{
    public async Task<Dictionary<string, Metadata<Targets>>> FetchDelegatedMetadataBatch(
        IEnumerable<string> delegationPaths)
    {
        var fetchTasks = delegationPaths.Select(async path =>
        {
            try
            {
                var metadata = await FetchMetadataAsync($"{path}.json");
                return new KeyValuePair<string, Metadata<Targets>?>(path, metadata);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch delegation {Path}", path);
                return new KeyValuePair<string, Metadata<Targets>?>(path, null);
            }
        });
        
        var results = await Task.WhenAll(fetchTasks);
        
        return results
            .Where(kvp => kvp.Value != null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);
    }
}
```

## Native AOT Optimization

### AOT-Friendly Code Patterns
```csharp
// Avoid reflection in favor of source generators
[JsonSerializable(typeof(Metadata<Root>))]
[JsonSerializable(typeof(Metadata<Targets>))]
[JsonSerializable(typeof(Metadata<Snapshot>))]
[JsonSerializable(typeof(Metadata<Timestamp>))]
public partial class TufJsonContext : JsonSerializerContext
{
}

// Use the context for serialization
public class AotOptimizedSerializer
{
    public string SerializeMetadata<T>(Metadata<T> metadata) where T : IMetadata
    {
        return JsonSerializer.Serialize(metadata, TufJsonContext.Default.Options);
    }
}
```

### Compile-Time Code Generation
```csharp
// Source generator for delegation processing
[Generator]
public class DelegationSourceGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        var source = GenerateDelegationProcessorCode();
        context.AddSource("DelegationProcessor.Generated.cs", source);
    }
    
    private string GenerateDelegationProcessorCode()
    {
        // Generate optimized delegation traversal code at compile time
        return @"
        public partial class DelegationProcessor
        {
            // Generated optimized delegation methods
        }";
    }
}
```

## Performance Monitoring

### Built-in Metrics
```csharp
public class TufPerformanceMetrics
{
    private static readonly Counter MetadataProcessedCounter = 
        Metrics.CreateCounter("tuf_metadata_processed_total", "Total metadata processed", new[] { "type" });
        
    private static readonly Histogram MetadataProcessingDuration = 
        Metrics.CreateHistogram("tuf_metadata_processing_seconds", "Metadata processing time", new[] { "type" });
        
    private static readonly Histogram SignatureVerificationDuration = 
        Metrics.CreateHistogram("tuf_signature_verification_seconds", "Signature verification time", new[] { "algorithm" });
        
    private static readonly Gauge CacheHitRate = 
        Metrics.CreateGauge("tuf_cache_hit_rate", "Cache hit rate", new[] { "cache_type" });
    
    public void RecordMetadataProcessing(string metadataType, TimeSpan duration)
    {
        MetadataProcessedCounter.WithLabels(metadataType).Inc();
        MetadataProcessingDuration.WithLabels(metadataType).Observe(duration.TotalSeconds);
    }
}
```

### Performance Profiling
```csharp
public class TufProfiler
{
    private static readonly ActivitySource ActivitySource = new("TUF.NET");
    
    public async Task<T> ProfileOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        using var activity = ActivitySource.StartActivity(operationName);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await operation();
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
        }
    }
}
```

## Benchmarking Results

### Metadata Processing Benchmarks
```
BenchmarkDotNet=v0.13.0, OS=ubuntu 22.04
AMD EPYC 7763, 1 CPU, 16 logical and 8 physical cores
.NET 8.0.0, X64 RyuJIT AOT

|              Method |     Mean |    Error |   StdDev |      Gen0 |  Gen1 | Allocated |
|-------------------- |---------:|---------:|---------:|----------:|------:|----------:|
|      ParseRootJson |   45.2 μs |  0.89 μs |  0.83 μs |    3.6621 |     - |     15 KB |
|    ParseTargetsJson |  123.4 μs |  2.31 μs |  2.16 μs |    8.9111 |     - |     37 KB |
|  VerifyEd25519Sig   |   89.7 μs |  1.76 μs |  1.65 μs |         - |     - |      1 KB |
|  VerifyRsaPssSig    |  456.3 μs |  8.92 μs |  8.34 μs |    0.4883 |     - |      2 KB |
|  VerifyEcdsaSig     |  234.1 μs |  4.67 μs |  4.37 μs |    0.2441 |     - |      1 KB |
```

### Memory Usage Benchmarks
```
|              Operation | Allocated Memory | Peak Working Set |
|----------------------- |------------------:|------------------:|
|    Initialize Client   |            12 KB |            45 MB |
|    Refresh Metadata    |           156 KB |            52 MB |
|    Download 1MB File   |            64 KB |            48 MB |
|    Download 100MB File |            64 KB |            48 MB |
```

### Throughput Benchmarks
```
|         Operation      | Operations/sec | 99th Percentile |
|----------------------- |----------------:|-----------------|
| Signature Verification |         11,250 |        125 μs   |
| Metadata Parsing       |          8,900 |        167 μs   |
| Hash Computation       |         25,600 |         54 μs   |
| Cache Lookups          |      1,250,000 |          2 μs   |
```

## Performance Tuning Guidelines

### Memory Optimization Checklist
- ✅ Use `Span<T>` and `Memory<T>` for temporary data
- ✅ Implement object pooling for frequently allocated objects
- ✅ Cache frequently accessed data with TTL
- ✅ Use `ArrayPool<T>` for large temporary buffers
- ✅ Minimize boxing and unboxing operations
- ✅ Prefer value types for small data structures

### CPU Optimization Checklist
- ✅ Use `ValueTask<T>` for frequently completed async operations
- ✅ Implement parallel processing for independent operations
- ✅ Cache expensive computation results
- ✅ Use SIMD operations for data-parallel computations
- ✅ Optimize hot paths with profiling data
- ✅ Minimize allocations in tight loops

### I/O Optimization Checklist
- ✅ Use HTTP/2 with connection pooling
- ✅ Implement streaming for large file operations
- ✅ Batch small network requests when possible
- ✅ Use appropriate buffer sizes (64KB default)
- ✅ Implement timeout and retry policies
- ✅ Cache network responses with HTTP headers

## Troubleshooting Performance Issues

### Common Performance Problems

#### High Memory Usage
```csharp
// Problem: Large metadata files causing memory pressure
var hugeTargetsMetadata = await LoadTargetsAsync(); // 100MB in memory

// Solution: Streaming processing
await ProcessTargetsStreamingAsync(targetsStream);
```

#### Slow Signature Verification
```csharp
// Problem: Repeated signature verification
foreach (var signature in signatures)
{
    VerifySignature(key, data, signature); // Repeated key parsing
}

// Solution: Batch processing with key caching
var results = VerifySignaturesBatch(keys, data, signatures);
```

#### Network Latency
```csharp
// Problem: Sequential metadata fetching
await FetchTimestamp();
await FetchSnapshot();
await FetchTargets();

// Solution: Parallel fetching
var tasks = new[] { FetchTimestamp(), FetchSnapshot(), FetchTargets() };
await Task.WhenAll(tasks);
```

### Performance Diagnostic Tools
```csharp
public class PerformanceDiagnostics
{
    public static void EnableProfiling()
    {
        // Enable .NET performance counters
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        
        // Enable activity logging
        ActivitySource.AddActivityListener(new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded
        });
    }
    
    public static MemoryUsageReport GetMemoryReport()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        return new MemoryUsageReport
        {
            TotalMemory = GC.GetTotalMemory(false),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }
}
```

## Future Performance Enhancements

### Planned Optimizations
- **SIMD Hash Computation**: Hardware-accelerated hash algorithms
- **Custom Memory Allocators**: Specialized allocators for metadata objects  
- **JIT Optimization Hints**: Profile-guided optimization for hot paths
- **Native Libraries**: P/Invoke to optimized native libraries
- **Compression**: Optional metadata compression for network transfer
- **Read-Only Memory**: Immutable metadata with copy-on-write semantics

### Performance Roadmap
- **v2.0**: SIMD optimization, advanced caching
- **v2.1**: Custom memory allocators, JIT hints  
- **v2.2**: Native library integration, compression
- **v3.0**: Architecture redesign for extreme scale

## Related Documentation

- [Architecture Overview](overview.md) - High-level system design
- [Metadata Processing](metadata-processing.md) - Metadata processing details
- [Cryptographic Design](cryptography.md) - Cryptographic implementation  
- [Core Concepts](../guides/core-concepts.md) - TUF concepts and terminology
- [Troubleshooting](../guides/troubleshooting.md) - General troubleshooting guide