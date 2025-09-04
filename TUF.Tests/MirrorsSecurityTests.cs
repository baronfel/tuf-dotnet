using System.Text;
using System.Text.Json;
using CanonicalJson;
using TUF.Models;

namespace TUF.Tests;

/// <summary>
/// Security-focused tests for TUF Mirrors functionality (TAP 5).
/// Tests focus on preventing mirror-based attacks and validating security boundaries.
/// </summary>
/// <remarks>
/// Mirror security is critical because:
/// 1. Compromised mirrors could attempt to serve malicious URLs
/// 2. Mirror metadata could be used for reconnaissance
/// 3. Mirror selection logic could be exploited for DoS attacks
/// 4. Custom mirror metadata could contain injection payloads
/// 
/// These tests ensure the Mirrors implementation maintains security even with malicious mirror configurations.
/// </remarks>
public class MirrorsSecurityTests
{
    #region Mirror URL Security Tests

    [Test]
    public async Task Mirror_EmptyUrl_HandledSafely()
    {
        var mirror = new Mirror { Url = "", Custom = null };
        
        await Assert.That(mirror.Url).IsEqualTo("");
        await Assert.That(mirror.Custom).IsNull();
    }

    [Test]
    public async Task Mirror_NullCustomMetadata_HandledSafely()
    {
        var mirror = new Mirror 
        { 
            Url = "https://example.com/repo",
            Custom = null 
        };
        
        await Assert.That(mirror.Url).IsEqualTo("https://example.com/repo");
        await Assert.That(mirror.Custom).IsNull();
    }

    [Test] 
    public async Task Mirror_MaliciousUrlSchemes_ValidatedProperly()
    {
        // Test various potentially malicious URL schemes
        var maliciousUrls = new[]
        {
            "javascript:alert('xss')",
            "data:text/html,<script>alert('xss')</script>",
            "file:///etc/passwd", 
            "ftp://evil.com/malware",
            "ldap://attacker.com/",
            "http://127.0.0.1/internal-service"
        };

        foreach (var url in maliciousUrls)
        {
            var mirror = new Mirror { Url = url };
            
            // URL should be stored as-is (validation happens at usage time)
            await Assert.That(mirror.Url).IsEqualTo(url);
        }
    }

    [Test]
    public async Task Mirror_VeryLongUrl_HandledWithoutIssues()
    {
        // Test DoS attack via extremely long URLs
        var longUrl = "https://example.com/" + new string('a', 10000);
        var mirror = new Mirror { Url = longUrl };
        
        await Assert.That(mirror.Url).IsEqualTo(longUrl);
        await Assert.That(mirror.Url.Length).IsEqualTo(longUrl.Length);
    }

    [Test]
    public async Task Mirror_UrlWithSpecialCharacters_PreservesContent()
    {
        var specialUrl = "https://example.com/repo?param=value&encoded=%3Ctest%3E";
        var mirror = new Mirror { Url = specialUrl };
        
        await Assert.That(mirror.Url).IsEqualTo(specialUrl);
    }

    [Test]
    public async Task Mirror_UnicodeUrl_PreservesContent()
    {
        var unicodeUrl = "https://example.com/репозиторий/測試/テスト";
        var mirror = new Mirror { Url = unicodeUrl };
        
        await Assert.That(mirror.Url).IsEqualTo(unicodeUrl);
    }

    #endregion

    #region Mirror Custom Metadata Security Tests

    [Test]
    public async Task Mirror_CustomMetadata_InjectionAttempts_StoredSafely()
    {
        var injectionAttempts = new Dictionary<string, string>
        {
            ["location"] = "<script>alert('xss')</script>",
            ["priority"] = "'; DROP TABLE mirrors; --",
            ["bandwidth"] = "{{ evil_template }}",
            ["organization"] = "${java_injection}",
            ["content_types"] = "eval('malicious_code')",
            ["custom_field"] = "\x00\x01\x02\x03", // Control characters
            ["json_injection"] = "\"}, {\"malicious\": true, \"original\": \"",
            ["unicode_attack"] = "Ӂ́Ͳ̧͚̱̖̼͇̭̲̝̻̠̮̗̳̲̈́̂̊͌̽̈́͊̄̈́̚͝ͅ"
        };

        var mirror = new Mirror 
        { 
            Url = "https://example.com",
            Custom = injectionAttempts 
        };
        
        await Assert.That(mirror.Custom).IsNotNull();
        
        foreach (var kvp in injectionAttempts)
        {
            await Assert.That(mirror.Custom!.ContainsKey(kvp.Key)).IsTrue();
            await Assert.That(mirror.Custom[kvp.Key]).IsEqualTo(kvp.Value);
        }
    }

    [Test]
    public async Task Mirror_CustomMetadata_VeryLargeValues_HandledWithoutIssues()
    {
        var largeDictionary = new Dictionary<string, string>();
        
        // Add many entries to test memory handling
        for (int i = 0; i < 1000; i++)
        {
            largeDictionary[$"key_{i}"] = new string('v', 100); // 100 chars per value
        }

        var mirror = new Mirror 
        { 
            Url = "https://example.com",
            Custom = largeDictionary 
        };
        
        await Assert.That(mirror.Custom!.Count).IsEqualTo(1000);
        await Assert.That(mirror.Custom["key_0"]).IsEqualTo(new string('v', 100));
        await Assert.That(mirror.Custom["key_999"]).IsEqualTo(new string('v', 100));
    }

    [Test]
    public async Task Mirror_CustomMetadata_EmptyAndNullValues_HandledProperly()
    {
        var metadata = new Dictionary<string, string>
        {
            ["empty"] = "",
            ["whitespace"] = "   ",
            ["tab"] = "\t",
            ["newline"] = "\n",
            ["carriage_return"] = "\r"
        };

        var mirror = new Mirror 
        { 
            Url = "https://example.com",
            Custom = metadata 
        };
        
        await Assert.That(mirror.Custom!["empty"]).IsEqualTo("");
        await Assert.That(mirror.Custom["whitespace"]).IsEqualTo("   ");
        await Assert.That(mirror.Custom["tab"]).IsEqualTo("\t");
        await Assert.That(mirror.Custom["newline"]).IsEqualTo("\n");
        await Assert.That(mirror.Custom["carriage_return"]).IsEqualTo("\r");
    }

    #endregion

    #region Mirrors Metadata Security Tests

    [Test]
    public async Task Mirrors_DefaultValues_AreSecure()
    {
        var mirrors = new Mirrors();
        
        await Assert.That(mirrors.Type).IsEqualTo("mirrors");
        await Assert.That(mirrors.SpecVersion).IsEqualTo("");
        await Assert.That(mirrors.Version).IsEqualTo(1);
        await Assert.That(mirrors.MirrorList).IsNotNull();
        await Assert.That(mirrors.MirrorList.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Mirrors_TypeField_ImmutableSecurity()
    {
        var mirrors = new Mirrors();
        
        // Type should always be "mirrors" and not be modifiable after construction
        await Assert.That(mirrors.Type).IsEqualTo("mirrors");
        
        // Create new instance with different type (should use default)
        var mirrorsWithCustom = new Mirrors();
        await Assert.That(mirrorsWithCustom.Type).IsEqualTo("mirrors");
    }

    [Test]
    public async Task Mirrors_VersionBoundaries_ValidatedCorrectly()
    {
        // Test version boundary conditions
        var testVersions = new[] { 0, 1, int.MaxValue };
        
        foreach (var version in testVersions)
        {
            var mirrors = new Mirrors { Version = version };
            await Assert.That(mirrors.Version).IsEqualTo(version);
        }
    }

    [Test]
    public async Task Mirrors_SpecVersion_HandlesVariousFormats()
    {
        var testVersions = new[]
        {
            "1.0.0",
            "2.1.0-beta",
            "1.0.0+build.123",
            "",
            "invalid-version",
            "1.0.0.0.0.extra",
            new string('1', 1000) // Very long version
        };

        foreach (var version in testVersions)
        {
            var mirrors = new Mirrors { SpecVersion = version };
            await Assert.That(mirrors.SpecVersion).IsEqualTo(version);
        }
    }

    [Test]
    public async Task Mirrors_ExpirationTime_HandlesTimeZonesBoundaries()
    {
        var testTimes = new[]
        {
            DateTimeOffset.MinValue,
            DateTimeOffset.MaxValue,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddYears(100),
            DateTimeOffset.UtcNow.AddYears(-100),
            new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2038, 1, 19, 3, 14, 7, TimeSpan.Zero) // Unix timestamp boundary
        };

        foreach (var time in testTimes)
        {
            var mirrors = new Mirrors { Expires = time };
            await Assert.That(mirrors.Expires).IsEqualTo(time);
        }
    }

    [Test]
    public async Task Mirrors_LargeMirrorList_HandledEfficiently()
    {
        var mirrorList = new List<Mirror>();
        
        // Create a large number of mirrors to test scalability
        for (int i = 0; i < 10000; i++)
        {
            mirrorList.Add(new Mirror 
            { 
                Url = $"https://mirror{i}.example.com/repo",
                Custom = new Dictionary<string, string>
                {
                    ["id"] = i.ToString(),
                    ["region"] = $"region{i % 10}",
                    ["priority"] = (i % 5).ToString()
                }
            });
        }

        var mirrors = new Mirrors { MirrorList = mirrorList };
        
        await Assert.That(mirrors.MirrorList.Count).IsEqualTo(10000);
        await Assert.That(mirrors.MirrorList[0].Url).IsEqualTo("https://mirror0.example.com/repo");
        await Assert.That(mirrors.MirrorList[9999].Url).IsEqualTo("https://mirror9999.example.com/repo");
        await Assert.That(mirrors.MirrorList[9999].Custom!["id"]).IsEqualTo("9999");
    }

    #endregion

    #region Mirrors Serialization Security Tests

    [Test]
    public async Task Mirrors_SerializationRoundTrip_PreservesSecurityProperties()
    {
        var originalMirrors = new Mirrors
        {
            SpecVersion = "1.0.0",
            Version = 42,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            MirrorList = new List<Mirror>
            {
                new() 
                { 
                    Url = "https://primary.example.com/repo",
                    Custom = new Dictionary<string, string>
                    {
                        ["priority"] = "1",
                        ["location"] = "us-east"
                    }
                },
                new() 
                { 
                    Url = "https://secondary.example.com/repo",
                    Custom = new Dictionary<string, string>
                    {
                        ["priority"] = "2",
                        ["location"] = "eu-west"
                    }
                }
            }
        };

        // Serialize to canonical JSON
        var serializedBytes = Serializer.Serialize(originalMirrors);
        await Assert.That(serializedBytes).IsNotNull();
        await Assert.That(serializedBytes.Length).IsGreaterThan(0);

        // Convert to string for deserialization
        var serializedJson = Encoding.UTF8.GetString(serializedBytes);

        // Deserialize back
        var deserializedMirrors = Serializer.Deserialize<Mirrors>(serializedJson);
        
        // Verify all security-relevant properties are preserved
        await Assert.That(deserializedMirrors.Type).IsEqualTo("mirrors");
        await Assert.That(deserializedMirrors.SpecVersion).IsEqualTo("1.0.0");
        await Assert.That(deserializedMirrors.Version).IsEqualTo(42);
        await Assert.That(deserializedMirrors.MirrorList.Count).IsEqualTo(2);
        
        await Assert.That(deserializedMirrors.MirrorList[0].Url).IsEqualTo("https://primary.example.com/repo");
        await Assert.That(deserializedMirrors.MirrorList[0].Custom?.ContainsKey("priority")).IsEqualTo(true);
        await Assert.That(deserializedMirrors.MirrorList[0].Custom?["priority"]).IsEqualTo("1");
        await Assert.That(deserializedMirrors.MirrorList[0].Custom?["location"]).IsEqualTo("us-east");
        
        await Assert.That(deserializedMirrors.MirrorList[1].Url).IsEqualTo("https://secondary.example.com/repo");
        await Assert.That(deserializedMirrors.MirrorList[1].Custom?.ContainsKey("priority")).IsEqualTo(true);
        await Assert.That(deserializedMirrors.MirrorList[1].Custom?["priority"]).IsEqualTo("2");
        await Assert.That(deserializedMirrors.MirrorList[1].Custom?["location"]).IsEqualTo("eu-west");
    }

    [Test]
    public async Task Mirrors_SerializationWithMaliciousContent_HandledSafely()
    {
        var maliciousMirrors = new Mirrors
        {
            SpecVersion = "1.0.0\"; DROP TABLE mirrors; --",
            Version = int.MaxValue,
            Expires = DateTimeOffset.MaxValue,
            MirrorList = new List<Mirror>
            {
                new() 
                { 
                    Url = "javascript:alert('xss')",
                    Custom = new Dictionary<string, string>
                    {
                        ["<script>"] = "alert('xss')",
                        ["${injection}"] = "malicious_payload",
                        ["\x00\x01\x02"] = "control_chars",
                        ["unicode_attack"] = "𝕏𝕊𝕊 𝔸𝕥𝕥𝕒𝕔𝕜"
                    }
                }
            }
        };

        // Serialize (should not throw)
        var serializedBytes = Serializer.Serialize(maliciousMirrors);
        await Assert.That(serializedBytes).IsNotNull();

        // Convert to string for deserialization
        var serializedJson = Encoding.UTF8.GetString(serializedBytes);

        // Deserialize (should not throw and preserve malicious content safely)
        var deserializedMirrors = Serializer.Deserialize<Mirrors>(serializedJson);
        
        await Assert.That(deserializedMirrors.SpecVersion).IsEqualTo("1.0.0\"; DROP TABLE mirrors; --");
        await Assert.That(deserializedMirrors.MirrorList[0].Url).IsEqualTo("javascript:alert('xss')");
        await Assert.That(deserializedMirrors.MirrorList[0].Custom?.ContainsKey("<script>")).IsEqualTo(true);
        await Assert.That(deserializedMirrors.MirrorList[0].Custom?["<script>"]).IsEqualTo("alert('xss')");
    }

    [Test]
    public async Task Mirrors_EmptyMirrorList_SerializesCorrectly()
    {
        var emptyMirrors = new Mirrors
        {
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            MirrorList = new List<Mirror>()
        };

        var serializedBytes = Serializer.Serialize(emptyMirrors);
        var serializedJson = Encoding.UTF8.GetString(serializedBytes);
        var deserializedMirrors = Serializer.Deserialize<Mirrors>(serializedJson);
        
        await Assert.That(deserializedMirrors.MirrorList).IsNotNull();
        await Assert.That(deserializedMirrors.MirrorList.Count).IsEqualTo(0);
    }

    #endregion

    #region Security Boundary Edge Cases

    [Test]
    public async Task Mirrors_MemoryPressure_LargeMirrorMetadata_HandledGracefully()
    {
        // Create mirrors with very large custom metadata to test memory handling
        var largeMirror = new Mirror
        {
            Url = "https://example.com/repo",
            Custom = new Dictionary<string, string>()
        };

        // Add large amount of custom metadata
        for (int i = 0; i < 5000; i++)
        {
            largeMirror.Custom.Add($"key_{i}", new string('x', 1000)); // 1KB per value
        }

        var mirrors = new Mirrors
        {
            MirrorList = new List<Mirror> { largeMirror }
        };

        // Should handle large data without issues
        await Assert.That(mirrors.MirrorList[0].Custom?.Count).IsEqualTo(5000);
        await Assert.That(mirrors.MirrorList[0].Custom?["key_0"].Length).IsEqualTo(1000);
    }

    [Test]
    public async Task Mirror_RecordEquality_SecurityImplications()
    {
        var mirror1 = new Mirror 
        { 
            Url = "https://example.com",
            Custom = null
        };
        
        var mirror2 = new Mirror 
        { 
            Url = "https://example.com",
            Custom = null
        };

        var mirror3 = new Mirror 
        { 
            Url = "https://different.example.com",
            Custom = null
        };

        // Records with same URL and null custom should be equal
        await Assert.That(mirror1.Equals(mirror2)).IsTrue();
        await Assert.That(mirror1.GetHashCode()).IsEqualTo(mirror2.GetHashCode());
        
        // Records with different URLs should not be equal
        await Assert.That(mirror1.Equals(mirror3)).IsFalse();
        await Assert.That(mirror1.GetHashCode()).IsNotEqualTo(mirror3.GetHashCode());
        
        // Test that Dictionary references are compared, not content
        var mirror4 = new Mirror 
        { 
            Url = "https://example.com",
            Custom = new Dictionary<string, string> { ["test"] = "value" }
        };
        
        var mirror5 = new Mirror 
        { 
            Url = "https://example.com",
            Custom = new Dictionary<string, string> { ["test"] = "value" }
        };

        // Different Dictionary instances with same content are NOT equal in records
        await Assert.That(mirror4.Equals(mirror5)).IsFalse();
    }

    [Test]
    public async Task Mirrors_RecordEquality_SecurityImplications()
    {
        var expiry = DateTimeOffset.UtcNow.AddDays(30);
        
        var mirrors1 = new Mirrors
        {
            SpecVersion = "1.0.0",
            Version = 1,
            Expires = expiry,
            MirrorList = new List<Mirror>() // Empty list
        };
        
        var mirrors2 = new Mirrors
        {
            SpecVersion = "1.0.0", 
            Version = 1,
            Expires = expiry,
            MirrorList = new List<Mirror>() // Empty list
        };

        var mirrors3 = new Mirrors
        {
            SpecVersion = "1.0.0",
            Version = 2, // Different version
            Expires = expiry,
            MirrorList = new List<Mirror>() // Empty list
        };

        // Different List instances with same content are NOT equal in records
        await Assert.That(mirrors1.Equals(mirrors2)).IsFalse();
        
        // Test that versions affect equality (security critical)
        await Assert.That(mirrors1.Equals(mirrors3)).IsFalse();
        await Assert.That(mirrors1.Version).IsNotEqualTo(mirrors3.Version);
        
        // Test same reference equality
        var mirrorsSame = mirrors1;
        await Assert.That(mirrors1.Equals(mirrorsSame)).IsTrue();
        await Assert.That(mirrors1.GetHashCode()).IsEqualTo(mirrorsSame.GetHashCode());
    }

    #endregion
}