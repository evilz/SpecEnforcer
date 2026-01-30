using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using Xunit;

namespace SpecEnforcer.Tests;

public class PathExclusionTests
{
    private readonly string _testSpecPath;

    public PathExclusionTests()
    {
        _testSpecPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-api.yaml");
    }

    [Fact]
    public async Task ExcludedPath_ExactMatch_SkipsValidation()
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddSpecEnforcer(options =>
                        {
                            options.OpenApiSpecPath = _testSpecPath;
                            options.ValidateRequests = true;
                            options.HardMode = true;
                            options.ExcludedPaths = new List<string> { "/health", "/metrics" };
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseSpecEnforcer();
                        app.Run(async context =>
                        {
                            context.Response.StatusCode = 200;
                            await context.Response.WriteAsync("OK");
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act - these paths are not in the OpenAPI spec but are excluded
        var healthResponse = await client.GetAsync("/health");
        var metricsResponse = await client.GetAsync("/metrics");

        // Assert - should pass through without validation error
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExcludedPath_WildcardMatch_SkipsValidation()
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddSpecEnforcer(options =>
                        {
                            options.OpenApiSpecPath = _testSpecPath;
                            options.ValidateRequests = true;
                            options.HardMode = true;
                            options.ExcludedPaths = new List<string> { "/api/internal/*", "/admin/*" };
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseSpecEnforcer();
                        app.Run(async context =>
                        {
                            context.Response.StatusCode = 200;
                            await context.Response.WriteAsync("OK");
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act - these paths match wildcard patterns
        var internalResponse = await client.GetAsync("/api/internal/debug");
        var adminResponse = await client.GetAsync("/admin/dashboard");

        // Assert - should pass through without validation error
        internalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonExcludedPath_StillValidates()
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddSpecEnforcer(options =>
                        {
                            options.OpenApiSpecPath = _testSpecPath;
                            options.ValidateRequests = true;
                            options.HardMode = true;
                            options.ExcludedPaths = new List<string> { "/health" };
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseSpecEnforcer();
                        app.Run(async context =>
                        {
                            context.Response.StatusCode = 200;
                            await context.Response.WriteAsync("OK");
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act - this path is NOT excluded and not in spec
        var response = await client.GetAsync("/invalid-path");

        // Assert - should fail validation
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExcludedPath_EmptyList_ValidatesAll()
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddSpecEnforcer(options =>
                        {
                            options.OpenApiSpecPath = _testSpecPath;
                            options.ValidateRequests = true;
                            options.HardMode = true;
                            options.ExcludedPaths = new List<string>();
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseSpecEnforcer();
                        app.Run(async context =>
                        {
                            context.Response.StatusCode = 200;
                            await context.Response.WriteAsync("OK");
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act - invalid path with no exclusions
        var response = await client.GetAsync("/invalid-path");

        // Assert - should fail validation
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
