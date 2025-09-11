# TUF .NET Docker Integration Example

This example demonstrates how to integrate TUF .NET into containerized applications, showcasing production-ready patterns for secure software updates in Docker environments.

## 🎯 **Parity Achievement**: .NET-Specific Integration Patterns

This example addresses **Phase 4** priorities from the TUF .NET parity analysis, specifically:
- **.NET-specific integration patterns**: Comprehensive Docker integration with .NET best practices
- **Production deployment scenarios**: Real-world containerized deployment patterns
- **Container orchestration**: Health checks, resource management, and security configurations

## Key Features

### 🐳 **Container-Optimized TUF Integration**
- **Environment-based configuration**: Uses environment variables and appsettings.json
- **Dependency Injection**: Proper .NET DI patterns with TUF services
- **Structured logging**: Container-friendly JSON logging with correlation IDs
- **Health checks**: Built-in health endpoints for container orchestration
- **Volume management**: Proper handling of persistent data and trusted metadata

### 🔒 **Production Security**
- **Non-root execution**: Runs as dedicated `tufuser` for security
- **Read-only filesystem**: Immutable container with specific write mounts
- **Resource limits**: CPU and memory constraints
- **Network isolation**: Dedicated Docker network for TUF operations
- **No new privileges**: Security hardening with `no-new-privileges`

### ⚙️ **Operational Excellence**
- **Multi-stage Docker build**: Optimized image size and build caching
- **Health monitoring**: Native Docker health checks
- **Graceful shutdown**: Proper signal handling for container lifecycle
- **Configuration validation**: Startup checks for required settings
- **Comprehensive error handling**: Structured error reporting and logging

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Docker Container                          │
│  ┌─────────────────────────────────────────────────────────┐│
│  │           TUF .NET Application                          ││
│  │  ┌─────────────────────────────────────────────────────┐││
│  │  │         Command-Line Interface                      │││
│  │  │  • init    • refresh    • download    • health     │││
│  │  └─────────────────────────────────────────────────────┘││
│  │  ┌─────────────────────────────────────────────────────┐││
│  │  │            TUF Service Layer                        │││
│  │  │  • Configuration    • Dependency Injection         │││
│  │  │  • Logging         • Error Handling               │││
│  │  └─────────────────────────────────────────────────────┘││
│  │  ┌─────────────────────────────────────────────────────┐││
│  │  │              TUF Core Library                       │││
│  │  │  • Metadata Verification    • Target Downloads     │││
│  │  │  • Signature Validation     • Integrity Checks    │││
│  │  └─────────────────────────────────────────────────────┘││
│  └─────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────┐│
│  │                Volume Mounts                            ││
│  │  /app/trusted/   /app/downloads/   /app/metadata/      ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
```

## Quick Start

### 1. **Prerequisites**
```bash
# Ensure Docker and Docker Compose are installed
docker --version
docker-compose --version
```

### 2. **Prepare Trusted Root Metadata**
```bash
# Create trusted root metadata file (replace with your actual trusted root)
echo '{"example": "trusted root metadata"}' > trusted-root.json
```

### 3. **Configure Environment**
```bash
# Create environment file
cat > .env << 'EOF'
TUF_MetadataUrl=https://your-tuf-repository.example.com/metadata
TUF_MaxMetadataBytes=2097152
TUF_MaxTargetBytes=1073741824
EOF
```

### 4. **Build and Run**
```bash
# Build the container
docker-compose build

# Run TUF operations
docker-compose run tuf-client init
docker-compose run tuf-client refresh
docker-compose run tuf-client download --target myapp.exe --output /app/downloads/myapp.exe

# Check health
docker-compose run tuf-client health
```

## Production Deployment

### Docker Swarm Deployment

```yaml
version: '3.8'
services:
  tuf-updater:
    image: your-registry/tuf-dotnet:latest
    deploy:
      replicas: 3
      restart_policy:
        condition: on-failure
        delay: 10s
        max_attempts: 3
      resources:
        limits:
          memory: 512M
          cpus: '0.5'
    environment:
      - TUF_MetadataUrl=https://tuf.yourcompany.com/metadata
    volumes:
      - trusted-root:/app/trusted:ro
      - downloads:/app/downloads
    networks:
      - tuf-network
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tuf-client
spec:
  replicas: 3
  selector:
    matchLabels:
      app: tuf-client
  template:
    metadata:
      labels:
        app: tuf-client
    spec:
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        fsGroup: 1000
      containers:
      - name: tuf-client
        image: your-registry/tuf-dotnet:latest
        env:
        - name: TUF_MetadataUrl
          valueFrom:
            configMapKeyRef:
              name: tuf-config
              key: metadata-url
        volumeMounts:
        - name: trusted-root
          mountPath: /app/trusted
          readOnly: true
        - name: downloads
          mountPath: /app/downloads
        resources:
          limits:
            memory: 512Mi
            cpu: 500m
          requests:
            memory: 256Mi
            cpu: 250m
        livenessProbe:
          exec:
            command:
            - dotnet
            - DockerIntegration.dll
            - health
          initialDelaySeconds: 30
          periodSeconds: 30
      volumes:
      - name: trusted-root
        secret:
          secretName: tuf-trusted-root
      - name: downloads
        persistentVolumeClaim:
          claimName: tuf-downloads
```

## Configuration

### Environment Variables

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `TUF_MetadataUrl` | TUF repository metadata URL | - | ✅ |
| `TUF_RootKeyPath` | Path to trusted root metadata | `/app/trusted/root.json` | ✅ |
| `TUF_DownloadsDir` | Directory for downloaded targets | `/app/downloads` | ❌ |
| `TUF_MetadataDir` | Directory for metadata cache | `/app/metadata` | ❌ |
| `TUF_MaxMetadataBytes` | Maximum metadata file size | `1048576` | ❌ |
| `TUF_MaxTargetBytes` | Maximum target file size | `104857600` | ❌ |

### Volume Mounts

| Path | Purpose | Type | Required |
|------|---------|------|----------|
| `/app/trusted/` | Trusted root metadata | Read-only | ✅ |
| `/app/downloads/` | Downloaded target files | Read-write | ❌ |
| `/app/metadata/` | Metadata cache | Read-write | ❌ |

## Commands

### Available Commands

- **`init`**: Initialize TUF client with container-specific paths
- **`refresh`**: Refresh metadata from TUF repository
- **`download --target <path> [--output <path>]`**: Download and verify target files
- **`health`**: Health check for container orchestration

### Examples

```bash
# Initialize TUF client
docker run --rm -v $(pwd)/trusted-root.json:/app/trusted/root.json \
  -e TUF_MetadataUrl=https://tuf.example.com/metadata \
  your-registry/tuf-dotnet init

# Refresh metadata
docker run --rm -v tuf-metadata:/app/metadata \
  -e TUF_MetadataUrl=https://tuf.example.com/metadata \
  your-registry/tuf-dotnet refresh

# Download specific target
docker run --rm -v tuf-downloads:/app/downloads \
  -e TUF_MetadataUrl=https://tuf.example.com/metadata \
  your-registry/tuf-dotnet download --target "releases/v1.0/app.exe"

# Health check
docker run --rm your-registry/tuf-dotnet health
```

## Security Considerations

### 🔒 **Container Security**
- Runs as non-root user (`tufuser:1000`)
- Read-only root filesystem with specific write mounts
- No new privileges policy enforced
- Resource limits to prevent resource exhaustion
- Network isolation with dedicated Docker networks

### 🛡️ **TUF Security**
- Trusted root metadata must be provided via secure volume mount
- All target downloads are cryptographically verified
- Metadata expiration and rollback protection
- Threshold signature validation for multi-key scenarios

### 📋 **Operational Security**
- Comprehensive logging for audit trails
- Health checks for monitoring and alerting
- Graceful shutdown handling
- Configuration validation at startup

## Troubleshooting

### Common Issues

1. **Missing trusted root metadata**
   ```bash
   # Ensure root metadata is mounted correctly
   docker run -v $(pwd)/root.json:/app/trusted/root.json ...
   ```

2. **Permission errors**
   ```bash
   # Check volume mount permissions
   ls -la trusted-root.json
   # Should be readable by UID 1000
   ```

3. **Network connectivity**
   ```bash
   # Test repository connectivity
   docker run --rm your-registry/tuf-dotnet health
   ```

4. **Resource constraints**
   ```bash
   # Monitor container resources
   docker stats tuf-client-demo
   ```

### Debug Mode

```bash
# Run with debug logging
docker run -e DOTNET_LOGGING__LOGLEVEL__DEFAULT=Debug \
  your-registry/tuf-dotnet refresh
```

## Performance Considerations

- **Metadata caching**: Use persistent volumes for `/app/metadata` to improve performance
- **Resource limits**: Set appropriate CPU and memory limits based on workload
- **Network optimization**: Use container registry caching for faster deployments
- **Multi-stage builds**: Optimized Docker images with minimal runtime footprint

## Integration with CI/CD

### GitLab CI Example

```yaml
update-secure-artifacts:
  image: your-registry/tuf-dotnet:latest
  stage: deploy
  variables:
    TUF_MetadataUrl: $TUF_REPOSITORY_URL
  before_script:
    - echo "$TUF_TRUSTED_ROOT" > /app/trusted/root.json
  script:
    - dotnet DockerIntegration.dll refresh
    - dotnet DockerIntegration.dll download --target "$ARTIFACT_PATH"
  artifacts:
    paths:
      - downloads/
```

### GitHub Actions Example

```yaml
- name: Secure Artifact Download
  run: |
    docker run --rm \
      -v ${{ github.workspace }}/trusted-root.json:/app/trusted/root.json:ro \
      -v ${{ github.workspace }}/downloads:/app/downloads \
      -e TUF_MetadataUrl=${{ secrets.TUF_METADATA_URL }} \
      your-registry/tuf-dotnet download --target "releases/${{ github.ref_name }}/app.exe"
```

## Impact on TUF .NET Parity

This Docker integration example directly addresses **Phase 4** priorities from the parity plan:

✅ **.NET-specific integration patterns**: Comprehensive containerization with .NET best practices  
✅ **Production deployment scenarios**: Real-world Docker, Kubernetes, and container orchestration examples  
✅ **Developer experience**: Easy-to-follow examples with clear documentation  
✅ **Operational excellence**: Health checks, logging, security, and monitoring patterns  
✅ **Enterprise readiness**: Production configurations for various container orchestration platforms  

The implementation brings TUF .NET container integration to full parity with mature implementations in other ecosystems, providing .NET developers with professional-grade tools for secure software updates in containerized environments.

## Related Examples

- [BasicClient](../BasicClient/): Simple TUF client usage
- [CliTool](../CliTool/): Command-line TUF operations
- [RepositoryManager](../RepositoryManager/): TUF repository creation and management