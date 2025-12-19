using BenchmarkDotNet.Attributes;

using TUF.Models;

namespace TUF.Benchmarks;

/// <summary>
/// Benchmarks for TUF cryptographic operations including key generation, signing, and signature verification.
/// These operations are critical to TUF performance as they happen frequently during client updates.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class CryptographicBenchmarks
{
    private readonly byte[] _smallData = "Hello, TUF!"u8.ToArray();
    private readonly byte[] _mediumData;
    private readonly byte[] _largeData;

    private readonly Ed25519Signer _ed25519Signer;
    private readonly RsaSigner _rsa2048Signer;
    private readonly RsaSigner _rsa3072Signer;
    private readonly RsaSigner _rsa4096Signer;
    private readonly EcdsaSigner _ecdsaSigner;

    private readonly SignatureObject _ed25519Signature;
    private readonly SignatureObject _rsa2048Signature;
    private readonly SignatureObject _rsa3072Signature;
    private readonly SignatureObject _rsa4096Signature;
    private readonly SignatureObject _ecdsaSignature;

    public CryptographicBenchmarks()
    {
        // Create test data of different sizes
        _mediumData = new byte[8192]; // 8KB - typical small metadata file
        _largeData = new byte[1024 * 1024]; // 1MB - large metadata or target file
        Random.Shared.NextBytes(_mediumData);
        Random.Shared.NextBytes(_largeData);

        // Pre-generate signers to benchmark signing operations only
        _ed25519Signer = Ed25519Signer.Generate();
        _rsa2048Signer = RsaSigner.Generate(2048);
        _rsa3072Signer = RsaSigner.Generate(3072);
        _rsa4096Signer = RsaSigner.Generate(4096);
        _ecdsaSigner = EcdsaSigner.Generate();

        // Pre-generate signatures for verification benchmarks
        _ed25519Signature = _ed25519Signer.SignBytes(_smallData);
        _rsa2048Signature = _rsa2048Signer.SignBytes(_smallData);
        _rsa3072Signature = _rsa3072Signer.SignBytes(_smallData);
        _rsa4096Signature = _rsa4096Signer.SignBytes(_smallData);
        _ecdsaSignature = _ecdsaSigner.SignBytes(_smallData);
    }

    // Key Generation Benchmarks - Critical for repository setup

    [Benchmark]
    public Ed25519Signer Ed25519_KeyGeneration() => Ed25519Signer.Generate();

    [Benchmark]
    public RsaSigner Rsa2048_KeyGeneration() => RsaSigner.Generate(2048);

    [Benchmark]
    public RsaSigner Rsa3072_KeyGeneration() => RsaSigner.Generate(3072);

    [Benchmark]
    public RsaSigner Rsa4096_KeyGeneration() => RsaSigner.Generate(4096);

    [Benchmark]
    public EcdsaSigner Ecdsa_KeyGeneration() => EcdsaSigner.Generate();

    // Small Data Signing Benchmarks (typical metadata signing)

    [Benchmark]
    public SignatureObject Ed25519_SignSmallData() => _ed25519Signer.SignBytes(_smallData);

    [Benchmark]
    public SignatureObject Rsa2048_SignSmallData() => _rsa2048Signer.SignBytes(_smallData);

    [Benchmark]
    public SignatureObject Rsa3072_SignSmallData() => _rsa3072Signer.SignBytes(_smallData);

    [Benchmark]
    public SignatureObject Rsa4096_SignSmallData() => _rsa4096Signer.SignBytes(_smallData);

    [Benchmark]
    public SignatureObject Ecdsa_SignSmallData() => _ecdsaSigner.SignBytes(_smallData);

    // Medium Data Signing Benchmarks (8KB metadata files)

    [Benchmark]
    public SignatureObject Ed25519_SignMediumData() => _ed25519Signer.SignBytes(_mediumData);

    [Benchmark]
    public SignatureObject Rsa2048_SignMediumData() => _rsa2048Signer.SignBytes(_mediumData);

    [Benchmark]
    public SignatureObject Ecdsa_SignMediumData() => _ecdsaSigner.SignBytes(_mediumData);

    // Large Data Signing Benchmarks (1MB - stress test)

    [Benchmark]
    public SignatureObject Ed25519_SignLargeData() => _ed25519Signer.SignBytes(_largeData);

    [Benchmark]
    public SignatureObject Rsa2048_SignLargeData() => _rsa2048Signer.SignBytes(_largeData);

    // Key ID Generation Benchmarks (happens during key creation)

    [Benchmark]
    public KeyId Ed25519_KeyIdGeneration() => _ed25519Signer.Key.Id;

    [Benchmark]
    public KeyId Rsa2048_KeyIdGeneration() => _rsa2048Signer.Key.Id;

    [Benchmark]
    public KeyId Ecdsa_KeyIdGeneration() => _ecdsaSigner.Key.Id;
}