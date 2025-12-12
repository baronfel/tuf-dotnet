# Sample TUF Repository

This directory contains sample TUF metadata and target files for **demonstration purposes only**. 

⚠️ **IMPORTANT**: These are **placeholder files with demo data** and should **NOT** be used in production. The signatures and keys are not valid.

## Directory Structure

```
sample-repository/
├── metadata/           # TUF metadata files
│   ├── root.json      # Sample root metadata (DEMO ONLY)
│   ├── timestamp.json # (Create with real TUF tools)
│   ├── snapshot.json  # (Create with real TUF tools)
│   └── targets.json   # (Create with real TUF tools)
└── targets/           # Target files
    └── sample-file.txt # Sample target file
```

## For Production Use

To use this production server with real TUF repositories:

1. **Generate Real TUF Repository**: Use the [RepositoryManager example](../RepositoryManager/) or TUF tools
2. **Replace Sample Data**: Replace this entire directory with your real TUF repository
3. **Verify Signatures**: Ensure all metadata is properly signed with valid keys
4. **Test Thoroughly**: Verify client operations before production deployment

## Security Notes

- **Root Keys**: Keep root signing keys offline and secure
- **Metadata Expiry**: Ensure timestamp metadata is regularly updated
- **File Integrity**: Verify target file hashes match metadata
- **Access Control**: Limit write access to authorized signing systems only

For real TUF repository creation, see:
- [TUF Specification](https://theupdateframework.io/specification/)
- [TUF .NET RepositoryManager](../RepositoryManager/)
- [TUF Python Tools](https://github.com/theupdateframework/python-tuf)