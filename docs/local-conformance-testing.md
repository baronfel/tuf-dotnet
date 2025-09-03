# Local TUF Conformance Testing

This document describes the local conformance testing infrastructure for debugging TUF implementation issues.

## Overview

The local conformance tests provide a way to test the TUF .NET implementation against the same patterns used by the official [TUF conformance test suite](https://github.com/theupdateframework/tuf-conformance), but with better debugging capabilities and local control.

## Quick Start

### Prerequisites

- .NET 10 SDK
- Built TUF .NET solution
- PowerShell (for Windows) or Bash (for Linux/macOS)

### Running Tests

**PowerShell (Windows/Cross-platform):**
```powershell
# Run all local conformance tests
./test-conformance-local.ps1

# Run specific test with verbose output
./test-conformance-local.ps1 -Test "Test_Init_Command" -Verbose
```

**Bash (Linux/macOS):**
```bash
# Run all local conformance tests  
./test-conformance-local.sh

# Run specific test with verbose output
./test-conformance-local.sh --test Test_Init_Command --verbose
```

## Architecture

### Components

1. **ConformanceTests.cs** - Main test class that sets up local test infrastructure
2. **Local HTTP Server** - Serves test metadata and target files on localhost:8080
3. **Test Data Generator** - Creates minimal TUF metadata for testing
4. **CLI Process Runner** - Executes the TufConformanceCli and captures output

### Test Flow

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Test Setup    │    │  HTTP Server    │    │ TufConformance  │
│                 │    │                 │    │      CLI        │
│ • Create temp   │    │ • Serves meta-  │    │                 │  
│   directories   │───▶│   data files    │◀───│ • init          │
│ • Generate test │    │ • Serves target │    │ • refresh       │
│   metadata      │    │   files         │    │ • download      │
│ • Start server  │    │ • Port 8080     │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Test Structure

Each test follows the TUF conformance CLI protocol:

1. **init** - Initialize client with trusted root metadata
2. **refresh** - Update metadata from repository  
3. **download** - Download and verify target files

## Test Cases

### Current Tests

- **Test_Init_Command** - Verifies CLI initialization with trusted root
- **Test_Refresh_Command** - Tests metadata refresh (may fail on signature validation)
- **Test_Download_Command** - Tests target download (may fail on signature validation) 
- **Test_Missing_Args_Error_Handling** - Verifies proper error handling

### Expected Failures

During development, signature validation tests are expected to fail. The tests are designed to:

1. **Capture detailed error information** for debugging
2. **Show exact CLI output and error messages**
3. **Allow inspection of generated test data**
4. **Provide controlled test environment**

## Debugging Signature Validation Issues

The local tests are specifically designed to help debug signature validation problems:

### 1. Test Data Inspection

Tests create temporary directories with:
- `server-metadata/` - Metadata files served by the HTTP server
- `metadata/` - CLI's local metadata directory
- `targets/` - Downloaded target files

### 2. CLI Output Capture

All CLI stdout and stderr is captured and displayed:

```csharp
Console.WriteLine($"Refresh Exit Code: {exitCode}");
Console.WriteLine($"Refresh Output: {output}");
Console.WriteLine($"Refresh Error: {error}");
```

### 3. HTTP Request Monitoring

The local HTTP server logs all requests, helping identify:
- Which metadata files are being requested
- Request patterns and timing
- HTTP response codes

### 4. Controlled Test Environment

Unlike the official conformance tests, local tests provide:
- **Predictable test data** - Same metadata every run
- **Local server control** - Modify served content easily
- **Debugging breakpoints** - Step through test execution
- **File system inspection** - Examine intermediate files

## Common Issues and Solutions

### Issue: "Could not find TufConformanceCli executable"

**Solution**: Build the conformance CLI first:
```bash
dotnet build examples/TufConformanceCli/TufConformanceCli.csproj --configuration Release
```

### Issue: "Address already in use" (Port 8080)

**Solution**: 
1. Kill any processes using port 8080
2. Tests should auto-cleanup, but may need manual cleanup if interrupted

### Issue: Signature validation failures

**Expected during development**. The tests will show:
1. Exact error messages from the CLI
2. Which metadata files failed validation  
3. Generated test metadata for inspection

## Extending the Tests

### Adding New Test Cases

1. Add test method to `ConformanceTests.cs`
2. Follow the pattern: setup → CLI execution → verification
3. Use `RunCli()` helper to execute commands
4. Capture and log output for debugging

### Modifying Test Data

Test metadata is generated in these methods:
- `CreateTestRootMetadata()` - Root metadata with keys and roles
- `CreateTestTimestampMetadata()` - Timestamp metadata 
- `CreateTestSnapshotMetadata()` - Snapshot metadata
- `CreateTestTargetsMetadata()` - Targets metadata with file info

**Note**: Currently uses placeholder signatures. For production testing, proper signatures need to be generated.

## Integration with Official Conformance Suite

The local tests are designed to complement, not replace, the official conformance testing. Use local tests for:

- **Development and debugging** - Fast iteration and detailed error info
- **Regression testing** - Verify fixes don't break existing functionality
- **CI integration** - Quick validation before official conformance runs

Use official conformance tests for:

- **Final validation** - Ensure compatibility with reference implementation
- **Cross-platform testing** - Test on multiple environments
- **Release qualification** - Official conformance certification

## Future Improvements

1. **Proper signature generation** - Replace placeholder signatures with real ones
2. **More test scenarios** - Add edge cases and error conditions
3. **Performance testing** - Measure CLI response times
4. **Cross-platform validation** - Test on Windows, Linux, macOS
5. **Integration with official conformance data** - Use same test vectors

## Related Documentation

- [TUF Conformance Test Suite](https://github.com/theupdateframework/tuf-conformance)
- [TUF Specification](https://theupdateframework.github.io/specification/)
- [CLIENT-CLI.md](https://github.com/theupdateframework/tuf-conformance/blob/main/CLIENT-CLI.md)
- [TufConformanceCli README](../examples/TufConformanceCli/README.md)