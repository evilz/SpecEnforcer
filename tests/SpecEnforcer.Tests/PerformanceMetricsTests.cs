using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace SpecEnforcer.Tests;

public class PerformanceMetricsTests
{
    private readonly string _testSpecPath;

    public PerformanceMetricsTests()
    {
        _testSpecPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-api.yaml");
    }

    [Fact]
    public async Task Metrics_Enabled_TracksRequestValidations()
    {
        // Arrange
        ValidationMetrics? capturedMetrics = null;
        
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
                            options.EnableMetrics = true;
                            options.LogErrors = false;
                        });
                    })
                    .Configure(app =>
                    {
                        capturedMetrics = app.ApplicationServices.GetService<ValidationMetrics>();
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

        // Act - make valid request
        await client.GetAsync("/users");
        await client.GetAsync("/users");

        // Assert
        capturedMetrics.Should().NotBeNull();
        capturedMetrics!.TotalRequestValidations.Should().Be(2);
        capturedMetrics.TotalRequestFailures.Should().Be(0);
        capturedMetrics.AverageRequestValidationTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Metrics_Enabled_TracksRequestFailures()
    {
        // Arrange
        ValidationMetrics? capturedMetrics = null;
        
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
                            options.EnableMetrics = true;
                            options.LogErrors = false;
                        });
                    })
                    .Configure(app =>
                    {
                        capturedMetrics = app.ApplicationServices.GetService<ValidationMetrics>();
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

        // Act - make invalid requests
        await client.GetAsync("/invalid-path");
        await client.GetAsync("/another-invalid");

        // Assert
        capturedMetrics.Should().NotBeNull();
        capturedMetrics!.TotalRequestValidations.Should().Be(2);
        capturedMetrics.TotalRequestFailures.Should().Be(2);
    }

    [Fact]
    public async Task Metrics_Enabled_TracksResponseValidations()
    {
        // Arrange
        ValidationMetrics? capturedMetrics = null;
        
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
                            options.ValidateResponses = true;
                            options.EnableMetrics = true;
                            options.LogErrors = false;
                        });
                    })
                    .Configure(app =>
                    {
                        capturedMetrics = app.ApplicationServices.GetService<ValidationMetrics>();
                        app.UseSpecEnforcer();
                        app.Run(async context =>
                        {
                            context.Response.StatusCode = 200;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync("[]");
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        await client.GetAsync("/users");
        await client.GetAsync("/users");

        // Assert
        capturedMetrics.Should().NotBeNull();
        capturedMetrics!.TotalResponseValidations.Should().Be(2);
        capturedMetrics.TotalResponseFailures.Should().Be(0);
        capturedMetrics.AverageResponseValidationTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Metrics_Disabled_DoesNotTrack()
    {
        // Arrange
        ValidationMetrics? capturedMetrics = null;
        
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
                            options.EnableMetrics = false; // Disabled
                            options.LogErrors = false;
                        });
                    })
                    .Configure(app =>
                    {
                        capturedMetrics = app.ApplicationServices.GetService<ValidationMetrics>();
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

        // Act
        await client.GetAsync("/users");

        // Assert - metrics should still be zero since it's disabled
        capturedMetrics.Should().NotBeNull();
        capturedMetrics!.TotalRequestValidations.Should().Be(0);
    }

    [Fact]
    public void Metrics_Reset_ClearsAllCounters()
    {
        // Arrange
        var metrics = new ValidationMetrics();
        metrics.RecordRequestValidation(10, false);
        metrics.RecordRequestValidation(20, true);
        metrics.RecordResponseValidation(15, false);

        // Act
        metrics.Reset();

        // Assert
        metrics.TotalRequestValidations.Should().Be(0);
        metrics.TotalRequestFailures.Should().Be(0);
        metrics.TotalResponseValidations.Should().Be(0);
        metrics.TotalResponseFailures.Should().Be(0);
        metrics.AverageRequestValidationTimeMs.Should().Be(0);
        metrics.AverageResponseValidationTimeMs.Should().Be(0);
    }
}
