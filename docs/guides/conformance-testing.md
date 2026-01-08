# TUF Conformance Testing Guide

This guide explains how TUF .NET achieves and maintains compliance with The Update Framework (TUF) specification through automated conformance testing.

## Overview

The TUF .NET implementation uses the official [TUF Conformance Test Suite](https://github.com/theupdateframework/tuf-conformance) to validate compliance with the TUF specification. This ensures interoperability with other TUF implementations and adherence to security standards.

## Conformance Testing Infrastructure

### Automated Testing

TUF .NET includes comprehensive conformance testing through GitHub Actions:

- **Continuous Integration**: Every push and pull request runs conformance tests
- **Scheduled Testing**: Weekly conformance validation to catch regressions  
- **Cross-platform Validation**: Tests run on Ubuntu with plan to expand to Windows/macOS
- **Automated Reporting**: Results are automatically analyzed and reported

### TUF Conformance CLI

The `examples/TufConformanceCli` project implements the [CLIENT-CLI.md specification](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md), providing:

**Core Commands:**
- `init`: Initialize client with trusted root metadata
- `refresh`: Update local metadata from repository  
- `download`: Download and verify target artifacts

**Implementation Features:**
- Full TUF client workflow compliance
- Proper error handling with informative messages
- Support for metadata and target directory management
- Integration with TUF .NET's `Updater` class

### Expected Failures Management

The `.tuf-conformance-xfails` file tracks tests that are expected to fail:

```bash
# Currently empty - TUF .NET aims for full compliance
# Tests can be added here temporarily during development
# Format: one test identifier per line
# test_example_failure[parameter]
```

**Current Status**: TUF .NET targets **zero expected failures**, indicating full TUF specification compliance.

## Running Conformance Tests

### Local Testing

You can run conformance tests locally using the published CLI:

```bash
# Build the conformance CLI
dotnet build examples/TufConformanceCli/TufConformanceCli.csproj --configuration Release

# Test basic functionality
dotnet run --project examples/TufConformanceCli -- --help

# The CLI will be used by the conformance test suite
```

### GitHub Actions Integration

The conformance workflow (`.github/workflows/conformance.yml`) includes:

1. **Build Phase**:
   - Restore dependencies and build solution
   - Compile and publish TufConformanceCli  
   - Validate CLI functionality

2. **Test Phase**:
   - Run official TUF conformance test suite
   - Use `.tuf-conformance-xfails` for expected failures
   - Generate detailed test reports

3. **Analysis Phase**:
   - Analyze test results and failure patterns
   - Update GitHub step summary with conformance status
   - Upload test artifacts for investigation

4. **Monitoring Phase**:
   - Automatically create issues for failures (scheduled runs)
   - Close success issues when tests pass
   - Maintain continuous compliance tracking

### Manual Conformance Testing

For development and debugging, you can run conformance tests manually:

```bash
# Install the conformance test suite
pip install tuf-conformance

# Run against the .NET implementation  
tuf-conformance --entrypoint "dotnet path/to/TufConformanceCli.dll" \
  --xfail-file .tuf-conformance-xfails

# Run specific test categories
tuf-conformance --entrypoint "dotnet path/to/TufConformanceCli.dll" \
  -k test_refresh
```

## Conformance Areas Tested

### Core TUF Workflows

- **Repository Initialization**: Trusted root metadata setup
- **Metadata Refresh**: Update flows for all metadata types (root, timestamp, snapshot, targets)
- **Target Download**: Artifact download and verification workflows
- **Signature Verification**: Multi-signature and threshold validation

### Advanced TUF Features

- **Role Delegations**: Delegated targets and role hierarchies
- **Key Management**: Key rotation, revocation, and threshold signatures
- **Consistent Snapshots**: Advanced repository consistency features
- **Multi-Repository**: TAP 4 multi-repository consensus (if supported by test suite)

### Security Features

- **Attack Resistance**: Protection against rollback, freeze, and other attacks
- **Hash Algorithm Support**: SHA256, SHA512 validation
- **Signature Schemes**: Ed25519, RSA-PSS, ECDSA verification
- **Metadata Validation**: Expiration, version, and consistency checks

### Error Handling

- **Network Failures**: Timeout and connectivity error scenarios
- **Malformed Data**: Invalid JSON, missing fields, corrupted signatures
- **Security Violations**: Attempted attacks and invalid operations
- **Edge Cases**: Boundary conditions and specification corner cases

## Interpreting Results

### Success Criteria

- **All tests pass**: Full TUF specification compliance
- **Zero expected failures**: Complete implementation without gaps
- **Cross-implementation compatibility**: Interoperates with Python TUF, Go TUF, etc.

### Failure Analysis

When conformance tests fail:

1. **Check test logs**: Detailed failure information in workflow artifacts
2. **Examine specific test**: Identify which TUF feature or scenario failed  
3. **Update implementation**: Fix the underlying TUF library issue
4. **Or mark as expected**: Add to `.tuf-conformance-xfails` if intentionally unsupported

### Compliance Tracking

The conformance workflow provides:

- **GitHub Step Summaries**: Real-time conformance status
- **Test Artifacts**: Detailed logs and result files
- **Issue Automation**: Automatic failure tracking and resolution
- **Historical Tracking**: Long-term compliance trend monitoring

## Development Workflow

### Adding New Features

When implementing new TUF features:

1. **Implement the feature** in the core TUF library
2. **Update conformance CLI** if new commands are needed
3. **Run conformance tests** to validate implementation
4. **Address any failures** or mark as expected temporarily
5. **Document the changes** and update guides

### Maintaining Compliance

Regular maintenance includes:

- **Monitor weekly conformance runs** for regressions
- **Update dependencies** and test for compatibility impact
- **Review specification updates** and implement new requirements
- **Maintain zero expected failures** as the target goal

## Integration with Development

### CI/CD Integration

Conformance testing is integrated into:

- **Pull Request Validation**: All changes tested for compliance
- **Release Qualification**: Releases require passing conformance
- **Regression Detection**: Automated notification of compliance breaks
- **Quality Gates**: Conformance success required for merges

### Code Quality

Conformance testing ensures:

- **Specification Adherence**: Implementation follows TUF specification exactly
- **Cross-platform Compatibility**: Works consistently across environments  
- **Security Compliance**: Meets TUF security guarantees and threat model
- **Interoperability**: Compatible with other TUF implementations

## Troubleshooting

### Common Issues

**CLI Build Failures:**
```bash
# Ensure .NET SDK is installed
dotnet --version

# Clean and rebuild
dotnet clean examples/TufConformanceCli/TufConformanceCli.csproj
dotnet build examples/TufConformanceCli/TufConformanceCli.csproj --configuration Release
```

**Conformance Test Failures:**
```bash
# Check .tuf-conformance-xfails for expected failures
cat .tuf-conformance-xfails

# Run specific failing test for debugging
tuf-conformance --entrypoint "dotnet path/to/cli.dll" -k test_specific_failure
```

**GitHub Actions Issues:**
- Check workflow permissions for artifact upload
- Verify .NET SDK version compatibility  
- Review test suite version compatibility

### Getting Help

- **TUF Conformance Issues**: [tuf-conformance GitHub](https://github.com/theupdateframework/tuf-conformance/issues)
- **TUF .NET Issues**: [tuf-dotnet GitHub Issues](https://github.com/baronfel/tuf-dotnet/issues)
- **TUF Specification**: [TUF Specification Documentation](https://theupdateframework.io/docs/)

## Future Enhancements

Planned conformance testing improvements:

- **Multi-platform Testing**: Windows and macOS conformance validation
- **Performance Benchmarking**: Conformance test execution time tracking
- **Extended Coverage**: Integration with additional TUF test scenarios  
- **Automated Fixes**: Self-healing conformance issue resolution
- **Specification Updates**: Automatic adaptation to TUF specification changes

The TUF .NET conformance testing infrastructure ensures reliable, specification-compliant, and secure TUF implementation that interoperates seamlessly with the broader TUF ecosystem.