# TUF Conformance Testing

This document explains how the .NET TUF implementation integrates with the [official TUF conformance test suite](https://github.com/theupdateframework/tuf-conformance) to ensure specification compliance and interoperability with other TUF implementations.

## Overview

Conformance testing validates that the TUF .NET implementation correctly implements the TUF specification and behaves consistently with reference implementations like python-tuf and go-tuf. This is critical for ensuring security properties and ecosystem interoperability.

## Test Suite Integration

### Automated Testing (GitHub Actions)

The conformance tests run automatically on every push and pull request via the `.github/workflows/conformance.yml` workflow:

```yaml
# Triggers
- Push to main branch
- Pull requests to main branch  
- Manual workflow dispatch
```

**Workflow Steps:**
1. **Environment Setup** - Installs .NET SDK and builds the TUF conformance CLI
2. **CLI Verification** - Tests that the conformance CLI responds correctly
3. **Conformance Suite Setup** - Clones and installs the official test suite
4. **Test Execution** - Runs tests against the .NET implementation
5. **Result Analysis** - Processes results and generates summary reports
6. **Artifact Upload** - Preserves test results for analysis

### Manual Testing (Local Development)

For local development and debugging, use the `scripts/run-conformance-tests.py` script:

```bash
# Full conformance test run
python3 scripts/run-conformance-tests.py

# Setup environment only
python3 scripts/run-conformance-tests.py --setup-only

# Analyze existing results only
python3 scripts/run-conformance-tests.py --analyze-only
```

## Understanding Test Results

### Success Metrics

- **Pass Rate**: Percentage of conformance tests that pass successfully
- **Failed Tests**: Specific test cases that need attention
- **Expected Failures**: Known limitations tracked in `.tuf-conformance-xfails`

### Result Categories

- ✅ **PASS**: Test completed successfully - implementation behaves correctly
- ❌ **FAIL**: Test failed - indicates implementation gap or bug
- ⏭️ **SKIP**: Test was skipped - typically due to expected failure configuration

### Example Results Output

```
🎯 TUF .NET Conformance Test Results:
   ✅ Passed:   85
   ❌ Failed:   12
   ⏭️ Skipped:   3
   📊 Total:   100
   🎯 Success Rate: 85.0%
```

## Expected Failures Management

### `.tuf-conformance-xfails` File

This file tracks tests that are expected to fail during development:

```bash
# Network/HTTP behavior - may need fine-tuning
test_max_request_*
test_timeout_*

# Advanced TUF features not yet implemented  
test_delegations_*
test_multisig_*
```

### Best Practices

1. **Document Reasons** - Each expected failure should include a comment explaining why
2. **Remove When Fixed** - Update the file as implementation gaps are resolved
3. **Regular Review** - Periodically review expected failures to ensure they're still valid
4. **Track Progress** - Use expected failure changes to measure parity improvement

## Test Categories

### Core TUF Functionality
- **Metadata Operations**: root.json, timestamp.json, snapshot.json, targets.json
- **Signature Verification**: Ed25519, RSA-PSS, ECDSA algorithms
- **Hash Verification**: SHA-256, SHA-512 algorithms
- **Version Validation**: Metadata version consistency and rollback protection

### Security Properties  
- **Timestamp Freshness**: Prevents freeze attacks
- **Snapshot Consistency**: Ensures consistent repository views
- **Target Integrity**: Verifies file content hasn't been tampered with
- **Key Validation**: Proper key usage and rotation handling

### Edge Cases
- **Expired Metadata**: Graceful handling of expired timestamps
- **Malformed Data**: Robustness against invalid input
- **Network Failures**: Retry logic and timeout handling
- **Key Rotation**: Smooth transitions between key sets

### Advanced Features
- **Delegations**: Target delegation trees and validation
- **Multi-signatures**: M-of-N signature schemes
- **Consistent Snapshots**: Advanced repository consistency
- **Custom Metadata**: Extension fields and unrecognized data

## CLI Implementation Details

The `examples/TufConformanceCli` project implements the [TUF conformance CLI specification](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md):

### Commands

#### `init`
Initialize client with trusted root metadata:
```bash
dotnet run -- init /path/to/trusted-root.json --metadata-dir ./metadata
```

#### `refresh`  
Update local metadata from repository:
```bash
dotnet run -- refresh --metadata-dir ./metadata --metadata-url https://repo.example.com
```

#### `download`
Download and verify target file:
```bash
dotnet run -- download \
  --metadata-dir ./metadata \
  --metadata-url https://repo.example.com \
  --target-name file.txt \
  --target-base-url https://repo.example.com/targets \
  --target-dir ./downloads
```

### Error Handling

The CLI uses standard exit codes:
- `0` - Success
- `1` - Failure (with descriptive error messages)

## Troubleshooting

### Common Issues

#### Build Failures
```bash
# Ensure .NET SDK is installed and restore dependencies
dotnet restore
dotnet build examples/TufConformanceCli/TufConformanceCli.csproj
```

#### CLI Not Responding
```bash
# Test CLI directly
dotnet run --project examples/TufConformanceCli/TufConformanceCli.csproj -- --help
```

#### Python Environment Issues
```bash
# Ensure Python 3.11+ and pip are available
python3 --version
pip install --upgrade pip
```

#### Missing Conformance Suite
```bash
# Clone manually if needed
git clone https://github.com/theupdateframework/tuf-conformance.git
cd tuf-conformance
pip install -e .
```

### Debug Mode

For detailed debugging, run tests with verbose output:

```bash
# Add verbose logging to conformance CLI
dotnet run --project examples/TufConformanceCli/TufConformanceCli.csproj -- --help --verbose
```

## Contributing to Conformance

### Improving Test Coverage

1. **Identify Gaps** - Review failed tests to understand implementation needs
2. **Implement Features** - Add missing TUF functionality to core libraries  
3. **Update Tests** - Remove fixed tests from `.tuf-conformance-xfails`
4. **Validate Changes** - Ensure new functionality passes conformance tests

### Reporting Issues

When conformance tests reveal bugs:

1. **Create Issue** - Document the specific test failure and expected behavior
2. **Include Logs** - Attach conformance test output and error messages
3. **Reproduce Locally** - Verify the issue can be reproduced outside CI
4. **Link to Spec** - Reference relevant TUF specification sections

## Integration with Development Workflow

### Pre-commit Checks

Consider running key conformance tests locally before committing:

```bash
# Quick conformance check
python3 scripts/run-conformance-tests.py --setup-only
```

### Release Validation

Before releases, ensure conformance test pass rate meets quality targets:

- **Development Releases**: 70%+ pass rate acceptable
- **Stable Releases**: 90%+ pass rate recommended
- **Production Releases**: 95%+ pass rate required

## Reference Links

- [TUF Conformance Test Suite](https://github.com/theupdateframework/tuf-conformance)
- [TUF Specification](https://theupdateframework.github.io/specification/)
- [Python TUF Conformance](https://github.com/theupdateframework/python-tuf)
- [Go TUF Conformance](https://github.com/theupdateframework/go-tuf)

---

*This conformance testing integration is part of the [achieve-parity effort](https://github.com/baronfel/tuf-dotnet/issues/87) to bring the .NET TUF implementation to feature parity with other mature implementations.*