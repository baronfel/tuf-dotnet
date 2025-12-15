# ASP.NET Core TUF Integration Example

This example demonstrates how to integrate TUF (The Update Framework) with ASP.NET Core web applications for secure software updates and configuration management.

## Features

### 🔧 **Core Integration**
- **Dependency Injection**: Proper registration of TUF services in the DI container
- **Configuration Management**: Configurable TUF settings via `appsettings.json`
- **Background Services**: Automatic periodic metadata refresh using hosted services
- **Health Checks**: Built-in health monitoring for TUF repository status

### 🚀 **API Endpoints**
- `GET /api/tuf/status` - Get current TUF repository status
- `POST /api/tuf/refresh` - Manually refresh TUF metadata
- `GET /api/tuf/targets` - List available target files
- `POST /api/tuf/targets/{name}/download` - Download and verify specific targets
- `POST /api/app/check-updates` - Check for application updates

### 📊 **Monitoring & Observability**
- `GET /health` - Overall application health including TUF status
- `GET /health/ready` - TUF-specific readiness check
- **Structured Logging**: High-performance logging with context preservation
- **Activity Tracing**: System.Diagnostics integration for distributed tracing
- **Swagger/OpenAPI**: Full API documentation

### ⚡ **Production Ready**
- **AOT Compatible**: Optimized for cloud-native deployments
- **Error Handling**: Comprehensive TUF exception handling and recovery
- **Security**: Proper validation and integrity checking
- **Performance**: Minimal overhead background operations

## Quick Start

### 1. Build and Run

```bash
cd examples/AspNetCoreIntegration
dotnet run
```

The application will start on `https://localhost:5001` (or the configured port).

### 2. Explore the API

Visit `https://localhost:5001/swagger` to explore the interactive API documentation.

### 3. Check Health Status

```bash
# Overall health (includes TUF status)
curl https://localhost:5001/health

# TUF-specific readiness
curl https://localhost:5001/health/ready

# TUF repository status  
curl https://localhost:5001/api/tuf/status
```

### 4. Refresh Metadata

```bash
# Manually trigger metadata refresh
curl -X POST https://localhost:5001/api/tuf/refresh
```

### 5. Check for Updates

```bash
# Check for application updates
curl -X POST https://localhost:5001/api/app/check-updates
```

## Configuration

Configure TUF settings in `appsettings.json`:

```json
{
  "Tuf": {
    "RepositoryUrl": "https://your-tuf-repo.com",
    "MetadataDirectory": "./tuf-cache/metadata",
    "TargetsDirectory": "./tuf-cache/targets", 
    "RefreshIntervalMinutes": 30,
    "MaxMetadataAgeHours": 24
  }
}
```

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `RepositoryUrl` | TUF repository base URL | `https://example.com/tuf-repo` |
| `MetadataDirectory` | Local metadata cache directory | `./tuf-cache/metadata` |
| `TargetsDirectory` | Local targets download directory | `./tuf-cache/targets` |
| `RefreshIntervalMinutes` | Background refresh interval | `30` |
| `MaxMetadataAgeHours` | Max age before health check degrades | `24` |

## Architecture

### Service Architecture

```
┌─────────────────────┐    ┌──────────────────┐    ┌─────────────────────┐
│   ASP.NET Core      │    │   TufClientService │    │   TUF Repository    │
│   Web Application   │───▶│   (ITufClient)     │───▶│   (Remote)          │
└─────────────────────┘    └──────────────────┘    └─────────────────────┘
         │                           │
         ▼                           ▼
┌─────────────────────┐    ┌──────────────────┐
│   TufUpdateService  │    │   TufHealthCheck │
│   (Background)      │    │   (Monitoring)   │
└─────────────────────┘    └──────────────────┘
```

### Key Components

1. **TufClientService**: Main TUF integration service implementing `ITufClient`
2. **TufUpdateService**: Background service for automatic metadata refresh
3. **TufHealthCheck**: Health check integration for operational monitoring
4. **API Controllers**: RESTful endpoints for TUF operations
5. **Configuration**: Centralized settings management

## Real-World Usage Patterns

### Web Application Updates

```csharp
// Check for application updates during startup
public class UpdateCheckMiddleware
{
    public async Task InvokeAsync(HttpContext context, ITufClient tufClient)
    {
        var updateInfo = await tufClient.CheckForUpdatesAsync("1.0.0");
        if (updateInfo.UpdateAvailable)
        {
            context.Response.Headers.Add("X-Update-Available", updateInfo.LatestVersion);
        }
        await _next(context);
    }
}
```

### Configuration Updates

```csharp
// Download updated configuration files
[HttpPost("api/config/update")]
public async Task<IActionResult> UpdateConfiguration(ITufClient tufClient)
{
    try
    {
        var configPath = await tufClient.DownloadTargetAsync("appsettings.production.json");
        await configuration.ReloadAsync(configPath);
        return Ok(new { message = "Configuration updated successfully" });
    }
    catch (TargetNotFoundException)
    {
        return NotFound("Configuration update not available");
    }
}
```

### Content Management

```csharp
// Update static content (images, documents, etc.)
[HttpPost("api/content/sync")]
public async Task<IActionResult> SyncContent(ITufClient tufClient)
{
    var targets = await tufClient.GetAvailableTargetsAsync();
    var contentTargets = targets.Where(t => t.StartsWith("content/"));
    
    foreach (var target in contentTargets)
    {
        await tufClient.DownloadTargetAsync(target);
    }
    
    return Ok(new { synced = contentTargets.Count() });
}
```

## Production Deployment

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AspNetCoreIntegration.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AspNetCoreIntegration.dll"]
```

### Kubernetes Health Checks

```yaml
apiVersion: v1
kind: Pod
spec:
  containers:
  - name: tuf-app
    image: tuf-app:latest
    livenessProbe:
      httpGet:
        path: /health
        port: 80
      initialDelaySeconds: 30
      periodSeconds: 30
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 80
      initialDelaySeconds: 5
      periodSeconds: 10
```

### Azure App Service

Configure health check endpoint in App Service:

```json
{
  "healthCheckPath": "/health",
  "healthCheckGracePeriod": "00:05:00"
}
```

## Monitoring & Observability

### Structured Logging

The application produces structured log events that can be ingested by log aggregation systems:

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "level": "Information", 
  "message": "TUF metadata refresh completed successfully",
  "properties": {
    "ElapsedMs": 245,
    "TargetCount": 5,
    "RepositoryUrl": "https://repo.example.com"
  }
}
```

### Activity Tracing

Distributed tracing support via System.Diagnostics.Activity:

```
TUF.RefreshTufMetadata
├── Duration: 245ms
├── Tags:
│   ├── repository.url: https://repo.example.com
│   ├── refresh.success: true
│   └── refresh.target_count: 5
└── Status: Ok
```

### Health Check Monitoring

Health check data structure:

```json
{
  "status": "Healthy",
  "totalDuration": 45.2,
  "checks": [{
    "name": "tuf-repository",
    "status": "Healthy",
    "duration": 42.1,
    "description": "TUF repository healthy with 5 targets",
    "data": {
      "repository_url": "https://repo.example.com",
      "last_refresh": "2024-01-15T10:25:00Z",
      "target_count": 5,
      "metadata_age_minutes": 5.2
    }
  }]
}
```

## Security Considerations

### Best Practices

1. **Repository URLs**: Always use HTTPS endpoints for TUF repositories
2. **Local Storage**: Secure metadata and target cache directories with appropriate permissions
3. **Logging**: Avoid logging sensitive information in TUF operations
4. **Updates**: Validate all downloaded content before application restart
5. **Rollback**: Maintain previous versions for rollback capability

### Production Checklist

- [ ] Configure real TUF repository URL (not example.com)
- [ ] Set appropriate metadata refresh intervals for your use case
- [ ] Configure secure local cache directories
- [ ] Set up monitoring alerts for TUF health check failures  
- [ ] Test update rollback procedures
- [ ] Document incident response procedures for TUF failures

## Troubleshooting

### Common Issues

**Service won't start**
- Check TUF repository URL accessibility
- Verify metadata cache directory permissions
- Review application logs for TUF configuration errors

**Health check failing**
- Verify network connectivity to TUF repository
- Check metadata age against configured thresholds
- Review TUF service logs for authentication issues

**Background updates not working** 
- Confirm `RefreshIntervalMinutes` configuration
- Check hosted service registration in Program.cs
- Review background service logs for exceptions

### Debug Commands

```bash
# Check service status
curl https://localhost:5001/api/tuf/status

# Manual metadata refresh
curl -X POST https://localhost:5001/api/tuf/refresh

# Detailed health information
curl https://localhost:5001/health | jq

# View application logs
docker logs <container-id> --tail 100 -f
```

## See Also

- [TUF Specification](https://theupdateframework.io/specification/)
- [ASP.NET Core Health Checks](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [.NET Dependency Injection](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [System.Diagnostics.Activity](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.activity)

This example demonstrates production-ready TUF integration patterns that can be adapted for various ASP.NET Core application scenarios.