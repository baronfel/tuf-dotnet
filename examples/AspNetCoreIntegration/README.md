# ASP.NET Core TUF Integration Example

This example demonstrates how to integrate The Update Framework (TUF) into an ASP.NET Core web application for secure file distribution and updates.

## What This Example Shows

- **Dependency Injection**: How to register TUF services in ASP.NET Core's DI container
- **Web API Integration**: RESTful endpoints for secure file downloads and repository status
- **Health Checks**: Monitoring TUF repository connectivity and metadata validity
- **Error Handling**: Production-ready exception handling for TUF security events
- **Logging**: Structured logging for security events and operational monitoring

## Use Cases

This pattern is ideal for:
- **Software Distribution**: Serving application updates and plugins securely
- **Configuration Management**: Distributing configuration files with integrity guarantees
- **Asset Distribution**: Secure delivery of media files, documentation, or data
- **Microservices**: Sharing secure artifacts between services
- **CI/CD Pipelines**: Distributing build artifacts with tamper protection

## Prerequisites

- .NET 8.0 or later
- A TUF repository with metadata and target files
- Trusted root metadata file (for production use)

## Running the Example

```bash
# Build and run the web application
dotnet build
dotnet run

# The web app will start on https://localhost:5001
```

## API Endpoints

### `GET /`
Web interface explaining the demo and available endpoints.

### `GET /secure-files`
Returns a JSON list of all files available for secure download:
```json
[
  {
    "fileName": "app.exe",
    "length": 1048576,
    "hashes": {
      "sha256": "abc123..."
    }
  }
]
```

### `GET /secure-download/{fileName}`
Downloads a specific file with TUF integrity verification. Returns:
- `200 OK` with file content if successful
- `404 Not Found` if file doesn't exist in TUF metadata
- `409 Conflict` if file integrity verification fails
- `500 Internal Server Error` for other errors

### `GET /tuf-status`
Returns current TUF repository status:
```json
{
  "rootVersion": 1,
  "timestampVersion": 42,
  "snapshotVersion": 42,
  "targetsVersion": 15,
  "lastRefresh": "2025-09-05T10:30:00Z",
  "targetsCount": 5
}
```

### `GET /health`
Health check endpoint that verifies TUF repository connectivity.

## Production Configuration

### 1. Repository URLs
Configure your TUF repository in `appsettings.json`:
```json
{
  "TufRepository": {
    "MetadataUrl": "https://your-secure-repository.com/metadata",
    "TargetsUrl": "https://your-secure-repository.com/targets",
    "LocalMetadataDir": "./tuf-cache/metadata",
    "LocalTargetsDir": "./tuf-cache/targets"
  }
}
```

### 2. Trusted Root Distribution
Distribute your trusted root metadata securely:
```csharp
// Load trusted root from secure location
var trustedRoot = File.ReadAllText("secure/trusted-root.json");
var config = new UpdaterConfiguration
{
    // ... other settings
    TrustedRoot = trustedRoot
};
```

### 3. Caching and Performance
For production, implement distributed caching:
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});

// Use cached TUF updater for better performance
builder.Services.AddSingleton<ITufUpdater, CachedTufUpdater>();
```

### 4. Monitoring and Alerts
Set up alerts for TUF security events:
```csharp
builder.Services.Configure<LoggerFilterOptions>(options =>
{
    // Log all TUF security events as Critical
    options.Rules.Add(new LoggerFilterRule(
        "TUF", "Security", LogLevel.Critical, null));
});
```

## Security Considerations

### ✅ Security Features Implemented
- **Cryptographic Verification**: All files are verified against TUF metadata hashes
- **Rollback Protection**: TUF prevents serving older, potentially compromised versions
- **Tamper Detection**: Any modification to files or metadata is detected
- **Structured Security Logging**: Security events are logged for monitoring
- **Health Monitoring**: Repository availability is continuously checked

### ⚠️ Production Security Checklist
- [ ] Use HTTPS for all TUF repository URLs
- [ ] Distribute trusted root metadata through secure out-of-band channels
- [ ] Implement proper key rotation procedures
- [ ] Set up monitoring for TUF security exceptions
- [ ] Configure rate limiting for download endpoints
- [ ] Use least-privilege principles for TUF service accounts
- [ ] Regularly audit TUF metadata signatures and expiration

## Integration Patterns

### Background Service Pattern
For applications that need to check for updates periodically:
```csharp
builder.Services.AddHostedService<TufUpdateBackgroundService>();
```

### Middleware Pattern
For applications that need to protect all static files:
```csharp
app.UseMiddleware<TufSecurityMiddleware>();
```

### Controller Pattern
For more complex scenarios, use MVC controllers:
```csharp
[ApiController]
[Route("api/[controller]")]
public class SecureDownloadController : ControllerBase
{
    // Implementation details...
}
```

## Performance Considerations

- **Metadata Caching**: TUF metadata is cached locally to reduce network calls
- **Async Operations**: All TUF operations use async/await for better scalability
- **Health Checks**: Repository connectivity is monitored without blocking requests
- **Streaming**: Large files are streamed to minimize memory usage
- **Compression**: Enable response compression for better performance

## Troubleshooting

### Common Issues

1. **"TUF repository is not accessible"**
   - Check repository URLs in configuration
   - Verify network connectivity and firewall rules
   - Ensure repository is serving proper TUF metadata structure

2. **"File integrity verification failed"**
   - File may have been modified after being added to TUF metadata
   - Repository may be under attack or corrupted
   - Check TUF repository integrity and re-sign if necessary

3. **"Target file not found"**
   - File may not exist in current TUF metadata
   - Refresh repository metadata or add the file to targets

### Debug Logging
Enable debug logging to troubleshoot issues:
```json
{
  "Logging": {
    "LogLevel": {
      "TUF": "Debug"
    }
  }
}
```

## Next Steps

1. **Deploy to Production**: Configure with real TUF repository and trusted root
2. **Add Monitoring**: Integrate with your observability stack (Prometheus, Grafana)
3. **Scale Horizontally**: Use load balancers and distributed caching
4. **Security Audit**: Review implementation with security team
5. **Automate Updates**: Integrate with CI/CD pipeline for automatic deployments

## Resources

- [ASP.NET Core Health Checks](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [TUF Specification](https://theupdateframework.io/specification/)
- [.NET Dependency Injection](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Structured Logging in .NET](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging)