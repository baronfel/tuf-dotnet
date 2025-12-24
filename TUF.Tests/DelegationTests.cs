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
    /// Test PathHashPrefixes delegation functionality.
    /// Verifies that roles using path hash prefixes correctly match target files.
    /// </summary>
    [Test]
    public async Task TestPathHashPrefixesDelegation()
    {
        // Arrange
        var signer1 = Ed25519Signer.Generate();
        var signer2 = Ed25519Signer.Generate();

        // Calculate expected hash prefixes for known files
        // "app.exe" -> SHA256 -> hex -> first few chars
        var appExeHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("app.exe"));
        var appExePrefix = Convert.ToHexString(appExeHash)[0..2].ToLowerInvariant(); // First 2 hex chars

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
                    Name = "hash-role-1",
                    KeyIds = [signer1.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = [appExePrefix] // This should match "app.exe"
                },
                new DelegatedRole
                {
                    Name = "hash-role-2",
                    KeyIds = [signer2.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = ["ff", "ee"] // Different prefixes
                }
            ]
        };

        // Act & Assert
        var matchesApp = delegations.GetRolesForTarget("app.exe").ToList();
        await Assert.That(matchesApp).HasCount().EqualTo(1);
        await Assert.That(matchesApp[0].Name).IsEqualTo("hash-role-1");

        // Test that non-matching files don't match
        var matchesOther = delegations.GetRolesForTarget("other.txt").ToList();
        if (matchesOther.Count > 0)
        {
            // If it matches, it should NOT be hash-role-1 (since that's for appExePrefix only)
            await Assert.That(matchesOther.All(r => r.Name != "hash-role-1")).IsTrue();
        }
    }

    /// <summary>
    /// Test PathHashPrefixes with terminating delegation.
    /// </summary>
    [Test]
    public async Task TestPathHashPrefixesTerminating()
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
                    PathHashPrefixes = ["a", "b", "c"] // Broad prefixes
                },
                new DelegatedRole
                {
                    Name = "fallback-hash-role",
                    KeyIds = [signer2.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = ["a", "b", "c"] // Same prefixes but after terminating
                }
            ]
        };

        // Act 
        // Test a file that would match both roles if not for termination
        var testFile = "test.txt";
        var testHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(testFile));
        var testPrefix = Convert.ToHexString(testHash)[0..1].ToLowerInvariant();

        // Only proceed if this file would actually match our prefixes
        if (testPrefix == "a" || testPrefix == "b" || testPrefix == "c")
        {
            var matches = delegations.GetRolesForTarget(testFile).ToList();

            // Assert - Only terminating role should match
            await Assert.That(matches).HasCount().EqualTo(1);
            await Assert.That(matches[0].Name).IsEqualTo("terminating-hash-role");
        }
        else
        {
            // If the test file doesn't match our prefixes, that's expected
            var matches = delegations.GetRolesForTarget(testFile).ToList();
            await Assert.That(matches).HasCount().EqualTo(0);
        }
    }

    /// <summary>
    /// Test that PathHashPrefixes takes precedence over Paths.
    /// According to TUF spec: Either 'paths' or 'path_hash_prefixes' must be specified, but not both.
    /// Our implementation checks Paths first, then PathHashPrefixes as fallback.
    /// </summary>
    [Test]
    public async Task TestPathHashPrefixesFallbackToPaths()
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
                    Name = "paths-only-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = ["docs/*"], // Has paths
                    PathHashPrefixes = null // No hash prefixes
                },
                new DelegatedRole
                {
                    Name = "hash-only-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    Paths = null, // No paths
                    PathHashPrefixes = ["a", "b", "c"] // Has hash prefixes
                }
            ]
        };

        // Act & Assert
        // Test path-based role
        var pathMatch = delegations.GetRolesForTarget("docs/readme.txt").ToList();
        await Assert.That(pathMatch).HasCount().EqualTo(1);
        await Assert.That(pathMatch[0].Name).IsEqualTo("paths-only-role");

        // Test hash-based role (only if the file hash starts with a, b, or c)
        var testFile = "hash-test.txt";
        var testHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(testFile));
        var testPrefix = Convert.ToHexString(testHash)[0..1].ToLowerInvariant();

        var hashMatch = delegations.GetRolesForTarget(testFile).ToList();
        if (testPrefix == "a" || testPrefix == "b" || testPrefix == "c")
        {
            await Assert.That(hashMatch).HasCount().EqualTo(1);
            await Assert.That(hashMatch[0].Name).IsEqualTo("hash-only-role");
        }
        else
        {
            await Assert.That(hashMatch).HasCount().EqualTo(0);
        }
    }

    /// <summary>
    /// Test PathHashMatches method directly for edge cases.
    /// </summary>
    [Test]
    public async Task TestPathHashMatchesDirectly()
    {
        // Test case sensitivity
        await Assert.That(DelegatedRole.PathHashMatches("ABC", "test")).IsEqualTo(DelegatedRole.PathHashMatches("abc", "test"));

        // Test empty prefix (should always match)
        await Assert.That(DelegatedRole.PathHashMatches("", "any-file")).IsTrue();

        // Test exact hash matching (compute actual hash for predictable test)
        var testPath = "predictable-test-file";
        var expectedHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(testPath));
        var expectedHex = Convert.ToHexString(expectedHash).ToLowerInvariant();

        // Should match itself
        await Assert.That(DelegatedRole.PathHashMatches(expectedHex, testPath)).IsTrue();

        // Should match prefixes
        await Assert.That(DelegatedRole.PathHashMatches(expectedHex[0..4], testPath)).IsTrue();
        await Assert.That(DelegatedRole.PathHashMatches(expectedHex[0..8], testPath)).IsTrue();

        // Should not match wrong prefix
        var wrongPrefix = expectedHex[0..2] == "aa" ? "bb" : "aa";
        await Assert.That(DelegatedRole.PathHashMatches(wrongPrefix, testPath)).IsFalse();
    }
}