# Basic TUF Client Example

This example demonstrates how to use the TUF .NET library as a client to securely download files from a TUF repository.

## What This Example Shows

- How to configure a TUF `Updater` with metadata and targets URLs
- How to refresh TUF metadata from a remote repository
- How to query for target file information
- How to download target files securely with hash verification
- How to use local caching for metadata and targets

## Prerequisites

- .NET 8.0 or later
- Access to a TUF repository (metadata and targets URLs)
- A trusted root metadata file (in production scenarios)

## Running the Example

```bash
# Build the example
dotnet build

# Run with a TUF repository URL and target file name
dotnet run https://example.com/metadata file.txt
```

## Understanding TUF Client Workflow

This example implements the standard TUF client workflow:

1. **Initialize**: Create an `Updater` with trusted root metadata and repository URLs
2. **Refresh**: Download and verify current metadata (root, timestamp, snapshot, targets)
3. **Query**: Get information about a specific target file
4. **Download**: Securely download the target file with integrity verification

## Security Features Demonstrated

- **Metadata verification**: Each metadata file is cryptographically verified
- **Consistent snapshot**: Ensures all metadata files are from the same repository state
- **Hash verification**: Target files are verified against expected hashes
- **Local caching**: Metadata and targets are cached locally for performance
- **Rollback protection**: Prevents downgrade attacks on metadata versions

## Important Notes

⚠️ **This example uses a placeholder trusted root for demonstration purposes.**

In a production application, you must:
- Distribute a real trusted root metadata file with your application
- Obtain the trusted root through a secure out-of-band channel
- Implement proper key management and rotation procedures

## Next Steps

- Look at the `CliTool` example for a more complete command-line interface
- Review the TUF specification to understand the security model
- Implement proper trusted root distribution for your use case