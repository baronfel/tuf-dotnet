# TUF .NET Cross-Language Interoperability Tests

This test project verifies that TUF .NET can interoperate with other TUF implementations (Python-TUF, Go-TUF) and vice versa.

## What These Tests Do

1. **Repository Cross-Reading**: Test that .NET TUF can read repositories created by Python/Go implementations
2. **Repository Creation**: Test that Python/Go implementations can read repositories created by .NET TUF
3. **Signature Verification**: Test that signatures created by one implementation can be verified by another
4. **Metadata Compatibility**: Test that metadata serialization/deserialization works across implementations

## Running the Tests

### Prerequisites

For full interoperability testing, you'll need:

- **.NET 10.0+**: For running the .NET tests
- **Python 3.8+** with `python-tuf`: For Python interoperability tests
- **Go 1.19+** with `go-tuf`: For Go interoperability tests

```bash
# Install Python dependencies
pip install tuf[complete]

# Install Go dependencies
go mod init test && go get github.com/theupdateframework/go-tuf@latest
```

### Basic Test Run

```bash
# Run all interoperability tests
dotnet test TUF.InteroperabilityTests

# Run with detailed output
dotnet test TUF.InteroperabilityTests --logger "console;verbosity=detailed"
```

### Generate Test Fixtures

To test against real Python/Go repositories, generate test fixtures:

```bash
# Generate Python TUF repository
cd TUF.InteroperabilityTests/test-fixtures
python3 scripts/generate_python_fixtures.py python-tuf-repo ed25519

# The Go fixture generation would be similar if we had a Go script
```

## Test Structure

### Core Test Classes

- **`CrossLanguageInteroperabilityTests`**: Main test class with all interoperability scenarios
- **Test Fixtures**: Sample repositories and metadata for testing

### Test Categories

1. **`.NET → Python`**: Test Python-TUF reading .NET repositories
2. **`.NET → Go`**: Test Go-TUF reading .NET repositories  
3. **`Python → .NET`**: Test .NET TUF reading Python repositories
4. **`Go → .NET`**: Test .NET TUF reading Go repositories
5. **`Cross-Verification`**: Test signature verification across implementations

### Test Scenarios

Each test scenario covers:
- Multiple signature algorithms (Ed25519, ECDSA, RSA)
- Different repository structures
- Various metadata configurations
- Error conditions and edge cases

## Understanding Test Results

### Success Indicators

- ✅ **Repository Structure Valid**: Other implementations can parse the metadata structure
- ✅ **Signature Verification**: Signatures created by one implementation verify correctly in another
- ✅ **Metadata Compatibility**: JSON serialization/deserialization works across implementations
- ✅ **Key ID Consistency**: Key IDs calculated consistently across implementations

### Common Issues

- 🔄 **Skipped Tests**: Missing Python/Go dependencies or test fixtures
- ⚠️ **Minor Differences**: Non-critical differences in metadata formatting
- ❌ **Compatibility Issues**: Actual interoperability problems requiring fixes

### Example Output

```
Testing .NET TUF with Python repository: ./test-fixtures/python-tuf-repo
✅ Successfully loaded root.json
   - File size: 1247 bytes
✅ Successfully deserialized Python root metadata:
   - Version: 1
   - Spec Version: 1.0.31
   - Keys: 4
   - Signatures: 1
   - Expires: 2025-10-01T05:26:16Z
✅ Successfully loaded and validated Python-generated root metadata
```

## Extending the Tests

### Adding New Test Cases

1. **Create test scenario** in `CrossLanguageInteroperabilityTests.cs`
2. **Add test fixtures** in `test-fixtures/` directory
3. **Update documentation** to reflect new compatibility testing
4. **Run against multiple implementations** to verify

### Adding New Implementations

To add support for testing against a new TUF implementation:

1. Create generation script in `test-fixtures/scripts/`
2. Create validation script to test reading .NET repositories
3. Add test methods in the test class
4. Update the compatibility matrix in documentation

### Test Data Organization

```
test-fixtures/
├── scripts/
│   ├── generate_python_fixtures.py
│   ├── test_python_can_read_dotnet.py
│   └── test_go_can_read_dotnet.go
├── python-tuf-repo/
│   ├── metadata/
│   └── targets/
├── go-tuf-repo/
│   ├── repository/
│   └── staged/
└── cross-verification/
    ├── ed25519/
    ├── ecdsa/
    └── rsa/
```

## Troubleshooting

### Python Dependencies

```bash
# If python-tuf is not found:
pip install --upgrade tuf[complete]

# If specific crypto libraries are missing:
pip install cryptography securesystemslib
```

### Go Dependencies

```bash
# If go-tuf is not found:
go get github.com/theupdateframework/go-tuf@latest

# If Go toolchain is missing:
# Install Go 1.19+ from https://golang.org/
```

### Test Fixture Issues

- **Missing fixtures**: Run the fixture generation scripts
- **Outdated fixtures**: Re-generate fixtures with latest implementations
- **Permission issues**: Check file permissions in test-fixtures directory

### Debugging Failed Tests

1. **Check detailed test output**: Use `--logger "console;verbosity=detailed"`
2. **Verify dependencies**: Ensure Python/Go implementations are installed
3. **Check test fixtures**: Verify test data is present and valid
4. **Review compatibility matrix**: Check if the tested feature is expected to work
5. **Compare with reference implementations**: Test the same scenario with Python-TUF

## Integration with CI/CD

These tests can be integrated into CI/CD pipelines:

```yaml
# GitHub Actions example
- name: Setup Python
  uses: actions/setup-python@v4
  with:
    python-version: '3.11'
    
- name: Install Python TUF
  run: pip install tuf[complete]
  
- name: Setup Go
  uses: actions/setup-go@v4
  with:
    go-version: '1.21'
    
- name: Run Interoperability Tests
  run: dotnet test TUF.InteroperabilityTests --logger "trx;LogFileName=interop-results.trx"
```

## Contributing

Help improve cross-language interoperability:

1. **Test with different implementations**: Try various versions of Python-TUF, Go-TUF, etc.
2. **Report compatibility issues**: File issues with detailed reproduction steps
3. **Add test coverage**: Create tests for scenarios not currently covered
4. **Update documentation**: Keep the compatibility matrix current

See the [Interoperability Documentation](../docs/standards/interoperability.md) for detailed compatibility information.