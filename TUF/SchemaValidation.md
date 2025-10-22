# JSON Schema Validation for TUF Metadata

The TUF .NET library includes comprehensive JSON Schema validation to ensure TUF metadata files conform to the official specification. This feature is essential for:

- **Specification compliance**: Ensuring metadata follows TUF specification requirements
- **Interoperability testing**: Validating compatibility with other TUF implementations
- **Early error detection**: Catching format issues before signature verification
- **Security audits**: Verifying metadata structure for compliance checks

## Features

- ✅ **Complete schema coverage**: Schemas for all TUF metadata types (root, targets, snapshot, timestamp)
- ✅ **Automatic type detection**: Validates based on `_type` field in metadata
- ✅ **Detailed error reporting**: Provides specific validation failure information
- ✅ **Performance optimized**: Schemas cached in memory, loaded from embedded resources
- ✅ **Flexible validation**: Support for both signed metadata wrappers and raw signed content
- ✅ **Logging integration**: Optional logging for validation operations

## Usage

### Basic Validation

```csharp
using TUF.Validation;

var metadataJson = await File.ReadAllTextAsync("root.json");
var result = SchemaValidator.ValidateMetadata(metadataJson);

if (result.IsValid)
{
    Console.WriteLine("Metadata is valid!");
}
else
{
    Console.WriteLine($"Validation failed with {result.Errors.Count} errors:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}
```

### Validate Against Specific Schema

```csharp
using TUF.Validation;

var targetsJson = await File.ReadAllTextAsync("targets.json");
var result = SchemaValidator.ValidateMetadata(targetsJson, SchemaType.Targets);

Console.WriteLine($"Targets metadata valid: {result.IsValid}");
```

### With Logging

```csharp
using TUF.Validation;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<MyClass>();

var result = SchemaValidator.ValidateMetadata(metadataJson, logger);
```

## Supported Schemas

### Root Metadata (`root.json`)
Validates root metadata structure including:
- Required fields: `_type`, `spec_version`, `version`, `expires`, `keys`, `roles`
- Key definitions with proper keytype/scheme combinations
- Role assignments with thresholds and key references
- Semantic version format for `spec_version`
- ISO 8601 date format for `expires`

### Targets Metadata (`targets.json`)
Validates targets metadata including:
- Required fields: `_type`, `spec_version`, `version`, `expires`, `targets`
- Target file specifications with length and hashes
- Hash algorithm validation (SHA-256, SHA-512, BLAKE2b-256)
- Optional delegation configurations
- Custom metadata fields

### Snapshot Metadata (`snapshot.json`)
Validates snapshot metadata including:
- Required fields: `_type`, `spec_version`, `version`, `expires`, `meta`
- Metadata file references with versions
- Required `targets.json` reference
- Optional delegated role references

### Timestamp Metadata (`timestamp.json`)
Validates timestamp metadata including:
- Required fields: `_type`, `spec_version`, `version`, `expires`, `meta`
- Required `snapshot.json` reference with version
- Short expiration times (typically minutes/hours)

### Signed Metadata Wrapper
Validates complete TUF metadata files with signature wrapper:
- Required `signed` and `signatures` fields
- Signature format with keyid and hex-encoded signature
- Automatic detection of inner metadata type

## Schema Validation Rules

### Common Validations
- **Type consistency**: `_type` field matches expected metadata type
- **Version semantics**: `spec_version` follows semantic versioning (e.g., "1.0.0")
- **Date format**: `expires` uses ISO 8601 format with timezone
- **Positive integers**: Version numbers and thresholds are positive integers
- **Required fields**: All mandatory fields are present and non-empty

### Security Validations
- **Key format**: Cryptographic keys have proper keytype/scheme/keyval structure
- **Hash format**: File hashes are hex-encoded with correct lengths
- **Threshold validation**: Signature thresholds are at least 1
- **Key references**: Role keyids reference existing keys
- **Path safety**: Target paths are relative (no leading slash or directory traversal)

### Advanced Validations
- **Algorithm combinations**: Valid keytype/scheme pairings (e.g., ed25519/ed25519)
- **Hash algorithms**: Supported hash algorithms with correct digest lengths
- **Delegation rules**: Either paths or path_hash_prefixes required for delegated roles
- **Role completeness**: All four top-level roles defined in root metadata

## Error Reporting

Validation errors include:
- **Location context**: JSON path where validation failed
- **Specific violations**: Details about what constraint was violated
- **Multiple errors**: All validation issues reported, not just the first

Example error output:
```
Validation failed at path '/version': Value must be greater than or equal to 1
Validation failed at path '/expires': String does not match required format 'date-time'
Validation failed at path '/roles/root': Required property 'keyids' not found
```

## Performance Considerations

- **Schema caching**: Schemas loaded once and cached in memory
- **Embedded resources**: No file I/O after initial assembly load  
- **Lazy loading**: Schemas loaded only when first needed
- **Thread safety**: Schema cache uses appropriate locking

## Integration with TUF Workflows

Schema validation integrates seamlessly with TUF operations:

```csharp
// Validate before signature verification
var validationResult = SchemaValidator.ValidateMetadata(metadataJson);
if (!validationResult.IsValid)
{
    throw new InvalidDataException($"Schema validation failed: {string.Join(", ", validationResult.Errors)}");
}

// Proceed with signature verification
var metadata = CanonicalJson.Serializer.Deserialize<Metadata<Root>>(metadataJson);
var isSignatureValid = await VerifySignatures(metadata);
```

## Contributing

Schema definitions are located in `TUF/Schemas/` and embedded as resources. When updating:

1. Modify the appropriate `.schema.json` file
2. Run tests to ensure no regressions
3. Add test cases for new validation rules
4. Update this documentation for any API changes

The schemas follow JSON Schema Draft 7 specification and are designed to be strict but practical for real-world TUF deployments.