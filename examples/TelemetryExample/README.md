# TUF Telemetry and Monitoring Example

This example demonstrates comprehensive telemetry and monitoring capabilities for TUF .NET operations using OpenTelemetry.

## Features

- **Metrics Collection**: OpenTelemetry metrics for TUF operations including performance, errors, and security events
- **Distributed Tracing**: Activity-based tracing for complex TUF workflows
- **Structured Logging**: High-performance logging with correlation to metrics
- **Production Monitoring**: Enterprise-grade observability for TUF deployments

## Key Metrics Collected

### Performance Metrics
- Operation duration and throughput
- Metadata processing performance
- Signature verification timing
- Network request latencies
- Cache hit/miss ratios

### Security Metrics
- Signature verification success/failure rates
- Security event detection (rollback attacks, integrity failures)
- Authentication and authorization metrics
- Suspicious activity patterns

### Operational Metrics
- Repository health and availability
- Update check success rates
- Error rates by category and type
- Resource utilization patterns

## Running the Example

```bash
# Build and run the telemetry example
dotnet run --project examples/TelemetryExample

# The example will simulate various TUF operations and output metrics
```

## Production Integration

### Metrics Exporters

Replace the console exporter with production-ready exporters:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(TufMetrics.MeterName)
    .AddPrometheusExporter()        // For Prometheus/Grafana
    .AddOtlpExporter()              // For OTEL Collector
    .AddAzureMonitorMetricExporter() // For Azure Monitor
    .Build();
```

### Dashboard Integration

Key metrics for monitoring dashboards:

**TUF Operations Health**
- `tuf_operations_total` - Operation success/failure rates
- `tuf_operation_duration_ms` - Performance trends
- `tuf_errors_total` - Error patterns by category

**Security Monitoring**
- `tuf_security_events_total` - Security event alerts
- `tuf_signature_verifications_total` - Authentication health
- `tuf_metadata_operations_total` - Metadata integrity

**Repository Performance**
- `tuf_network_requests_total` - Repository availability
- `tuf_network_request_duration_ms` - Network performance
- `tuf_metadata_cache_operations_total` - Cache efficiency

### Alerting Rules

Example Prometheus alerting rules:

```yaml
# High error rate alert
- alert: TUFHighErrorRate
  expr: rate(tuf_errors_total[5m]) > 0.1
  for: 2m
  labels:
    severity: warning

# Security event alert
- alert: TUFSecurityEvent
  expr: increase(tuf_security_events_total[1m]) > 0
  for: 0s
  labels:
    severity: critical

# Repository unavailable
- alert: TUFRepositoryDown
  expr: rate(tuf_network_requests_total{request_result="failure"}[5m]) > 0.5
  for: 1m
  labels:
    severity: critical
```

## Tracing Integration

The example also demonstrates distributed tracing with `ActivitySource`:

```csharp
using var activity = TufActivitySource.StartActivity("MyTufOperation");
activity?.SetTag("repository.url", repositoryUrl);
activity?.SetTag("operation.type", "update_check");

// Your TUF operation code here...

activity?.SetStatus(ActivityStatusCode.Ok);
```

## Custom Metrics

Extend the metrics for application-specific monitoring:

```csharp
public static class MyAppTufMetrics
{
    public static readonly Counter<long> AppUpdateChecks = TufMetrics.Instance.CreateCounter<long>(
        "myapp_tuf_update_checks_total",
        "check",
        "Application-specific TUF update checks");
        
    public static void RecordUpdateCheck(string updateType, bool success)
    {
        AppUpdateChecks.Add(1, new("update.type", updateType), new("success", success));
    }
}
```

## Integration with .NET Hosting

For ASP.NET Core or .NET Generic Host applications:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(TufMetrics.MeterName);
        metrics.AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource(TufActivitySource.Name);
        tracing.AddOtlpExporter();
    });
```

This comprehensive telemetry enables production monitoring, performance optimization, and security incident response for TUF deployments.