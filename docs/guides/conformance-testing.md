# TUF Conformance Testing Guide

This document describes how to run and interpret the official TUF conformance test suite for the .NET TUF implementation.

## Overview

The TUF conformance test suite is the official testing framework that verifies TUF client implementations' compliance with the TUF specification. It's maintained by the TUF project at [`theupdateframework/tuf-conformance`](https://github.com/theupdateframework/tuf-conformance).

## What Tests Are Included

The conformance test suite covers:

- **Core TUF Workflow**: Initialization, metadata refresh, target download
- **Cryptographic Verification**: Ed25519, ECDSA P-256, RSA-PSS signatures  
- **Hash Algorithm Support**: SHA-256, SHA-512, BLAKE2b variants
- **Metadata Validation**: Proper handling of root, timestamp, snapshot, targets
- **Security Features**: Attack prevention (rollback, freeze, etc.)
- **Error Handling**: Malformed metadata, expired certificates
- **Advanced Features**: Delegations, multi-signature thresholds, consistent snapshots
- **Network Behavior**: HTTP handling, timeouts, redirects

## Running Conformance Tests

### GitHub Actions (Recommended)

The easiest way to run conformance tests is via the GitHub Actions workflow:

1. **Automatic Runs**: Tests run automatically on:
   - Pushes to `main` branch
   - Pull requests targeting `main`  
   - Pushes to `achieve-parity/*` branches

2. **Manual Trigger**: 
   - Go to the Actions tab in GitHub
   - Select "TUF Conformance Testing" workflow
   - Click "Run workflow"

3. **Results**: Check the workflow output for:
   - Test pass/fail summary
   - Detailed logs for failed tests
   - Artifact uploads with test results

### Local Development

For development and debugging, you can run individual conformance test scenarios:

```bash
# Build the conformance CLI
dotnet build examples/TufConformanceCli/TufConformanceCli.csproj --configuration Release

# Test the CLI directly  
dotnet run --project examples/TufConformanceCli/TufConformanceCli.csproj --configuration Release -- --help

# Example: Initialize with a test root
mkdir -p /tmp/tuf-test
dotnet run --project examples/TufConformanceCli/TufConformanceCli.csproj --configuration Release -- \
  init /path/to/trusted-root.json --metadata-dir /tmp/tuf-test

# Example: Refresh metadata
dotnet run --project examples/TufConformanceCli/TufConformanceCli.csproj --configuration Release -- \
  refresh --metadata-dir /tmp/tuf-test --metadata-url https://example.com/metadata

# Example: Download a target
dotnet run --project examples/TufConformanceCli/TufConformanceCli.csproj --configuration Release -- \
  download --metadata-dir /tmp/tuf-test --metadata-url https://example.com/metadata \
  --target-name example.txt --target-base-url https://example.com/targets --target-dir /tmp/downloads
```

## Understanding Test Results

### Expected Failures (.tuf-conformance-xfails)

The `.tuf-conformance-xfails` file lists tests that are expected to fail. This is normal during development and allows tracking progress toward full compliance.

**Current Status**: Our implementation starts with conservative expected failures in these areas:
- Advanced TUF features (delegations, multi-sig thresholds)
- Network-specific behaviors (timeouts, redirects, headers)
- Edge cases and attack scenarios
- BLAKE2b hash algorithm support

**Goal**: Reduce the number of expected failures by implementing missing features.

### Interpreting Results

**✅ Passing Tests**: Features working correctly according to TUF specification

**❌ Failing Tests**: 
1. **Expected Failures**: Listed in `.tuf-conformance-xfails` - known gaps we're working on
2. **Unexpected Failures**: Not in xfails file - potential regressions or new issues

**📊 Progress Tracking**:
- **Pass Rate**: Percentage of tests passing (excluding expected failures)
- **Expected vs Actual**: Compare xfails list with actual failures
- **Trending**: Track improvement over time

## Debugging Failed Tests

### Common Failure Patterns

1. **CLI Protocol Issues**:
   ```
   Error: Command not recognized
   ```
   - Check that init/refresh/download commands are properly implemented
   - Verify command-line argument parsing

2. **Metadata Validation Errors**:
   ```  
   Error: Invalid signature on root metadata
   ```
   - Check cryptographic signature verification
   - Verify canonical JSON serialization

3. **Network/HTTP Issues**:
   ```
   Error: Failed to fetch metadata from URL
   ```
   - Check HTTP client implementation
   - Verify URL construction and handling

4. **File System Issues**:
   ```
   Error: Cannot write to metadata directory  
   ```
   - Check directory creation and permissions
   - Verify file path handling

### Debugging Steps

1. **Enable Detailed Logging**:
   - Check TufConformanceCli console output
   - Look for error messages and stack traces

2. **Manual CLI Testing**:
   - Run CLI commands manually with test data
   - Use `--help` to verify command structure

3. **Isolate the Issue**:
   - Test individual TUF operations (init, refresh, download)
   - Check with minimal test data

4. **Compare with Reference Implementations**:
   - Look at python-tuf, go-tuf, or tuf-js behavior
   - Check TUF specification for requirements

## Improving Conformance

### Development Workflow

1. **Identify Failing Tests**: Run conformance suite to see current failures
2. **Analyze Root Cause**: Determine if it's a missing feature or bug
3. **Implement Fix**: Add feature or fix bug in core TUF library
4. **Update CLI**: Ensure TufConformanceCli properly exposes the feature
5. **Test Locally**: Verify the specific test case now passes  
6. **Update Xfails**: Remove test from `.tuf-conformance-xfails` if now passing
7. **Commit and Test**: Ensure CI passes with the changes

### Common Improvement Areas

1. **Advanced TUF Features**:
   - Implement delegation validation and traversal
   - Add multi-signature threshold verification
   - Support consistent snapshots

2. **Network Robustness**:
   - Add timeout handling
   - Implement proper HTTP redirect following
   - Support custom HTTP headers

3. **Error Handling**:
   - Improve malformed metadata detection
   - Add proper expiration checking
   - Implement attack detection (rollback, freeze, etc.)

4. **Hash Algorithm Support**:
   - Add BLAKE2b-256 and BLAKE2b-512 support
   - Ensure all combinations work correctly

## CI/CD Integration

The conformance testing workflow is integrated into our CI/CD pipeline:

### Workflow File: `.github/workflows/tuf-conformance.yml`

Key features:
- **Multi-platform**: Currently Ubuntu, can extend to Windows/macOS  
- **Artifact Upload**: Saves test results for analysis
- **Flexible Triggering**: Automatic and manual runs
- **Integration**: Uses official `theupdateframework/tuf-conformance@v2` action

### Monitoring Progress

Track conformance improvement over time:

1. **Baseline Measurement**: Initial run establishes starting point
2. **Regular Monitoring**: Every PR shows impact on conformance  
3. **Goal Tracking**: Measure progress toward 90%+ pass rate
4. **Regression Detection**: Catch any loss of conformance quickly

## Resources

- **TUF Conformance Suite**: https://github.com/theupdateframework/tuf-conformance
- **TUF Specification**: https://theupdateframework.github.io/specification/latest/
- **CLI Protocol**: See CLIENT-CLI.md in the conformance suite repo
- **Reference Implementations**:
  - Python-TUF: https://github.com/theupdateframework/python-tuf
  - Go-TUF: https://github.com/theupdateframework/go-tuf
  - JavaScript: https://github.com/theupdateframework/tuf-js

---

This document will be updated as we gain experience with the conformance test suite and identify additional best practices for the .NET TUF implementation.