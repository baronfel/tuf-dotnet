# TUF Conformance Testing

This guide explains how to run and interpret TUF conformance tests for the .NET implementation. Conformance testing ensures our implementation correctly follows the TUF specification and is compatible with other implementations.

## What is TUF Conformance Testing?

The [TUF Conformance Test Suite](https://github.com/theupdateframework/tuf-conformance) is an official testing framework that validates TUF client implementations against the specification. It tests:

- **Metadata parsing and validation**
- **Signature verification**
- **Client refresh workflows** 
- **Error handling and security requirements**
- **Cross-implementation compatibility**

## Running Conformance Tests

### Prerequisites

1. **.NET 10 SDK** installed
2. **TUF Conformance CLI** built
3. **Python 3** (for the conformance test suite)

### Build the Conformance CLI

```bash
# Build the conformance CLI tool
dotnet publish examples/TufConformanceCli/TufConformanceCli.csproj \
    --configuration Release \
    --output ./tuf-conformance-cli

# Make it executable (Linux/macOS)
chmod +x ./tuf-conformance-cli/TufConformanceCli
```

### Local Testing

#### Quick Test
```bash
# Test the CLI works
./tuf-conformance-cli/TufConformanceCli --help

# Test basic functionality
./tuf-conformance-cli/TufConformanceCli init test-data/root.json --metadata-dir ./temp-metadata
```

#### Manual Conformance Run

If you have the conformance test suite locally:

```bash
# Clone the conformance test suite
git clone https://github.com/theupdateframework/tuf-conformance.git
cd tuf-conformance

# Install dependencies
pip install -r requirements.txt

# Run tests against our .NET implementation
python -m pytest --client-cmd="/path/to/tuf-conformance-cli/TufConformanceCli" \
    --xfail-file=/path/to/tuf-dotnet/.tuf-conformance-xfails
```

### GitHub Actions Integration

The conformance tests run automatically in CI:

```yaml
# .github/workflows/tuf-conformance.yml
- name: Run TUF Conformance Tests
  uses: theupdateframework/tuf-conformance@v2
  with:
    entrypoint: ./tuf-conformance-cli/TufConformanceCli
    xfail-file: .tuf-conformance-xfails
```

## Expected Failures (.tuf-conformance-xfails)

The `.tuf-conformance-xfails` file lists tests that are expected to fail during development. These represent:

- **Features not yet implemented**
- **Known issues being worked on** 
- **Specification ambiguities**

### Current Status

```bash
# Check current expected failures
cat .tuf-conformance-xfails
```

As of the latest version, the .NET implementation has **no expected failures** - all conformance tests pass.

### Managing Expected Failures

When implementing new features or fixing bugs:

1. **Remove lines** from `.tuf-conformance-xfails` as tests start passing
2. **Add lines** temporarily if introducing breaking changes
3. **Document reasons** for expected failures in comments

```bash
# Example .tuf-conformance-xfails content
# 
# Tests for features not yet implemented:
# test_delegations_hash_bin_*  # Hash-bin delegations not implemented
# test_custom_metadata_*       # Custom metadata fields not supported
#
# Tests with known issues:
# test_large_metadata_*        # Memory usage needs optimization
```

## Understanding Test Results

### Passing Tests ✅

```
PASSED test_basic_refresh[ed25519] - Basic metadata refresh with Ed25519 signatures
PASSED test_signature_verification[ecdsa] - ECDSA signature verification  
PASSED test_key_rotation[rsa-pss] - Root key rotation with RSA-PSS
```

**What this means**: The implementation correctly handles the tested functionality.

### Expected Failures ⚠️

```
XFAIL test_delegations_complex - Complex delegation workflows not implemented yet
```

**What this means**: The test was expected to fail (listed in `.tuf-conformance-xfails`).

### Unexpected Failures ❌

```
FAILED test_basic_refresh[ed25519] - AssertionError: Signature verification failed
```

**What this means**: A test that should pass is failing. This needs investigation.

### Unexpected Passes 🎉

```
XPASS test_delegations_simple - Delegation test passed unexpectedly
```

**What this means**: A test marked as expected failure actually passed. Remove it from `.tuf-conformance-xfails`.

## Common Test Categories

### Basic Functionality Tests

- **Metadata parsing**: Can the client parse valid metadata?
- **Signature verification**: Are signatures verified correctly?
- **Key handling**: Ed25519, ECDSA, RSA-PSS support
- **Expiration checking**: Does the client reject expired metadata?

### Error Handling Tests

- **Invalid metadata**: Malformed JSON, missing fields
- **Signature failures**: Invalid signatures, wrong keys  
- **Network errors**: Simulated connection failures
- **Security attacks**: Replay, freeze, mix-and-match attacks

### Advanced Feature Tests

- **Delegations**: Target delegation workflows
- **Multi-signature**: Threshold signature requirements
- **Key rotation**: Root key updates
- **Consistent snapshots**: Repository consistency features

## Debugging Failed Tests

### Step 1: Identify the Failure

```bash
# Run a specific test for detailed output
python -m pytest tests/test_basic.py::test_basic_refresh \
    --client-cmd="/path/to/TufConformanceCli" \
    -v -s
```

### Step 2: Check CLI Output

The conformance tests capture CLI stdout/stderr:

```
>       assert result.returncode == 0
E       AssertionError: assert 1 == 0
E        +  where 1 = CompletedProcess(args=['/path/to/TufConformanceCli', 'refresh', ...], returncode=1, stdout='', stderr='Error: Signature verification failed for key abc123...').returncode
```

### Step 3: Test CLI Manually

```bash
# Replicate the exact command that failed
./TufConformanceCli refresh \
    --metadata-dir /tmp/test-metadata \
    --metadata-url file:///tmp/test-repo/metadata

# Check what error occurs
echo $?  # Exit code (should be 0 for success)
```

### Step 4: Debug with Detailed Logging

```bash
# Enable detailed logging in the CLI
export TUF_LOG_LEVEL=Debug

# Or modify the CLI to add more diagnostic output
```

### Step 5: Compare with Python-TUF

```bash
# Run the same operation with Python-TUF for comparison
pip install tuf
python -c "
from tuf.ngclient import Updater
# ... test the same operation
"
```

## Writing Custom Conformance Tests

### Test Structure

Conformance tests use the CLI protocol defined in [CLIENT-CLI.md](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md):

```python
def test_custom_feature():
    # Setup: Create test repository
    with TemporaryDirectory() as temp_dir:
        repo_dir = create_test_repository(temp_dir)
        
        # Test: Run client CLI commands
        result = subprocess.run([
            CLIENT_CMD, "init", 
            f"{repo_dir}/root.json",
            "--metadata-dir", f"{temp_dir}/metadata"
        ])
        
        # Verify: Check results
        assert result.returncode == 0
        assert Path(f"{temp_dir}/metadata/root.json").exists()
```

### Adding Tests to the Suite

1. **Fork** the conformance test repository
2. **Add tests** following the existing patterns
3. **Test** against multiple implementations
4. **Submit PR** to the upstream repository

## Continuous Integration

### GitHub Actions Workflow

```yaml
name: TUF Conformance

on: [push, pull_request]

jobs:
  conformance:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
        
      - name: Build CLI
        run: |
          dotnet publish examples/TufConformanceCli/ \
            --configuration Release \
            --output ./cli
            
      - name: Run Conformance Tests
        uses: theupdateframework/tuf-conformance@v2
        with:
          entrypoint: ./cli/TufConformanceCli
          xfail-file: .tuf-conformance-xfails
```

### Monitoring Conformance Status

The conformance status is tracked in:

- **GitHub Actions**: Workflow results
- **Repository badges**: Pass/fail status
- **Issue tracking**: Failed test investigations

## Performance Considerations

### Test Execution Time

Conformance tests can be slow due to:

- **Cryptographic operations**: Key generation, signature verification
- **File I/O**: Repository creation, metadata parsing
- **Network simulation**: Timeout and error tests

### Optimization Strategies

1. **Parallel testing**: Run independent tests concurrently
2. **Test data caching**: Reuse generated repositories when possible
3. **Selective testing**: Run only relevant tests during development
4. **Fast feedback**: Quick smoke tests before full suite

```bash
# Run only basic tests for quick feedback
python -m pytest tests/test_basic.py \
    --client-cmd="/path/to/TufConformanceCli"

# Run full suite for comprehensive validation  
python -m pytest \
    --client-cmd="/path/to/TufConformanceCli"
```

## Conformance in Different Environments

### Development Environment

- **Run frequently**: After significant changes
- **Focus on failures**: Debug specific test categories
- **Quick iterations**: Use subset of tests for rapid development

### Staging Environment

- **Full test suite**: Run all conformance tests
- **Multiple platforms**: Test on different OS/architectures
- **Performance testing**: Measure test execution time

### Production Validation

- **Release gates**: Must pass before release
- **Regression testing**: Ensure no breaking changes
- **Documentation**: Update compatibility matrices

## Troubleshooting Guide

### CLI Not Found

```bash
Error: Command '/path/to/TufConformanceCli' not found
```

**Solution**: Ensure the CLI is built and executable:
```bash
dotnet publish examples/TufConformanceCli/ --output ./cli
chmod +x ./cli/TufConformanceCli
```

### Permission Errors

```bash
Error: Permission denied writing to /tmp/metadata
```

**Solution**: Ensure proper permissions:
```bash
chmod -R 755 /tmp/test-directory
```

### Memory Issues

```bash
Error: System.OutOfMemoryException during large metadata test
```

**Solution**: Optimize memory usage or increase limits:
```bash
export DOTNET_GCHeapHardLimit=4GB
```

### Network Timeouts

```bash
Error: Request timeout during metadata refresh
```

**Solution**: Check network configuration and timeouts:
```bash
# Test network connectivity
curl -v http://test-repository/metadata/root.json
```

## Best Practices

### Development Workflow

1. **Run conformance tests** before committing changes
2. **Update expected failures** as features are implemented
3. **Add tests** for new functionality
4. **Document** any implementation-specific behavior

### Debugging Approach

1. **Start simple**: Test basic functionality first
2. **Isolate issues**: Run individual failing tests
3. **Compare implementations**: Check behavior against Python-TUF
4. **Log extensively**: Add diagnostic output to understand flow

### Maintenance

1. **Keep tests current**: Update with new conformance suite versions
2. **Monitor upstream**: Track changes in TUF specification
3. **Regular execution**: Run full suite periodically
4. **Performance tracking**: Monitor test execution time trends

---

*For more information, see the [TUF Conformance Test Suite documentation](https://github.com/theupdateframework/tuf-conformance).*