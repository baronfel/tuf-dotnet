using System.Text.Json;

using Microsoft.Extensions.Logging;

using TUnit.Assertions;
using TUnit.Core;

using TUF.Validation;

namespace TUF.Tests;

/// <summary>
/// Unit tests for the SchemaValidator class that validates TUF metadata against JSON schemas.
/// </summary>
public class SchemaValidatorTests
{
    /// <summary>
    /// Tests that valid root metadata passes schema validation.
    /// </summary>
    [Test]
    public async Task ValidateMetadata_ValidRootMetadata_ReturnsSuccess()
    {
        // Arrange
        var validRootJson = """
        {
          "_type": "root",
          "spec_version": "1.0.0",
          "version": 1,
          "expires": "2025-12-31T23:59:59Z",
          "keys": {
            "test_key_id": {
              "keytype": "ed25519",
              "scheme": "ed25519",
              "keyval": {
                "public": "abcdef1234567890"
              }
            }
          },
          "roles": {
            "root": {
              "keyids": ["test_key_id"],
              "threshold": 1
            },
            "timestamp": {
              "keyids": ["test_key_id"],
              "threshold": 1
            },
            "snapshot": {
              "keyids": ["test_key_id"],
              "threshold": 1
            },
            "targets": {
              "keyids": ["test_key_id"],
              "threshold": 1
            }
          }
        }
        """;

        // Act
        var result = SchemaValidator.ValidateMetadata(validRootJson);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Errors.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Tests that invalid metadata fails schema validation with appropriate errors.
    /// </summary>
    [Test]
    public async Task ValidateMetadata_InvalidMetadata_ReturnsFailure()
    {
        // Arrange - missing required fields
        var invalidJson = """
        {
          "_type": "root",
          "spec_version": "1.0.0"
        }
        """;

        // Act
        var result = SchemaValidator.ValidateMetadata(invalidJson);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// Tests that null or empty JSON throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task ValidateMetadata_NullJson_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(SchemaValidator.ValidateMetadata(null!)));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(SchemaValidator.ValidateMetadata("")));
    }
}