using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.AspNetCore.ResponseCompression;

namespace ProductionTufServer;

/// <summary>
/// Production-ready TUF metadata and targets repository server.
/// 
/// Features:
/// - Secure HTTPS-only operation
/// - Response compression for better performance
/// - Proper CORS headers for cross-origin access
/// - Health checks for load balancer integration
/// - Configurable metadata and targets directories
/// - Production logging and monitoring
/// - Security headers and best practices
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure services for production
        ConfigureServices(builder.Services, builder.Configuration);
        ConfigureLogging(builder.Logging);

        var app = builder.Build();

        // Configure middleware pipeline for production
        ConfigureMiddleware(app);
        ConfigureRoutes(app);

        app.Run();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Response compression for better performance
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ["application/json", "text/plain"];
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        // Health checks for load balancer integration
        services.AddHealthChecks()
            .AddCheck("metadata_directory", () =>
            {
                var metadataPath = configuration["TUF_METADATA_PATH"] ?? "metadata";
                return Directory.Exists(metadataPath)
                    ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Metadata directory accessible")
                    : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Metadata directory not found");
            })
            .AddCheck("targets_directory", () =>
            {
                var targetsPath = configuration["TUF_TARGETS_PATH"] ?? "targets";
                return Directory.Exists(targetsPath)
                    ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Targets directory accessible")
                    : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Targets directory not found");
            });

        // CORS for browser-based TUF clients
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var allowedOrigins = configuration.GetSection("CORS:AllowedOrigins").Get<string[]>() ?? ["*"];

                if (allowedOrigins.Contains("*"))
                {
                    policy.AllowAnyOrigin();
                }
                else
                {
                    policy.WithOrigins(allowedOrigins);
                }

                policy.WithMethods("GET", "HEAD")
                      .WithHeaders("Accept", "Accept-Encoding", "User-Agent");
            });
        });
    }

    private static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        // Security headers
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Cache control for TUF metadata
            var path = context.Request.Path.Value?.ToLowerInvariant();
            if (path?.EndsWith(".json") == true)
            {
                if (path.Contains("timestamp"))
                {
                    // Timestamp metadata should not be cached (freshness critical)
                    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                    context.Response.Headers["Pragma"] = "no-cache";
                    context.Response.Headers["Expires"] = "0";
                }
                else
                {
                    // Other metadata can be cached briefly
                    context.Response.Headers["Cache-Control"] = "public, max-age=300"; // 5 minutes
                }
            }

            await next.Invoke();
        });

        // HTTPS redirection for production security
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseResponseCompression();
        app.UseCors();

        // Health checks endpoint
        app.UseHealthChecks("/health");
        app.UseHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Name != "self"
        });
    }

    private static void ConfigureRoutes(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var configuration = app.Services.GetRequiredService<IConfiguration>();

        var metadataPath = configuration["TUF_METADATA_PATH"] ?? "metadata";
        var targetsPath = configuration["TUF_TARGETS_PATH"] ?? "targets";

        logger.LogInformation("🚀 Starting Production TUF Server");
        logger.LogInformation("📁 Metadata directory: {MetadataPath}", Path.GetFullPath(metadataPath));
        logger.LogInformation("🎯 Targets directory: {TargetsPath}", Path.GetFullPath(targetsPath));

        // Root endpoint with server information
        app.MapGet("/", () => new
        {
            Service = "Production TUF Repository Server",
            Version = "1.0.0",
            Endpoints = new
            {
                Metadata = "/metadata/{role}.json",
                Targets = "/targets/{filename}",
                Health = "/health",
                Ready = "/ready"
            },
            TufSpecification = "https://theupdateframework.io/specification/",
            Implementation = "TUF .NET"
        });

        // TUF metadata endpoints
        app.MapGet("/metadata/{role}.json", async (string role, ILogger<Program> requestLogger) =>
        {
            try
            {
                var filePath = Path.Combine(metadataPath, $"{role}.json");

                if (!File.Exists(filePath))
                {
                    requestLogger.LogWarning("📋 Metadata file not found: {Role}", role);
                    return Results.NotFound(new { Error = "Metadata not found", Role = role });
                }

                var content = await File.ReadAllTextAsync(filePath);

                // Validate JSON structure
                try
                {
                    JsonDocument.Parse(content);
                }
                catch (JsonException)
                {
                    requestLogger.LogError("📋 Invalid JSON in metadata file: {Role}", role);
                    return Results.Problem("Invalid metadata format", statusCode: 500);
                }

                requestLogger.LogInformation("📋 Served metadata: {Role} ({Size} bytes)", role, content.Length);

                return Results.Content(content, "application/json");
            }
            catch (Exception ex)
            {
                requestLogger.LogError(ex, "📋 Error serving metadata: {Role}", role);
                return Results.Problem("Internal server error", statusCode: 500);
            }
        });

        // TUF targets endpoints with proper content type detection
        app.MapGet("/targets/{**filename}", async (string filename, ILogger<Program> requestLogger) =>
        {
            try
            {
                // Security: Prevent directory traversal attacks
                if (filename.Contains("..") || filename.Contains("\\") || Path.IsPathRooted(filename))
                {
                    requestLogger.LogWarning("🎯 Rejected potentially malicious filename: {Filename}", filename);
                    return Results.BadRequest(new { Error = "Invalid filename" });
                }

                var filePath = Path.Combine(targetsPath, filename);

                if (!File.Exists(filePath))
                {
                    requestLogger.LogWarning("🎯 Target file not found: {Filename}", filename);
                    return Results.NotFound(new { Error = "Target not found", Filename = filename });
                }

                var fileInfo = new FileInfo(filePath);
                requestLogger.LogInformation("🎯 Serving target: {Filename} ({Size} bytes)", filename, fileInfo.Length);

                // Determine content type based on file extension
                var contentType = GetContentType(filename);

                // Add integrity metadata if available
                var checksumPath = filePath + ".sha256";
                if (File.Exists(checksumPath))
                {
                    var checksum = (await File.ReadAllTextAsync(checksumPath)).Trim();
                    requestLogger.LogDebug("🔍 Target checksum: {Filename} -> {Checksum}", filename, checksum);
                }

                return Results.File(filePath, contentType);
            }
            catch (Exception ex)
            {
                requestLogger.LogError(ex, "🎯 Error serving target: {Filename}", filename);
                return Results.Problem("Internal server error", statusCode: 500);
            }
        });

        // Server metrics and status (useful for monitoring)
        app.MapGet("/metrics", (IConfiguration config) => new
        {
            Timestamp = DateTimeOffset.UtcNow,
            MetadataPath = config["TUF_METADATA_PATH"] ?? "metadata",
            TargetsPath = config["TUF_TARGETS_PATH"] ?? "targets",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            Version = Environment.Version.ToString(),
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
            WorkingSet = Environment.WorkingSet
        });

        logger.LogInformation("✅ TUF Repository Server configured and ready");
    }

    private static string GetContentType(string filename)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        return extension switch
        {
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            ".exe" => "application/octet-stream",
            ".dll" => "application/octet-stream",
            ".so" => "application/octet-stream",
            ".dylib" => "application/octet-stream",
            _ => "application/octet-stream"
        };
    }
}