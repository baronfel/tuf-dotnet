using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspNetCoreIntegration;

/// <summary>
/// Extension methods for registering TUF validation middleware.
/// </summary>
public static class TufMiddlewareExtensions
{
    /// <summary>
    /// Adds TUF validation services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddTufValidation(
        this IServiceCollection services,
        Action<TufValidationOptions>? configure = null)
    {
        services.Configure<TufValidationOptions>(options =>
        {
            configure?.Invoke(options);
        });
        
        services.AddSingleton<TufValidationOptions>(provider =>
            provider.GetRequiredService<IOptions<TufValidationOptions>>().Value);
            
        return services;
    }

    /// <summary>
    /// Adds TUF validation middleware to the ASP.NET Core pipeline.
    /// </summary>
    public static IApplicationBuilder UseTufValidation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TufValidationMiddleware>();
    }
    
    /// <summary>
    /// Adds TUF validation middleware with specific options to the ASP.NET Core pipeline.
    /// </summary>
    public static IApplicationBuilder UseTufValidation(
        this IApplicationBuilder app, 
        TufValidationOptions options)
    {
        return app.UseMiddleware<TufValidationMiddleware>(options);
    }
}