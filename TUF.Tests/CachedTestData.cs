using System.Collections.Concurrent;
using CanonicalJson;
using TUF.Models;

namespace TUF.Tests;

/// <summary>
/// Provides cached, pre-generated test data to reduce expensive cryptographic operations during testing.
/// This improves test performance by reusing signers and metadata across test runs.
/// </summary>
public static class CachedTestData
{
    // Thread-safe caches for expensive-to-generate test data
    private static readonly Lazy<Ed25519Signer[]> _ed25519Signers = new(() => GenerateEd25519Signers(20));
    private static readonly Lazy<RsaSigner[]> _rsaSigners = new(() => GenerateRsaSigners(5));
    private static readonly Lazy<EcdsaSigner[]> _ecdsaSigners = new(() => GenerateEcdsaSigners(5));
    private static readonly Lazy<GoldenTestData> _goldenTestData = new(GoldenTestDataGenerator.Generate);
    
    private static readonly ConcurrentDictionary<string, object> _metadataCache = new();
    private static int _ed25519Index = 0;
    private static int _rsaIndex = 0;
    private static int _ecdsaIndex = 0;

    /// <summary>
    /// Statistics for monitoring cache usage and performance.
    /// </summary>
    public static class Statistics
    {
        public static int Ed25519SignersGenerated => _ed25519Signers.IsValueCreated ? _ed25519Signers.Value.Length : 0;
        public static int RsaSignersGenerated => _rsaSigners.IsValueCreated ? _rsaSigners.Value.Length : 0;
        public static int EcdsaSignersGenerated => _ecdsaSigners.IsValueCreated ? _ecdsaSigners.Value.Length : 0;
        public static int MetadataCacheSize => _metadataCache.Count;
        public static bool GoldenDataGenerated => _goldenTestData.IsValueCreated;
        
        public static void LogStatistics()
        {
            Console.WriteLine($"[CachedTestData] Ed25519 Signers: {Ed25519SignersGenerated}, RSA Signers: {RsaSignersGenerated}, ECDSA Signers: {EcdsaSignersGenerated}, Metadata Cache: {MetadataCacheSize}, Golden Data: {GoldenDataGenerated}");
        }
    }

    /// <summary>
    /// Gets a pre-generated Ed25519 signer. Thread-safe with round-robin distribution.
    /// </summary>
    public static Ed25519Signer GetEd25519Signer()
    {
        var signers = _ed25519Signers.Value;
        var index = Interlocked.Increment(ref _ed25519Index) % signers.Length;
        return signers[index];
    }

    /// <summary>
    /// Gets a pre-generated RSA signer. Thread-safe with round-robin distribution.
    /// </summary>
    public static RsaSigner GetRsaSigner()
    {
        var signers = _rsaSigners.Value;
        var index = Interlocked.Increment(ref _rsaIndex) % signers.Length;
        return signers[index];
    }

    /// <summary>
    /// Gets a pre-generated ECDSA signer. Thread-safe with round-robin distribution.
    /// </summary>
    public static EcdsaSigner GetEcdsaSigner()
    {
        var signers = _ecdsaSigners.Value;
        var index = Interlocked.Increment(ref _ecdsaIndex) % signers.Length;
        return signers[index];
    }

    /// <summary>
    /// Gets the pre-generated golden test data containing valid TUF metadata.
    /// </summary>
    public static GoldenTestData GetGoldenTestData() => _goldenTestData.Value;

    /// <summary>
    /// Creates a test-specific metadata object with caching to avoid repeated expensive operations.
    /// </summary>
    public static T GetCachedMetadata<T>(string cacheKey, Func<T> generator) where T : class
    {
        return (T)_metadataCache.GetOrAdd(cacheKey, _ => generator());
    }

    /// <summary>
    /// Creates a simple root metadata for testing with cached signers.
    /// </summary>
    public static Metadata<Root> CreateTestRoot(string? cacheKey = null)
    {
        cacheKey ??= "simple-test-root";
        
        return GetCachedMetadata(cacheKey, () =>
        {
            var rootSigner = GetEd25519Signer();
            var keyId = rootSigner.Key.GetKeyId();

            var root = new Metadata<Root>
            {
                Signed = new Root
                {
                    Type = "root",
                    SpecVersion = "1.0.0",
                    Version = 1,
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    Keys = new Dictionary<string, Key>
                    {
                        [keyId] = rootSigner.Key
                    },
                    Roles = new Roles
                    {
                        Root = new RoleKeys { KeyIds = [keyId], Threshold = 1 },
                        Timestamp = new RoleKeys { KeyIds = [keyId], Threshold = 1 },
                        Snapshot = new RoleKeys { KeyIds = [keyId], Threshold = 1 },
                        Targets = new RoleKeys { KeyIds = [keyId], Threshold = 1 }
                    }
                },
                Signatures = []
            };

            // Sign the metadata
            var signedBytes = CanonicalJson.Serializer.Serialize(root.Signed);
            var signature = rootSigner.SignBytes(signedBytes);
            root = root with { Signatures = [signature] };

            return root;
        });
    }

    /// <summary>
    /// Creates simple targets metadata for testing with cached signers.
    /// </summary>
    public static Metadata<Targets> CreateTestTargets(string? cacheKey = null)
    {
        cacheKey ??= "simple-test-targets";
        
        return GetCachedMetadata(cacheKey, () =>
        {
            var targetsSigner = GetEd25519Signer();

            var targets = new Metadata<Targets>
            {
                Signed = new Targets
                {
                    Type = "targets",
                    SpecVersion = "1.0.0",
                    Version = 1,
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    TargetMap = new Dictionary<string, TargetFile>
                    {
                        ["test-file.txt"] = new TargetFile
                        {
                            Length = 13,
                            Hashes = new Dictionary<string, string>
                            {
                                ["sha256"] = "2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7ae"
                            }
                        }
                    }
                },
                Signatures = []
            };

            // Sign the metadata
            var signedBytes = CanonicalJson.Serializer.Serialize(targets.Signed);
            var signature = targetsSigner.SignBytes(signedBytes);
            targets = targets with { Signatures = [signature] };

            return targets;
        });
    }

    /// <summary>
    /// Clears all cached data. Useful for testing scenarios that need fresh data.
    /// </summary>
    public static void ClearCache()
    {
        _metadataCache.Clear();
        _ed25519Index = 0;
        _rsaIndex = 0;
        _ecdsaIndex = 0;
    }

    /// <summary>
    /// Pre-generates Ed25519 signers for testing.
    /// </summary>
    private static Ed25519Signer[] GenerateEd25519Signers(int count)
    {
        var signers = new Ed25519Signer[count];
        for (int i = 0; i < count; i++)
        {
            signers[i] = Ed25519Signer.Generate();
        }
        return signers;
    }

    /// <summary>
    /// Pre-generates RSA signers for testing. Uses 2048-bit keys for faster generation.
    /// </summary>
    private static RsaSigner[] GenerateRsaSigners(int count)
    {
        var signers = new RsaSigner[count];
        for (int i = 0; i < count; i++)
        {
            signers[i] = RsaSigner.Generate(2048); // Use minimum key size for faster tests
        }
        return signers;
    }

    /// <summary>
    /// Pre-generates ECDSA signers for testing.
    /// </summary>
    private static EcdsaSigner[] GenerateEcdsaSigners(int count)
    {
        var signers = new EcdsaSigner[count];
        for (int i = 0; i < count; i++)
        {
            signers[i] = EcdsaSigner.Generate();
        }
        return signers;
    }
}