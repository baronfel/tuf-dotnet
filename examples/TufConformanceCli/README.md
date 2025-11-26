# TUF Conformance CLI

This is a TUF client implementation that follows the [CLIENT-CLI.md specification](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md) from the TUF conformance test suite.

## Overview

The TUF Conformance CLI provides a standardized interface for testing TUF client implementations against the official conformance test suite. This enables the .NET TUF implementation to be validated for compliance with the TUF specification alongside other implementations like python-tuf and go-tuf.

## Commands

All commands support the `--verbose` flag for detailed logging during conformance test debugging.

### init

Initialize the client's local trusted metadata:

```bash
dotnet run --project examples/TufConformanceCli --metadata-dir ./metadata init path/to/trusted-root.json
```

With verbose logging:
```bash
dotnet run --project examples/TufConformanceCli --verbose --metadata-dir ./metadata init path/to/trusted-root.json
```

- `--metadata-dir`: Directory where metadata will be stored
- `--verbose`: Enable detailed logging for debugging
- `trusted-root`: Path to the trusted root.json file

### refresh

Update local metadata from the repository:

```bash
dotnet run --project examples/TufConformanceCli refresh --metadata-dir ./metadata --metadata-url https://example.com/metadata
```

With verbose logging:
```bash
dotnet run --project examples/TufConformanceCli --verbose refresh --metadata-dir ./metadata --metadata-url https://example.com/metadata
```

- `--metadata-dir`: Directory containing local metadata
- `--metadata-url`: Base URL for metadata repository
- `--verbose`: Enable detailed logging for debugging

### download

Download and verify a target artifact:

```bash
dotnet run --project examples/TufConformanceCli download \
  --metadata-dir ./metadata \
  --metadata-url https://example.com/metadata \
  --target-name file.txt \
  --target-base-url https://example.com/targets \
  --target-dir ./downloads
```

With verbose logging:
```bash
dotnet run --project examples/TufConformanceCli --verbose download \
  --metadata-dir ./metadata \
  --metadata-url https://example.com/metadata \
  --target-name file.txt \
  --target-base-url https://example.com/targets \
  --target-dir ./downloads
```

- `--metadata-dir`: Directory containing local metadata
- `--metadata-url`: Base URL for metadata repository
- `--target-name`: Name of the target file to download
- `--target-base-url`: Base URL for target files
- `--target-dir`: Directory to save the downloaded file
- `--verbose`: Enable detailed logging for debugging

## Exit Codes

All commands use standard exit codes:
- `0`: Success
- `1`: Failure

## Usage with TUF Conformance Test Suite

This CLI can be integrated with the official TUF conformance test suite using GitHub Actions:

```yaml
- uses: theupdateframework/tuf-conformance@v2
  with:
    entrypoint: ./tuf-conformance-cli/TufConformanceCli
```

### Complete GitHub Actions Workflow Example

Here's a complete workflow for TUF conformance testing:

```yaml
name: TUF Conformance Testing

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  conformance:
    runs-on: ubuntu-latest
    name: TUF Conformance Test Suite
    
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Restore dependencies
        run: dotnet restore ./TUF.slnx

      - name: Build TUF Conformance CLI
        run: |
          dotnet build examples/TufConformanceCli/TufConformanceCli.csproj \
            --configuration Release \
            --no-restore

      - name: Publish TUF Conformance CLI (Self-Contained)
        run: |
          dotnet publish examples/TufConformanceCli/TufConformanceCli.csproj \
            --configuration Release \
            --runtime linux-x64 \
            --self-contained true \
            --output ./tuf-conformance-cli \
            -p:PublishSingleFile=true \
            -p:PublishTrimmed=true

      - name: Make CLI executable
        run: chmod +x ./tuf-conformance-cli/TufConformanceCli

      - name: Verify CLI works
        run: ./tuf-conformance-cli/TufConformanceCli --help

      - name: Run TUF Conformance Test Suite
        uses: theupdateframework/tuf-conformance@v2
        with:
          entrypoint: ./tuf-conformance-cli/TufConformanceCli

      - name: Upload conformance test results on failure
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: conformance-test-results
          path: |
            ./tuf-conformance-cli/
            ./*.log
            ./*.xml
          retention-days: 7
```

This workflow:
1. Builds the TUF .NET solution
2. Publishes the conformance CLI as a self-contained executable
3. Runs the official TUF conformance test suite
4. Handles expected test failures via `TufConformanceCli.xfails`

### Expected Test Failures

The `TufConformanceCli.xfails` file lists tests that are expected to fail during development. As features are implemented, tests should be removed from this file.

## Implementation Notes

- Uses non-versioned metadata filenames as required by the specification
- Ensures metadata is up-to-date before downloading targets
- Provides proper error handling and informative error messages
- Follows TUF workflow for metadata refresh and target verification
- Enhanced logging capabilities for conformance test debugging
- Structured output with clear INFO/ERROR/VERBOSE message levels
- Comprehensive exception handling with debug information in verbose mode

## Security Considerations

This CLI is designed for testing and conformance validation. For production use, consider:
- Secure storage of trusted root metadata
- Proper network configuration and timeouts
- Key pinning and rotation procedures
- Monitoring and logging for security events