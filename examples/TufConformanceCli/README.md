# TUF Conformance CLI

This CLI tool implements the [TUF conformance test suite client-under-test protocol](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md), enabling the .NET TUF implementation to participate in official TUF conformance testing.

## Purpose

The TUF conformance test suite helps measure client conformance with the TUF specification and improve compatibility between implementations. This CLI allows the `tuf-dotnet` implementation to be tested against the same conformance tests used by python-tuf, go-tuf, and other implementations.

## Commands

### init
Initialize the client's local trusted metadata.

```bash
dotnet run --metadata-dir <METADATA_DIR> init <TRUSTED_ROOT>
```

- Copies the initial root.json into the metadata directory
- No repository requests are made during initialization
- Exit code 0 for success, 1 for failure

### refresh
Update local metadata from the repository.

```bash
dotnet run --metadata-dir <METADATA_DIR> --metadata-url <METADATA_URL> refresh
```

- Updates top-level metadata per TUF workflow
- Uses non-versioned metadata filenames (e.g., "root.json")
- Exit code 0 for success, 1 for failure

### download
Download an artifact from repository and store it locally.

```bash
dotnet run --metadata-dir <METADATA_DIR> \
           --metadata-url <METADATA_URL> \
           --target-name <TARGET_PATH> \
           --target-base-url <TARGET_URL> \
           --target-dir <TARGET_DIR> \
           download
```

- Ensures metadata is up-to-date before downloading
- Downloads and verifies the artifact
- Supports artifact caching
- Exit code 0 for success, 1 for failure

## Usage with TUF Conformance Test Suite

To integrate this CLI with the TUF conformance test suite in GitHub Actions:

```yaml
- uses: theupdateframework/tuf-conformance@v2
  with:
    entrypoint: dotnet run --project examples/TufConformanceCli/TufConformanceCli.csproj --
```

## Expected Failures

If certain tests are expected to fail (due to unimplemented features), create a `.xfails` file listing:

```
# Comments are allowed
test-name
test-group
test-group[parameter]
```

## Building and Testing

```bash
# Build the project
dotnet build examples/TufConformanceCli/TufConformanceCli.csproj

# Run the CLI
dotnet run --project examples/TufConformanceCli/TufConformanceCli.csproj -- --help
```