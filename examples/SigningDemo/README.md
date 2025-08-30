# TUF .NET Signing Demonstration

This example demonstrates the cryptographic signing capabilities in TUF .NET, showcasing how to:

- Generate Ed25519 and RSA key pairs
- Sign data with both algorithms  
- Verify signatures and detect tampering
- Sign TUF metadata using the signing infrastructure

## What This Example Shows

### 🔐 Key Generation and Management
- **Ed25519**: Fast, secure, and widely used in modern TUF implementations
- **RSA-PSS**: Traditional algorithm with broad compatibility
- **Key IDs**: How TUF computes unique identifiers for keys

### ✍️ Digital Signing Process
- **Data Signing**: How to sign arbitrary data with TUF keys
- **Signature Verification**: How to verify signatures and detect tampering
- **Multiple Signers**: Demonstration that different keys produce different signatures

### 📝 TUF Metadata Signing
- **Metadata Creation**: Building TUF metadata structures
- **Metadata Signing**: Using signers to sign TUF metadata
- **Serialization**: Converting signed metadata to JSON format

## Running the Example

```bash
# From the repository root
dotnet build examples/SigningDemo
dotnet run --project examples/SigningDemo
```

## Security Notes

⚠️ **Important**: This example generates ephemeral keys for demonstration purposes only. In production:

1. **Key Storage**: Store private keys securely (HSM, secure enclave, encrypted storage)
2. **Key Distribution**: Distribute public keys through secure channels
3. **Key Rotation**: Implement proper key rotation procedures
4. **Access Control**: Limit access to signing capabilities

## Understanding the Output

The example produces output showing:

- **Key Generation**: Key IDs and types for generated keys
- **Signing Process**: Data sizes and signature lengths
- **Verification Results**: Whether signatures are valid or invalid
- **JSON Output**: Signed TUF metadata in canonical JSON format

## Integration with TUF

This signing infrastructure enables:

- **Repository Creation**: Tools can sign TUF metadata for publishing
- **Client Verification**: Clients can verify downloaded metadata
- **Key Management**: Support for multiple signing algorithms
- **Security Properties**: Cryptographic guarantees for software updates

## Next Steps

To build on this example:

1. **Repository Tools**: Create utilities for TUF repository management
2. **Key Management**: Implement secure key storage and rotation
3. **Integration**: Connect with existing build and deployment pipelines
4. **Compliance**: Ensure compatibility with TUF specification requirements

## Related Examples

- [BasicClient](../BasicClient/): TUF client implementation
- [CliTool](../CliTool/): Command-line TUF utilities

## Further Reading

- [TUF Specification](https://theupdateframework.io/specification/)
- [TUF Security Model](https://theupdateframework.io/security/)
- [Digital Signatures in TUF](https://theupdateframework.io/specification/#signatures)