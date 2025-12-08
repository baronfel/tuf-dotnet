# TUF Conformance Testing

This document explains how TUF .NET integrates with the official TUF Conformance Test Suite to ensure specification compliance and interoperability with other TUF implementations.

## Overview

The [TUF Conformance Test Suite](https://github.com/theupdateframework/tuf-conformance) is the official test suite maintained by The Update Framework community to validate TUF client implementations against the TUF specification. It helps ensure that TUF .NET:

- Complies with the TUF specification
- Maintains interoperability with other TUF implementations  
- Catches regressions in TUF behavior
- Identifies gaps in specification coverage

## Automated Testing

### GitHub Actions Integration

TUF .NET includes automated conformance testing via GitHub Actions in `.github/workflows/tuf-conformance.yml`. The workflow:

- **Triggers**: 
  - On all pushes to `main` branch
  - On all pull requests to `main` branch  
  - Daily at 6 AM UTC (to catch upstream spec changes)
  - Manual dispatch for testing

- **Process**:
  1. Builds the `TufConformanceCli` as a self-contained Linux executable
  2. Runs the official conformance test suite against the CLI
  3. Uploads test results as artifacts for analysis
  4. Reports summary results in the GitHub Actions summary

### Test Results

Conformance test results are available as:

- **GitHub Actions Summary**: High-level pass/fail status and key metrics
- **Artifacts**: Complete test output, logs, and XML results (retained for 30 days)
- **Workflow Status**: Overall conformance status visible in commit/PR status checks

## Local Testing

### Prerequisites

To run conformance tests locally, you need:

- .NET 10+ SDK
- Python 3.8+ (for the conformance test suite)
- Make (optional, for convenience commands)

### Manual Test Execution

1. **Build the conformance CLI**:
   ```bash
   dotnet publish examples/TufConformanceCli/TufConformanceCli.csproj \
     --configuration Release \
     --self-contained true \
     --runtime linux-x64 \
     --output ./conformance-cli
   chmod +x ./conformance-cli/TufConformanceCli
   ```

2. **Clone the conformance test suite**:
   ```bash
   git clone https://github.com/theupdateframework/tuf-conformance.git
   cd tuf-conformance
   ```

3. **Run the tests**:
   ```bash
   # Run all conformance tests
   python -m pytest --entrypoint ../conformance-cli/TufConformanceCli

   # Run specific tests
   python -m pytest --entrypoint ../conformance-cli/TufConformanceCli -k "test_refresh"

   # Run with verbose output
   python -m pytest --entrypoint ../conformance-cli/TufConformanceCli -v
   ```

## Expected Failures

### Configuration

Expected test failures are configured in `.tuf-conformance-xfails` at the repository root. This file lists tests that are expected to fail due to:

- **Unimplemented features**: Advanced TUF features not yet supported
- **Platform limitations**: Features unavailable on the target platform  
- **Specification gaps**: Edge cases or ambiguities in the TUF specification
- **Known issues**: Documented bugs or limitations being addressed

### Format

The xfails file format:
```bash
# Comments start with #
test_specific_failure_name
test_pattern_*[parameter]
test_category_*
```

### Maintenance

As features are implemented and bugs are fixed:

1. **Remove resolved failures** from `.tuf-conformance-xfails`
2. **Update documentation** to reflect new capabilities
3. **Add new expected failures** for any newly discovered limitations

## Conformance CLI Implementation

### Architecture

The `TufConformanceCli` (`examples/TufConformanceCli/`) implements the [TUF Conformance CLI Protocol](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md):

```
TufConformanceCli [global-options] <command> [args]

Commands:
  init     - Initialize client with trusted root metadata
  refresh  - Update local metadata from repository  
  download - Download and verify target from repository

Global Options:
  --metadata-dir   - Directory for metadata storage
  --metadata-url   - Base URL for metadata
  --target-name    - Name of target to download
  --target-base-url - Base URL for targets  
  --target-dir     - Directory to save downloaded target
```

### Key Features

- **Self-contained deployment**: No external dependencies required
- **AOT compilation**: Fast startup and low memory footprint
- **Error handling**: Proper exit codes and error messages for test validation
- **Logging**: Structured logging for debugging test failures
- **Platform support**: Cross-platform compatibility (Windows, macOS, Linux)

## Integration with TUF .NET Library

### API Usage

The conformance CLI demonstrates real-world usage patterns of the TUF .NET library:

```csharp
// Initialize with trusted root
var config = new UpdaterConfig(rootBytes, new Uri(metadataUrl))
{
    LocalMetadataDir = metadataDir,
    LocalTargetsDir = targetDir,
    Client = new HttpClient()
};

// Create updater and refresh metadata
var updater = new Updater(config);
await updater.RefreshAsync();

// Download and verify targets
var (targetPath, targetInfo) = await updater.GetTargetInfo(targetName);
var result = await updater.DownloadTarget(targetInfo, targetPath, null, new Uri(targetBaseUrl));
```

### Library Coverage

The conformance tests exercise these TUF .NET library components:

- **Core types**: `Metadata<T>`, `Root`, `Timestamp`, `Snapshot`, `Targets`
- **Cryptographic verification**: Ed25519, ECDSA, RSA-PSS signature validation
- **Client workflow**: `Updater`, `TrustedMetadata`, update process
- **JSON handling**: Canonical JSON serialization/deserialization
- **Network operations**: HTTP client integration and error handling
- **File operations**: Metadata and target file management

## Troubleshooting

### Common Issues

**Build Failures**:
- Ensure .NET 10+ SDK is installed
- Run `dotnet restore` before building
- Check for AOT compatibility issues in dependencies

**Test Execution Failures**:  
- Verify the conformance CLI executable has execute permissions
- Check that the conformance test suite Python dependencies are installed
- Review test output logs for specific error details

**Unexpected Test Failures**:
- Check if the failure is due to a new feature/requirement in the test suite
- Review the TUF specification for changes
- Add to `.tuf-conformance-xfails` if it's a known limitation
- File an issue if it indicates a bug in TUF .NET

### Debugging

1. **Enable verbose logging** in the conformance CLI
2. **Run single tests** to isolate issues
3. **Compare behavior** with reference implementations (Python TUF, Go TUF)
4. **Review artifacts** from failed GitHub Actions runs

## Contributing

### Adding New Features

When implementing new TUF features:

1. **Add library support** in the core TUF .NET library
2. **Update conformance CLI** to expose the feature if needed
3. **Remove expected failures** from `.tuf-conformance-xfails`
4. **Test locally** before opening a pull request
5. **Document behavior** in relevant documentation files

### Improving Test Coverage

To improve conformance test coverage:

1. **Review failing tests** to understand specification requirements
2. **Implement missing functionality** in the TUF .NET library
3. **Add edge case handling** to improve robustness
4. **Contribute to the conformance suite** if you identify gaps

## References

- [TUF Conformance Test Suite](https://github.com/theupdateframework/tuf-conformance)
- [TUF Specification](https://theupdateframework.io/)
- [TUF .NET Library Documentation](../README.md)
- [Client CLI Protocol Specification](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md)