using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using CanonicalJson;
using TUF.Models;

namespace TUF.PerformanceBenchmarks;

/// <summary>
/// Simple performance benchmarks for core TUF operations.
/// These measure the most critical operations that affect real TUF client performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class SimpleBenchmarks
{
    private Root _sampleRoot = null!;
    private string _sampleRootJson = null!;
    private byte[] _sampleData = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create a simple but realistic root metadata
        _sampleRoot = new Root
        {
            Type = "root",
            SpecVersion = "1.0.31",
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            ConsistentSnapshot = true,
            Keys = new Dictionary<string, Key>
            {
                ["test-key-id"] = new Key
                {
                    KeyType = "ed25519",
                    Scheme = "ed25519",
                    KeyVal = new KeyValue
                    {
                        Public = "test-public-key-data"
                    }
                }
            },
            Roles = new Roles
            {
                Root = new RoleKeys { KeyIds = ["test-key-id"], Threshold = 1 },
                Timestamp = new RoleKeys { KeyIds = ["test-key-id"], Threshold = 1 },
                Snapshot = new RoleKeys { KeyIds = ["test-key-id"], Threshold = 1 },
                Targets = new RoleKeys { KeyIds = ["test-key-id"], Threshold = 1 }
            }
        };

        // Pre-serialize for deserialization benchmarks
        _sampleRootJson = Encoding.UTF8.GetString(CanonicalJson.Serializer.Serialize(_sampleRoot));
        
        // Create test data for crypto operations
        _sampleData = Encoding.UTF8.GetBytes("Test data for signing and verification");
    }

    [Benchmark(Description = "Serialize Root Metadata")]
    public byte[] SerializeRootMetadata()
    {
        return CanonicalJson.Serializer.Serialize(_sampleRoot);
    }

    [Benchmark(Description = "Deserialize Root Metadata")]
    public Root DeserializeRootMetadata()
    {
        return CanonicalJson.Serializer.Deserialize<Root>(_sampleRootJson);
    }

    [Benchmark(Description = "Calculate Key ID")]
    public string CalculateKeyId()
    {
        return _sampleRoot.Keys["test-key-id"].GetKeyId();
    }

    [Benchmark(Description = "Create Ed25519 Signer")]
    public Ed25519Signer CreateEd25519Signer()
    {
        return Ed25519Signer.Generate();
    }

    [Benchmark(Description = "Sign Data with Ed25519")]
    public SignatureObject SignDataEd25519()
    {
        var signer = Ed25519Signer.Generate();
        return signer.SignBytes(_sampleData);
    }

    [Benchmark(Description = "Verify Ed25519 Signature")]
    public bool VerifyEd25519Signature()
    {
        var signer = Ed25519Signer.Generate();
        var signature = signer.SignBytes(_sampleData);
        return signer.Key.VerifySignature(signature.Sig, _sampleData);
    }
}