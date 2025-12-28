# TUF Conformance Testing

This document describes how the .NET TUF implementation integrates with the official TUF conformance test suite to ensure specification compliance.

## Overview

The TUF .NET implementation includes a conformance CLI tool (`examples/TufConformanceCli`) that implements the [CLIENT-CLI.md specification](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md) from the TUF conformance test suite. This enables automated testing of our implementation against the official TUF specification.

## Conformance CLI

The conformance CLI provides three commands that follow the exact specification:

### `init` - Initialize Client Metadata
```bash
dotnet run --project examples/TufConformanceCli --metadata-dir ./metadata init path/to/trusted-root.json
```

- Copies the trusted root.json into the metadata directory
- Creates the directory if it doesn't exist
- Returns exit code 0 for success, 1 for failure

### `refresh` - Update Metadata from Repository
```bash
dotnet run --project examples/TufConformanceCli refresh --metadata-dir ./metadata --metadata-url https://example.com/metadata
```

- Updates all top-level metadata per TUF workflow
- Uses non-versioned metadata filenames
- Returns exit code 0 for full success, 1 for any failure

### `download` - Download and Verify Target
```bash
dotnet run --project examples/TufConformanceCli download \
  --metadata-dir ./metadata \
  --metadata-url https://example.com/metadata \
  --target-name file.txt \
  --target-base-url https://example.com/targets \
  --target-dir ./downloads
```

- Ensures metadata is up-to-date before downloading
- Downloads and verifies the target artifact
- Supports artifact caching
- Returns exit code 0 for success, 1 for failure

## Running Conformance Tests

### Automated CI/CD Testing

The conformance tests are automatically run in GitHub Actions:

1. **On every push and pull request** to validate changes
2. **Weekly scheduled runs** to catch any regressions
3. **Manual workflow dispatch** for on-demand testing

View the workflow: `.github/workflows/conformance.yml`

### Local Testing

To test conformance locally, you need to have the official TUF conformance test suite available. The basic CLI functionality can be tested with:

```bash
# Build the conformance CLI
dotnet publish examples/TufConformanceCli/TufConformanceCli.csproj \
  --configuration Release \
  --output ./conformance-cli

# Test basic functionality
./conformance-cli/TufConformanceCli --help
```

### Manual Integration with TUF Conformance Suite

If you want to run the full conformance test suite manually:

```bash
# Clone the conformance test suite
git clone https://github.com/theupdateframework/tuf-conformance.git
cd tuf-conformance

# Use our CLI as the test subject
pytest --entrypoint="/path/to/our/TufConformanceCli" --xfails-file="/path/to/.tuf-conformance-xfails"
```

## Expected Failures

The `.tuf-conformance-xfails` file in the repository root tracks tests that are expected to fail during development. As features are implemented and bugs are fixed, entries should be removed from this file.

Currently, the .NET implementation has no expected failures, indicating good baseline conformance with the TUF specification.

## Implementation Details

### Standards Compliance

The conformance CLI strictly follows:

- **CLIENT-CLI.md specification** for command-line interface
- **TUF specification v1.0+** for all metadata handling
- **Canonical JSON** for consistent metadata serialization
- **Standard exit codes** (0 = success, 1 = failure)

### Architecture Integration

The conformance CLI is built on the same core TUF library (`TUF/`) used by all other examples and applications in this repository:

- **`Updater` class** for metadata refresh and target download
- **`TrustedMetadata` classes** for secure metadata verification
- **Canonical JSON serialization** for deterministic output
- **Multi-algorithm signature support** (Ed25519, RSA-PSS, ECDSA)

### Performance Characteristics

The conformance CLI is optimized for:

- **Fast startup time** with AOT compilation support
- **Minimal memory usage** for resource-constrained testing environments
- **Deterministic behavior** for reproducible test results
- **Clear error reporting** for debugging failed tests

## Monitoring Conformance

### CI/CD Integration

The GitHub Actions workflow provides:

- **Build verification** before running conformance tests
- **Artifact upload** of test results for analysis
- **Failure notifications** when conformance regressions occur
- **Weekly monitoring** to catch external changes

### Results Analysis

Conformance test results include:

- **Pass/fail status** for each test case
- **Error details** for any failures
- **Performance metrics** for test execution time
- **Coverage analysis** of TUF specification features

## Contributing to Conformance

### Adding New Test Coverage

When implementing new TUF features:

1. **Ensure conformance CLI supports the feature** if it affects client behavior
2. **Update expected failures** if new tests are added to the conformance suite
3. **Test locally** before submitting pull requests
4. **Document any limitations** in the conformance CLI README

### Reporting Issues

If conformance tests reveal issues:

1. **File GitHub issues** with conformance test output
2. **Include reproduction steps** using the conformance CLI
3. **Reference TUF specification sections** that may be affected
4. **Suggest fixes** when possible

## Future Enhancements

Potential improvements to conformance testing:

- **Performance benchmarking** against other TUF implementations
- **Extended test scenarios** beyond the basic conformance suite
- **Integration testing** with real TUF repositories
- **Compatibility testing** across different .NET versions and platforms

---

This conformance testing infrastructure ensures the .NET TUF implementation maintains high quality and specification compliance, providing confidence for production deployments.