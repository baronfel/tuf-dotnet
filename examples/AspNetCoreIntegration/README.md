# ASP.NET Core TUF Integration Example

This example demonstrates how to integrate The Update Framework (TUF) validation into ASP.NET Core web applications using custom middleware.

## Overview

The example includes:
- **TufValidationMiddleware**: Core middleware for TUF validation
- **TufMiddlewareExtensions**: Extension methods for easy registration
- **Sample API endpoints**: Demonstrating protected and unprotected routes
- **Configuration options**: Flexible TUF validation settings

## Key Features

### TUF Validation Middleware
- **Path-based filtering**: Only validate specific endpoints
- **Configurable validation**: Flexible options for different scenarios  
- **Error handling**: Graceful failures with proper HTTP responses
- **Logging integration**: Comprehensive logging for monitoring

### Configuration Options
- `ValidatedPaths`: Specify which paths require TUF validation
- `MetadataPath`: Location of TUF repository metadata
- `FailOpen`: Whether to allow requests when validation fails
- `ValidationTimeout`: Maximum time for validation operations

## Usage

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add TUF validation services
builder.Services.AddTufValidation(options =>
{
    options.ValidatedPaths.Add("/api/packages");
    options.ValidatedPaths.Add("/secure");
    options.MetadataPath = "/path/to/tuf/metadata";
    options.FailOpen = false;
});

var app = builder.Build();

// Add TUF validation middleware
app.UseTufValidation();
```

### Advanced Configuration

```csharp
// Custom validation options
builder.Services.AddTufValidation(options =>
{
    options.ValidatedPaths.Add("/api/packages");
    options.ValidatedPaths.Add("/downloads");
    options.MetadataPath = Environment.GetEnvironmentVariable("TUF_METADATA_PATH") ?? "/tmp/tuf";
    options.FailOpen = builder.Environment.IsDevelopment();
    options.ValidationTimeout = TimeSpan.FromSeconds(30);
});
```

## Running the Example

1. **Start the application**:
   ```bash
   cd examples/AspNetCoreIntegration
   dotnet run
   ```

2. **Test endpoints**:
   ```bash
   # This endpoint will be validated by TUF (may fail without proper setup)
   curl http://localhost:5000/api/packages/example
   
   # This endpoint bypasses TUF validation
   curl http://localhost:5000/public/info
   
   # This demonstrates successful validation
   curl http://localhost:5000/api/packages/demo
   ```

## API Endpoints

| Endpoint | TUF Validated | Description |
|----------|--------------|-------------|
| `/` | No | Welcome page |
| `/api/packages/{id}` | Yes | Package endpoint requiring TUF validation |
| `/secure/data` | Yes | Secure endpoint requiring TUF validation |
| `/public/info` | No | Public endpoint bypassing validation |
| `/health` | No | Health check endpoint |
| `/api/packages/demo` | Yes | Demo endpoint with simulated validation |

## Integration Scenarios

### Package Management API
Perfect for APIs serving software packages, container images, or other downloadable content where integrity verification is critical.

### Microservices Communication  
Use TUF validation to verify the authenticity of inter-service communications in a microservices architecture.

### Content Delivery Networks
Integrate with CDN endpoints to validate content before serving to clients.

### Enterprise Software Distribution
Implement in internal software distribution systems to ensure only validated packages are deployed.

## Production Considerations

### Performance
- The middleware adds minimal overhead (~10ms in this demo)
- Consider caching TUF metadata for better performance
- Use async patterns to avoid blocking the request pipeline

### Security
- Store TUF metadata securely and update regularly
- Use proper key management for TUF root keys
- Implement proper error handling to avoid information disclosure

### Monitoring
- Log all validation attempts for security monitoring
- Set up alerts for validation failures
- Monitor middleware performance metrics

### High Availability
- Configure `FailOpen = true` for development environments
- Set appropriate timeouts for validation operations
- Consider fallback mechanisms for TUF repository unavailability

## Extending the Example

### Custom Validation Logic
Extend `ValidateTufMetadataAsync` to implement real TUF validation:

```csharp
private async Task<bool> ValidateTufMetadataAsync(HttpContext context)
{
    // Extract package information from request
    var packageId = context.Request.RouteValues["id"]?.ToString();
    
    // Load TUF metadata
    var trustedMetadata = await LoadTrustedMetadataAsync();
    
    // Validate package signatures
    return await ValidatePackageSignatureAsync(packageId, trustedMetadata);
}
```

### Integration with TUF.NET Library
```csharp
// Use the TUF library for real validation
var updater = new Updater(trustedMetadata, metadataDir, targetDir, targetFilename);
var targetInfo = await updater.RefreshAsync();
return targetInfo.IsValid;
```

### Custom Configuration Providers
```csharp
// Load TUF configuration from external sources
services.Configure<TufValidationOptions>(configuration.GetSection("TufValidation"));
```

## Real-World Integration

This example provides a foundation for integrating TUF validation into production ASP.NET Core applications. Key areas for production implementation:

1. **Actual TUF Integration**: Replace demo validation with real TUF library calls
2. **Metadata Management**: Implement proper TUF repository metadata loading and updating  
3. **Key Management**: Secure handling of TUF root keys and delegated keys
4. **Performance Optimization**: Caching strategies for metadata and signatures
5. **Monitoring Integration**: Comprehensive logging and metrics collection

## Related Examples

- [BasicClient](../BasicClient/README.md) - Simple TUF client implementation
- [SigningDemo](../SigningDemo/README.md) - TUF signing and verification
- [MultiRepositoryClient](../MultiRepositoryClient/README.md) - Multi-repository TUF client

## References

- [TUF Specification](https://github.com/theupdateframework/specification)
- [ASP.NET Core Middleware Documentation](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware/)
- [.NET TUF Library Documentation](../../docs/README.md)