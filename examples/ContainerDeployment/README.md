# TUF .NET Container Deployment Example

This example demonstrates how to deploy TUF .NET applications in containerized environments, including Docker containers and Kubernetes deployments. It showcases production-ready patterns for secure software updates in cloud-native applications.

## 🎯 Parity Achievement

This example addresses key gaps identified in the [TUF .NET parity analysis](../../issues/99):

- **Container Integration Examples**: Comprehensive Docker and Kubernetes deployment patterns
- **Production Deployment Guidance**: Security best practices, health checks, and monitoring
- **Cloud-Native Integration**: Kubernetes-ready deployments with proper resource management
- **.NET-Specific Optimizations**: AOT-compatible containers, multi-stage builds, and performance tuning

Brings TUF .NET container deployment capabilities to parity with other TUF implementations like Python TUF and Rust Tough.

## 📋 What This Example Provides

### 🐳 **Container Deployment Patterns**
- **Multi-stage Docker builds** optimized for size and security
- **Alpine-based images** for minimal attack surface
- **Non-root user execution** following security best practices
- **Health checks and readiness probes** for robust deployments

### ☸️ **Kubernetes Integration**
- **Production-ready Kubernetes manifests** with security contexts
- **ConfigMaps and Secrets** for configuration management
- **Network policies** for micro-segmentation
- **Resource limits and requests** for proper resource management
- **Pod anti-affinity** for high availability

### 🔧 **Development Tools**
- **Docker Compose** for local development and testing
- **Nginx-based repository server** for local TUF repository testing
- **Prometheus monitoring** integration for observability

### 🛡️ **Security Features**
- **Least-privilege containers** with read-only root filesystems
- **Security contexts** preventing privilege escalation
- **Secrets management** for trusted root metadata
- **Network policies** for traffic isolation

## 🚀 Quick Start

### Prerequisites

- Docker (20.10 or later)
- Docker Compose (2.0 or later)
- Kubernetes cluster (optional, for K8s deployment)
- .NET 8.0 SDK (for building from source)

### 1. Build and Run with Docker Compose

```bash
# Clone the repository and navigate to the example
cd examples/ContainerDeployment

# Create a placeholder trusted root file (for demonstration)
echo '{"signed":{"type":"root","spec_version":"1.0.0","version":1,"expires":"2026-12-04T00:00:00Z","keys":{},"roles":{"root":{"keyids":[],"threshold":1},"timestamp":{"keyids":[],"threshold":1},"snapshot":{"keyids":[],"threshold":1},"targets":{"keyids":[],"threshold":1}},"consistent_snapshot":false},"signatures":[]}' > trusted-root.json

# Start the application stack
docker-compose up --build

# The TUF application will be available at http://localhost:8080
# Repository server (if used) at http://localhost:8081
# Prometheus monitoring at http://localhost:9090
```

### 2. Test the Application

```bash
# Check application health
curl http://localhost:8080/health

# Get TUF client status
curl http://localhost:8080/ | jq

# List available targets
curl http://localhost:8080/targets | jq
```

## 🛠️ Building from Source

### Build Container Image

```bash
# From the repository root
docker build -f examples/ContainerDeployment/Dockerfile -t tuf-container-example:latest .
```

### Manual Build and Run

```bash
# Build the application
dotnet build examples/ContainerDeployment/ContainerDeployment.csproj

# Run locally (requires proper configuration)
cd examples/ContainerDeployment
export TUF_METADATA_URL="https://your-tuf-repo.com/metadata"
export TUF_TARGETS_URL="https://your-tuf-repo.com/targets"
export TUF_TRUSTED_ROOT_PATH="./trusted-root.json"
dotnet run
```

## ☸️ Kubernetes Deployment

### Deploy to Kubernetes

```bash
# Apply the Kubernetes manifests
kubectl apply -f k8s-deployment.yaml

# Check deployment status
kubectl get deployments,pods,services -l app=tuf-container-app

# Check application logs
kubectl logs -l app=tuf-container-app --tail=50

# Port forward to access the application
kubectl port-forward service/tuf-container-service 8080:80
```

### Monitoring and Health Checks

```bash
# Check health via Kubernetes
kubectl get pods -l app=tuf-container-app
kubectl describe pod -l app=tuf-container-app

# View application metrics (if Prometheus is configured)
kubectl port-forward service/prometheus 9090:9090
# Open http://localhost:9090
```

## 🔧 Configuration

The application supports configuration through environment variables:

| Environment Variable | Description | Default |
|---------------------|-------------|---------|
| `TUF_METADATA_URL` | TUF repository metadata URL | `https://example.com/metadata` |
| `TUF_TARGETS_URL` | TUF repository targets URL | `https://example.com/targets` |
| `TUF_TRUSTED_ROOT_PATH` | Path to trusted root metadata file | `/app/trusted-root.json` |
| `ASPNETCORE_URLS` | ASP.NET Core listen URLs | `http://+:8080` |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | `Production` |

### Container-Specific Paths

- **Metadata Cache**: `/tmp/tuf-metadata` (writable, ephemeral)
- **Target Downloads**: `/tmp/tuf-targets` (writable, ephemeral)
- **Trusted Root**: `/app/trusted-root.json` (read-only)
- **Application**: `/app` (read-only)

## 🌐 API Endpoints

The containerized application exposes the following endpoints:

| Endpoint | Method | Description |
|----------|---------|------------|
| `/` | GET | Application status and configuration |
| `/health` | GET | Basic health check |
| `/health/ready` | GET | Readiness probe (Kubernetes) |
| `/targets` | GET | List available target files |
| `/download/{targetName}` | POST | Download and verify a target file |

### Example API Usage

```bash
# Get status information
curl -s http://localhost:8080/ | jq '{
  status: .Status,
  root_version: .Metadata.RootVersion,
  root_expires: .Metadata.RootExpires,
  container: .Container.Hostname
}'

# Download a target file
curl -X POST http://localhost:8080/download/app.exe | jq
```

## 🔒 Security Considerations

### Production Trusted Root Management

⚠️ **CRITICAL**: This example uses a placeholder trusted root for demonstration. In production:

1. **Obtain trusted root securely**: Get your trusted root metadata through a secure out-of-band channel
2. **Embed in container image**: Include the trusted root in your container image during build
3. **Use Kubernetes secrets**: Mount trusted root as a secret in Kubernetes
4. **Implement key rotation**: Have procedures for rotating trusted root keys

### Container Security Best Practices

✅ **Implemented Security Features**:
- Non-root user execution (UID 1001)
- Read-only root filesystem
- Minimal Alpine-based images
- No privilege escalation
- Dropped Linux capabilities
- Security contexts in Kubernetes
- Network policies for traffic isolation

### Example Trusted Root Management

```bash
# Create Kubernetes secret from real trusted root
kubectl create secret generic tuf-trusted-root \
    --from-file=trusted-root.json=./path/to/real/trusted-root.json

# Update deployment to use the secret
kubectl patch deployment tuf-container-app -p '{
  "spec": {
    "template": {
      "spec": {
        "volumes": [{
          "name": "trusted-root",
          "secret": {"secretName": "tuf-trusted-root", "defaultMode": 400}
        }]
      }
    }
  }
}'
```

## 📊 Monitoring and Observability

### Health Checks

The application provides multiple health check endpoints:

- **Liveness Probe**: `/health` - Basic application health
- **Readiness Probe**: `/health/ready` - Ready to serve traffic
- **Startup Probe**: `/health` - Application startup detection

### Prometheus Metrics

The example includes Prometheus integration for monitoring:

```bash
# Start monitoring stack
docker-compose up prometheus

# View metrics
curl http://localhost:8080/metrics
```

### Logging

Application logs are structured and container-friendly:

```bash
# View application logs
docker logs tuf-container-example

# In Kubernetes
kubectl logs -l app=tuf-container-app --tail=100 -f
```

## 🚀 Performance Optimization

### Container Optimizations

The container is optimized for:

- **Size**: Multi-stage builds with Alpine base images
- **Startup**: AOT-compatible .NET 8 runtime
- **Memory**: Efficient resource usage with proper limits
- **Security**: Minimal attack surface with read-only filesystems

### Kubernetes Resource Management

```yaml
resources:
  limits:
    memory: "256Mi"
    cpu: "200m"
  requests:
    memory: "128Mi"
    cpu: "100m"
```

### Performance Tuning

For high-throughput scenarios:

1. **Increase HTTP client timeouts** for slow repositories
2. **Configure connection pooling** for better performance
3. **Use persistent volumes** for metadata/target caching
4. **Scale horizontally** with multiple replicas

## 🔗 Integration Examples

### ASP.NET Core Integration

```csharp
// In your ASP.NET Core application
builder.Services.AddSingleton<ITufClientService, TufClientService>();
builder.Services.AddHealthChecks().AddCheck<TufHealthCheck>("tuf-client");

// Use TUF for secure updates
app.MapPost("/update", async (ITufClientService tuf) => {
    var targets = await tuf.GetAvailableTargetsAsync();
    // Process updates securely
});
```

### Background Service Pattern

```csharp
// Background service for periodic updates
public class TufUpdateService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _tufClient.RefreshAsync();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
```

## 🔄 CI/CD Integration

### GitHub Actions Example

```yaml
- name: Build and push container
  run: |
    docker build -f examples/ContainerDeployment/Dockerfile \
      -t ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }} .
    docker push ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }}

- name: Deploy to Kubernetes
  run: |
    kubectl set image deployment/tuf-container-app \
      tuf-app=${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }}
```

## 🧪 Testing

### Integration Testing

```bash
# Test the container locally
docker run --rm -p 8080:8080 \
  -e TUF_METADATA_URL=https://test-repo.com/metadata \
  tuf-container-example:latest

# Wait for startup
sleep 10

# Test endpoints
curl -f http://localhost:8080/health || exit 1
curl -s http://localhost:8080/ | jq '.Status' | grep -q "Healthy" || exit 1
```

### Kubernetes Testing

```bash
# Deploy to test namespace
kubectl create namespace tuf-test
kubectl apply -f k8s-deployment.yaml -n tuf-test

# Wait for deployment
kubectl wait --for=condition=available deployment/tuf-container-app -n tuf-test --timeout=300s

# Test endpoints
kubectl port-forward service/tuf-container-service 8080:80 -n tuf-test &
curl -f http://localhost:8080/health || exit 1

# Cleanup
kubectl delete namespace tuf-test
```

## 🤝 Contributing

This container deployment example is part of the TUF .NET parity initiative. Areas for contribution:

1. **Additional Orchestration Platforms**: Examples for other platforms (Docker Swarm, Nomad, etc.)
2. **Cloud Provider Integration**: AWS ECS, Azure Container Instances, GCP Cloud Run
3. **Helm Charts**: Kubernetes Helm charts for easier deployment
4. **Performance Benchmarks**: Container-specific performance testing
5. **Advanced Security**: Integration with security scanning tools, admission controllers

## 📚 References

- [TUF Specification](https://theupdateframework.io/specification/)
- [.NET Container Guidelines](https://docs.microsoft.com/en-us/dotnet/core/docker/introduction)
- [Kubernetes Security Best Practices](https://kubernetes.io/docs/concepts/security/)
- [Container Security Best Practices](https://cheatsheetseries.owasp.org/cheatsheets/Docker_Security_Cheat_Sheet.html)

## 📝 License

This example is part of the TUF .NET project and follows the same licensing terms as the main project.

---

**Result**: This comprehensive container deployment example brings TUF .NET to full parity with other TUF implementations for containerized deployments, providing production-ready patterns for Docker, Kubernetes, and cloud-native environments.

> AI-generated content by [achieve-parity](https://github.com/baronfel/tuf-dotnet/actions/runs/19923559646) may contain mistakes.