using BenchmarkDotNet.Attributes;
using CanonicalJson;
using TUF.Models;

namespace TUF.Benchmarks;

/// <summary>
/// Simplified benchmarks focusing on core TUF operations that are well-tested.
/// These benchmarks measure the most performance-critical operations in TUF workflows.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net80)]
[SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net90)]
public class SimplifiedBenchmarks
{
    private Ed25519Signer _ed25519Signer = default!;
    private RsaSigner _rsaSigner = default!;
    private EcdsaSigner _ecdsaSigner = default!;
    
    private Key _ed25519Key = default!;
    private Key _rsaKey = default!;
    private Key _ecdsaKey = default!;
    
    private byte[] _testData1KB = default!;
    private byte[] _testData64KB = default!;
    private byte[] _testData1MB = default!;

    [GlobalSetup]
    public void Setup()
    {
        // Create signers using the correct API
        _ed25519Signer = Ed25519Signer.Generate();
        _rsaSigner = RsaSigner.Generate(2048); // Use smaller key for faster benchmarks
        _ecdsaSigner = EcdsaSigner.Generate();
        
        // Cache keys
        _ed25519Key = _ed25519Signer.Key;
        _rsaKey = _rsaSigner.Key;
        _ecdsaKey = _ecdsaSigner.Key;

        // Create test data of different sizes
        var random = new Random(42);
        
        _testData1KB = new byte[1024];
        random.NextBytes(_testData1KB);
        
        _testData64KB = new byte[64 * 1024];
        random.NextBytes(_testData64KB);
        
        _testData1MB = new byte[1024 * 1024];
        random.NextBytes(_testData1MB);
    }

    // Key serialization benchmarks
    [Benchmark(Baseline = true)]
    public byte[] SerializeEd25519Key()
    {
        return Serializer.Serialize(_ed25519Key);
    }

    [Benchmark]
    public byte[] SerializeRsaKey()
    {
        return Serializer.Serialize(_rsaKey);
    }

    [Benchmark]
    public byte[] SerializeEcdsaKey()
    {
        return Serializer.Serialize(_ecdsaKey);
    }

    // Signing benchmarks - 1KB data
    [Benchmark]
    public SignatureObject Ed25519_Sign_1KB()
    {
        return _ed25519Signer.SignBytes(_testData1KB);
    }

    [Benchmark]
    public SignatureObject Rsa_Sign_1KB()
    {
        return _rsaSigner.SignBytes(_testData1KB);
    }

    [Benchmark]
    public SignatureObject Ecdsa_Sign_1KB()
    {
        return _ecdsaSigner.SignBytes(_testData1KB);
    }

    // Signing benchmarks - 64KB data
    [Benchmark]
    public SignatureObject Ed25519_Sign_64KB()
    {
        return _ed25519Signer.SignBytes(_testData64KB);
    }

    [Benchmark]
    public SignatureObject Rsa_Sign_64KB()
    {
        return _rsaSigner.SignBytes(_testData64KB);
    }

    [Benchmark]
    public SignatureObject Ecdsa_Sign_64KB()
    {
        return _ecdsaSigner.SignBytes(_testData64KB);
    }

    // Signing benchmarks - 1MB data
    [Benchmark]
    public SignatureObject Ed25519_Sign_1MB()
    {
        return _ed25519Signer.SignBytes(_testData1MB);
    }

    [Benchmark]
    public SignatureObject Rsa_Sign_1MB()
    {
        return _rsaSigner.SignBytes(_testData1MB);
    }

    [Benchmark]
    public SignatureObject Ecdsa_Sign_1MB()
    {
        return _ecdsaSigner.SignBytes(_testData1MB);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Dispose signers that implement IDisposable
        _rsaSigner?.Dispose();
        _ecdsaSigner?.Dispose();
    }
}