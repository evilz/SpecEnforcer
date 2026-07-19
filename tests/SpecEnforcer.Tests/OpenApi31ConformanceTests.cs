using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace SpecEnforcer.Tests;

public class OpenApi31ConformanceTests
{
    private static OpenApiValidator CreateValidator() => new(
        Path.Combine(AppContext.BaseDirectory, "TestData", "openapi-3.1-api.yaml"),
        NullLogger<OpenApiValidator>.Instance);

    [Fact]
    public void OpenApi31_ValidRequestAndResponse_AreAccepted()
    {
        var validator = CreateValidator();

        var requestError = validator.ValidateRequest(
            "POST", "/widgets/wid-42", "application/json", """{"name":"terminal"}""");
        var responseError = validator.ValidateResponse(
            "POST", "/widgets/wid-42", 201, "application/json", """{"id":"wid-42","name":"terminal"}""");

        requestError.Should().BeNull();
        responseError.Should().BeNull();
    }

    [Fact]
    public void OpenApi31_PathParameterPattern_IsEnforced()
    {
        var error = CreateValidator().ValidateRequest(
            "POST", "/widgets/not-a-widget", "application/json", """{"name":"terminal"}""");

        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(value => value.Contains("widgetId"));
    }

    [Fact]
    public void OpenApi31_RequiredRequestProperty_IsEnforced()
    {
        var error = CreateValidator().ValidateRequest(
            "POST", "/widgets/wid-42", "application/json", "{}");

        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(value => value.Contains("name"));
    }

    [Fact]
    public void OpenApi31_RequiredResponseProperty_IsEnforced()
    {
        var error = CreateValidator().ValidateResponse(
            "POST", "/widgets/wid-42", 201, "application/json", """{"id":"wid-42"}""");

        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(value => value.Contains("name"));
    }
}
