using System.Text;
using TUF.Models;
using TUnit.Core;
using TUnit.Assertions;
using CanonicalJson;

namespace TUF.ConformanceTests;

/// <summary>
/// Golden master tests for signature verification using known-good test vectors.
/// These tests help ensure that our signature verification implementation works correctly
/// with real-world TUF metadata samples.
/// </summary>
public class SignatureVerificationGoldenTests
{
    public static string SampleRootJson = """{"_type":"root","consistent_snapshot":true,"expires":"2025-10-01T05:26:16Z","keys":{"2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7":{"keytype":"ecdsa","keyval":{"public":"-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n"},"scheme":"ecdsa-sha2-nistp256"},"302012e0fc7674cbe082657d1a655f281b4fd5ea77c3605f14b1617a82496fc5":{"keytype":"ecdsa","keyval":{"public":"-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEdBC0LVnw8OtbbtQLpIfa0463/bFe\nmlpkS8Qd8uLnZaPFH85keSJtF...cDQgAEj7HrbIgIvwAYZK+tDMOv9SWg70x1\nGZvXuFYnaiZoDz2y7LvntrARKu/tjBh+fssk+BDdhFJmIsM+sbObMVgq6g==\n-----END PUBLIC KEY-----\n"},"scheme":"ecdsa-sha2-nistp256"},"9e180a675201d1725c5eda39d886abdcaf8a718777d0dd3620e4b9eff4a2a66f":{"keytype":"ecdsa","keyval":{"public":"-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE/qCad5Aq3yz9fOfr8seYvkbDv7EM\nFOY9Oyph7xtScaWHTOfkJRvkNVbsLBm0XLfuQTRNbVVvGBS1zsUpHQU/Pg==\n-----END PUBLIC KEY-----\n"},"scheme":"ecdsa-sha2-nistp256"}},"roles":{"root":{"keyids":["2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7"],"threshold":1},"snapshot":{"keyids":["302012e0fc7674cbe082657d1a655f281b4fd5ea77c3605f14b1617a82496fc5"],"threshold":1},"targets":{"keyids":["521d9281004708db89dcf198c20d5faf5ad287bb3b0e627e571708bf2eaff149"],"threshold":1},"timestamp":{"keyids":["9e180a675201d1725c5eda39d886abdcaf8a718777d0dd3620e4b9eff4a2a66f"],"threshold":1}},"spec_version":"1.0.31","version":1}""";
    public static string SampleRootMetadataJson = $$"""{"signatures":[{"keyid":"2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7","sig":"304502201d77f1efa297539b56c755832691dae9be83ea95c185c10d4c6f3dea1e635d1e022100fa3ca29eb195cf90d95563edc25cfe40a48186b03e2a7ec0c14d2f6ff1f8aa1a"}],"signed":{{SampleRootJson}}}""";

    public static Metadata<Root> SampleRoot = new Metadata<Root>()
    {
        Signed = new Root
        {
            Type = "root",
            ConsistentSnapshot = true,
            Expires = "2025-10-01T05:26:16Z",
            Keys = new Dictionary<string, Key>
            {
                ["2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7"] = new Key
                {
                    KeyType = "ecdsa",
                    Scheme = "ecdsa-sha2-nistp256",
                    KeyVal = new KeyValue { Public = "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n" }
                },
                ["302012e0fc7674cbe082657d1a655f281b4fd5ea77c3605f14b1617a82496fc5"] = new()
                {
                    KeyType = "ecdsa",
                    Scheme = "ecdsa-sha2-nistp256",
                    KeyVal = new KeyValue { Public = "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEdBC0LVnw8OtbbtQLpIfa0463/bFe\nmlpkS8Qd8uLnZaPFH85keSJtF...cDQgAEj7HrbIgIvwAYZK+tDMOv9SWg70x1\nGZvXuFYnaiZoDz2y7LvntrARKu/tjBh+fssk+BDdhFJmIsM+sbObMVgq6g==\n-----END PUBLIC KEY-----\n" }
                },
                ["9e180a675201d1725c5eda39d886abdcaf8a718777d0dd3620e4b9eff4a2a66f"] = new()
                {
                    KeyType = "ecdsa",
                    Scheme = "ecdsa-sha2-nistp256",
                    KeyVal = new KeyValue { Public = "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE/qCad5Aq3yz9fOfr8seYvkbDv7EM\nFOY9Oyph7xtScaWHTOfkJRvkNVbsLBm0XLfuQTRNbVVvGBS1zsUpHQU/Pg==\n-----END PUBLIC KEY-----\n" }
                }
            },
            Roles = new Roles
            {
                Root = new RoleKeys
                {
                    KeyIds = ["2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7"],
                    Threshold = 1
                },
                Snapshot = new RoleKeys
                {
                    KeyIds = ["302012e0fc7674cbe082657d1a655f281b4fd5ea77c3605f14b1617a82496fc5"],
                    Threshold = 1
                },
                Targets = new RoleKeys
                {
                    KeyIds = ["521d9281004708db89dcf198c20d5faf5ad287bb3b0e627e571708bf2eaff149"],
                    Threshold = 1
                },
                Timestamp = new RoleKeys
                {
                    KeyIds = ["9e180a675201d1725c5eda39d886abdcaf8a718777d0dd3620e4b9eff4a2a66f"],
                    Threshold = 1
                }
            },
            SpecVersion = "1.0.31",
            Version = 1
        },
        Signatures = [
            new(){ KeyId = "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7",
                  Sig = "304502201d77f1efa297539b56c755832691dae9be83ea95c185c10d4c6f3dea1e635d1e022100fa3ca29eb195cf90d95563edc25cfe40a48186b03e2a7ec0c14d2f6ff1f8aa1a" }
        ]
    };

    [Test]
    public async Task ECDSA_P256_Signature_Should_Verify_Correctly()
    {
        // This is the actual failing case from the tuf_conformance tests
        // Key ID: 2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7
        // We need to recreate the exact scenario using proper TUF metadata structures
        // Instead of using raw string data, properly create the Root metadata structure

        var (keyId, key) = SampleRoot.Signed.Keys.First();

        // Use canonical JSON serialization to get the signed bytes, just like the real implementation
        var signedBytes = CanonicalJson.Serializer.Serialize(SampleRoot.Signed);

        Console.WriteLine($"Testing ECDSA P-256 signature verification");
        Console.WriteLine($"Key Type: {key.KeyType}");
        Console.WriteLine($"Key Scheme: {key.Scheme}");
        Console.WriteLine($"Public Key Length: {key.KeyVal.Public.Length}");
        Console.WriteLine($"Signature Length: {SampleRoot.Signatures.First().Sig.Length}");
        Console.WriteLine($"Signed Data Length: {signedBytes.Length}");

        try
        {
            var result = key.VerifySignature(SampleRoot.Signatures.First().Sig, signedBytes);
            // This should now work with proper canonical JSON serialization
            await Assert.That(result).IsTrue();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during verification: {ex}");
            throw;
        }
    }

    [Test]
    public async Task SampleRootCanonicalEqualsStringLiteral()
    {
        Metadata<Root> samplefromJson = CanonicalJson.Serializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(SampleRootMetadataJson);
        await Assert.That(samplefromJson).IsEquivalentTo(SampleRoot);
    }

    [Test]
    public async Task SampleRootSignedBytesEqualsRootStringLiteral()
    {
        Metadata<Root> samplefromJson = CanonicalJson.Serializer.Deserialize<Metadata<Root>, MetadataProxy.De<Root>>(SampleRootMetadataJson);
        await Assert.That(Encoding.UTF8.GetString(samplefromJson.GetSignedBytes())).IsEquivalentTo(SampleRootJson);
    }

    [Test]
    public async Task Ed25519_Signature_Should_Verify_Correctly()
    {
        // Create a known-good Ed25519 test vector
        var signer = Ed25519Signer.Generate();
        var testData = "Test data for Ed25519 signature"u8.ToArray();
        var signature = signer.SignBytes(testData);

        Console.WriteLine($"Testing Ed25519 signature verification with generated key");
        Console.WriteLine($"Key ID: {signature.KeyId}");
        Console.WriteLine($"Key Type: {signer.Key.KeyType}");
        Console.WriteLine($"Key Scheme: {signer.Key.Scheme}");

        var result = signer.Key.VerifySignature(signature.Sig, testData);
        Console.WriteLine($"Verification Result: {result}");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task RSA_PSS_Signature_Should_Verify_Correctly()
    {
        // Create a known-good RSA PSS test vector
        var signer = RsaSigner.Generate(2048);
        var testData = "Test data for RSA PSS signature"u8.ToArray();
        var signature = signer.SignBytes(testData);

        Console.WriteLine($"Testing RSA PSS signature verification with generated key");
        Console.WriteLine($"Key ID: {signature.KeyId}");
        Console.WriteLine($"Key Type: {signer.Key.KeyType}");
        Console.WriteLine($"Key Scheme: {signer.Key.Scheme}");

        var result = signer.Key.VerifySignature(signature.Sig, testData);
        Console.WriteLine($"Verification Result: {result}");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ECDSA_P256_Signature_Should_Verify_Correctly_WithGeneratedKey()
    {
        // Create a known-good ECDSA P-256 test vector
        var signer = EcdsaSigner.Generate();
        var testData = "Test data for ECDSA P-256 signature"u8.ToArray();
        var signature = signer.SignBytes(testData);

        Console.WriteLine($"Testing ECDSA P-256 signature verification with generated key");
        Console.WriteLine($"Key ID: {signature.KeyId}");
        Console.WriteLine($"Key Type: {signer.Key.KeyType}");
        Console.WriteLine($"Key Scheme: {signer.Key.Scheme}");

        var result = signer.Key.VerifySignature(signature.Sig, testData);
        Console.WriteLine($"Verification Result: {result}");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TestKeyIdCalculation()
    {
        // Test that key ID calculation matches the expected value from TUF conformance tests.
        // 
        // This test data comes from the original failing tuf_conformance test:
        // "test_basic_refresh_requests" which failed with:
        // "Signature verification failed for key 2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7"
        //
        // The public key and expected key ID are extracted from the root metadata JSON
        // that was being verified in the conformance test. According to the TUF specification,
        // the key ID should be the SHA-256 hash of the canonical JSON representation of the key.
        //
        // If this test fails, it indicates our key ID calculation algorithm or JSON
        // canonicalization doesn't match the TUF spec, which would cause signature
        // verification to fail even with correct signatures.
        var key = new Key
        {
            KeyType = "ecdsa",
            Scheme = "ecdsa-sha2-nistp256",
            KeyVal = new KeyValue
            {
                Public = @"-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n"
            }
        };

        var keyId = key.GetKeyId();
        var expectedKeyId = "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7";

        Console.WriteLine($"Calculated Key ID: {keyId}");
        Console.WriteLine($"Expected Key ID: {expectedKeyId}");
        Console.WriteLine($"Key ID matches: {keyId == expectedKeyId}");

        var keyJson = CanonicalJson.Serializer.Serialize(key);
        Console.WriteLine($"Key JSON: {keyJson}");

        // This test helps us understand if the key ID calculation is working correctly
        // which is critical for signature verification
        await Assert.That(keyId).IsEqualTo(expectedKeyId);
    }

    [Test]
    public async Task TestKeyIdCalculation_FromOriginalConformanceTestJson()
    {
        // Test key ID calculation using the exact JSON key data from the failing tuf_conformance test.
        // This is the raw key JSON from the root metadata that caused the original failure:
        // "keys": {
        //   "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7": {
        //     "keytype": "ecdsa",
        //     "keyval": {
        //       "public": "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n"
        //     },
        //     "scheme": "ecdsa-sha2-nistp256"
        //   }
        // }

        var keyJson = """{"keytype":"ecdsa","keyval":{"public":"-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n"},"scheme":"ecdsa-sha2-nistp256"}""";

        var expectedKeyId = "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7";

        Console.WriteLine("Testing key ID calculation from original conformance test JSON...");
        Console.WriteLine($"Source JSON: {keyJson}");
        Console.WriteLine($"Expected Key ID: {expectedKeyId}");

        try
        {
            // Deserialize the exact JSON from the failing test
            var keyBytes = Encoding.UTF8.GetBytes(keyJson);
            var deserializedKey = CanonicalJson.Serializer.Deserialize<Key>(keyJson);

            Console.WriteLine($"Deserialized Key Type: {deserializedKey.KeyType}");
            Console.WriteLine($"Deserialized Key Scheme: {deserializedKey.Scheme}");
            Console.WriteLine($"Deserialized Public Key Length: {deserializedKey.KeyVal.Public.Length}");

            // Calculate the key ID from the deserialized key
            var calculatedKeyId = deserializedKey.GetKeyId();
            Console.WriteLine($"Calculated Key ID: {calculatedKeyId}");
            Console.WriteLine($"Key ID matches expected: {calculatedKeyId == expectedKeyId}");

            // This test verifies that our deserialization + key ID calculation pipeline
            // produces the same result as expected by the TUF conformance tests
            await Assert.That(calculatedKeyId).IsEqualTo(expectedKeyId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during key deserialization or ID calculation: {ex}");
            throw;
        }
    }
}