# Multi-Repository Client API

The `MultiRepositoryClient` class implements **TAP 4** (Multiple repository consensus on entrusted targets), enabling secure target retrieval from multiple independent TUF repositories with consensus validation. This provides enhanced security by requiring agreement across multiple repositories before trusting target files.

## Overview

Multi-repository consensus addresses the scenario where you want to distribute trust across multiple independent TUF repositories rather than relying on a single repository. The client validates that target files are consistent across multiple repositories before considering them trustworthy.

### Key Benefits

- **Enhanced Security**: Requires consensus across multiple repositories
- **Fault Tolerance**: Continues to work if some repositories are unavailable
- **Attack Mitigation**: Makes repository compromise attacks much more difficult
- **Flexible Configuration**: Supports complex mapping rules and thresholds

## Quick Start

```csharp
using TUF;

// Configure multi-repository client
var config = new MultiRepositoryConfig
{
    MapFilePath = "./map.json",
    MetadataDir = "./multi-repo-metadata",
    TargetsDir = "./multi-repo-targets",
    HttpClient = new HttpClient()
};

var client = new MultiRepositoryClient(config);

// Initialize with repository configuration
await client.InitializeAsync();

// Refresh metadata from all repositories
await client.RefreshAsync();

// Get target with consensus validation
var result = await client.GetTargetInfoAsync("app.exe");
if (result != null && result.IsValid)
{
    Console.WriteLine($"Target found with {result.AgreementCount}/{result.RepositoriesChecked.Length} repository agreement");
    
    // Download with consensus validation
    await client.DownloadTargetAsync("app.exe", "./app.exe");
}
```

## Configuration

### MultiRepositoryConfig

Configuration class for the multi-repository client.

**Properties:**
- `MapFilePath` - Path to the `map.json` configuration file
- `MetadataDir` - Local directory for caching repository metadata
- `TargetsDir` - Local directory for caching downloaded targets
- `HttpClient` - HTTP client instance for network operations

### Map.json Format

The `map.json` file defines repository configurations and consensus rules:

```json
{
  "repositories": {
    "repo-a": {
      "metadata_url": "https://repo-a.example.com/metadata/",
      "targets_url": "https://repo-a.example.com/targets/",
      "trusted_root_path": "./keys/repo-a-root.json"
    },
    "repo-b": {
      "metadata_url": "https://repo-b.example.com/metadata/", 
      "targets_url": "https://repo-b.example.com/targets/",
      "trusted_root_path": "./keys/repo-b-root.json"
    },
    "repo-c": {
      "metadata_url": "https://repo-c.example.com/metadata/",
      "targets_url": "https://repo-c.example.com/targets/", 
      "trusted_root_path": "./keys/repo-c-root.json"
    }
  },
  "mapping": [
    {
      "paths": ["*.exe", "*.dll"],
      "repositories": ["repo-a", "repo-b", "repo-c"],
      "threshold": 2,
      "terminating": false
    },
    {
      "paths": ["config/*"],
      "repositories": ["repo-a", "repo-b"],
      "threshold": 2,
      "terminating": true
    }
  ]
}
```

## Core Classes

### MultiRepositoryClient

The main client for multi-repository operations.

#### Methods

##### `InitializeAsync()`

Initializes the client by loading the map configuration and setting up individual TUF clients for each repository.

**Returns:** `Task` representing the async operation.

**Throws:**
- `FileNotFoundException` - If map.json or trusted root files are not found
- `JsonException` - If map.json format is invalid
- `HttpRequestException` - If initial repository connections fail

**Example:**
```csharp
var client = new MultiRepositoryClient(config);
await client.InitializeAsync();
```

##### `RefreshAsync()`

Refreshes metadata for all configured repositories in parallel.

**Returns:** `Task` representing the async operation.

**Throws:**
- `InvalidOperationException` - If called before `InitializeAsync()`
- `AggregateException` - If multiple repositories fail to refresh

**Example:**
```csharp
await client.RefreshAsync();
Console.WriteLine("All repository metadata refreshed");
```

##### `GetTargetInfoAsync(string targetPath)`

Finds target information across repositories using the TAP 4 consensus mechanism.

**Parameters:**
- `targetPath` - Path of the target file to search for

**Returns:** `Task<MultiRepositoryTargetResult?>` - The consensus result, or `null` if no valid consensus found

**Process:**
1. Iterates through mapping rules in order
2. For each applicable mapping, checks specified repositories
3. Applies consensus validation based on threshold
4. Returns first valid consensus or null

**Example:**
```csharp
var result = await client.GetTargetInfoAsync("myapp.exe");
if (result?.IsValid == true)
{
    Console.WriteLine($"Found target with {result.AgreementCount} repository consensus");
}
```

##### `DownloadTargetAsync(string targetPath, string destinationPath)`

Downloads a target file using multi-repository consensus validation.

**Parameters:**
- `targetPath` - Path of the target file to download
- `destinationPath` - Local path where the file should be saved

**Returns:** `Task<bool>` - `true` if download succeeded, `false` otherwise

**Process:**
1. Gets target info using consensus validation
2. Attempts download from each repository that has the target
3. Returns `true` on first successful download

**Example:**
```csharp
bool success = await client.DownloadTargetAsync("app.exe", "./downloads/app.exe");
if (success)
{
    Console.WriteLine("Target downloaded successfully");
}
```

### MultiRepositoryTargetResult

Represents the result of a multi-repository target lookup with consensus information.

#### Properties

- `TargetPath` - The path that was searched for
- `TargetFile` - The consensus target file metadata (if found)
- `AgreementCount` - Number of repositories that agreed on this target
- `RequiredThreshold` - Minimum agreement count required for validity
- `RepositoriesChecked` - Array of repository names that were checked
- `IsValid` - Whether the result meets the consensus threshold

#### Methods

##### `TryGetValidFile(out TargetFile targetFile)`

Attempts to get the valid target file if consensus was reached.

**Parameters:**
- `targetFile` - Output parameter containing the target file metadata

**Returns:** `bool` - `true` if consensus was valid and target file is available

## Advanced Usage

### Complex Mapping Rules

```json
{
  "mapping": [
    {
      "paths": ["critical/*", "*.exe"],
      "repositories": ["repo-a", "repo-b", "repo-c"],
      "threshold": 3,
      "terminating": true
    },
    {
      "paths": ["docs/*", "*.txt"], 
      "repositories": ["repo-a", "repo-b"],
      "threshold": 1,
      "terminating": false
    },
    {
      "paths": ["*"],
      "repositories": ["repo-a"],
      "threshold": 1,
      "terminating": true
    }
  ]
}
```

**Rule Interpretation:**
1. **Critical files** require all 3 repositories to agree
2. **Documentation** needs only 1 repository, but continues to next rule if not found
3. **Everything else** falls back to repo-a only

### Error Handling and Diagnostics

```csharp
try
{
    var result = await client.GetTargetInfoAsync("app.exe");
    
    if (result == null)
    {
        Console.WriteLine("Target not found in any repository");
    }
    else if (!result.IsValid)
    {
        Console.WriteLine($"Insufficient consensus: {result.AgreementCount}/{result.RequiredThreshold}");
        Console.WriteLine($"Checked repositories: {string.Join(", ", result.RepositoriesChecked)}");
    }
    else
    {
        Console.WriteLine($"Valid consensus achieved: {result.AgreementCount}/{result.RequiredThreshold}");
        // Proceed with download
    }
}
catch (InvalidOperationException ex) when (ex.Message.Contains("not initialized"))
{
    Console.WriteLine("Client not initialized - call InitializeAsync() first");
}
```

### Repository Health Monitoring

```csharp
public async Task<Dictionary<string, bool>> CheckRepositoryHealth(MultiRepositoryClient client)
{
    var health = new Dictionary<string, bool>();
    
    try
    {
        await client.RefreshAsync();
        // If refresh succeeds, all repos are considered healthy
        // Individual repo failures would be in AggregateException
        
        return health;
    }
    catch (AggregateException ex)
    {
        // Analyze individual repository failures
        foreach (var inner in ex.InnerExceptions)
        {
            // Log specific repository failures
            Console.WriteLine($"Repository failure: {inner.Message}");
        }
        throw;
    }
}
```

## Best Practices

### Repository Configuration

1. **Diverse Infrastructure**: Use repositories hosted on different infrastructure
2. **Independent Keys**: Ensure each repository uses completely independent key sets
3. **Geographic Distribution**: Consider repositories in different regions/jurisdictions
4. **Update Coordination**: Plan how repositories will stay synchronized

### Threshold Selection

1. **Critical Files**: Require high consensus (e.g., 3/3 or 2/3)
2. **Documentation**: Lower thresholds acceptable (1/2 or 1/3)
3. **Fallback Strategy**: Always include a fallback rule for unmatched paths

### Performance Considerations

1. **Parallel Operations**: Client performs repository operations in parallel
2. **Metadata Caching**: Local metadata is cached between operations
3. **Network Timeouts**: Configure appropriate HTTP client timeouts
4. **Repository Availability**: Design for some repositories being temporarily unavailable

### Security Considerations

1. **Trust Bootstrap**: Each repository's root key must be independently verified
2. **Consensus Attacks**: Higher thresholds provide better attack resistance
3. **Repository Compromise**: Multi-repo consensus makes single-repo compromise ineffective
4. **Network Security**: Use HTTPS for all repository communications

## Complete Example

```csharp
public class ProductionMultiRepoExample
{
    public async Task SetupProductionClient()
    {
        // Configure for production use
        var config = new MultiRepositoryConfig
        {
            MapFilePath = "/etc/tuf/production-map.json",
            MetadataDir = "/var/cache/tuf/metadata",
            TargetsDir = "/var/cache/tuf/targets", 
            HttpClient = new HttpClient 
            { 
                Timeout = TimeSpan.FromSeconds(30) 
            }
        };

        var client = new MultiRepositoryClient(config);

        try
        {
            // Initialize with retry logic
            await InitializeWithRetry(client, maxRetries: 3);
            
            // Refresh metadata
            await client.RefreshAsync();
            
            // Download critical application files
            await DownloadCriticalFiles(client);
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Multi-repository setup failed: {ex.Message}");
            throw;
        }
    }

    private async Task InitializeWithRetry(MultiRepositoryClient client, int maxRetries)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await client.InitializeAsync();
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                Console.WriteLine($"Initialization attempt {attempt} failed: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponential backoff
            }
        }
    }

    private async Task DownloadCriticalFiles(MultiRepositoryClient client)
    {
        var criticalFiles = new[] { "app.exe", "core.dll", "config.json" };
        
        foreach (var file in criticalFiles)
        {
            var result = await client.GetTargetInfoAsync(file);
            
            if (result?.IsValid == true)
            {
                var success = await client.DownloadTargetAsync(file, $"./production/{file}");
                if (success)
                {
                    Console.WriteLine($"✅ Downloaded {file} with {result.AgreementCount}-repository consensus");
                }
                else
                {
                    throw new InvalidOperationException($"Failed to download critical file: {file}");
                }
            }
            else
            {
                throw new InvalidOperationException($"Insufficient consensus for critical file: {file}");
            }
        }
    }
}
```

## Related APIs

- **[Updater API](updater.md)** - Single-repository TUF client operations
- **[Repository Builder](repository-builder.md)** - Creating TUF repositories
- **[Building Clients Guide](../guides/building-clients.md)** - Production client patterns