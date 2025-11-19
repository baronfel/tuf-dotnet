# TUF NuGet Package Integration Example

This example demonstrates how to integrate TUF (The Update Framework) security with NuGet package management, providing cryptographic verification of packages before installation.

## What This Example Shows

- **Secure Package Verification**: Verify NuGet packages against TUF metadata before installation
- **Integrity Checking**: Use TUF-secured hashes to validate package authenticity  
- **Supply Chain Security**: Protect against compromised or malicious packages
- **Production Integration Patterns**: Real-world integration approaches for package managers

## Security Model

This example implements a security model where:

1. **NuGet packages are treated as TUF targets** - Each package version is represented as a target in the TUF repository
2. **TUF provides cryptographic verification** - Package metadata (name, version, hash) is secured by TUF signatures
3. **Hash validation occurs before installation** - Package contents are verified against TUF-secured hashes
4. **Only verified packages are installed** - Packages that fail TUF verification are rejected

## Prerequisites

- .NET 8.0 or later
- Access to a TUF repository containing package metadata
- NuGet packages stored as TUF targets (or test setup with sample data)

## Running the Example

```bash
# Build the example
dotnet build

# Run with TUF repository and package information
dotnet run https://secure-packages.example.com Newtonsoft.Json 13.0.1

# The example will:
# 1. Initialize TUF client with the repository
# 2. Look up the specified package in TUF metadata
# 3. Verify the package's cryptographic signatures
# 4. Download and validate the package
# 5. Confirm it's ready for secure installation
```

## Integration Workflow

### 1. TUF Repository Setup
```
secure-packages.example.com/
├── metadata/           # TUF metadata files
│   ├── root.json
│   ├── targets.json    # Contains package information
│   ├── snapshot.json
│   └── timestamp.json
└── targets/           # Package files as TUF targets
    └── packages/
        └── Newtonsoft.Json/
            └── 13.0.1/
                └── Newtonsoft.Json.13.0.1.nupkg
```

### 2. Package Verification Flow
1. **Query TUF**: Look up `packages/{PackageName}/{Version}/{PackageName}.{Version}.nupkg` in TUF targets
2. **Validate Metadata**: Verify TUF signatures on package metadata
3. **Download Package**: Use TUF client to download with integrity verification
4. **Final Validation**: Additional package format validation using NuGet APIs
5. **Installation**: Package is ready for secure installation

### 3. Error Handling
- **Package Not Found**: Package doesn't exist in TUF repository
- **Hash Mismatch**: Downloaded package doesn't match TUF-secured hash
- **Invalid Format**: Package file is corrupt or not a valid NuGet package
- **TUF Verification Failure**: Metadata signatures are invalid

## Production Considerations

### Trusted Root Distribution
```csharp
// ❌ Don't use placeholder roots in production
var trustedRoot = GetPlaceholderTrustedRoot();

// ✅ Distribute real trusted root with your application
var trustedRoot = await File.ReadAllBytesAsync("embedded-trusted-root.json");
```

### Repository Configuration
```csharp
// Configure multiple TUF repositories for redundancy
var primaryRepo = "https://secure-packages.primary.com";
var mirrorRepo = "https://secure-packages.mirror.com";

// Use multi-repository client for consensus verification
var multiRepoClient = new MultiRepositoryClient(config);
```

### Package Repository Structure
Organize packages in TUF repository following consistent patterns:
```
packages/
├── {PackageName}/
│   └── {Version}/
│       ├── {PackageName}.{Version}.nupkg      # Main package
│       ├── {PackageName}.{Version}.nupkg.sha256  # Optional hash file
│       └── {PackageName}.{Version}.nuspec     # Optional metadata
```

## Security Benefits

1. **Supply Chain Attack Prevention**: TUF signatures prevent malicious packages from being accepted
2. **Rollback Attack Protection**: Version numbers in TUF prevent downgrade attacks  
3. **Compromise Resilience**: Multiple keys required to compromise package verification
4. **Freshness Guarantees**: Timestamp metadata prevents indefinite freeze attacks
5. **Mix-and-Match Prevention**: Snapshot metadata ensures consistent repository state

## Integration with Existing Systems

### Package Manager Integration
```csharp
public class SecureNuGetPackageManager 
{
    private readonly TufSecurePackageManager _tufManager;
    private readonly IPackageManager _nugetManager;

    public async Task<bool> InstallPackageAsync(string packageId, string version)
    {
        // 1. Verify through TUF first
        var verification = await _tufManager.VerifyPackageAsync(packageId, version);
        if (!verification.IsValid) return false;

        // 2. Install using standard NuGet APIs
        return await _nugetManager.InstallAsync(verification.PackagePath);
    }
}
```

### CI/CD Pipeline Integration
```yaml
# .github/workflows/secure-package-install.yml
steps:
  - name: Verify and Install Packages
    run: |
      # Use TUF-secured package verification
      dotnet run --project tools/TufPackageVerifier -- verify packages.json
      
      # Only install if TUF verification passes
      dotnet restore --source ./verified-packages/
```

## Advanced Scenarios

### Offline Package Verification
```csharp
// Pre-download TUF metadata for offline scenarios
await tufManager.RefreshAndCacheAsync();

// Later, verify packages without network access
var result = tufManager.VerifyPackageFromCache(packageName, version);
```

### Custom Package Sources
```csharp
// Integrate with private package repositories
var config = new TufSecurePackageManagerConfig
{
    TufRepositoryUrl = "https://internal-packages.company.com",
    NuGetSources = new[] { "https://private-nuget.company.com" },
    RequireTufVerification = true
};
```

## Related Examples

- **[BasicClient](../BasicClient/)**: Fundamental TUF client operations
- **[MultiRepositoryClient](../MultiRepositoryClient/)**: Multi-repository consensus verification
- **[CliTool](../CliTool/)**: Command-line TUF operations

## Resources

- [TUF Specification](https://theupdateframework.io/specification/)
- [NuGet Client Libraries](https://docs.microsoft.com/nuget/reference/nuget-client-libraries)
- [Supply Chain Security Best Practices](https://slsa.dev/)
- [Package Manager Security Patterns](https://www.cisa.gov/sites/default/files/publications/ESF_SECURING_THE_SOFTWARE_SUPPLY_CHAIN_DEVELOPERS.PDF)

---

> This example demonstrates real-world integration patterns for securing package management workflows with TUF. For production use, ensure proper trusted root distribution and repository management practices.