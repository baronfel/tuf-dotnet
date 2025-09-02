# TUF .NET Implementation - Development Guide

## Overview

This is a .NET implementation of The Update Framework (TUF) specification, providing secure software update capabilities and utilities for canonical JSON handling. The project includes core libraries, comprehensive tests, conformance testing, and example applications.

## Project Structure

```
TUF.slnx                     # Main solution file (SLNX format)
├── CanonicalJson/           # Canonical JSON serialization library
├── CanonicalJson.Tests/     # Tests for canonical JSON
├── TUF/                     # Core TUF implementation
├── TUF.Tests/               # Unit tests for TUF library  
├── TUF.ConformanceTests/    # TUF specification conformance tests
├── examples/                # Example applications
│   ├── BasicClient/         # Simple TUF client demo
│   ├── CliTool/             # Command-line TUF operations
│   ├── SigningDemo/         # Digital signing examples
│   └── TufConformanceCli/   # CLI for conformance testing
└── scenarios/               # Test scenarios and data
```

## Key Components

### CanonicalJson Library
- **Purpose**: Produces deterministic, canonical JSON serialization
- **Key Files**:
  - `CanonicalJson/CanonicalJson.cs` - Main serializer interface
  - `CanonicalJson/CanonicalJsonSerializer.cs` - Core serialization logic
  - `CanonicalJson/CanonicalJsonDeserializer.cs` - Deserialization logic
- **Features**: Lexicographic property ordering, minimal escaping, consistent formatting
- **Uses**: Serde.NET for low-level JSON control

### TUF Core Library  
- **Purpose**: TUF metadata models, signing, and verification
- **Key Files**:
  - `TUF/Metadata.cs` - Generic metadata container with signatures
  - `TUF/Root.cs` - Root metadata (trusted keys and roles)
  - `TUF/Timestamp.cs`, `TUF/Snapshot.cs`, `TUF/Targets.cs` - Metadata types
  - `TUF/Signing.cs` - Cryptographic signing operations
  - `TUF/TrustedMetadata.cs` - Verified metadata management
  - `TUF/Updater.cs` - Client update workflow
- **Dependencies**: NSec.Cryptography for signatures, Microsoft.Extensions.FileSystemGlobbing
- **Architecture**: Generic `Metadata<T>` wrapper provides type safety and signature verification

### Test Projects
- **TUF.Tests**: Unit tests using TUnit framework
- **CanonicalJson.Tests**: Canonical JSON serialization tests  
- **TUF.ConformanceTests**: TUF specification compliance tests
- **Key Test Files**:
  - `TUF.ConformanceTests/SignatureVerificationGoldenTests.cs` - Golden signature tests
  - `.tuf-conformance-xfails` - Expected test failures during development

## Build & Development Commands

### Build
```bash
dotnet restore ./TUF.slnx
dotnet build ./TUF.slnx --configuration Release
```

### Testing
```bash
# Run all tests
dotnet test --configuration Release

# Run specific project tests
dotnet test CanonicalJson.Tests/CanonicalJson.Tests.csproj
dotnet test TUF.Tests/TUF.Tests.csproj
dotnet test TUF.ConformanceTests/TUF.ConformanceTests.csproj
```

### TUF Conformance Testing
```bash
# Build conformance CLI
dotnet publish examples/TufConformanceCli/TufConformanceCli.csproj

# The conformance CLI is used by GitHub Actions with the official TUF conformance test suite
```

### Examples
```bash
# Basic client example
cd examples/BasicClient
dotnet run https://example.com/metadata file.txt

# CLI tool
cd examples/CliTool  
dotnet run -- --help

# Signing demo
cd examples/SigningDemo
dotnet run
```

## Technology Stack & Library Choices

**IMPORTANT**: All new development MUST use these established libraries and frameworks to maintain consistency:

### Core Dependencies
- **.NET 8.0+** (library target) - Modern .NET with nullable reference types enabled
- **.NET 10.0** (required for SLNX solution format)
- **Multi-targeting**: When newer .NET APIs are beneficial, add multi-targeting (e.g., `net8.0;net9.0`) and use `#if` conditionals
- **AOT Compatibility**: **NON-NEGOTIABLE** - All projects have `<IsAotCompatible>true</IsAotCompatible>` enabled
- **Serde.NET (v0.8.0)** - **MANDATORY** for ALL serialization/deserialization
  - Provides explicit, low-level control over JSON output
  - Required for canonical JSON compliance
  - DO NOT use System.Text.Json or Newtonsoft.Json
- **NSec.Cryptography (v25.4.0)** - **MANDATORY** for cryptographic operations NOT in .NET BCL
  - Modern, secure cryptography library
  - Handles Ed25519, ECDSA, RSA signatures
  - **CRITICAL**: If an algorithm exists in System.Security.Cryptography (.NET BCL), use BCL FIRST
  - Only use NSec.Cryptography for algorithms missing from BCL (e.g., Ed25519)
  - This ensures maximum compatibility and reduces external dependencies

### Testing Framework
- **TUnit (v0.57.24)** - **MANDATORY** for ALL new tests
  - Modern alternative to xUnit/NUnit with better performance
  - Includes built-in analyzers and code fixers
  - DO NOT add xUnit, NUnit, or MSTest

### Additional Libraries (USE THESE)
- **Microsoft.Extensions.FileSystemGlobbing (v10.0.0)** - File pattern matching
- **System.CommandLine (v2.0.0-beta7)** - CLI applications (see examples/)

## Development Guidelines

### Dependency Management Strategy

**CORE LIBRARIES** (`CanonicalJson/` and `TUF/` projects):
- Keep PackageReference dependencies to **ABSOLUTE MINIMUM**
- Every new dependency requires strong justification
- Prefer .NET BCL APIs over external libraries
- **Performance is CRITICAL** - these operations typically happen at app startup:
  - Minimize allocations (use `Span<T>`, `Memory<T>`, object pooling)
  - Optimize hot paths for speed and low GC pressure
  - Prefer stack allocation over heap allocation where possible
  - Consider async/await overhead in performance-critical sections
- Current approved core dependencies only:
  - Serde.NET (serialization)
  - NSec.Cryptography (missing crypto algorithms)
  - Microsoft.Extensions.FileSystemGlobbing (file patterns)

**NON-CORE PROJECTS** (examples, tests, tools):
- May use additional libraries as needed
- Still follow established patterns where possible
- Examples: TUnit for testing, System.CommandLine for CLI tools

### Mandatory Library Usage
**Before adding ANY new dependencies**, check if functionality exists in these established libraries:

1. **JSON Operations**: Use only Serde.NET - DO NOT add System.Text.Json or Newtonsoft.Json
2. **Cryptography**: Prefer .NET BCL (System.Security.Cryptography) FIRST, then NSec.Cryptography for missing algorithms - DO NOT add BouncyCastle
3. **Testing**: Use only TUnit - DO NOT add xUnit, NUnit, MSTest, or FluentAssertions
4. **CLI**: Use only System.CommandLine - DO NOT add CommandLineParser or other CLI libraries
5. **File Operations**: Use Microsoft.Extensions.FileSystemGlobbing for patterns
6. **Newer .NET APIs**: If a useful API exists in .NET 9/10/11+ but not .NET 8, consider multi-targeting with conditional compilation:
   ```xml
   <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
   ```
   ```csharp
   #if NET9_0_OR_GREATER
       // Use newer API
   #else
       // Fallback implementation
   #endif
   ```

### Code Style
- **EditorConfig**: `.editorconfig` defines formatting rules
- **Indentation**: 4 spaces for C#, 2 spaces for XML
- **Usings**: System directives first, import groups separated
- **Nullable**: Enabled project-wide
- **AOT Compatible**: **NON-NEGOTIABLE** - All code MUST support ahead-of-time compilation
  - Test with `<PublishAot>true</PublishAot>` during development
  - Avoid reflection, dynamic code generation, or AOT-incompatible patterns
  - Use source generators instead of runtime reflection when possible
- **Performance-First**: Core libraries must be optimized for startup performance
  - Use `Span<T>` and `Memory<T>` to minimize allocations
  - Prefer stackalloc for small, short-lived data
  - Avoid LINQ in hot paths (use for loops instead)
  - Consider `ValueTask<T>` over `Task<T>` for frequently completed operations

### Analyzers & Linting
- **Built-in analyzers**: Roslyn analyzers via project dependencies
- **SerdeGenerator**: Code generation for serialization
- **TUnit analyzers**: Test framework analyzers and code fixers
- **No explicit linting commands**: Uses built-in .NET analyzers during build

### Git Workflow
- **Main branch**: `main`
- **Current branch**: `actually-do-signing-tests`
- **CI/CD**: GitHub Actions runs on Ubuntu, Windows, macOS
- **Conformance**: Automated TUF conformance testing via external test suite

## Common Development Tasks

### Adding New Projects
1. **MANDATORY**: Add all new projects to `TUF.slnx` solution file
2. Follow existing project structure and naming conventions
3. Ensure `<IsAotCompatible>true</IsAotCompatible>` is set in `.csproj`
4. Use centralized package management via `Directory.Packages.props`

### Adding New Metadata Types
1. Create model in `TUF/` with **Serde.NET attributes** (NOT System.Text.Json)
2. Add to `Metadata<T>` usage patterns
3. Implement signing/verification logic using **NSec.Cryptography**
4. Add comprehensive **TUnit tests** (NOT xUnit/NUnit)
5. Use **CanonicalJson.Serializer** for JSON operations
6. Update examples if needed

### Debugging Serialization
- Use CanonicalJson tests to verify output format
- Check `.tuf-conformance-xfails` for known serialization issues
- Leverage Serde.NET's explicit control for troubleshooting

### Conformance Testing
- Expected failures are tracked in `.tuf-conformance-xfails`
- Remove entries as features are implemented
- GitHub Actions automatically runs conformance suite
- CLI tool provides local testing capability

### Architecture Notes
- **Type Safety**: Generic `Metadata<T>` ensures compile-time correctness
- **Signature Verification**: Standardized across all metadata types
- **Canonical JSON**: Critical for signature consistency
- **Extensibility**: Easy to add new metadata types while reusing common logic

## Useful File Locations

### Configuration
- `Directory.Packages.props` - Centralized package versions
- `nuget.config` - NuGet package sources
- `.editorconfig` - Code formatting rules
- `TUF.slnx` - Solution configuration

### Key Implementation Files
- `TUF/Metadata.cs:30` - Generic metadata wrapper
- `CanonicalJson/CanonicalJson.cs:19` - Serializer entry point  
- `TUF/Signing.cs` - Cryptographic operations
- `TUF/TrustedMetadata.cs` - Metadata verification logic

### Test Data
- `scenarios/` - Test scenarios and sample data
- `.tuf-conformance-xfails` - Expected test failures
- Golden test files in `TUF.ConformanceTests/`

## Security Considerations

This codebase implements **critical security infrastructure** for software updates:

- **All cryptographic operations must be reviewed** for timing attacks, side channels
- **Never log sensitive data** (private keys, signatures, metadata content)
- **Validate all inputs** especially from untrusted sources (network, files)
- **Follow TUF specification exactly** - deviations can compromise security
- **Test attack scenarios** - malformed metadata, replay attacks, downgrade attempts
- **Use constant-time comparisons** for signature verification and key material

## Common Pitfalls to Avoid

### Serialization
- **DO NOT** use default JSON serializers - they don't guarantee canonical output
- **DO NOT** modify Serde attributes without understanding canonical JSON impact
- **VERIFY** serialization round-trips produce identical byte sequences

### Cryptography  
- **DO NOT** implement custom crypto - use established libraries only
- **DO NOT** use non-constant time operations for sensitive comparisons
- **VALIDATE** all key material and signatures before use
- **CHECK** algorithm parameters match TUF specification requirements

### Performance
- **PROFILE** before optimizing - measure actual bottlenecks
- **TEST** AOT compilation regularly - catches reflection issues early  
- **BENCHMARK** critical paths - startup performance matters for end users

## Debugging Tips

- **TUF Conformance failures**: Check `.tuf-conformance-xfails` for known issues
- **Serialization issues**: Compare output with reference implementations
- **Signature verification**: Verify canonical JSON bytes match expected
- **AOT compilation errors**: Look for reflection usage, dynamic code generation
- **Performance issues**: Use dotnet-trace, PerfView, or BenchmarkDotNet

## External Resources

- [TUF Specification](https://theupdateframework.github.io/specification/latest/) - Official specification
- [Serde.NET Documentation](https://serdedotnet.github.io/) - Serialization library docs
- [NSec Documentation](https://nsec.rocks/) - Cryptography library docs
- [.NET Performance Guidelines](https://docs.microsoft.com/en-us/dotnet/core/deploying/ready-to-run) - AOT and performance best practices