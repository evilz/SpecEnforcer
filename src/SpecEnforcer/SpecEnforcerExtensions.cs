using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SpecEnforcer;

/// <summary>
/// Extension methods for configuring SpecEnforcer middleware.
/// </summary>
public static class SpecEnforcerExtensions
{
    /// <summary>
    /// Adds SpecEnforcer services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure SpecEnforcer options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSpecEnforcer(
        this IServiceCollection services,
        Action<SpecEnforcerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services;
    }

    /// <summary>
    /// Adds SpecEnforcer middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseSpecEnforcer(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SpecEnforcerMiddleware>();
    }
}
