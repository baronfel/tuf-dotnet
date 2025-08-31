# Multi-Repository TUF Client (TAP 4)

This example demonstrates how to use the TUF .NET multi-repository client, which implements **TAP 4: Multiple repository consensus on entrusted targets**.

## What is Multi-Repository Support?

Multi-repository support allows TUF clients to securely retrieve target files from multiple independent TUF repositories with consensus validation. This provides:

- **Enhanced Security**: Multiple repositories must agree on target metadata before files are trusted
- **Compromise Resilience**: Attackers must compromise multiple repositories to succeed
- **Flexible Policies**: Different consensus requirements for different types of files
- **Distributed Trust**: Reduces single points of failure

## How It Works

1. **Configuration**: A `map.json` file defines repositories and mapping rules
2. **Search Algorithm**: Client searches repositories according to mapping rules
3. **Consensus Validation**: Target metadata must match across a threshold of repositories
4. **Secure Download**: Files are downloaded only after consensus is achieved

## Configuration Format

The `map.json` file contains two main sections:

### Repositories
```json
{
  "repositories": {
    "repo-a": {
      "name": "repo-a",
      "metadata_url": "https://example.com/repo-a/metadata",
      "targets_url": "https://example.com/repo-a/targets", 
      "trusted_root": "./trusted-roots/repo-a-root.json"
    },
    "repo-b": {
      "name": "repo-b",
      "metadata_url": "https://example.com/repo-b/metadata", 
      "targets_url": "https://example.com/repo-b/targets",
      "trusted_root": "./trusted-roots/repo-b-root.json"
    }
  }
}
```

### Mapping Rules
```json
{
  "mapping": [
    {
      "paths": ["critical/*.exe"],
      "repositories": ["repo-a", "repo-b"],
      "threshold": 2,
      "terminating": true
    },
    {
      "paths": ["*"],
      "repositories": ["repo-a", "repo-b"],
      "threshold": 1,
      "terminating": false
    }
  ]
}
```

**Fields:**
- `paths`: Array of glob patterns for target file paths
- `repositories`: List of repository names to search
- `threshold`: Minimum number of repositories that must agree
- `terminating`: If true, stops processing additional mappings

## Usage Examples

### Basic Usage
```bash
# Create sample configuration
dotnet run

# Use with specific target
dotnet run ./demo-map.json app.exe
```

### Advanced Configuration
```bash
# High-security mode: require all repositories to agree
dotnet run ./secure-map.json critical-update.exe

# Resilient mode: allow fallback repositories
dotnet run ./resilient-map.json regular-update.zip
```

## Use Cases

### 1. Critical Software Updates
Require multiple independent repositories to sign off on critical system updates:
```json
{
  "paths": ["system/*.exe", "drivers/*"],
  "repositories": ["primary-repo", "security-repo", "vendor-repo"],
  "threshold": 3,
  "terminating": true
}
```

### 2. Organizational Trust
Different trust requirements for internal vs external software:
```json
[
  {
    "paths": ["internal/*"],
    "repositories": ["internal-repo"],
    "threshold": 1,
    "terminating": true
  },
  {
    "paths": ["external/*"],
    "repositories": ["repo-a", "repo-b"],
    "threshold": 2,
    "terminating": true
  }
]
```

### 3. High Availability
Fallback to alternative repositories if primary is unavailable:
```json
{
  "paths": ["*"],
  "repositories": ["primary", "mirror-1", "mirror-2"],
  "threshold": 1,
  "terminating": false
}
```

## Security Considerations

- **Trusted Roots**: Each repository must have its own trusted root.json file
- **Key Management**: Keep repository signing keys separate and secure
- **Threshold Selection**: Higher thresholds provide better security but reduce availability
- **Path Patterns**: Be specific with path patterns to avoid unintended trust delegation
- **Network Security**: Use HTTPS for all repository communications

## Integration with Other Examples

The multi-repository client can work alongside other TUF examples:

```bash
# Use repository manager to create test repositories
dotnet run --project ../RepositoryManager ./repo-a
dotnet run --project ../RepositoryManager ./repo-b

# Configure map.json to use local repositories
# Then use multi-repository client
dotnet run ./local-map.json test-file.txt
```

## Troubleshooting

### Common Issues

1. **"Client not initialized"**: Call `InitializeAsync()` before other operations
2. **"Failed to parse map.json"**: Check JSON syntax and required fields
3. **"Target not found"**: Ensure target exists in at least one mapped repository
4. **"Insufficient consensus"**: Reduce threshold or check repository availability

### Debug Tips

- Enable verbose logging to see repository communications
- Check that all trusted root files exist and are valid
- Verify repository URLs are accessible
- Test with simple mappings first, then add complexity

## Performance Considerations

- **Parallel Requests**: The client queries repositories in parallel for better performance
- **Caching**: Metadata is cached locally to reduce network requests
- **Threshold Optimization**: Lower thresholds reduce latency but may compromise security
- **Repository Selection**: Place faster/more reliable repositories first in mappings

## Standards Compliance

This implementation follows:
- **TAP 4**: Multiple repository consensus on entrusted targets
- **TUF Specification**: Core TUF security model and metadata validation
- **Best Practices**: Secure key management and network communications

## Next Steps

1. Try the example with sample data
2. Configure your own repositories and map.json
3. Integrate multi-repository support into your applications
4. Consider advanced features like delegation and custom metadata