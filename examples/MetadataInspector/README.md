# TUF Metadata Inspector

A comprehensive command-line tool for inspecting, validating, and analyzing TUF (The Update Framework) metadata files. This tool provides advanced debugging capabilities and detailed analysis of TUF metadata to help developers understand, troubleshoot, and verify TUF repositories.

## Overview

The TUF Metadata Inspector addresses **Phase 2 (Advanced CLI Tooling)** of the TUF .NET parity roadmap by providing sophisticated metadata analysis capabilities comparable to tools available in other TUF ecosystems like Python and Go.

## Features

### 🔍 **Comprehensive Inspection**
- **Metadata Type Detection**: Automatically identifies root, timestamp, snapshot, and targets metadata
- **JSON Structure Analysis**: Detailed breakdown of file structure, properties, and sizes
- **Canonical JSON Validation**: Verifies adherence to TUF's canonical JSON requirements
- **Signature Analysis**: Examines signature count, key IDs, and signature structure

### 🔐 **Validation and Verification**
- **Structure Validation**: Ensures metadata follows TUF specification requirements
- **Signature Validation**: Cryptographic signature verification with trusted root
- **Expiration Checking**: Identifies expired metadata files
- **Required Property Validation**: Verifies presence of mandatory TUF fields

### 📊 **Comparison and Analysis**
- **File Comparison**: Side-by-side comparison of two metadata files
- **Repository Analysis**: Comprehensive analysis of entire TUF metadata directories
- **Version Tracking**: Compare versions, expiration dates, and signature changes
- **Health Monitoring**: Repository health checks and missing file detection

### 📤 **Export and Reporting**
- **Multiple Output Formats**: Export metadata information in JSON, canonical JSON, or summary formats
- **Detailed Reports**: Generate comprehensive analysis reports
- **Debugging Output**: Verbose mode for detailed troubleshooting information

## Installation

```bash
# From the repository root
cd examples/MetadataInspector
dotnet build -c Release
```

## Usage

### Basic Commands

#### Inspect Metadata File
```bash
# Basic inspection
dotnet run -- inspect --file root.json

# Verbose inspection with detailed analysis
dotnet run -- inspect --file root.json --verbose

# JSON format output
dotnet run -- inspect --file root.json --format json
```

#### Validate Metadata
```bash
# Basic structure validation
dotnet run -- validate --file targets.json

# Full validation with signature verification
dotnet run -- validate --file targets.json --trusted-root root.json --validate-signatures --verbose
```

#### Compare Metadata Files
```bash
# Compare two versions of the same metadata
dotnet run -- compare --file1 root-v1.json --file2 root-v2.json --verbose
```

#### Analyze Repository
```bash
# Analyze entire metadata directory
dotnet run -- analyze --metadata-dir /path/to/tuf/metadata --verbose

# Generate JSON report
dotnet run -- analyze --metadata-dir /path/to/tuf/metadata --format json
```

#### Export Metadata
```bash
# Export as pretty-printed JSON
dotnet run -- export --file snapshot.json --output snapshot-pretty.json --format json

# Export as canonical JSON
dotnet run -- export --file snapshot.json --output snapshot-canonical.json --format canonical

# Export analysis summary
dotnet run -- export --file snapshot.json --output snapshot-summary.json --format summary
```

## Command Reference

### Global Options
- `--verbose, -v`: Enable detailed verbose output
- `--format`: Output format (`text`, `json`, `summary`)

### Commands

#### `inspect`
Inspect and analyze a single TUF metadata file.

**Options:**
- `--file, -f <FILE>`: Path to metadata file (required)
- `--verbose, -v`: Detailed analysis output
- `--format <FORMAT>`: Output format

**Example Output:**
```
🔍 Inspecting TUF metadata file: root.json
📁 Path: /path/to/root.json
📊 Size: 2,456 bytes

📋 Metadata Type: root

📊 JSON Structure Analysis:
   Top-level properties: signed, signatures
   Number of signatures: 1
   Signed content size: ~2,123 bytes
   Expires: 2025-12-31T23:59:59Z
   Version: 1

🔬 ROOT Metadata Analysis:
   Spec Version: 1.0.0
   Version: 1
   Expires: 2025-12-31T23:59:59Z
   Number of keys: 4
   Number of roles: 4

📐 Canonical JSON Validation:
   Is canonical JSON: ✅ Yes
```

#### `validate`
Validate metadata file structure and optionally verify signatures.

**Options:**
- `--file, -f <FILE>`: Path to metadata file (required)
- `--trusted-root <FILE>`: Trusted root metadata for signature validation
- `--validate-signatures`: Enable signature validation
- `--verbose, -v`: Detailed validation output

#### `compare`
Compare two metadata files side-by-side.

**Options:**
- `--file1 <FILE>`: First file to compare (required)
- `--file2 <FILE>`: Second file to compare (required)
- `--verbose, -v`: Detailed comparison output

#### `analyze`
Perform comprehensive analysis of a TUF metadata directory.

**Options:**
- `--metadata-dir, -d <DIR>`: Directory containing metadata files (required)
- `--verbose, -v`: Detailed analysis
- `--format <FORMAT>`: Output format

#### `export`
Export metadata information in various formats.

**Options:**
- `--file, -f <FILE>`: Path to metadata file (required)
- `--output, -o <FILE>`: Output file path (required)
- `--format <FORMAT>`: Export format (`json`, `canonical`, `summary`)

## Use Cases

### 🐛 **Debugging TUF Issues**
When TUF operations fail, use the inspector to identify problems:

```bash
# Check if metadata is properly formatted
dotnet run -- inspect --file problematic-metadata.json --verbose

# Validate structure
dotnet run -- validate --file problematic-metadata.json --verbose

# Compare with working version
dotnet run -- compare --file1 working-metadata.json --file2 problematic-metadata.json
```

### 🏥 **Repository Health Monitoring**
Regular repository health checks:

```bash
# Check entire repository
dotnet run -- analyze --metadata-dir /var/tuf/metadata --verbose

# Look for expired metadata
dotnet run -- analyze --metadata-dir /var/tuf/metadata | grep "EXPIRED"
```

### 🔍 **Development and Testing**
During TUF implementation development:

```bash
# Verify canonical JSON format
dotnet run -- inspect --file test-metadata.json | grep "canonical JSON"

# Check signature structure
dotnet run -- inspect --file signed-metadata.json --verbose | grep "Signature"

# Compare before/after changes
dotnet run -- compare --file1 before-changes.json --file2 after-changes.json
```

### 📊 **Automated Analysis**
Integration with CI/CD pipelines:

```bash
# Generate JSON report for automated processing
dotnet run -- analyze --metadata-dir metadata/ --format json > report.json

# Export canonical form for verification
dotnet run -- export --file root.json --output root-canonical.json --format canonical
```

## Advanced Features

### Verbose Mode Analysis
With `--verbose`, the tool provides extensive details:
- Complete key information (key types, schemes, key IDs)
- Full role definitions and thresholds
- Detailed target file information
- Hash algorithm analysis
- Delegation hierarchy breakdown
- Signature key mapping

### Repository Health Checks
The analyzer performs comprehensive health checks:
- **Required Files**: Ensures all mandatory metadata files are present
- **Expiration Status**: Identifies expired or soon-to-expire metadata
- **Version Consistency**: Checks for version mismatches
- **File Size Analysis**: Detects unusually large or small files
- **JSON Format Validation**: Verifies proper JSON structure

### Comparison Capabilities
File comparison includes:
- **Version Differences**: Identifies version number changes
- **Key ID Changes**: Detects signing key modifications
- **Content Differences**: Highlights structural changes
- **Signature Analysis**: Compares signature counts and key usage
- **Expiration Updates**: Shows expiration date changes

## Integration with TUF .NET

The metadata inspector is built on the TUF .NET library and demonstrates:
- **Canonical JSON**: Uses the same `CanonicalJsonSerializer` as the core library
- **Metadata Models**: Leverages `Metadata<T>` generic types
- **Type Safety**: Proper deserialization of all TUF metadata types
- **Error Handling**: Consistent error handling patterns

## Contributing

This tool addresses **Phase 2** of the TUF .NET parity roadmap. Future enhancements could include:
- **Interactive Mode**: REPL-style metadata exploration
- **Diff Visualization**: Side-by-side diff display for comparisons
- **Performance Metrics**: Analysis of metadata file performance characteristics
- **Integration Testing**: Automated validation of test repositories
- **Custom Validators**: Plugin system for custom validation rules

## Security Considerations

- **Signature Validation**: Currently provides basic signature analysis; full cryptographic verification requires trusted root metadata
- **Input Validation**: All file inputs are validated before processing
- **Error Handling**: Graceful handling of malformed metadata prevents crashes
- **Information Disclosure**: Verbose mode may display sensitive metadata; use carefully in production

## Troubleshooting

### Common Issues

**"Error inspecting metadata: JSON parsing failed"**
- Ensure the file is valid JSON
- Check for UTF-8 encoding issues
- Verify file is not corrupted

**"Failed to parse as [type] metadata"**
- Metadata structure doesn't match expected TUF format
- May be a different metadata type than detected
- Could indicate format version incompatibility

**"Canonical JSON validation failed"**
- File is not in canonical JSON format
- May have extra whitespace or property ordering issues
- Use export with `--format canonical` to generate correct format

### Getting Help

1. **Use verbose mode** for detailed error information: `--verbose`
2. **Check file structure** with basic inspect before advanced operations
3. **Validate input files** are proper TUF metadata format
4. **Compare with known-good** metadata files for debugging

## Related Tools

This metadata inspector complements other TUF .NET tools:
- **[CliTool](../CliTool/)**: Basic TUF client operations
- **[RepositoryManager](../RepositoryManager/)**: Repository creation and management
- **[SigningDemo](../SigningDemo/)**: Cryptographic operations demonstration
- **[TufConformanceCli](../TufConformanceCli/)**: Official TUF conformance testing

## License

This tool is part of the TUF .NET library and is subject to the same licensing terms.