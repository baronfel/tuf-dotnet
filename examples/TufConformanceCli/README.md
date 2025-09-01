# TUF Conformance CLI

This is a TUF client implementation that follows the [CLIENT-CLI.md specification](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md) from the TUF conformance test suite.

## Overview

The TUF Conformance CLI provides a standardized interface for testing TUF client implementations against the official conformance test suite. This enables the .NET TUF implementation to be validated for compliance with the TUF specification alongside other implementations like python-tuf and go-tuf.

## Commands

### init

Initialize the client's local trusted metadata:

```bash
dotnet run --project examples/TufConformanceCli --metadata-dir ./metadata path/to/trusted-root.json
```

- `--metadata-dir`: Directory where metadata will be stored
- `trusted-root`: Path to the trusted root.json file

### refresh

Update local metadata from the repository:

```bash
dotnet run --project examples/TufConformanceCli refresh --metadata-dir ./metadata --metadata-url https://example.com/metadata
```

- `--metadata-dir`: Directory containing local metadata
- `--metadata-url`: Base URL for metadata repository

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

- `--metadata-dir`: Directory containing local metadata
- `--metadata-url`: Base URL for metadata repository
- `--target-name`: Name of the target file to download
- `--target-base-url`: Base URL for target files
- `--target-dir`: Directory to save the downloaded file

## Exit Codes

All commands use standard exit codes:
- `0`: Success
- `1`: Failure

## Usage with TUF Conformance Test Suite

This CLI can be integrated with the official TUF conformance test suite using GitHub Actions:

```yaml
- uses: theupdateframework/tuf-conformance@v2
  with:
    entrypoint: dotnet run --project examples/TufConformanceCli/TufConformanceCli.csproj --
```

## Implementation Notes

- Uses non-versioned metadata filenames as required by the specification
- Ensures metadata is up-to-date before downloading targets
- Provides proper error handling and informative error messages
- Follows TUF workflow for metadata refresh and target verification

## Security Considerations

This CLI is designed for testing and conformance validation. For production use, consider:
- Secure storage of trusted root metadata
- Proper network configuration and timeouts
- Key pinning and rotation procedures
- Monitoring and logging for security events