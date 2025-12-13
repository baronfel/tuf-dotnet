using System.Text;

using CanonicalJson;

using TUF.Models;

using TUnit.Assertions;
using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Tests for delegation functionality.
/// Mirrors Go TUF TestIsDelegatedRole and delegation-related patterns.
/// </summary>
public class DelegationTests
{

    /// <summary>
    /// Test path matching for delegated roles.
    /// Mirrors Go TUF TestIsDelegatedRole pattern.
    /// </summary>
    [Test]
    public async Task TestDelegatedRolePathMatching()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();

        var delegations = new Delegations
        {
            Keys = new Dictionary<string, Key>
            {
                [signer.Key.GetKeyId()] = signer.Key
            },
            Roles = [
                new DelegatedRole
                {
                    Name = "docs-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = ["docs/*"]
                },
                new DelegatedRole
                {
                    Name = "src-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = ["src/*", "lib/*"]
                }
            ]
        };

        // Act & Assert - Test path matching
        var docsRole = delegations.GetRolesForTarget("docs/readme.txt").ToList();
        await Assert.That(docsRole).HasCount().EqualTo(1);
        await Assert.That(docsRole[0].Name).IsEqualTo("docs-role");

        var srcRole = delegations.GetRolesForTarget("src/main.cs").ToList();
        await Assert.That(srcRole).HasCount().EqualTo(1);
        await Assert.That(srcRole[0].Name).IsEqualTo("src-role");

        var libRole = delegations.GetRolesForTarget("lib/helper.cs").ToList();
        await Assert.That(libRole).HasCount().EqualTo(1);
        await Assert.That(libRole[0].Name).IsEqualTo("src-role");

        // No matching role
        var noMatch = delegations.GetRolesForTarget("test/unit.cs").ToList();
        await Assert.That(noMatch).HasCount().EqualTo(0);
    }

    /// <summary>
    /// Test terminating delegation behavior.
    /// </summary>
    [Test]
    public async Task TestTerminatingDelegation()
    {
        // Arrange
        var signer1 = Ed25519Signer.Generate();
        var signer2 = Ed25519Signer.Generate();

        var delegations = new Delegations
        {
            Keys = new Dictionary<string, Key>
            {
                [signer1.Key.GetKeyId()] = signer1.Key,
                [signer2.Key.GetKeyId()] = signer2.Key
            },
            Roles = [
                new DelegatedRole
                {
                    Name = "terminating-role",
                    KeyIds = [signer1.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = true,
                    Paths = ["protected/*"]
                },
                new DelegatedRole
                {
                    Name = "fallback-role",
                    KeyIds = [signer2.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = ["protected/*"]
                }
            ]
        };

        // Act
        var matchingRoles = delegations.GetRolesForTarget("protected/secret.txt").ToList();

        // Assert - Only terminating role should match, fallback should be ignored
        await Assert.That(matchingRoles).HasCount().EqualTo(1);
        await Assert.That(matchingRoles[0].Name).IsEqualTo("terminating-role");
        await Assert.That(matchingRoles[0].Terminating).IsTrue();
    }

    /// <summary>
    /// Test multiple non-terminating delegations.
    /// </summary>
    [Test]
    public async Task TestMultipleNonTerminatingDelegations()
    {
        // Arrange
        var signer1 = Ed25519Signer.Generate();
        var signer2 = Ed25519Signer.Generate();

        var delegations = new Delegations
        {
            Keys = new Dictionary<string, Key>
            {
                [signer1.Key.GetKeyId()] = signer1.Key,
                [signer2.Key.GetKeyId()] = signer2.Key
            },
            Roles = [
                new DelegatedRole
                {
                    Name = "first-role",
                    KeyIds = [signer1.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = ["shared/*"]
                },
                new DelegatedRole
                {
                    Name = "second-role",
                    KeyIds = [signer2.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = ["shared/*"]
                }
            ]
        };

        // Act
        var matchingRoles = delegations.GetRolesForTarget("shared/file.txt").ToList();

        // Assert - Both non-terminating roles should match
        await Assert.That(matchingRoles).HasCount().EqualTo(2);
        await Assert.That(matchingRoles.Select(r => r.Name)).Contains("first-role");
        await Assert.That(matchingRoles.Select(r => r.Name)).Contains("second-role");
    }

    /// <summary>
    /// Test wildcard path patterns.
    /// </summary>
    [Test]
    public async Task TestWildcardPathPatterns()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();

        var delegations = new Delegations
        {
            Keys = new Dictionary<string, Key>
            {
                [signer.Key.GetKeyId()] = signer.Key
            },
            Roles = [
                new DelegatedRole
                {
                    Name = "wildcard-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = ["*"]
                }
            ]
        };

        // Act & Assert - Wildcard should match everything
        var matches1 = delegations.GetRolesForTarget("file.txt").ToList();
        await Assert.That(matches1).HasCount().EqualTo(1);

        var matches2 = delegations.GetRolesForTarget("deep/nested/path/file.txt").ToList();
        await Assert.That(matches2).HasCount().EqualTo(0).Because("'*' is not a recursive match");
    }



    /// <summary>
    /// Test empty delegation paths behavior.
    /// Important: validates that empty paths result in no matches.
    /// </summary>
    [Test]
    public async Task TestEmptyDelegationPaths()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();

        var delegations = new Delegations
        {
            Keys = new Dictionary<string, Key>
            {
                [signer.Key.GetKeyId()] = signer.Key
            },
            Roles = [
                new DelegatedRole
                {
                    Name = "no-paths-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = [] // Empty paths
                }
            ]
        };

        // Act
        var matches = delegations.GetRolesForTarget("any/file.txt").ToList();

        // Assert - No paths means no matches
        await Assert.That(matches).HasCount().EqualTo(0);
    }

    /// <summary>
    /// Test specific path matching (no wildcards).
    /// Important: validates exact path matching behavior.
    /// </summary>
    [Test]
    public async Task TestSpecificPathMatching()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();

        var delegations = new Delegations
        {
            Keys = new Dictionary<string, Key>
            {
                [signer.Key.GetKeyId()] = signer.Key
            },
            Roles = [
                new DelegatedRole
                {
                    Name = "specific-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = ["exact/file.txt"]
                }
            ]
        };

        // Act & Assert
        var exactMatch = delegations.GetRolesForTarget("exact/file.txt").ToList();
        await Assert.That(exactMatch).HasCount().EqualTo(1);

        var noMatch = delegations.GetRolesForTarget("exact/other.txt").ToList();
        await Assert.That(noMatch).HasCount().EqualTo(0);
    }

    /// <summary>
    /// Test role ordering in delegation matching.
    /// </summary>
    [Test]
    public async Task TestDelegationRoleOrdering()
    {
        // Arrange
        var signer1 = Ed25519Signer.Generate();
        var signer2 = Ed25519Signer.Generate();

        var delegations = new Delegations
        {
            Keys = new Dictionary<string, Key>
            {
                [signer1.Key.GetKeyId()] = signer1.Key,
                [signer2.Key.GetKeyId()] = signer2.Key
            },
            Roles = [
                new DelegatedRole
                {
                    Name = "first-role",
                    KeyIds = [signer1.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = ["shared/*"]
                },
                new DelegatedRole
                {
                    Name = "second-role",
                    KeyIds = [signer2.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = ["shared/*"]
                }
            ]
        };

        // Act
        var matchingRoles = delegations.GetRolesForTarget("shared/file.txt").ToList();

        // Assert - Roles should maintain definition order
        await Assert.That(matchingRoles).HasCount().EqualTo(2);
        await Assert.That(matchingRoles[0].Name).IsEqualTo("first-role");
        await Assert.That(matchingRoles[1].Name).IsEqualTo("second-role");
    }

    #region Hash-Bin Delegation Tests

    /// <summary>
    /// Test basic hash-bin delegation functionality.
    /// Tests that PathHashPrefixes work correctly for deterministic path assignment.
    /// </summary>
    [Test]
    public async Task TestHashBinDelegationBasic()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();

        var delegations = new Delegations
        {
            Keys = new Dictionary<string, Key>
            {
                [signer.Key.GetKeyId()] = signer.Key
            },
            Roles = [
                new DelegatedRole
                {
                    Name = "hash-bin-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = ["00", "01", "02"] // First 3 hex prefixes
                }
            ]
        };

        // Act & Assert
        // Test known hashes that should match the prefixes
        var testCases = new Dictionary<string, bool>
        {
            // These will be tested dynamically based on actual SHA-256 hashes
            ["test-file-1.txt"] = false, // Will be determined by actual hash
            ["test-file-2.txt"] = false,
            ["test-file-3.txt"] = false
        };

        foreach (var (targetFile, _) in testCases)
        {
            var matchingRoles = delegations.GetRolesForTarget(targetFile).ToList();
            
            // Calculate expected result
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var pathBytes = System.Text.Encoding.UTF8.GetBytes(targetFile);
            var hashBytes = sha256.ComputeHash(pathBytes);
            var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            var shouldMatch = hashHex.StartsWith("00") || hashHex.StartsWith("01") || hashHex.StartsWith("02");

            if (shouldMatch)
            {
                await Assert.That(matchingRoles).HasCount().EqualTo(1);
                await Assert.That(matchingRoles[0].Name).IsEqualTo("hash-bin-role");
            }
            else
            {
                await Assert.That(matchingRoles).HasCount().EqualTo(0);
            }
        }
    }

    /// <summary>
    /// Test IsHashBinMatch static method directly.
    /// </summary>
    [Test]
    public async Task TestIsHashBinMatchMethod()
    {
        // Arrange - Use specific test cases with known SHA-256 hashes
        var testCases = new Dictionary<string, (List<string> prefixes, bool expectedMatch)>
        {
            // Empty cases
            [""] = (new List<string> {"aa"}, false),
            ["test.txt"] = (new List<string>(), false),
            ["test.txt"] = (null!, false),
            
            // Known hash prefixes (we'll calculate actual hashes)
            ["hello.txt"] = (new List<string> {"aa", "bb"}, false), // Will be determined dynamically
            ["world.txt"] = (new List<string> {"cc", "dd"}, false)  // Will be determined dynamically
        };

        foreach (var (targetFile, (prefixes, _)) in testCases)
        {
            if (string.IsNullOrEmpty(targetFile) || prefixes == null || prefixes.Count == 0)
            {
                // Test edge cases
                var result = DelegatedRole.IsHashBinMatch(targetFile, prefixes);
                await Assert.That(result).IsFalse();
                continue;
            }

            // Calculate actual hash for dynamic testing
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var pathBytes = System.Text.Encoding.UTF8.GetBytes(targetFile);
            var hashBytes = sha256.ComputeHash(pathBytes);
            var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            
            var actualMatch = DelegatedRole.IsHashBinMatch(targetFile, prefixes);
            var expectedMatch = prefixes.Any(p => hashHex.StartsWith(p.ToLowerInvariant()));
            
            await Assert.That(actualMatch).IsEqualTo(expectedMatch);
        }
    }

    /// <summary>
    /// Test that paths and path_hash_prefixes are mutually exclusive as per TUF spec.
    /// </summary>
    [Test]
    public async Task TestPathsAndHashPrefixesMutualExclusion()
    {
        // Arrange
        var signer = Ed25519Signer.Generate();

        var delegatedRoleWithBoth = new DelegatedRole
        {
            Name = "both-role",
            KeyIds = [signer.Key.GetKeyId()],
            Threshold = 1,
            Terminating = false,
            Paths = ["files/*"],
            PathHashPrefixes = ["aa", "bb"]
        };

        var delegatedRoleWithPaths = new DelegatedRole
        {
            Name = "paths-role",
            KeyIds = [signer.Key.GetKeyId()],
            Threshold = 1,
            Terminating = false,
            Paths = ["files/*"],
            PathHashPrefixes = null
        };

        var delegatedRoleWithHashPrefixes = new DelegatedRole
        {
            Name = "hash-role", 
            KeyIds = [signer.Key.GetKeyId()],
            Threshold = 1,
            Terminating = false,
            Paths = null,
            PathHashPrefixes = ["cc", "dd"]
        };

        // Act & Assert
        // When both are specified, paths take precedence
        var bothResult = delegatedRoleWithBoth.IsDelegatedPath("files/test.txt");
        await Assert.That(bothResult).IsTrue().Because("Paths should take precedence when both are specified");

        // Paths-only should work
        var pathsResult = delegatedRoleWithPaths.IsDelegatedPath("files/test.txt");
        await Assert.That(pathsResult).IsTrue();

        // Hash prefixes should work when paths are not specified
        // Calculate if test file matches hash prefixes
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var pathBytes = System.Text.Encoding.UTF8.GetBytes("test-file.txt");
        var hashBytes = sha256.ComputeHash(pathBytes);
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var shouldMatch = hashHex.StartsWith("cc") || hashHex.StartsWith("dd");
        
        var hashResult = delegatedRoleWithHashPrefixes.IsDelegatedPath("test-file.txt");
        await Assert.That(hashResult).IsEqualTo(shouldMatch);
    }

    /// <summary>
    /// Test hash-bin delegation with multiple roles and terminating behavior.
    /// </summary>
    [Test]
    public async Task TestHashBinDelegationWithTerminating()
    {
        // Arrange
        var signer1 = Ed25519Signer.Generate();
        var signer2 = Ed25519Signer.Generate();

        var delegations = new Delegations
        {
            Keys = new Dictionary<string, Key>
            {
                [signer1.Key.GetKeyId()] = signer1.Key,
                [signer2.Key.GetKeyId()] = signer2.Key
            },
            Roles = [
                new DelegatedRole
                {
                    Name = "terminating-hash-role",
                    KeyIds = [signer1.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = true,
                    PathHashPrefixes = ["a0", "a1", "a2"] // Covers some range
                },
                new DelegatedRole
                {
                    Name = "fallback-hash-role", 
                    KeyIds = [signer2.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = ["a0", "a1", "a2", "a3"] // Overlapping but broader range
                }
            ]
        };

        // Act - Find files that would match both roles
        var testFiles = new[] { "file1.txt", "file2.txt", "file3.txt", "file4.txt", "file5.txt" };
        
        foreach (var testFile in testFiles)
        {
            var matchingRoles = delegations.GetRolesForTarget(testFile).ToList();
            
            // Calculate expected behavior
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var pathBytes = System.Text.Encoding.UTF8.GetBytes(testFile);
            var hashBytes = sha256.ComputeHash(pathBytes);
            var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            
            var matchesTerminating = hashHex.StartsWith("a0") || hashHex.StartsWith("a1") || hashHex.StartsWith("a2");
            var matchesFallback = hashHex.StartsWith("a0") || hashHex.StartsWith("a1") || hashHex.StartsWith("a2") || hashHex.StartsWith("a3");

            if (matchesTerminating)
            {
                // Should only get terminating role, not fallback
                await Assert.That(matchingRoles).HasCount().EqualTo(1);
                await Assert.That(matchingRoles[0].Name).IsEqualTo("terminating-hash-role");
            }
            else if (matchesFallback)
            {
                // Should get fallback role only
                await Assert.That(matchingRoles).HasCount().EqualTo(1);
                await Assert.That(matchingRoles[0].Name).IsEqualTo("fallback-hash-role");
            }
            else
            {
                // Should get no roles
                await Assert.That(matchingRoles).HasCount().EqualTo(0);
            }
        }
    }

    /// <summary>
    /// Test case sensitivity in hash prefixes.
    /// </summary>
    [Test] 
    public async Task TestHashBinCaseSensitivity()
    {
        // Arrange - Test both uppercase and lowercase prefixes
        var testCases = new Dictionary<string, bool>
        {
            // All should work regardless of case in prefix specification
        };

        var upperPrefixes = new List<string> { "AA", "BB", "CC" };
        var lowerPrefixes = new List<string> { "aa", "bb", "cc" };
        var mixedPrefixes = new List<string> { "Aa", "bB", "Cc" };

        var testFile = "test-case-sensitivity.txt";

        // Act - Test that all variations work the same way
        var upperResult = DelegatedRole.IsHashBinMatch(testFile, upperPrefixes);
        var lowerResult = DelegatedRole.IsHashBinMatch(testFile, lowerPrefixes);
        var mixedResult = DelegatedRole.IsHashBinMatch(testFile, mixedPrefixes);

        // Assert - All should produce the same result (case insensitive)
        await Assert.That(upperResult).IsEqualTo(lowerResult);
        await Assert.That(lowerResult).IsEqualTo(mixedResult);
    }

    #endregion
}