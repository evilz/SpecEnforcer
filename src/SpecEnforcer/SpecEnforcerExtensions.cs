using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
        services.AddSingleton<ValidationMetrics>();
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

    /// <summary>
    /// Maps a metrics endpoint that returns validation statistics.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern for the metrics endpoint. Default is "/spec-enforcer/metrics".</param>
    /// <returns>The endpoint convention builder for chaining.</returns>
    public static IEndpointConventionBuilder MapSpecEnforcerMetrics(
        this IEndpointRouteBuilder endpoints, 
        string pattern = "/spec-enforcer/metrics")
    {
        return endpoints.MapGet(pattern, (ValidationMetrics metrics) =>
        {
            return Results.Ok(new
            {
                requestValidations = new
                {
                    total = metrics.TotalRequestValidations,
                    failures = metrics.TotalRequestFailures,
                    averageTimeMs = metrics.AverageRequestValidationTimeMs
                },
                responseValidations = new
                {
                    total = metrics.TotalResponseValidations,
                    failures = metrics.TotalResponseFailures,
                    averageTimeMs = metrics.AverageResponseValidationTimeMs
                }
            });
        });
    }
}
