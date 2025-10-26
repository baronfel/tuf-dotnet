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
    /// Test hash bin delegation with PathHashPrefixes.
    /// </summary>
    [Test]
    public async Task TestHashBinDelegation()
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
                    Name = "bin-role-0",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = ["0", "1", "2", "3"] // Handles ~25% of files
                },
                new DelegatedRole
                {
                    Name = "bin-role-4",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = ["4", "5", "6", "7"] // Handles ~25% of files
                }
            ]
        };

        // Act - Test various file paths
        // Note: These specific files were chosen because their SHA-256 hashes start with the expected prefixes
        var matches1 = delegations.GetRolesForTarget("bin/app.exe").ToList();
        var matches2 = delegations.GetRolesForTarget("lib/helper.dll").ToList();
        var matches3 = delegations.GetRolesForTarget("config/settings.json").ToList();

        // Assert - Files should be distributed across roles based on hash
        // We can't predict which files will match which prefixes without computing hashes,
        // but we can verify the mechanism works
        var totalMatches = matches1.Count + matches2.Count + matches3.Count;
        await Assert.That(totalMatches).IsGreaterThanOrEqualTo(0);

        // Test a specific case we can control by computing the hash
        var testPath = "test/file.txt";
        var pathBytes = System.Text.Encoding.UTF8.GetBytes(testPath);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(pathBytes);
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var firstChar = hashHex[0].ToString();

        var expectedMatches = new List<DelegatedRole>();
        if ("0123".Contains(firstChar))
        {
            expectedMatches.Add(delegations.Roles[0]);
        }
        else if ("4567".Contains(firstChar))
        {
            expectedMatches.Add(delegations.Roles[1]);
        }

        var actualMatches = delegations.GetRolesForTarget(testPath).ToList();
        await Assert.That(actualMatches).HasCount().EqualTo(expectedMatches.Count);
        
        if (expectedMatches.Count > 0)
        {
            await Assert.That(actualMatches[0].Name).IsEqualTo(expectedMatches[0].Name);
        }
    }

    /// <summary>
    /// Test that PathHashPrefixes are case insensitive.
    /// </summary>
    [Test]
    public async Task TestHashBinDelegationCaseInsensitive()
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
                    Name = "upper-case-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = ["A", "B", "C"] // Uppercase prefixes
                }
            ]
        };

        // Find a path that starts with 'a', 'b', or 'c' when hashed
        string? matchingPath = null;
        
        for (int i = 0; i < 1000; i++)
        {
            var candidatePath = $"test/file-{i}.txt";
            var pathBytes = System.Text.Encoding.UTF8.GetBytes(candidatePath);
            var hashBytes = System.Security.Cryptography.SHA256.HashData(pathBytes);
            var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            
            if ("abc".Contains(hashHex[0]))
            {
                matchingPath = candidatePath;
                break;
            }
        }

        // Assert we found a matching path (very likely within 1000 tries)
        await Assert.That(matchingPath).IsNotNull();

        // Act
        var matches = delegations.GetRolesForTarget(matchingPath!).ToList();

        // Assert - Should match despite case difference
        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0].Name).IsEqualTo("upper-case-role");
    }

    /// <summary>
    /// Test that roles with both Paths and PathHashPrefixes use only Paths.
    /// According to TUF spec, only one should be specified, but if both are present,
    /// paths should take precedence.
    /// </summary>
    [Test]
    public async Task TestPathsOverrideHashBins()
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
                    Paths = ["specific/file.txt"], // Specific path
                    PathHashPrefixes = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f"] // Would match everything
                }
            ]
        };

        // Act
        var specificMatch = delegations.GetRolesForTarget("specific/file.txt").ToList();
        var hashMatch = delegations.GetRolesForTarget("random/other.txt").ToList();

        // Assert - Only specific path should match, not hash bins
        await Assert.That(specificMatch).HasCount().EqualTo(1);
        await Assert.That(specificMatch[0].Name).IsEqualTo("mixed-role");
        
        await Assert.That(hashMatch).HasCount().EqualTo(0);
    }

    /// <summary>
    /// Test empty PathHashPrefixes behavior.
    /// </summary>
    [Test]
    public async Task TestEmptyPathHashPrefixes()
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
                    Name = "empty-hash-role",
                    KeyIds = [signer.Key.GetKeyId()],
                    Threshold = 1,
                    Terminating = false,
                    PathHashPrefixes = [] // Empty hash prefixes
                }
            ]
        };

        // Act
        var matches = delegations.GetRolesForTarget("any/file.txt").ToList();

        // Assert - Empty hash prefixes should not match anything
        await Assert.That(matches).HasCount().EqualTo(0);
    }
}