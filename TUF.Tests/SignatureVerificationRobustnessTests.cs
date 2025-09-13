using System.Security.Cryptography;
using System.Text;

using TUF.Models;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Comprehensive robustness tests for signature verification.
/// Tests error conditions, boundary cases, and security edge cases
/// to achieve parity with mature TUF implementations like Python-TUF and Go-TUF.
/// </summary>
/// <remarks>
/// These tests ensure that the .NET TUF implementation handles all the error
/// conditions and edge cases that other mature TUF implementations handle,
/// improving security posture and compatibility.
/// 
/// Test categories:
/// 1. Invalid signature format handling
/// 2. Cross-algorithm verification attempts
/// 3. Malformed key data handling
/// 4. Boundary conditions (empty data, huge signatures, etc.)
/// 5. Security-focused negative testing
/// 6. Memory safety and error propagation
/// </remarks>
public class SignatureVerificationRobustnessTests
{
    #region Invalid Signature Format Tests

    [Test]
    public async Task VerifySignature_InvalidHexString_ReturnsFalse()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "test data"u8.ToArray();
        var invalidSignatures = new[]
        {
            "invalid-hex-string",
            "zzzzzzzzzzzzzzzz",
            "12345g67890abcdef", // Contains invalid hex character 'g'
            "12345", // Too short
            "", // Empty
            "   ", // Whitespace only
            "0x123456", // With 0x prefix
            "123456 789abc" // With space
        };

        // Act & Assert
        foreach (var invalidSig in invalidSignatures)
        {
            var result = signer.Key.VerifySignature(invalidSig, testData);
            await Assert.That(result).IsFalse();
        }
    }

    [Test]
    public async Task VerifySignature_TruncatedSignature_ReturnsFalse()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "test data"u8.ToArray();
        var validSignature = signer.SignBytes(testData);

        // Act & Assert - Test various truncated signatures
        for (int length = 1; length < validSignature.Sig.Length; length += 2)
        {
            var truncatedSig = validSignature.Sig[..length];
            var result = signer.Key.VerifySignature(truncatedSig, testData);
            await Assert.That(result).IsFalse();
        }
    }

    [Test]
    public async Task VerifySignature_ExtendedSignature_ReturnsFalse()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "test data"u8.ToArray();
        var validSignature = signer.SignBytes(testData);

        // Act & Assert - Test signatures with extra bytes
        var extendedSignatures = new[]
        {
            validSignature.Sig + "00",
            validSignature.Sig + "deadbeef",
            validSignature.Sig + validSignature.Sig, // Doubled
        };

        foreach (var extendedSig in extendedSignatures)
        {
            var result = signer.Key.VerifySignature(extendedSig, testData);
            await Assert.That(result).IsFalse();
        }
    }

    #endregion

    #region Cross-Algorithm Verification Tests

    [Test]
    public async Task VerifySignature_Ed25519KeyWithRsaSignature_ReturnsFalse()
    {
        // Arrange
        var ed25519Signer = Ed25519Signer.Generate();
        var rsaSigner = RsaSigner.Generate(2048);
        var testData = "test data"u8.ToArray();
        
        // Create RSA signature
        var rsaSignature = rsaSigner.SignBytes(testData);

        // Act - Try to verify RSA signature with Ed25519 key
        var result = ed25519Signer.Key.VerifySignature(rsaSignature.Sig, testData);

        // Assert - Should fail
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifySignature_RsaKeyWithEd25519Signature_ReturnsFalse()
    {
        // Arrange
        var ed25519Signer = Ed25519Signer.Generate();
        var rsaSigner = RsaSigner.Generate(2048);
        var testData = "test data"u8.ToArray();
        
        // Create Ed25519 signature
        var ed25519Signature = ed25519Signer.SignBytes(testData);

        // Act - Try to verify Ed25519 signature with RSA key
        var result = rsaSigner.Key.VerifySignature(ed25519Signature.Sig, testData);

        // Assert - Should fail
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifySignature_EcdsaKeyWithRsaSignature_ReturnsFalse()
    {
        // Arrange
        var ecdsaSigner = EcdsaSigner.Generate();
        var rsaSigner = RsaSigner.Generate(2048);
        var testData = "test data"u8.ToArray();
        
        // Create RSA signature
        var rsaSignature = rsaSigner.SignBytes(testData);

        // Act - Try to verify RSA signature with ECDSA key
        var result = ecdsaSigner.Key.VerifySignature(rsaSignature.Sig, testData);

        // Assert - Should fail
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Malformed Key Data Tests

    [Test]
    public async Task VerifySignature_CorruptedEd25519PublicKey_ReturnsFalse()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "test data"u8.ToArray();
        var signature = signer.SignBytes(testData);

        // Create a key with corrupted public key data
        var corruptedKey = new Key
        {
            KeyType = "ed25519",
            Scheme = "ed25519",
            KeyVal = new KeyValue 
            { 
                Public = "invalid-key-data-not-hex" 
            }
        };

        // Act
        var result = corruptedKey.VerifySignature(signature.Sig, testData);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifySignature_Ed25519KeyWithWrongLength_ReturnsFalse()
    {
        // Arrange
        var testData = "test data"u8.ToArray();
        var validSignature = "0123456789abcdef" + "0123456789abcdef" + "0123456789abcdef" + "0123456789abcdef"; // 64 hex chars = 32 bytes

        // Ed25519 public keys should be 32 bytes (64 hex chars), test various wrong lengths
        var wrongLengthKeys = new[]
        {
            "01", // Too short - 1 byte
            "0123456789abcdef", // Too short - 8 bytes  
            "0123456789abcdef" + "0123456789abcdef", // Too short - 16 bytes
            "0123456789abcdef" + "0123456789abcdef" + "0123456789abcdef", // Too short - 24 bytes
            "0123456789abcdef" + "0123456789abcdef" + "0123456789abcdef" + "0123456789abcdef" + "01234567", // Too long - 36 bytes
        };

        foreach (var keyData in wrongLengthKeys)
        {
            var key = new Key
            {
                KeyType = "ed25519",
                Scheme = "ed25519", 
                KeyVal = new KeyValue { Public = keyData }
            };

            // Act
            var result = key.VerifySignature(validSignature, testData);

            // Assert
            await Assert.That(result).IsFalse();
        }
    }

    [Test]
    public async Task VerifySignature_RsaKeyWithInvalidPem_ReturnsFalse()
    {
        // Arrange
        var testData = "test data"u8.ToArray();
        var validSignature = new string('0', 512); // 256 bytes in hex

        var invalidPemKeys = new[]
        {
            "-----BEGIN PUBLIC KEY-----\nINVALID_BASE64_DATA\n-----END PUBLIC KEY-----",
            "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQ...\n-----END PRIVATE KEY-----", // Wrong key type
            "NOT_PEM_FORMAT_AT_ALL",
            "", // Empty
            "-----BEGIN PUBLIC KEY-----", // Missing end
            "-----END PUBLIC KEY-----", // Missing begin
            "-----BEGIN PUBLIC KEY-----\n\n-----END PUBLIC KEY-----", // Empty content
        };

        foreach (var invalidPem in invalidPemKeys)
        {
            var key = new Key
            {
                KeyType = "rsa",
                Scheme = "rsassa-pss-sha256",
                KeyVal = new KeyValue { Public = invalidPem }
            };

            // Act
            var result = key.VerifySignature(validSignature, testData);

            // Assert
            await Assert.That(result).IsFalse();
        }
    }

    #endregion

    #region Boundary Condition Tests

    [Test]
    public async Task VerifySignature_EmptyData_HandlesGracefully()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var emptyData = Array.Empty<byte>();
        var signature = signer.SignBytes(emptyData);

        // Act
        var result = signer.Key.VerifySignature(signature.Sig, emptyData);

        // Assert - Should still work with empty data
        await Assert.That(result).IsTrue();

        // Test with different data - should fail
        var nonEmptyData = "not empty"u8.ToArray();
        var resultWithDifferentData = signer.Key.VerifySignature(signature.Sig, nonEmptyData);
        await Assert.That(resultWithDifferentData).IsFalse();
    }

    [Test]
    public async Task VerifySignature_LargeData_HandlesEfficiently()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        
        // Create large test data (1MB)
        var largeData = new byte[1024 * 1024];
        Random.Shared.NextBytes(largeData);
        
        // Act
        var signature = signer.SignBytes(largeData);
        var result = signer.Key.VerifySignature(signature.Sig, largeData);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifySignature_NullAndEmptySignature_ReturnsFalse()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "test data"u8.ToArray();

        // Act & Assert
        var emptyResult = signer.Key.VerifySignature("", testData);
        await Assert.That(emptyResult).IsFalse();

        var whitespaceResult = signer.Key.VerifySignature("   ", testData);
        await Assert.That(whitespaceResult).IsFalse();
    }

    #endregion

    #region Data Integrity Tests

    [Test]
    public async Task VerifySignature_TamperedData_ReturnsFalse()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var originalData = "original test data"u8.ToArray();
        var signature = signer.SignBytes(originalData);

        // Create variations of tampered data
        var tamperedVariations = new[]
        {
            "Original test data"u8.ToArray(), // Different case
            "original test dat"u8.ToArray(),  // Truncated
            "original test data!"u8.ToArray(), // Extra character
            "original test data\0"u8.ToArray(), // Null terminator
            "original test\ndata"u8.ToArray(), // Different whitespace
            Array.Empty<byte>(), // Empty
            "completely different data"u8.ToArray(), // Completely different
        };

        // Act & Assert
        foreach (var tamperedData in tamperedVariations)
        {
            var result = signer.Key.VerifySignature(signature.Sig, tamperedData);
            await Assert.That(result).IsFalse();
        }
    }

    [Test]
    public async Task VerifySignature_SingleBitFlip_ReturnsFalse()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var originalData = "test data for single bit flip detection"u8.ToArray();
        var signature = signer.SignBytes(originalData);

        // Act & Assert - Test single bit flips at various positions
        for (int byteIndex = 0; byteIndex < originalData.Length; byteIndex++)
        {
            for (int bitIndex = 0; bitIndex < 8; bitIndex++)
            {
                var tamperedData = (byte[])originalData.Clone();
                tamperedData[byteIndex] ^= (byte)(1 << bitIndex); // Flip single bit

                var result = signer.Key.VerifySignature(signature.Sig, tamperedData);
                await Assert.That(result).IsFalse();
            }
        }
    }

    #endregion

    #region Security Edge Cases

    [Test]
    public async Task VerifySignature_ReplayAttack_DetectedCorrectly()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var message1 = "message 1"u8.ToArray();
        var message2 = "message 2"u8.ToArray();
        
        var signature1 = signer.SignBytes(message1);
        var signature2 = signer.SignBytes(message2);

        // Act & Assert - Signature from message1 should not validate message2
        var replayResult = signer.Key.VerifySignature(signature1.Sig, message2);
        await Assert.That(replayResult).IsFalse();

        // But signatures should verify their own messages
        var valid1 = signer.Key.VerifySignature(signature1.Sig, message1);
        var valid2 = signer.Key.VerifySignature(signature2.Sig, message2);
        await Assert.That(valid1).IsTrue();
        await Assert.That(valid2).IsTrue();
    }

    [Test]
    public async Task VerifySignature_DifferentKeysSameData_ProduceDifferentSignatures()
    {
        // Arrange
        var signer1 = Ed25519Signer.Generate();
        var signer2 = Ed25519Signer.Generate();
        var testData = "same test data for both signers"u8.ToArray();

        // Act
        var signature1 = signer1.SignBytes(testData);
        var signature2 = signer2.SignBytes(testData);

        // Assert - Signatures should be different
        await Assert.That(signature1.KeyId).IsNotEqualTo(signature2.KeyId);
        await Assert.That(signature1.Sig).IsNotEqualTo(signature2.Sig);

        // Cross verification should fail
        var crossVerify1 = signer1.Key.VerifySignature(signature2.Sig, testData);
        var crossVerify2 = signer2.Key.VerifySignature(signature1.Sig, testData);
        await Assert.That(crossVerify1).IsFalse();
        await Assert.That(crossVerify2).IsFalse();

        // Self verification should succeed
        var selfVerify1 = signer1.Key.VerifySignature(signature1.Sig, testData);
        var selfVerify2 = signer2.Key.VerifySignature(signature2.Sig, testData);
        await Assert.That(selfVerify1).IsTrue();
        await Assert.That(selfVerify2).IsTrue();
    }

    #endregion

    #region Memory and Performance Tests

    [Test]
    public async Task VerifySignature_MemoryPressure_HandlesGracefully()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "test data under memory pressure"u8.ToArray();
        var signature = signer.SignBytes(testData);

        // Act - Repeatedly verify under memory pressure
        for (int i = 0; i < 1000; i++)
        {
            // Create some memory pressure
            var tempData = new byte[1024];
            Random.Shared.NextBytes(tempData);
            
            var result = signer.Key.VerifySignature(signature.Sig, testData);
            
            // Assert - Should always succeed
            await Assert.That(result).IsTrue();
        }
    }

    [Test]
    public async Task VerifySignature_ConcurrentVerification_ThreadSafe()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();
        var testData = "concurrent test data"u8.ToArray();
        var signature = signer.SignBytes(testData);
        var results = new bool[100];

        // Act - Verify signature concurrently from multiple threads
        await Parallel.ForAsync(0, 100, (i, cancellationToken) =>
        {
            results[i] = signer.Key.VerifySignature(signature.Sig, testData);
            return ValueTask.CompletedTask;
        });

        // Assert - All concurrent verifications should succeed
        foreach (var result in results)
        {
            await Assert.That(result).IsTrue();
        }
    }

    #endregion

    #region Algorithm-Specific Edge Cases

    [Test]
    public async Task VerifySignature_RsaKeyTooSmall_HandledGracefully()
    {
        // Arrange - Try to test very small RSA keys (not supported by modern .NET)
        // This test verifies our implementation handles key size validation properly
        var testData = "test data"u8.ToArray();
        var validSignature = new string('0', 256); // 128 bytes in hex

        // Create a key claiming to be RSA but with invalid size data
        var smallRsaKey = new Key
        {
            KeyType = "rsa",
            Scheme = "rsassa-pss-sha256",
            KeyVal = new KeyValue 
            { 
                Public = "-----BEGIN PUBLIC KEY-----\nTINY_KEY\n-----END PUBLIC KEY-----" 
            }
        };

        // Act
        var result = smallRsaKey.VerifySignature(validSignature, testData);

        // Assert - Should fail gracefully
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifySignature_EcdsaWrongCurve_HandledGracefully()
    {
        // Test ECDSA key claiming P-256 but with wrong curve data
        var testData = "test data"u8.ToArray();
        var validSignature = new string('0', 128); // 64 bytes in hex

        var wrongCurveKey = new Key
        {
            KeyType = "ecdsa",
            Scheme = "ecdsa-sha2-nistp256", 
            KeyVal = new KeyValue 
            { 
                Public = "-----BEGIN PUBLIC KEY-----\nWRONG_CURVE_DATA\n-----END PUBLIC KEY-----" 
            }
        };

        // Act
        var result = wrongCurveKey.VerifySignature(validSignature, testData);

        // Assert - Should fail gracefully
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Scheme Validation Tests

    [Test]
    public async Task VerifySignature_UnsupportedScheme_ReturnsFalse()
    {
        // Arrange
        var testData = "test data"u8.ToArray();
        var signature = new string('0', 128);

        var unsupportedSchemes = new[]
        {
            new Key { KeyType = "ed25519", Scheme = "unsupported-scheme", KeyVal = new KeyValue { Public = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef" }},
            new Key { KeyType = "rsa", Scheme = "rsa-pkcs1-sha256", KeyVal = new KeyValue { Public = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...\n-----END PUBLIC KEY-----" }}, // Wrong RSA scheme
            new Key { KeyType = "ecdsa", Scheme = "ecdsa-sha1", KeyVal = new KeyValue { Public = "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE...\n-----END PUBLIC KEY-----" }}, // Wrong ECDSA scheme
            new Key { KeyType = "unknown", Scheme = "unknown-scheme", KeyVal = new KeyValue { Public = "unknown" }}
        };

        // Act & Assert
        foreach (var key in unsupportedSchemes)
        {
            var result = key.VerifySignature(signature, testData);
            await Assert.That(result).IsFalse();
        }
    }

    #endregion
}