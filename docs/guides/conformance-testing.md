# TUF Conformance Testing

This document describes how the .NET TUF implementation validates conformance with The Update Framework specification using the official TUF conformance test suite.

## Overview

The TUF .NET implementation includes comprehensive conformance testing to ensure compatibility with other TUF implementations and adherence to the TUF specification. This testing uses the official [TUF conformance test suite](https://github.com/theupdateframework/tuf-conformance) maintained by The Update Framework project.

## Conformance CLI

The conformance testing relies on a specialized CLI (`TufConformanceCli`) that implements the client-under-test protocol defined by the conformance test suite.

### CLI Commands

The conformance CLI supports the three required commands:

#### `init`
Initializes the client's local trusted metadata:
```bash
TufConformanceCli --metadata-dir <DIR> init <TRUSTED_ROOT>
```

- Copies the initial root.json into the metadata directory
- No repository requests are made during initialization
- Exit code 0 for success, 1 for failure

#### `refresh`  
Updates local metadata from the repository:
```bash
TufConformanceCli --metadata-dir <DIR> --metadata-url <URL> refresh
```

- Updates top-level metadata following the TUF client workflow
- Uses non-versioned metadata filenames (e.g., "root.json")
- Exit code 0 for success, 1 for failure

#### `download`
Downloads and verifies a target artifact:
```bash
TufConformanceCli --metadata-dir <DIR> --metadata-url <URL> --target-name <NAME> --target-base-url <URL> --target-dir <DIR> download
```

- Ensures metadata is up-to-date before downloading
- Supports artifact caching and verification
- Exit code 0 for success, 1 for failure

## GitHub Actions Integration

The conformance testing is automated via GitHub Actions in the `.github/workflows/conformance.yml` workflow:

```yaml
- uses: theupdateframework/tuf-conformance@v2
  with:
    entrypoint: ./tuf-conformance-cli
    artifact-name: tuf-dotnet-conformance-results
```

### When Tests Run

- **Pull Requests**: All PRs against main/develop branches
- **Pushes**: Direct pushes to main/develop branches  
- **Scheduled**: Daily at 2 AM UTC to catch upstream changes
- **Manual**: Via GitHub Actions dispatch

### Expected Failures

Tests that are expected to fail (for features not yet implemented or intentionally unsupported) are listed in `tuf-conformance-cli.xfails`. The format supports:

- Individual tests: `test_specific_feature`
- Test groups: `test_group_name`
- Parameterized tests: `test_group[parameter]`
- Comments: Lines starting with `#`

## Running Conformance Tests Locally

### Prerequisites
- .NET 10.0+ SDK
- Internet connection (for downloading test suite)

### Build the Conformance CLI
```bash
dotnet build examples/TufConformanceCli/TufConformanceCli.csproj --configuration Release
```

### Create CLI Wrapper Script
```bash
cat > tuf-conformance-cli << 'EOF'
#!/bin/bash
dotnet exec "examples/TufConformanceCli/bin/Release/net10.0/TufConformanceCli.dll" "$@"
EOF
chmod +x tuf-conformance-cli
```

### Test CLI Interface
```bash
./tuf-conformance-cli --help
./tuf-conformance-cli init --help
./tuf-conformance-cli refresh --help
./tuf-conformance-cli download --help
```

### Manual Conformance Testing

While the full conformance test suite requires Python/pytest infrastructure, you can validate basic CLI compliance manually:

1. **Interface Validation**: Ensure all required commands and options exist
2. **Error Handling**: Test appropriate exit codes for missing arguments
3. **Basic Workflow**: Test init → refresh → download sequence with real metadata

## Implementation Coverage

The .NET TUF implementation supports the following conformance areas:

### ✅ Implemented Features
- **Signature Algorithms**: Ed25519, RSA-PSS, ECDSA P-256
- **Hash Algorithms**: SHA-256, SHA-512  
- **Client Workflow**: Complete TUF update workflow
- **Metadata Types**: Root, Timestamp, Snapshot, Targets
- **Security Features**: Signature verification, rollback protection, delegation handling
- **Error Handling**: Comprehensive exception hierarchy with appropriate exit codes
- **Consistent Snapshots**: Full support for consistent snapshot repositories

### ⚠️ Areas Under Review
- **Blake2b Hashing**: Not currently implemented (SHA-256/SHA-512 preferred)
- **Advanced Delegations**: Complex delegation scenarios may need additional testing
- **Network Resilience**: Retry policies and timeout handling are implemented but may need tuning

### 🔧 Monitoring
- Features listed in `tuf-conformance-cli.xfails` represent known gaps
- Empty xfails file indicates full conformance
- Regular monitoring via scheduled CI runs

## Contributing

When making changes that may affect conformance:

1. **Run Tests**: Ensure conformance tests pass locally when possible
2. **Update XFails**: Add any newly discovered limitations to xfails file
3. **Documentation**: Update this guide if new features affect conformance
4. **Issue Tracking**: Link conformance failures to GitHub issues for tracking

## Related Resources

- [TUF Conformance Test Suite](https://github.com/theupdateframework/tuf-conformance)
- [TUF Specification](https://theupdateframework.github.io/specification/latest/)
- [Client-Under-Test CLI Protocol](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md)
- [TUF .NET Examples](../examples/)

---

For questions about conformance testing, please open an issue in the repository or refer to the [TUF community resources](https://github.com/theupdateframework).