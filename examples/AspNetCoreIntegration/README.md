# TUF ASP.NET Core Integration Example

This example demonstrates how to integrate TUF (The Update Framework) security into an ASP.NET Core web application. It shows best practices for production deployments including dependency injection, configuration management, logging, and error handling.

## Features Demonstrated

- ✅ **Dependency Injection**: TUF integrated with ASP.NET Core DI container
- ✅ **Configuration Management**: Configurable TUF settings via appsettings.json
- ✅ **Secure File Downloads**: REST API endpoint for verified file downloads
- ✅ **Repository Status**: Health check and status endpoints
- ✅ **Comprehensive Logging**: Structured logging throughout TUF operations
- ✅ **Error Handling**: Production-ready error handling and user feedback
- ✅ **AOT Compatible**: Supports native ahead-of-time compilation

## Architecture

### Key Components

1. **TufService**: Core service implementing TUF operations
2. **TufConfiguration**: Configuration model for TUF settings
3. **REST Endpoints**: Web API for secure file operations
4. **Dependency Injection**: Proper lifetime management of TUF components

### Design Patterns

- **Service Layer Pattern**: TUF operations abstracted behind ITufService interface
- **Configuration Pattern**: Settings externalized to appsettings.json
- **Repository Pattern**: TUF updater managed as scoped service
- **Error Handling Pattern**: Structured error responses with logging

## Running the Example

### Prerequisites

- .NET 8.0 or later
- Understanding of ASP.NET Core basics

### Quick Start

```bash
# Navigate to the example directory
cd examples/AspNetCoreIntegration

# Run the application
dotnet run
```

The application will start on `https://localhost:5001` (or the port shown in the console).

### API Endpoints

Once running, you can test these endpoints:

#### 1. Health Check
```bash
curl https://localhost:5001/health
```
**Response**: `TUF ASP.NET Core Integration - OK`

#### 2. TUF Repository Status
```bash
curl https://localhost:5001/tuf-status
```
**Response**:
```json
{
  "metadataUrl": "https://example.com/metadata/",
  "localMetadataDir": "./tuf-metadata",
  "localTargetsDir": "./downloads",
  "trustedRootPath": "trusted-root.json",
  "status": "Ready",
  "lastRefresh": "2025-11-15T10:30:00.000Z"
}
```

#### 3. List Available Files
```bash
curl https://localhost:5001/available-files
```
**Response**:
```json
{
  "files": ["example-file.txt", "app.exe", "config.json"]
}
```

#### 4. Secure File Download
```bash
curl -X POST "https://localhost:5001/secure-download?fileName=example-file.txt"
```
**Response**:
```json
{
  "fileName": "example-file.txt",
  "size": 1024,
  "verified": true,
  "downloadPath": "./downloads/example-file.txt"
}
```

## Configuration

The application reads TUF configuration from `appsettings.json`:

```json
{
  "TufConfiguration": {
    "TrustedRootPath": "trusted-root.json",
    "MetadataUrl": "https://your-tuf-repo.com/metadata/",
    "LocalMetadataDir": "./tuf-metadata",
    "LocalTargetsDir": "./downloads",
    "HttpTimeoutSeconds": 30
  }
}
```

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `TrustedRootPath` | Path to trusted root metadata file | `trusted-root.json` |
| `MetadataUrl` | URL of TUF repository metadata | `https://example.com/metadata/` |
| `LocalMetadataDir` | Local metadata cache directory | `./tuf-metadata` |
| `LocalTargetsDir` | Local target files directory | `./downloads` |
| `HttpTimeoutSeconds` | HTTP request timeout in seconds | `30` |

## Production Deployment

### Key Considerations

1. **Trusted Root Management**
   - Bundle trusted root metadata with your application
   - Use secure distribution channels for root updates
   - Implement root key rotation procedures

2. **Security**
   - Always use HTTPS for TUF repository URLs
   - Implement proper authentication/authorization for API endpoints
   - Monitor TUF operations for security events

3. **Performance**
   - Configure appropriate HTTP timeouts
   - Implement caching strategies for metadata
   - Use connection pooling for HTTP clients

4. **Monitoring**
   - Log all TUF operations for audit trails
   - Monitor download success/failure rates
   - Set up alerts for signature verification failures

### Example Production appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Information",
      "TUF": "Information"
    }
  },
  "TufConfiguration": {
    "TrustedRootPath": "/app/config/trusted-root.json",
    "MetadataUrl": "https://secure-cdn.example.com/tuf-metadata/",
    "LocalMetadataDir": "/app/data/tuf-cache",
    "LocalTargetsDir": "/app/data/downloads",
    "HttpTimeoutSeconds": 60
  }
}
```

### Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy trusted root metadata
COPY trusted-root.json /app/config/

# Copy application
COPY bin/Release/net8.0/publish/ .

# Create data directories
RUN mkdir -p /app/data/tuf-cache /app/data/downloads

EXPOSE 8080
ENTRYPOINT ["dotnet", "AspNetCoreIntegration.dll"]
```

## Integration Patterns

### Dependency Injection Registration

```csharp
// In Program.cs or Startup.cs
builder.Services.AddHttpClient();
builder.Services.AddScoped<ITufService, TufService>();
builder.Services.Configure<TufConfiguration>(
    builder.Configuration.GetSection("TufConfiguration"));
```

### Controller Integration

```csharp
[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly ITufService _tufService;
    
    public FilesController(ITufService tufService)
    {
        _tufService = tufService;
    }
    
    [HttpPost("download/{fileName}")]
    public async Task<IActionResult> Download(string fileName)
    {
        var result = await _tufService.SecureDownloadAsync(fileName);
        
        if (!result.Success)
            return BadRequest(result.Error);
            
        return File(result.Data!, "application/octet-stream", fileName);
    }
}
```

### Background Service Integration

```csharp
public class TufBackgroundService : BackgroundService
{
    private readonly ITufService _tufService;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _tufService.RefreshMetadataAsync();
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex)
            {
                // Log error and continue
            }
        }
    }
}
```

## Testing

### Unit Testing

```csharp
[Test]
public async Task SecureDownloadAsync_ValidFile_ReturnsSuccess()
{
    // Arrange
    var mockHttpClient = new Mock<HttpClient>();
    var config = new TufConfiguration { /* test config */ };
    var logger = new Mock<ILogger<TufService>>();
    
    var service = new TufService(mockHttpClient.Object, 
        Options.Create(config), logger.Object);
    
    // Act
    var result = await service.SecureDownloadAsync("test.txt");
    
    // Assert
    Assert.True(result.Success);
}
```

### Integration Testing

```csharp
public class AspNetCoreIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    
    [Fact]
    public async Task GetTufStatus_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/tuf-status");
        
        response.EnsureSuccessStatusCode();
    }
}
```

## Error Handling

The example implements comprehensive error handling:

1. **Service Level**: TufService catches and logs all TUF-related exceptions
2. **API Level**: Controllers return appropriate HTTP status codes
3. **Global Level**: ASP.NET Core exception middleware handles unexpected errors

### Common Error Scenarios

- **Trusted root not found**: Application creates demo root for testing
- **Network connectivity issues**: HTTP client timeout and retry logic
- **Signature verification failures**: Detailed error messages and logging
- **Repository unavailable**: Graceful degradation with cached metadata

## Security Considerations

1. **API Security**
   - Implement authentication/authorization as needed
   - Use HTTPS for all communications
   - Validate all input parameters

2. **TUF Security**
   - Protect trusted root metadata file
   - Monitor for signature verification failures
   - Implement proper key rotation procedures

3. **Operational Security**
   - Use structured logging for audit trails
   - Monitor download patterns for anomalies
   - Implement rate limiting if needed

## Related Examples

- **[BasicClient](../BasicClient/)** - Simple command-line TUF client
- **[RepositoryManager](../RepositoryManager/)** - Create and manage TUF repositories
- **[MultiRepositoryClient](../MultiRepositoryClient/)** - Multi-repository setup
- **[ErrorHandlingDemo](../ErrorHandlingDemo/)** - Comprehensive error handling patterns

This example demonstrates production-ready TUF integration patterns that can be adapted for your specific ASP.NET Core applications.