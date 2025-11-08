# TUF Repository Validator

A comprehensive validation and auditing tool for TUF (The Update Framework) repositories. This CLI tool helps ensure that TUF repositories meet security and compliance requirements before deployment to production environments.

## Features

### Core Validation Checks
- **Repository Structure**: Validates presence of required metadata files and directory structure
- **Metadata Integrity**: Verifies all metadata files can be parsed and contain required fields
- **Signature Verification**: Validates cryptographic signatures against role requirements
- **Role Configuration**: Checks role definitions, key assignments, and threshold requirements
- **Delegation Validation**: Validates targets delegation hierarchies and path restrictions
- **Expiration Monitoring**: Checks metadata expiration dates and provides early warnings

### Advanced Security Auditing
- **Threshold Verification**: Ensures signature thresholds meet or exceed configured minimums
- **Key Authorization**: Validates that all signatures come from authorized keys
- **Cross-Reference Validation**: Verifies metadata cross-references (timestamp → snapshot → targets)
- **Delegation Tree Analysis**: Validates complex delegation hierarchies and path restrictions

## Usage

### Basic Validation
```bash
# Validate a TUF repository with standard checks
dotnet run --repository-path /path/to/tuf-repo

# Enable verbose output for detailed validation results
dotnet run --repository-path /path/to/tuf-repo --verbose
```

### Advanced Options
```bash
# Strict mode: treat warnings as errors
dotnet run --repository-path /path/to/tuf-repo --strict

# Disable expiration date checking
dotnet run --repository-path /path/to/tuf-repo --check-expiration false

# Full validation with all options
dotnet run --repository-path /path/to/tuf-repo --verbose --strict --check-expiration true
```

## Validation Categories

### ✅ Repository Structure
- Validates presence of `metadata/` directory
- Checks for required metadata files: `root.json`, `timestamp.json`, `snapshot.json`, `targets.json`
- Verifies file accessibility and basic structure

### 🔐 Cryptographic Validation
- **Signature Verification**: Validates all signatures using appropriate algorithms (Ed25519, RSA-PSS, ECDSA)
- **Key Validation**: Ensures all referenced keys exist and are properly formatted
- **Threshold Compliance**: Verifies that signature counts meet or exceed role thresholds

### 🎯 Role Configuration
- **Role Definitions**: Validates that all required roles are defined with proper configurations
- **Key Assignments**: Ensures each role has appropriate key assignments
- **Threshold Settings**: Validates threshold values are sensible and achievable
- **Authorization Chains**: Verifies the complete chain of authorization from root to all roles

### 📅 Temporal Validation
- **Expiration Monitoring**: Checks that all metadata is within valid time periods
- **Early Warning System**: Provides warnings for metadata approaching expiration
- **Time Consistency**: Validates that metadata timestamps are consistent and logical

### 🌳 Delegation Analysis
- **Delegation Hierarchy**: Validates complex targets delegation trees
- **Path Restrictions**: Ensures delegation path patterns are properly configured
- **Key Inheritance**: Validates that delegated roles have appropriate key assignments
- **Circular Reference Detection**: Prevents circular delegation references

## Integration Examples

### CI/CD Pipeline Integration
```yaml
# GitHub Actions example
- name: Validate TUF Repository
  run: |
    dotnet run --project examples/RepositoryValidator \\
      --repository-path ./tuf-repository \\
      --strict \\
      --verbose
```

### Automated Monitoring
```bash
#!/bin/bash
# Daily repository validation script
dotnet run --project examples/RepositoryValidator \\
  --repository-path /var/tuf/repository \\
  --check-expiration true \\
  --verbose > /var/log/tuf-validation.log 2>&1

if [ $? -ne 0 ]; then
  echo "TUF repository validation failed!" | mail -s "TUF Alert" admin@company.com
fi
```

## Exit Codes

- **0**: Validation passed (no errors, warnings allowed in standard mode)
- **1**: Validation failed (errors found, or warnings in strict mode)
- **2**: Fatal error during validation (unable to complete validation)

## Output Examples

### Successful Validation
```
🔍 TUF Repository Validation
============================
Repository: /path/to/tuf-repo
Mode: Standard

🔍 Starting repository structure validation...
🔍 Loading and validating root metadata...
🔍 Validating timestamp metadata...
🔍 Validating snapshot metadata...
🔍 Validating targets metadata...
🔍 Performing role validation checks...
🔍 Checking metadata expiration dates...

📊 Validation Summary
====================
✅ Passed: 6
⚠️  Warnings: 1
   - Root metadata expires soon: 2025-11-15 10:30:00 UTC

✅ Repository validation PASSED
   (All issues are warnings only)
```

### Failed Validation
```
🔍 TUF Repository Validation
============================
Repository: /path/to/tuf-repo
Mode: Strict

🔍 Starting repository structure validation...
🔍 Loading and validating root metadata...
🔍 Validating timestamp metadata...

📊 Validation Summary
====================
✅ Passed: 2
❌ Errors: 1
   - Timestamp metadata signature validation failed: Signature verification failed for key abc123def456

❌ Repository validation FAILED
```

## Security Considerations

### Validation Scope
This tool validates repository metadata and structure but does not:
- Download or validate target files themselves
- Perform network security assessments
- Validate the underlying PKI infrastructure
- Check for social engineering or supply chain attacks beyond TUF scope

### Trust Model
- The validator assumes the root metadata is trusted (bootstrap trust)
- All other validation derives from root metadata's key assignments
- Private keys are never accessed or required for validation

### Recommended Usage
1. **Pre-deployment**: Run validation before publishing repository updates
2. **Continuous Monitoring**: Regular validation of production repositories
3. **Incident Response**: Use during security incident investigation
4. **Compliance**: Regular audits for security compliance requirements

## Comparison with Other TUF Implementations

This validator provides parity with validation capabilities found in:
- **python-tuf**: Repository structure and signature validation
- **go-tuf**: Role configuration and delegation validation  
- **tuf-conformance**: Standards compliance checking

### Unique .NET Features
- **AOT Compatibility**: Optimized for containerized and cloud environments
- **System.CommandLine**: Modern CLI with comprehensive help and error handling
- **Performance Optimized**: Uses Span<T> and efficient JSON processing
- **Enterprise Integration**: Designed for .NET-centric enterprise environments

## Development and Contributing

### Running Tests
```bash
# Build and test the validator
dotnet build
dotnet test

# Test against sample repository
dotnet run --repository-path ../RepositoryManager/sample-tuf-repo --verbose
```

### Adding New Validation Checks
1. Add validation method to `RepositoryValidator` class
2. Call from `ValidateAsync()` method
3. Add appropriate error/warning handling
4. Update documentation and tests

This tool addresses the "Repository validation and auditing tools" gap identified in the TUF .NET parity analysis, providing comprehensive validation capabilities comparable to other mature TUF implementations.