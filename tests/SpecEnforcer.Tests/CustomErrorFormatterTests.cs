using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;
using Xunit;

namespace SpecEnforcer.Tests;

public class CustomErrorFormatterTests
{
    private readonly string _testSpecPath;

    public CustomErrorFormatterTests()
    {
        _testSpecPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-api.yaml");
    }

    [Fact]
    public async Task HardMode_WithCustomErrorFormatter_ReturnsFormattedError()
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
                            options.HardModeStatusCode = 400;
                            options.CustomErrorFormatter = (error) => new
                            {
                                message = $"Validation failed: {error.Message}",
                                code = "VALIDATION_ERROR",
                                errorType = error.ValidationType
                            };
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

        // Act - send request to invalid path
        var response = await client.GetAsync("/invalid-path");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        
        json.RootElement.GetProperty("message").GetString().Should().Contain("Validation failed");
        json.RootElement.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        json.RootElement.GetProperty("errorType").GetString().Should().Be("Request");
    }

    [Fact]
    public async Task HardMode_WithoutCustomErrorFormatter_ReturnsDefaultError()
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
                            options.HardModeStatusCode = 400;
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

        // Act
        var response = await client.GetAsync("/invalid-path");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        
        json.RootElement.GetProperty("error").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("validationType").GetString().Should().Be("Request");
        json.RootElement.GetProperty("method").GetString().Should().Be("GET");
    }
}
