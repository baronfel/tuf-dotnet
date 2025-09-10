# TUF Property-Based Testing Suite

This project provides comprehensive property-based testing for the TUF .NET implementation using FsCheck. Property-based testing is essential for TUF because it helps discover edge cases in complex security-critical systems that traditional unit tests might miss.

## Overview

Property-based testing works by generating hundreds of random test cases and verifying that certain properties (invariants) always hold true. This is particularly valuable for TUF because:

- **Complex State Space**: TUF has intricate metadata relationships, delegation hierarchies, and cryptographic validation chains
- **Security Critical**: Edge cases can lead to security vulnerabilities
- **Specification Compliance**: Properties can encode TUF specification requirements
- **Robust Validation**: Helps ensure the implementation handles unexpected inputs gracefully

## Test Categories

### 1. Metadata Serialization Properties (`MetadataPropertyTests.cs`)

Tests fundamental serialization invariants:
- **Roundtrip Preservation**: `serialize(deserialize(serialize(x))) == serialize(x)`
- **Determinism**: Same object always produces identical JSON
- **Field Preservation**: All required fields survive serialization
- **Spec Version Compliance**: TAP 6 compliance verification

```csharp
// Example property: Root metadata roundtrip preservation
Property.ForAll<Root>(root => {
    var json = CanonicalJsonSerializer.Serialize(root);
    var deserialized = CanonicalJsonDeserializer.Deserialize<Root>(json);
    return root.Equals(deserialized);
});
```

### 2. Delegation Properties (`DelegationPropertyTests.cs`)

Tests TUF delegation behavior:
- **Deterministic Matching**: Same input always produces same delegation matches
- **Termination Behavior**: Terminating delegations properly halt search
- **Role Ordering**: Delegation precedence is preserved
- **Path Pattern Consistency**: Glob patterns match consistently

```csharp
// Example property: Delegation matching determinism
Property.ForAll<Delegations, string>((delegations, path) => {
    var matches1 = delegations.GetRolesForTarget(path);
    var matches2 = delegations.GetRolesForTarget(path);
    return matches1.SequenceEqual(matches2);
});
```

### 3. Cryptographic Properties (`CryptographicPropertyTests.cs`)

Tests cryptographic operation invariants:
- **Signature Verification Consistency**: Same key/data always gives same result
- **Key ID Determinism**: Same key always produces same ID
- **Hash Consistency**: Same data always produces same hash
- **Security Properties**: Wrong keys fail verification, tampering is detected

```csharp
// Example property: Ed25519 signature verification consistency
Property.ForAll<byte[]>(message => {
    using var key = Key.Create(SignatureAlgorithm.Ed25519);
    var signature = algorithm.Sign(key, message);
    var verify1 = algorithm.Verify(key.PublicKey, message, signature);
    var verify2 = algorithm.Verify(key.PublicKey, message, signature);
    return verify1 && verify2; // Must always succeed
});
```

### 4. Version Management Properties (`VersionPropertyTests.cs`)

Tests version-related security invariants:
- **Monotonic Ordering**: Version relationships are preserved
- **Rollback Detection**: Older versions are correctly identified as rollbacks  
- **Transitivity**: Version comparison logic is mathematically sound
- **Freshness Validation**: Expired metadata is properly rejected

```csharp
// Example property: Version monotonicity
Property.ForAll<uint, uint>((v1, v2) => {
    var ordering1 = v1 < v2;
    // ... operations ...
    var ordering2 = processedV1 < processedV2;
    return ordering1 == ordering2; // Ordering preserved
});
```

## Running Tests

### Basic Usage

```bash
# Run all property tests
dotnet test TUF.PropertyTests

# Run with verbose output for debugging
dotnet test TUF.PropertyTests --logger "console;verbosity=detailed"

# Run specific test class
dotnet test TUF.PropertyTests --filter "MetadataPropertyTests"
```

### Development and Debugging

```bash
# Quick tests for development (fewer test cases)
dotnet test TUF.PropertyTests -e PROPERTY_CONFIG=Quick

# Extensive testing for release validation
dotnet test TUF.PropertyTests -e PROPERTY_CONFIG=Extensive
```

## Configuration

The test suite provides three configurations:

### 1. Standard Configuration (Default)
- 100 test cases per property
- Good balance of thoroughness and speed
- Suitable for CI/CD pipelines

### 2. Quick Configuration  
- 20 test cases per property
- Fast execution for development
- Good for rapid iteration

### 3. Extensive Configuration
- 500 test cases per property  
- Thorough testing for release validation
- Use when investigating specific issues

## Custom Generators

The test suite includes custom generators for TUF-specific types:

```csharp
// Valid TUF version numbers
TufGenerators.ValidVersions  // Generates: 1, 2, 3, ..., 10000

// Valid spec version strings
TufGenerators.ValidSpecVersions  // Generates: "1.0.0", "1.0.1", "2.0.0"

// Valid expiration times (future dates)
TufGenerators.ValidExpirationTimes  // Generates future DateTime values

// Valid target file paths
DelegationGenerators.ValidTargetPaths  // Generates: "file.txt", "dir/file.txt", etc.

// Valid glob patterns
DelegationGenerators.ValidGlobPatterns  // Generates: "*", "*.txt", "dir/*", etc.
```

## Property Test Benefits for TUF

### 1. **Edge Case Discovery**
Property tests generate inputs that humans might not think to test:
- Extremely large version numbers
- Complex delegation hierarchies
- Unusual path patterns
- Edge cases in cryptographic operations

### 2. **Specification Compliance**
Properties can encode TUF specification requirements:
- Version numbers must be positive
- Signatures must verify with correct keys
- Delegation paths must follow specification rules
- Metadata expiration must be respected

### 3. **Regression Prevention**
Properties serve as living documentation of system invariants:
- If a change breaks a property, it's likely a regression
- Properties catch subtle bugs introduced during refactoring
- Comprehensive property coverage prevents specification violations

### 4. **Security Assurance**
Properties can encode security requirements:
- Tampering with signed data must be detected
- Rollback attacks must be prevented
- Authorization rules must be enforced consistently

## Integration with CI/CD

The property tests are designed to integrate smoothly with CI/CD:

```yaml
# Example GitHub Actions step
- name: Run Property-Based Tests
  run: dotnet test TUF.PropertyTests --logger trx --results-directory TestResults
  
- name: Run Extended Property Tests (Release)
  run: dotnet test TUF.PropertyTests -e PROPERTY_CONFIG=Extensive
  if: github.event_name == 'release'
```

## Troubleshooting

### Flaky Tests
If property tests occasionally fail:
1. Check if the property is too strict (false positives)
2. Verify generators produce valid inputs
3. Add guards for edge cases in properties
4. Consider if the property correctly captures the intended invariant

### Performance Issues
If tests run too slowly:
1. Use Quick configuration for development
2. Reduce MaxTest count in custom configurations  
3. Optimize custom generators
4. Profile specific properties to identify bottlenecks

### Debugging Failures
When a property fails:
1. FsCheck will try to shrink the failing case to a minimal example
2. Use verbose output to see the failing inputs
3. Add logging to properties to understand the failure
4. Create a focused unit test for the minimal failing case

## Relationship to Other TUF Implementations

This property-based testing approach aligns TUF .NET with testing practices in other mature TUF implementations:

- **Python-TUF**: Uses hypothesis for property-based testing
- **Go-TUF**: Uses Go's testing/quick package for property tests  
- **Rust-TUF**: Uses quickcheck for property verification

The comprehensive property test suite helps ensure TUF .NET meets the same robustness standards as these established implementations.

## Future Enhancements

Potential areas for expanding property-based testing:

1. **Multi-Repository Properties**: Properties for TAP 4 multi-repository consensus
2. **Performance Properties**: Ensure operations complete within time bounds
3. **Concurrency Properties**: Thread safety and concurrent operation properties
4. **Network Properties**: Resilience to network failures and timeouts
5. **TAP Compliance**: Properties verifying compliance with specific TAPs

This property-based testing infrastructure provides a solid foundation for ensuring TUF .NET's robustness and security through comprehensive automated validation.