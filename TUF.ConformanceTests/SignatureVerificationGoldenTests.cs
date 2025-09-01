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
    [Test]
    public async Task ECDSA_P256_Signature_Should_Verify_Correctly()
    {
        // This is the actual failing case from the tuf_conformance tests
        // Key ID: 2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7
        
        var publicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt
        0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==
        -----END PUBLIC KEY-----
        """;
        
        var signature = "304502201d77f1efa297539b56c755832691dae9be83ea95c185c10d4c6f3dea1e635d1e022100fa3ca29eb195cf90d95563edc25cfe40a48186b03e2a7ec0c14d2f6ff1f8aa1a";
        
        var signedData = """
        {"_type":"root","consistent_snapshot":true,"expires":"2025-10-01T05:26:16Z","keys":{"2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7":{"keytype":"ecdsa","keyval":{"public":"-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n"},"scheme":"ecdsa-sha2-nistp256"}},"roles":{"root":{"keyids":["2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7"],"threshold":1}},"spec_version":"1.0.31","version":1}
        """;
        
        var key = new Key
        {
            KeyType = "ecdsa",
            Scheme = "ecdsa-sha2-nistp256",
            KeyVal = new KeyValue { Public = publicKeyPem }
        };
        
        var signedBytes = Encoding.UTF8.GetBytes(signedData);
        
        Console.WriteLine($"Testing ECDSA P-256 signature verification");
        Console.WriteLine($"Key Type: {key.KeyType}");
        Console.WriteLine($"Key Scheme: {key.Scheme}");
        Console.WriteLine($"Public Key Length: {key.KeyVal.Public.Length}");
        Console.WriteLine($"Signature Length: {signature.Length}");
        Console.WriteLine($"Signed Data Length: {signedBytes.Length}");
        
        try
        {
            var result = key.VerifySignature(signature, signedBytes);
            Console.WriteLine($"Verification Result: {result}");
            
            // This will initially fail, showing us the issue
            await Assert.That(result).IsTrue();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during verification: {ex}");
            throw;
        }
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
    public void TestCanonicalJsonSerialization()
    {
        // Test that our JSON serialization produces the same canonical form as expected
        var root = new Root
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
                    KeyVal = new KeyValue
                    {
                        Public = "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n"
                    }
                }
            },
            Roles = new Roles
            {
                Root = new RoleKeys
                {
                    KeyIds = ["2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7"],
                    Threshold = 1
                }
            },
            SpecVersion = "1.0.31",
            Version = 1
        };
        
        var serializedJson = Serde.Json.JsonSerializer.Serialize(root);
        Console.WriteLine($"Serialized JSON: {serializedJson}");
        
        var expectedJson = """{"_type":"root","consistent_snapshot":true,"expires":"2025-10-01T05:26:16Z","keys":{"2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7":{"keytype":"ecdsa","keyval":{"public":"-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n"},"scheme":"ecdsa-sha2-nistp256"}},"roles":{"root":{"keyids":["2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7"],"threshold":1}},"spec_version":"1.0.31","version":1}""";
        
        Console.WriteLine($"Expected JSON: {expectedJson}");
        Console.WriteLine($"JSON matches expected: {serializedJson == expectedJson}");
        
        // For debugging purposes, let's see the character-by-character difference if they don't match
        if (serializedJson != expectedJson)
        {
            var minLength = Math.Min(serializedJson.Length, expectedJson.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (serializedJson[i] != expectedJson[i])
                {
                    Console.WriteLine($"First difference at position {i}: got '{serializedJson[i]}' expected '{expectedJson[i]}'");
                    Console.WriteLine($"Context: ...{serializedJson.Substring(Math.Max(0, i - 20), Math.Min(40, serializedJson.Length - Math.Max(0, i - 20)))}...");
                    break;
                }
            }
            if (serializedJson.Length != expectedJson.Length)
            {
                Console.WriteLine($"Length difference: got {serializedJson.Length}, expected {expectedJson.Length}");
            }
        }
        
        // This test helps us understand if the canonical JSON serialization is causing the signature verification issues
        // It doesn't need to pass initially, but helps with debugging
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
                Public = "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n"
            }
        };
        
        var keyId = key.GetKeyId();
        var expectedKeyId = "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7";
        
        Console.WriteLine($"Calculated Key ID: {keyId}");
        Console.WriteLine($"Expected Key ID: {expectedKeyId}");
        Console.WriteLine($"Key ID matches: {keyId == expectedKeyId}");
        
        var keyJson = Serde.Json.JsonSerializer.Serialize(key);
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
        
        var keyJson = """
        {
          "keytype": "ecdsa",
          "keyval": {
            "public": "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJwsHFs2fOgFNIFnX7g+q5Q+ZIdBt\n0sZSWIgYQPjnA7GPirxVsRt/CG8OR9ueMZ43RDlbw3BuN7dd3Dpd+0pKTQ==\n-----END PUBLIC KEY-----\n"
          },
          "scheme": "ecdsa-sha2-nistp256"
        }
        """;
        
        var expectedKeyId = "2ec2f35daed840da76fdd6e2ca51dfb1919992aae5331e4f1edfd70618f9b2b7";
        
        Console.WriteLine("Testing key ID calculation from original conformance test JSON...");
        Console.WriteLine($"Source JSON: {keyJson}");
        Console.WriteLine($"Expected Key ID: {expectedKeyId}");
        
        try
        {
            // Deserialize the exact JSON from the failing test
            var keyBytes = Encoding.UTF8.GetBytes(keyJson);
            var deserializedKey = CanonicalJsonSerializer.Deserialize<Key>(keyJson);
            
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