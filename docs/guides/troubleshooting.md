# TUF .NET Troubleshooting Guide

This guide provides practical solutions to common problems encountered when using TUF .NET, with step-by-step troubleshooting procedures and diagnostic commands.

## Table of Contents

- [Quick Diagnostic Checklist](#quick-diagnostic-checklist)
- [Installation and Setup Issues](#installation-and-setup-issues)
- [Configuration Problems](#configuration-problems)
- [Network and Connectivity Issues](#network-and-connectivity-issues)
- [Metadata and Repository Issues](#metadata-and-repository-issues)
- [Cryptographic and Security Issues](#cryptographic-and-security-issues)
- [Target File Issues](#target-file-issues)
- [Performance Issues](#performance-issues)
- [Debugging Tools and Commands](#debugging-tools-and-commands)

## Quick Diagnostic Checklist

When experiencing TUF issues, run through this checklist first:

### ✅ Basic System Check

```csharp
// 1. Verify TUF .NET version
var assembly = typeof(TUF.Updater).Assembly;
Console.WriteLine($"TUF .NET Version: {assembly.GetName().Version}");

// 2. Check .NET runtime version
Console.WriteLine($".NET Version: {Environment.Version}");

// 3. Verify configuration is loaded correctly
Console.WriteLine($"Metadata URL: {config.RemoteMetadataUrl}");
Console.WriteLine($"Local cache: {config.LocalMetadataDir}");

// 4. Test network connectivity
using var client = new HttpClient();
var response = await client.GetAsync(config.RemoteMetadataUrl);
Console.WriteLine($"Repository accessible: {response.IsSuccessStatusCode}");
```

### ✅ TUF Status Check

```csharp
public static async Task<bool> RunTufDiagnostic(UpdaterConfig config)
{
    var issues = new List<string>();
    
    // Check trusted root
    if (config.LocalTrustedRoot == null || config.LocalTrustedRoot.Length == 0)
        issues.Add("❌ No trusted root metadata configured");
    else
        Console.WriteLine("✅ Trusted root metadata configured");
    
    // Check URLs
    if (config.RemoteMetadataUrl?.Scheme != "https")
        issues.Add("⚠️  Metadata URL should use HTTPS");
    else
        Console.WriteLine("✅ Using HTTPS for metadata");
    
    // Check directories
    if (!Directory.Exists(config.LocalMetadataDir))
    {
        Directory.CreateDirectory(config.LocalMetadataDir);
        Console.WriteLine("✅ Created local metadata directory");
    }
    
    // Test repository connectivity
    try
    {
        using var client = new HttpClient();
        await client.GetAsync(config.RemoteMetadataUrl);
        Console.WriteLine("✅ Repository is accessible");
    }
    catch (Exception ex)
    {
        issues.Add($"❌ Repository connectivity: {ex.Message}");
    }
    
    if (issues.Any())
    {
        Console.WriteLine("\nIssues found:");
        foreach (var issue in issues)
            Console.WriteLine($"  {issue}");
        return false;
    }
    
    Console.WriteLine("\n✅ All diagnostic checks passed");
    return true;
}
```

## Installation and Setup Issues

### Problem: "Package 'TUF' not found"

**Symptoms**: NuGet package restoration fails

**Solution**:
```bash
# Verify NuGet package source
dotnet nuget list source

# Add NuGet.org if missing
dotnet nuget add source https://api.nuget.org/v3/index.json -n "nuget.org"

# Clear NuGet caches
dotnet nuget locals all --clear

# Restore packages
dotnet restore
```

### Problem: "Type 'Updater' could not be found"

**Symptoms**: Compilation errors when using TUF types

**Solution**:
```csharp
// Ensure correct using statements
using TUF;
using TUF.Exceptions;

// Check project file includes TUF package
<PackageReference Include="TUF" Version="1.0.0" />
```

### Problem: AOT Compilation Issues

**Symptoms**: Native AOT publishing fails

**Solution**:
```xml
<!-- In your .csproj file -->
<PropertyGroup>
    <IsAotCompatible>true</IsAotCompatible>
    <PublishAot>true</PublishAot>
</PropertyGroup>

<!-- Ensure TUF .NET is AOT compatible -->
<PackageReference Include="TUF" Version="1.0.0" />
```

## Configuration Problems

### Problem: "LocalTrustedRoot is required"

**Symptoms**: `TufConfigurationException` during `Updater` creation

**Diagnosis**:
```csharp
// Check if trusted root file exists
var rootPath = "trusted-root.json";
if (!File.Exists(rootPath))
{
    Console.WriteLine($"❌ Trusted root file not found: {rootPath}");
    // Get trusted root from repository administrator
}

// Validate trusted root content
try
{
    var rootContent = await File.ReadAllTextAsync(rootPath);
    var rootMetadata = JsonSerializer.Deserialize<object>(rootContent);
    Console.WriteLine("✅ Trusted root is valid JSON");
}
catch (JsonException ex)
{
    Console.WriteLine($"❌ Trusted root is invalid JSON: {ex.Message}");
}
```

**Solution**:
```csharp
// Load trusted root from file
var trustedRootBytes = await File.ReadAllBytesAsync("trusted-root.json");
var config = new UpdaterConfig
{
    LocalTrustedRoot = trustedRootBytes,
    // ... other configuration
};

// Alternative: load from embedded resource
var assembly = Assembly.GetExecutingAssembly();
using var stream = assembly.GetManifestResourceStream("MyApp.trusted-root.json");
using var memoryStream = new MemoryStream();
await stream.CopyToAsync(memoryStream);
config.LocalTrustedRoot = memoryStream.ToArray();
```

### Problem: "Invalid repository URL"

**Symptoms**: URL format errors or connection issues

**Diagnosis**:
```csharp
public static bool ValidateRepositoryUrls(UpdaterConfig config)
{
    var issues = new List<string>();
    
    // Check metadata URL
    if (config.RemoteMetadataUrl == null)
        issues.Add("RemoteMetadataUrl is null");
    else
    {
        if (!config.RemoteMetadataUrl.IsAbsoluteUri)
            issues.Add("RemoteMetadataUrl must be absolute");
        
        if (config.RemoteMetadataUrl.Scheme != "https")
            issues.Add("RemoteMetadataUrl should use HTTPS");
        
        if (!config.RemoteMetadataUrl.ToString().EndsWith("/"))
            issues.Add("RemoteMetadataUrl should end with /");
    }
    
    // Check targets URL
    if (config.RemoteTargetsUrl != null)
    {
        if (!config.RemoteTargetsUrl.IsAbsoluteUri)
            issues.Add("RemoteTargetsUrl must be absolute");
    }
    
    foreach (var issue in issues)
        Console.WriteLine($"❌ {issue}");
    
    return !issues.Any();
}
```

**Solution**:
```csharp
var config = new UpdaterConfig
{
    // Ensure URLs are properly formatted
    RemoteMetadataUrl = new Uri("https://example.com/tuf/metadata/"),  // Note trailing slash
    RemoteTargetsUrl = new Uri("https://example.com/tuf/targets/"),    // Note trailing slash
    
    // Use HTTPS for security
    // Avoid HTTP in production environments
};
```

## Network and Connectivity Issues

### Problem: "Repository is unreachable"

**Symptoms**: `RepositoryNetworkException` during metadata refresh

**Diagnosis**:
```csharp
public static async Task DiagnoseNetworkIssue(UpdaterConfig config)
{
    Console.WriteLine("Diagnosing network connectivity...");
    
    // Test basic connectivity
    try
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        
        var response = await client.GetAsync(config.RemoteMetadataUrl);
        Console.WriteLine($"✅ HTTP Status: {response.StatusCode}");
        Console.WriteLine($"✅ Server: {response.Headers.Server}");
    }
    catch (TaskCanceledException)
    {
        Console.WriteLine("❌ Request timed out - check network connectivity");
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"❌ HTTP error: {ex.Message}");
    }
    
    // Test DNS resolution
    try
    {
        var hostAddresses = await Dns.GetHostAddressesAsync(config.RemoteMetadataUrl.Host);
        Console.WriteLine($"✅ DNS resolved to: {string.Join(", ", hostAddresses.Select(a => a.ToString()))}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ DNS resolution failed: {ex.Message}");
    }
    
    // Check proxy settings
    var proxy = WebRequest.GetSystemWebProxy();
    var proxyUri = proxy.GetProxy(config.RemoteMetadataUrl);
    if (proxyUri != config.RemoteMetadataUrl)
    {
        Console.WriteLine($"ℹ️  Using proxy: {proxyUri}");
    }
}
```

**Solutions**:

1. **Corporate Firewall/Proxy**:
```csharp
var httpClientHandler = new HttpClientHandler()
{
    Proxy = new WebProxy("http://proxy.company.com:8080"),
    UseProxy = true
};

var config = new UpdaterConfig
{
    Client = new HttpClient(httpClientHandler),
    // ... other settings
};
```

2. **Certificate Issues**:
```csharp
// For development only - don't use in production
var httpClientHandler = new HttpClientHandler()
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
};
```

3. **Timeout Configuration**:
```csharp
var httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromSeconds(30); // Increase timeout
var config = new UpdaterConfig
{
    Client = httpClient,
    // ... other settings
};
```

### Problem: "SSL/TLS connection errors"

**Symptoms**: Certificate validation failures

**Diagnosis**:
```bash
# Test SSL certificate manually
openssl s_client -connect repository.example.com:443 -servername repository.example.com
```

**Solution**:
```csharp
// For custom CA certificates
var httpClientHandler = new HttpClientHandler();
httpClientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;
httpClientHandler.ClientCertificates.Add(new X509Certificate2("custom-ca.crt"));
```

## Metadata and Repository Issues

### Problem: "Metadata deserialization failed"

**Symptoms**: `MetadataDeserializationException` with corrupted JSON

**Diagnosis**:
```csharp
public static async Task DiagnoseMetadataIssue(string metadataUrl, string metadataType)
{
    try
    {
        using var client = new HttpClient();
        var response = await client.GetAsync($"{metadataUrl}{metadataType}.json");
        var content = await response.Content.ReadAsStringAsync();
        
        Console.WriteLine($"✅ Downloaded {metadataType} metadata ({content.Length} bytes)");
        
        // Validate JSON structure
        var jsonDoc = JsonDocument.Parse(content);
        Console.WriteLine("✅ Valid JSON structure");
        
        // Check for required TUF fields
        var root = jsonDoc.RootElement;
        if (root.TryGetProperty("signed", out var signed))
        {
            Console.WriteLine("✅ Contains 'signed' property");
            
            if (signed.TryGetProperty("_type", out var typeProperty))
            {
                Console.WriteLine($"✅ Metadata type: {typeProperty.GetString()}");
            }
            else
            {
                Console.WriteLine("❌ Missing '_type' field in signed metadata");
            }
        }
        else
        {
            Console.WriteLine("❌ Missing 'signed' property");
        }
        
        if (root.TryGetProperty("signatures", out var signatures))
        {
            Console.WriteLine($"✅ Contains {signatures.GetArrayLength()} signatures");
        }
        else
        {
            Console.WriteLine("❌ Missing 'signatures' property");
        }
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"❌ JSON parsing error: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Download error: {ex.Message}");
    }
}
```

**Solutions**:

1. **Clear metadata cache**:
```csharp
public static void ClearMetadataCache(UpdaterConfig config)
{
    if (Directory.Exists(config.LocalMetadataDir))
    {
        Directory.Delete(config.LocalMetadataDir, recursive: true);
        Directory.CreateDirectory(config.LocalMetadataDir);
        Console.WriteLine("✅ Metadata cache cleared");
    }
}
```

2. **Validate repository integrity**:
```csharp
public static async Task ValidateRepository(string baseUrl)
{
    var metadataTypes = new[] { "root", "timestamp", "snapshot", "targets" };
    
    foreach (var type in metadataTypes)
    {
        try
        {
            await DiagnoseMetadataIssue(baseUrl, type);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ {type} metadata validation failed: {ex.Message}");
        }
    }
}
```

### Problem: "Metadata has expired"

**Symptoms**: `ExpiredMetadataException` preventing updates

**Diagnosis**:
```csharp
public static async Task CheckMetadataExpiry(string metadataUrl)
{
    var metadataTypes = new[] { "root", "timestamp", "snapshot", "targets" };
    
    foreach (var type in metadataTypes)
    {
        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"{metadataUrl}{type}.json");
            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);
            
            if (jsonDoc.RootElement.TryGetProperty("signed", out var signed) &&
                signed.TryGetProperty("expires", out var expiresProperty))
            {
                if (DateTime.TryParse(expiresProperty.GetString(), out var expires))
                {
                    var timeUntilExpiry = expires - DateTime.UtcNow;
                    
                    if (timeUntilExpiry.TotalDays < 0)
                    {
                        Console.WriteLine($"❌ {type} expired {-timeUntilExpiry.TotalDays:F1} days ago");
                    }
                    else if (timeUntilExpiry.TotalDays < 7)
                    {
                        Console.WriteLine($"⚠️  {type} expires in {timeUntilExpiry.TotalDays:F1} days");
                    }
                    else
                    {
                        Console.WriteLine($"✅ {type} expires in {timeUntilExpiry.TotalDays:F1} days");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to check {type} expiry: {ex.Message}");
        }
    }
}
```

**Solution**: Contact repository administrator to refresh metadata with updated expiry dates.

## Cryptographic and Security Issues

### Problem: "Signature verification failed"

**Symptoms**: `SignatureVerificationException` preventing metadata validation

**Diagnosis**:
```csharp
public static async Task DiagnoseSignatureIssue(UpdaterConfig config)
{
    // Check trusted root keys
    try
    {
        var trustedRoot = JsonDocument.Parse(Encoding.UTF8.GetString(config.LocalTrustedRoot));
        var keys = trustedRoot.RootElement.GetProperty("signed").GetProperty("keys");
        
        Console.WriteLine($"✅ Trusted root contains {keys.GetProperty("keys").EnumerateObject().Count()} keys:");
        
        foreach (var key in keys.EnumerateObject())
        {
            var keyData = key.Value;
            var keyType = keyData.GetProperty("keytype").GetString();
            var scheme = keyData.GetProperty("scheme").GetString();
            Console.WriteLine($"  - {key.Name}: {keyType}/{scheme}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Failed to parse trusted root: {ex.Message}");
    }
    
    // Verify against current repository metadata
    try
    {
        using var client = new HttpClient();
        var rootResponse = await client.GetAsync($"{config.RemoteMetadataUrl}root.json");
        var rootContent = await rootResponse.Content.ReadAsStringAsync();
        var currentRoot = JsonDocument.Parse(rootContent);
        
        // Compare key IDs
        var currentKeys = currentRoot.RootElement.GetProperty("signed").GetProperty("keys");
        Console.WriteLine($"\nRepository root contains {currentKeys.EnumerateObject().Count()} keys:");
        
        foreach (var key in currentKeys.EnumerateObject())
        {
            Console.WriteLine($"  - {key.Name}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Failed to fetch repository root: {ex.Message}");
    }
}
```

**Solutions**:

1. **Verify trusted root is current**:
```csharp
// Get fresh trusted root from repository administrator
var newTrustedRoot = await File.ReadAllBytesAsync("updated-trusted-root.json");
config.LocalTrustedRoot = newTrustedRoot;
```

2. **Check for key rotation**:
```csharp
// Repository keys may have been rotated
// Contact repository administrator for updated trusted root
```

### Problem: "Insufficient signatures"

**Symptoms**: `InsufficientSignaturesException` with valid metadata

**Diagnosis**:
```csharp
public static async Task DiagnoseSignatureThreshold(string metadataUrl, string metadataType)
{
    using var client = new HttpClient();
    var response = await client.GetAsync($"{metadataUrl}{metadataType}.json");
    var content = await response.Content.ReadAsStringAsync();
    var metadata = JsonDocument.Parse(content);
    
    // Count signatures
    var signatures = metadata.RootElement.GetProperty("signatures");
    var signatureCount = signatures.GetArrayLength();
    Console.WriteLine($"Metadata has {signatureCount} signatures:");
    
    foreach (var sig in signatures.EnumerateArray())
    {
        var keyId = sig.GetProperty("keyid").GetString();
        Console.WriteLine($"  - Key ID: {keyId}");
    }
    
    // Check against trusted root threshold requirements
    // This requires parsing the trusted root to get role threshold info
}
```

**Solution**: Contact repository administrator to ensure sufficient valid signatures are present.

## Target File Issues

### Problem: "Target file not found"

**Symptoms**: `TargetNotFoundException` when looking up files

**Diagnosis**:
```csharp
public static async Task ListAvailableTargets(Updater updater)
{
    try
    {
        await updater.RefreshAsync();
        
        // Get all available targets (this would need to be implemented in TUF .NET)
        // For now, we can suggest checking the targets metadata directly
        
        Console.WriteLine("Available targets in repository:");
        Console.WriteLine("(Check targets.json metadata for complete list)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to refresh metadata: {ex.Message}");
    }
}
```

**Solutions**:

1. **List available targets**:
```csharp
// Check targets.json directly
using var client = new HttpClient();
var targetsResponse = await client.GetAsync($"{config.RemoteMetadataUrl}targets.json");
var targetsContent = await targetsResponse.Content.ReadAsStringAsync();
var targetsMetadata = JsonDocument.Parse(targetsContent);

var targets = targetsMetadata.RootElement.GetProperty("signed").GetProperty("targets");
Console.WriteLine("Available targets:");
foreach (var target in targets.EnumerateObject())
{
    Console.WriteLine($"  - {target.Name}");
}
```

2. **Use pattern matching for similar names**:
```csharp
public static List<string> FindSimilarTargets(string searchTarget, List<string> availableTargets)
{
    return availableTargets
        .Where(target => target.Contains(searchTarget, StringComparison.OrdinalIgnoreCase) ||
                        LevenshteinDistance(target, searchTarget) < 3)
        .OrderBy(target => LevenshteinDistance(target, searchTarget))
        .Take(5)
        .ToList();
}
```

### Problem: "File integrity verification failed"

**Symptoms**: `TargetIntegrityException` during download

**Diagnosis**:
```csharp
public static async Task DiagnoseIntegrityIssue(string filePath, string expectedHash)
{
    if (File.Exists(filePath))
    {
        Console.WriteLine($"File exists: {filePath}");
        Console.WriteLine($"File size: {new FileInfo(filePath).Length} bytes");
        
        // Calculate actual hash
        using var sha256 = SHA256.Create();
        using var fileStream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(fileStream);
        var actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        Console.WriteLine($"Expected hash: {expectedHash}");
        Console.WriteLine($"Actual hash:   {actualHash}");
        
        if (actualHash == expectedHash.ToLowerInvariant())
        {
            Console.WriteLine("✅ Hashes match - integrity verification would pass");
        }
        else
        {
            Console.WriteLine("❌ Hash mismatch - file is corrupted or tampered");
        }
    }
    else
    {
        Console.WriteLine($"❌ File does not exist: {filePath}");
    }
}
```

**Solutions**:

1. **Re-download the file**:
```csharp
// Delete corrupted file and retry
if (File.Exists(localPath))
{
    File.Delete(localPath);
    Console.WriteLine("Deleted corrupted file, retrying download...");
}

// Implement retry logic
var (newLocalPath, data) = await RetryDownloadTarget(targetFile, targetName);
```

2. **Check network stability**:
```csharp
// Use checksums to verify network transfer integrity
// Consider implementing resume capabilities for large files
```

## Performance Issues

### Problem: "TUF operations are slow"

**Diagnosis**:
```csharp
public static async Task BenchmarkTufOperations(UpdaterConfig config)
{
    var stopwatch = Stopwatch.StartNew();
    
    // Measure updater creation
    stopwatch.Restart();
    var updater = new Updater(config);
    Console.WriteLine($"Updater creation: {stopwatch.ElapsedMilliseconds}ms");
    
    // Measure metadata refresh
    stopwatch.Restart();
    await updater.RefreshAsync();
    Console.WriteLine($"Metadata refresh: {stopwatch.ElapsedMilliseconds}ms");
    
    // Measure target lookup
    stopwatch.Restart();
    var targetInfo = await updater.GetTargetInfo("example-file.txt");
    Console.WriteLine($"Target lookup: {stopwatch.ElapsedMilliseconds}ms");
}
```

**Solutions**:

1. **HTTP connection reuse**:
```csharp
// Use a shared HttpClient instance
private static readonly HttpClient SharedHttpClient = new HttpClient();

var config = new UpdaterConfig
{
    Client = SharedHttpClient, // Reuse connections
    // ... other settings
};
```

2. **Metadata caching**:
```csharp
// Ensure local caching is enabled
var config = new UpdaterConfig
{
    LocalMetadataDir = "./tuf-cache", // Enable local caching
    // ... other settings
};
```

3. **Connection timeouts**:
```csharp
var httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromSeconds(10); // Reduce timeout for faster failure
```

## Debugging Tools and Commands

### Enable Verbose Logging

```csharp
// Configure detailed logging
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug); // Enable debug logging
});

var logger = loggerFactory.CreateLogger<Program>();
```

### Create Diagnostic Reports

```csharp
public static async Task CreateDiagnosticReport(UpdaterConfig config, string outputPath)
{
    var report = new StringBuilder();
    report.AppendLine($"TUF .NET Diagnostic Report - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    report.AppendLine(new string('=', 60));
    
    // System information
    report.AppendLine($".NET Version: {Environment.Version}");
    report.AppendLine($"OS: {Environment.OSVersion}");
    report.AppendLine($"Machine: {Environment.MachineName}");
    report.AppendLine();
    
    // Configuration
    report.AppendLine("Configuration:");
    report.AppendLine($"  Metadata URL: {config.RemoteMetadataUrl}");
    report.AppendLine($"  Targets URL: {config.RemoteTargetsUrl}");
    report.AppendLine($"  Local cache: {config.LocalMetadataDir}");
    report.AppendLine();
    
    // Network test
    report.AppendLine("Network Connectivity:");
    try
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        var response = await client.GetAsync(config.RemoteMetadataUrl);
        report.AppendLine($"  Repository: {response.StatusCode}");
    }
    catch (Exception ex)
    {
        report.AppendLine($"  Repository: ERROR - {ex.Message}");
    }
    
    // Cache status
    report.AppendLine("Local Cache:");
    if (Directory.Exists(config.LocalMetadataDir))
    {
        var files = Directory.GetFiles(config.LocalMetadataDir, "*.json");
        report.AppendLine($"  Cached files: {files.Length}");
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            report.AppendLine($"    {Path.GetFileName(file)}: {info.Length} bytes, {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
        }
    }
    else
    {
        report.AppendLine("  Cache directory does not exist");
    }
    
    await File.WriteAllTextAsync(outputPath, report.ToString());
    Console.WriteLine($"Diagnostic report saved to: {outputPath}");
}
```

### Test Repository Access

```bash
# Command-line tools for testing TUF repositories

# Test HTTP connectivity
curl -I https://your-repository.com/metadata/root.json

# Download and validate JSON
curl -s https://your-repository.com/metadata/root.json | jq .

# Check certificate
openssl s_client -connect your-repository.com:443 -servername your-repository.com < /dev/null
```

### Memory and Performance Profiling

```csharp
// For development environments - enable detailed diagnostics
#if DEBUG
public static void EnableDiagnostics()
{
    // Monitor GC behavior
    GC.Collect();
    var beforeMemory = GC.GetTotalMemory(false);
    
    // ... perform TUF operations ...
    
    GC.Collect();
    var afterMemory = GC.GetTotalMemory(false);
    
    Console.WriteLine($"Memory used: {(afterMemory - beforeMemory) / 1024:N0} KB");
}
#endif
```

## Getting Additional Help

### Support Channels
- **GitHub Issues**: [TUF .NET Repository Issues](https://github.com/your-org/tuf-dotnet/issues)
- **Documentation**: [TUF .NET Documentation](../README.md)
- **TUF Specification**: [The Update Framework](https://theupdateframework.io/)

### Reporting Issues
When reporting issues, please include:

1. **Environment information**:
   - .NET version (`dotnet --info`)
   - Operating system
   - TUF .NET version

2. **Configuration** (sanitized):
   - Repository URLs (remove sensitive information)
   - Local directory paths
   - HTTP client configuration

3. **Full exception details**:
   - Exception type and message
   - Stack trace
   - Inner exception details

4. **Diagnostic information**:
   - Run diagnostic commands from this guide
   - Include network connectivity test results
   - Repository metadata validation results

### Creating Minimal Reproduction Cases

```csharp
// Template for minimal reproduction case
public static async Task ReproduceIssue()
{
    try
    {
        // Minimal configuration that reproduces the issue
        var config = new UpdaterConfig
        {
            LocalTrustedRoot = await File.ReadAllBytesAsync("test-root.json"),
            RemoteMetadataUrl = new Uri("https://test-repo.example.com/metadata/"),
            LocalMetadataDir = "./temp-cache",
            Client = new HttpClient()
        };
        
        var updater = new Updater(config);
        
        // Minimal operations that trigger the issue
        await updater.RefreshAsync();
        var targetInfo = await updater.GetTargetInfo("test-file.txt");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Issue reproduced: {ex}");
        throw;
    }
}
```

## Related Documentation

- [Error Handling Guide](./error-handling.md) - Comprehensive exception handling
- [Configuration Guide](./configuration.md) - Advanced configuration options
- [Core Concepts](./core-concepts.md) - Understanding TUF fundamentals
- [Performance Guide](./performance.md) - Optimization techniques

This troubleshooting guide should be your first stop when encountering TUF .NET issues. If problems persist after following these steps, please create a GitHub issue with the diagnostic information requested above.