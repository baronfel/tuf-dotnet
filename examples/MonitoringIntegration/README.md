# TUF .NET Monitoring and Observability Integration

This example demonstrates comprehensive monitoring and observability integration patterns for TUF .NET in production environments. It shows how to integrate TUF operations with modern observability stacks including OpenTelemetry, Prometheus, and structured logging.

## 🎯 What This Example Demonstrates

### Core Observability Patterns
- **Structured Logging**: Security events, operations, and diagnostics with proper context
- **Metrics Collection**: Prometheus-compatible metrics for TUF operations
- **Distributed Tracing**: OpenTelemetry integration for request tracing across services
- **Health Checks**: Service health monitoring and alerting
- **Performance Monitoring**: Operation timing and performance optimization

### Production-Ready Features
- **Security Event Alerting**: Critical security events with proper severity classification
- **Performance Profiling**: Detailed timing metrics for TUF operations
- **Resource Monitoring**: Cache, storage, and network connectivity health checks
- **Observability Integration**: Ready for Prometheus, Grafana, and OpenTelemetry stacks

## 🚀 Quick Start

### Running the Example

```bash
cd examples/MonitoringIntegration
dotnet run
```

### Expected Output

The example will demonstrate:
1. **Metrics Collection**: TUF update checks with timing and counters
2. **Distributed Tracing**: Nested operations with parent-child relationships
3. **Security Monitoring**: Various security events with proper alerting
4. **Health Checks**: Service health validation across multiple dimensions
5. **Performance Analysis**: Operation timing with slow operation detection

## 📊 Metrics and Observability

### Key Metrics Collected

| Metric Name | Type | Description |
|-------------|------|-------------|
| `tuf_updates_checked_total` | Counter | Total number of TUF update checks performed |
| `tuf_security_events_total` | Counter | Security events detected (by type and severity) |
| `tuf_operation_duration_seconds` | Histogram | Duration of TUF operations in seconds |
| `tuf_metadata_refreshes_total` | Counter | Total metadata refresh operations |
| `tuf_cache_hits` | Gauge | Current cache hit/miss statistics |

### Activity Sources and Tracing

- **Activity Source**: `TUF.NET` with version `1.0.0`
- **Trace Operations**: Metadata refresh, signature verification, health checks
- **Trace Context**: Repository URLs, operation types, performance data
- **Error Tracking**: Failed operations with detailed error context

### Security Event Classifications

| Event Type | Severity | Alert Level |
|------------|----------|-------------|
| `rollback_attack_attempt` | Critical | Immediate |
| `signature_verification_failure` | High | High Priority |
| `integrity_check_failure` | High | High Priority |
| `expired_metadata_warning` | Medium | Standard |
| `suspicious_network_activity` | Medium | Standard |

## 🔧 Production Integration

### OpenTelemetry Integration

```csharp
// Configure OpenTelemetry in your startup
services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder.AddSource(TufActivitySource.Name)
               .AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation();
    })
    .WithMetrics(builder =>
    {
        builder.AddMeter("TUF.MonitoringDemo")
               .AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation();
    });
```

### Prometheus Metrics Export

```csharp
// Add Prometheus metrics endpoint
services.AddOpenTelemetry()
    .WithMetrics(builder =>
    {
        builder.AddMeter("TUF.MonitoringDemo")
               .AddPrometheusExporter();
    });

// Configure metrics endpoint
app.MapPrometheusScrapingEndpoint();
```

### Structured Logging Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "TUF": "Debug"
    },
    "Console": {
      "IncludeScopes": true,
      "TimestampFormat": "[yyyy-MM-dd HH:mm:ss.fff] "
    }
  }
}
```

## 📈 Grafana Dashboard Integration

### Key Queries for TUF Monitoring

#### Update Check Rate
```promql
rate(tuf_updates_checked_total[5m])
```

#### Security Event Alert
```promql
increase(tuf_security_events_total{severity="critical"}[5m]) > 0
```

#### Operation Performance (95th percentile)
```promql
histogram_quantile(0.95, rate(tuf_operation_duration_seconds_bucket[5m]))
```

#### Service Health Overview
```promql
up{job="tuf-service"}
```

### Recommended Alerts

1. **Critical Security Events**
   ```promql
   increase(tuf_security_events_total{severity="critical"}[1m]) > 0
   ```

2. **High Operation Latency**
   ```promql
   histogram_quantile(0.95, rate(tuf_operation_duration_seconds_bucket[5m])) > 1.0
   ```

3. **Service Health Degradation**
   ```promql
   up{job="tuf-service"} == 0
   ```

## 🏗️ Architecture and Design

### Monitoring Components

1. **TufMetricsCollector**: Centralized metrics collection for TUF operations
2. **TufHealthCheckService**: Comprehensive health validation across TUF components  
3. **TufMonitoringService**: Background service for continuous monitoring
4. **Activity Sources**: Distributed tracing integration with OpenTelemetry
5. **Structured Logging**: Security-focused event logging with proper context

### Integration Points

- **Prometheus**: Metrics scraping endpoint for operational data
- **OpenTelemetry**: Traces and metrics export to observability backends
- **Application Insights**: Azure-native monitoring integration
- **Grafana**: Visualization dashboards and alerting rules
- **ELK Stack**: Log aggregation and analysis

## 🔒 Security Monitoring Best Practices

### Critical Security Events

1. **Immediate Alerting**: Rollback attacks, signature failures, integrity violations
2. **Forensic Logging**: Detailed context for security incident investigation
3. **Threat Intelligence**: Pattern detection for suspicious activities
4. **Compliance Reporting**: Audit trails for security compliance requirements

### Security Metrics Dashboards

- Real-time security event counts by severity
- Geographic distribution of security events
- Timeline analysis of attack patterns
- Service health correlation with security events

## 🚀 Production Deployment

### Environment Variables

```bash
ASPNETCORE_ENVIRONMENT=Production
TUF_REPOSITORY_URL=https://your-tuf-repo.com
OTEL_EXPORTER_OTLP_ENDPOINT=https://your-otel-collector:4317
PROMETHEUS_METRICS_PORT=8080
```

### Docker Integration

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
COPY bin/Release/net8.0/publish/ /app/
WORKDIR /app
EXPOSE 8080
ENTRYPOINT ["./MonitoringIntegration"]
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tuf-monitoring-service
spec:
  template:
    spec:
      containers:
      - name: tuf-service
        image: tuf-monitoring:latest
        ports:
        - containerPort: 8080
          name: metrics
        env:
        - name: OTEL_EXPORTER_OTLP_ENDPOINT
          value: "http://otel-collector:4317"
```

## 🔍 Troubleshooting

### Common Issues

1. **Missing Metrics**: Verify OpenTelemetry configuration and meter registration
2. **Trace Gaps**: Check activity source registration and propagation
3. **Log Correlation**: Ensure structured logging includes correlation IDs
4. **Performance Overhead**: Monitor observability system resource usage

### Debug Commands

```bash
# Check metrics endpoint
curl http://localhost:8080/metrics

# Verify tracing
curl http://localhost:8080/health

# Test logging configuration
dotnet run --verbosity detailed
```

## 🎯 Next Steps

### Advanced Integration Scenarios

1. **Custom Metrics**: Add business-specific TUF metrics for your use case
2. **Alert Rules**: Configure alerting rules based on your SLA requirements
3. **Dashboard Customization**: Create organization-specific monitoring dashboards
4. **Integration Testing**: Add monitoring validation to your CI/CD pipeline

### Extending the Example

- Add custom metrics for your specific TUF operations
- Integrate with your existing observability infrastructure
- Create custom health checks for your environment
- Add correlation with business metrics and user experience data

---

This example provides a comprehensive foundation for integrating TUF .NET with modern observability stacks. It demonstrates production-ready patterns that can be adapted to your specific monitoring and alerting requirements.