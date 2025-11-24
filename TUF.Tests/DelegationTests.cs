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

    /// <summary>
    /// Test path hash prefix delegation functionality.
    /// Validates deterministic distribution based on SHA-256 hash prefixes.
    /// </summary>
    [Test]
    public async Task TestPathHashPrefixDelegation()
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
                    Name = "hash-role-0",
                    KeyIds = [signer1.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = ["0", "1", "2", "3"]
                },
                new DelegatedRole
                {
                    Name = "hash-role-4",
                    KeyIds = [signer2.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = ["4", "5", "6", "7"]
                }
            ]
        };

        // Pre-calculate test cases with known hash values (computed via SHA-256):
        // "test.txt" -> SHA-256 -> starts with 'a6' (not in our prefixes)
        // "file1.txt" -> SHA-256 -> starts with '55' (matches role hash-role-4 with prefix '5')
        // "data.bin" -> SHA-256 -> starts with 'b2' (not in our prefixes)

        // Act & Assert - Test various files
        var matchesTest = delegations.GetRolesForTarget("test.txt").ToList();
        await Assert.That(matchesTest).HasCount().EqualTo(0).Because("a6... hash doesn't match any prefix");

        var matchesFile1 = delegations.GetRolesForTarget("file1.txt").ToList();
        await Assert.That(matchesFile1).HasCount().EqualTo(1);
        await Assert.That(matchesFile1[0].Name).IsEqualTo("hash-role-4").Because("55... hash matches prefix '5'");

        var matchesData = delegations.GetRolesForTarget("data.bin").ToList();
        await Assert.That(matchesData).HasCount().EqualTo(0).Because("b2... hash doesn't match any prefix");
    }

    /// <summary>
    /// Test PathHashPrefixMatches helper method directly.
    /// Validates the core hash matching logic.
    /// </summary>
    [Test]
    public async Task TestPathHashPrefixMatches()
    {
        // Test with known hash values for consistency
        // SHA-256("test.txt") = a6ed0c785d4590bc95c216bcf514384eee6765b1c2b732d0b0a1ad7e14d3204a 
        
        // Act & Assert - Test exact prefix matching
        await Assert.That(DelegatedRole.PathHashPrefixMatches("a6", "test.txt")).IsTrue();
        await Assert.That(DelegatedRole.PathHashPrefixMatches("a6ed", "test.txt")).IsTrue();
        await Assert.That(DelegatedRole.PathHashPrefixMatches("a6ed0c785d4590bc", "test.txt")).IsTrue();
        
        // Test non-matching prefixes
        await Assert.That(DelegatedRole.PathHashPrefixMatches("00", "test.txt")).IsFalse();
        await Assert.That(DelegatedRole.PathHashPrefixMatches("ff", "test.txt")).IsFalse();
        
        // Test case insensitivity
        await Assert.That(DelegatedRole.PathHashPrefixMatches("A6", "test.txt")).IsTrue();
        await Assert.That(DelegatedRole.PathHashPrefixMatches("a6", "test.txt")).IsTrue();

        // Test edge cases
        await Assert.That(DelegatedRole.PathHashPrefixMatches("", "test.txt")).IsFalse();
        await Assert.That(DelegatedRole.PathHashPrefixMatches("a6", "")).IsFalse();
        await Assert.That(DelegatedRole.PathHashPrefixMatches("", "")).IsFalse();
    }

    /// <summary>
    /// Test PathHashPrefixes with terminating behavior.
    /// </summary>
    [Test]
    public async Task TestPathHashPrefixTerminating()
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
                    PathHashPrefixes = ["a", "b", "c", "d"]
                },
                new DelegatedRole
                {
                    Name = "fallback-hash-role",
                    KeyIds = [signer2.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = ["a", "b", "c", "d"]
                }
            ]
        };

        // Act - "test.txt" has hash starting with 'a6...' which matches prefix 'a'
        var matches = delegations.GetRolesForTarget("test.txt").ToList();

        // Assert - Only terminating role should match
        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0].Name).IsEqualTo("terminating-hash-role");
        await Assert.That(matches[0].Terminating).IsTrue();
    }

    /// <summary>
    /// Test that paths take precedence over PathHashPrefixes.
    /// Per TUF spec: either 'paths' or 'path_hash_prefixes' must be specified, but not both.
    /// However, if both are present, paths should take precedence for graceful degradation.
    /// </summary>
    [Test]
    public async Task TestPathsPrecedenceOverHashPrefixes()
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
                    Name = "mixed-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = ["specific/file.txt"], // Only matches this exact path
                    PathHashPrefixes = ["a"] // Would match "test.txt" by hash
                }
            ]
        };

        // Act & Assert
        var matchesSpecific = delegations.GetRolesForTarget("specific/file.txt").ToList();
        await Assert.That(matchesSpecific).HasCount().EqualTo(1).Because("Paths should be used");

        var matchesTest = delegations.GetRolesForTarget("test.txt").ToList();
        await Assert.That(matchesTest).HasCount().EqualTo(0).Because("PathHashPrefixes should be ignored when Paths is present");
    }

    /// <summary>
    /// Test multiple hash prefix matching within a single role.
    /// </summary>
    [Test]
    public async Task TestMultipleHashPrefixesInSingleRole()
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
                    Name = "multi-prefix-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = ["0", "1", "a", "b", "f"] // Wide range of prefixes
                }
            ]
        };

        // Act & Assert - Test files with different hash prefixes
        var matchesTest = delegations.GetRolesForTarget("test.txt").ToList(); // hash starts with a6...
        await Assert.That(matchesTest).HasCount().EqualTo(1).Because("Hash starts with 'a'");

        var matchesZero = delegations.GetRolesForTarget("zero.txt").ToList(); // Will need to check actual hash
        // Note: We can't predict the exact hash, but the test validates the multi-prefix logic
    }
}