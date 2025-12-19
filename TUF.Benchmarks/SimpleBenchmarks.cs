using BenchmarkDotNet.Attributes;

using CanonicalJson;

using TUF.Models;

namespace TUF.Benchmarks;

/// <summary>
/// Simple benchmarks focusing on the most critical TUF operations.
/// These measure the core performance characteristics without complex setup.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class SimpleBenchmarks
{
    private readonly byte[] _smallData = "Hello, TUF!"u8.ToArray();
    private readonly byte[] _mediumData = new byte[8192]; // 8KB
    private readonly Ed25519Signer _ed25519Signer;
    private readonly RsaSigner _rsa2048Signer;
    private readonly EcdsaSigner _ecdsaSigner;

    private readonly Root _sampleRoot;
    private readonly string _serializedRoot;

    public SimpleBenchmarks()
    {
        Random.Shared.NextBytes(_mediumData);

        // Pre-generate signers
        _ed25519Signer = Ed25519Signer.Generate();
        _rsa2048Signer = RsaSigner.Generate(2048);
        _ecdsaSigner = EcdsaSigner.Generate();

        // Create a simple root metadata for serialization benchmarks
        _sampleRoot = new Root
        {
            Type = "root",
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddDays(365),
            Keys = new Dictionary<string, Key>
            {
                [_ed25519Signer.Key.Id.Value] = _ed25519Signer.Key
            },
            Roles = new Roles
            {
                Root = new RoleKeys { KeyIds = [_ed25519Signer.Key.Id.Value], Threshold = 1 },
                Timestamp = new RoleKeys { KeyIds = [_ed25519Signer.Key.Id.Value], Threshold = 1 },
                Snapshot = new RoleKeys { KeyIds = [_ed25519Signer.Key.Id.Value], Threshold = 1 },
                Targets = new RoleKeys { KeyIds = [_ed25519Signer.Key.Id.Value], Threshold = 1 }
            }
        };

        // Pre-serialize for deserialization benchmark
        var serializedBytes = CanonicalJson.Serializer.Serialize(_sampleRoot);
        _serializedRoot = System.Text.Encoding.UTF8.GetString(serializedBytes);
    }

    // Cryptographic Operation Benchmarks

    [Benchmark]
    public Ed25519Signer Ed25519_KeyGeneration() => Ed25519Signer.Generate();

    [Benchmark]
    public RsaSigner Rsa2048_KeyGeneration() => RsaSigner.Generate(2048);

    [Benchmark]
    public EcdsaSigner Ecdsa_KeyGeneration() => EcdsaSigner.Generate();

    [Benchmark]
    public SignatureObject Ed25519_SignSmallData() => _ed25519Signer.SignBytes(_smallData);

    [Benchmark]
    public SignatureObject Rsa2048_SignSmallData() => _rsa2048Signer.SignBytes(_smallData);

    [Benchmark]
    public SignatureObject Ecdsa_SignSmallData() => _ecdsaSigner.SignBytes(_smallData);

    [Benchmark]
    public SignatureObject Ed25519_SignMediumData() => _ed25519Signer.SignBytes(_mediumData);

    [Benchmark]
    public SignatureObject Rsa2048_SignMediumData() => _rsa2048Signer.SignBytes(_mediumData);

    // Serialization Benchmarks

    [Benchmark]
    public byte[] SerializeRoot() => CanonicalJson.Serializer.Serialize(_sampleRoot);

    [Benchmark]
    public Root DeserializeRoot() => CanonicalJson.Serializer.Deserialize<Root>(_serializedRoot);

    [Benchmark]
    public Root RoundTripRoot()
    {
        var serializedBytes = CanonicalJson.Serializer.Serialize(_sampleRoot);
        var serializedString = System.Text.Encoding.UTF8.GetString(serializedBytes);
        return CanonicalJson.Serializer.Deserialize<Root>(serializedString);
    }

    // Key ID computation benchmark

    [Benchmark]
    public KeyId Ed25519_GetKeyId() => _ed25519Signer.Key.Id;

    [Benchmark]
    public KeyId Rsa2048_GetKeyId() => _rsa2048Signer.Key.Id;
}