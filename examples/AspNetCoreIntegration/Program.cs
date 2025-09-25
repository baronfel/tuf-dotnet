using AspNetCoreIntegration;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Configure TUF validation
builder.Services.AddTufValidation(options =>
{
    options.ValidatedPaths.Add("/api/packages");
    options.ValidatedPaths.Add("/secure");
    options.MetadataPath = "/tmp/tuf-metadata";
    options.FailOpen = false;
    options.ValidationTimeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseLogging();

// Add TUF validation middleware early in the pipeline
app.UseTufValidation();

// Example API endpoints
app.MapGet("/", () => "TUF ASP.NET Core Integration Demo");

app.MapGet("/api/packages/{id}", (string id) =>
{
    return Results.Ok(new { PackageId = id, Status = "Validated by TUF" });
});

app.MapGet("/secure/data", () =>
{
    return Results.Ok(new { Message = "This endpoint is protected by TUF validation" });
});

app.MapGet("/public/info", () =>
{
    return Results.Ok(new { Message = "This endpoint bypasses TUF validation" });
});

// Health check endpoint for monitoring
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", TufEnabled = true }));

// Demo endpoint to simulate TUF validation success
app.MapGet("/api/packages/demo", (HttpContext context) =>
{
    // This endpoint demonstrates successful TUF validation
    // The middleware will check for "tuf-validated" query parameter
    return Results.Ok(new { 
        PackageId = "demo-package", 
        Version = "1.0.0",
        TufValidated = context.Request.Query.ContainsKey("tuf-validated"),
        Message = "Package validated using TUF metadata"
    });
});

app.Run();

/// <summary>
/// Middleware to log all requests for demonstration purposes.
/// </summary>
public static class LoggingExtensions
{
    public static IApplicationBuilder UseLogging(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Request: {Method} {Path}", 
                context.Request.Method, context.Request.Path);
            
            await next();
            
            logger.LogInformation("Response: {StatusCode}", context.Response.StatusCode);
        });
    }
}