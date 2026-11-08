using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace SpecEnforcer.Tests;

public class OpenApi31ConformanceTests
{
    private static void WithSpec(string content, Action<string> action)
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, content); action(path); }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("openapi: '3.1.0' # version\ninfo: { title: Test, version: '1' }\npaths: {}")]
    [InlineData("{\"openapi\":\"3.1.0\",\"info\":{\"title\":\"Test\",\"version\":\"1\"},\"paths\":{}}")]
    public void OpenApi31_ValidJsonAndQuotedYaml_AreLoaded(string spec)
    {
        WithSpec(spec, path => new OpenApiValidator(path, NullLogger<OpenApiValidator>.Instance));
    }

    [Theory]
    [InlineData("const: 1")]
    [InlineData("type: [string, 'null']")]
    [InlineData("additionalProperties: false")]
    [InlineData("$ref: '#/components/schemas/Foo'")]
    public void OpenApi31_UnsupportedSchema_FailsClosed(string constraint)
    {
        var spec = $"openapi: 3.1.0\ninfo: {{ title: Test, version: '1' }}\npaths: {{}}\ncomponents:\n  schemas:\n    Foo:\n      {constraint}\n";
        WithSpec(spec, path =>
            FluentActions.Invoking(() => new OpenApiValidator(path, NullLogger<OpenApiValidator>.Instance))
                .Should().Throw<InvalidOperationException>().WithMessage("*OpenAPI 3.1*"));
    }

    [Fact]
    public void OpenApi31_CustomDialect_FailsClosed()
    {
        const string spec = "openapi: 3.1.0\ninfo: { title: Test, version: '1' }\npaths: {}\njsonSchemaDialect: https://example.com/custom";
        WithSpec(spec, path =>
            FluentActions.Invoking(() => new OpenApiValidator(path, NullLogger<OpenApiValidator>.Instance))
                .Should().Throw<InvalidOperationException>().WithMessage("*dialect*"));
    }

    [Fact]
    public void OpenApi31_NestedUnsupportedConstraint_FailsClosed()
    {
        const string spec = """
            openapi: 3.1.0
            info: { title: Test, version: '1' }
            paths:
              /items:
                post:
                  requestBody:
                    content:
                      application/json:
                        schema:
                          type: object
                          properties:
                            state: { type: string, const: ready }
                  responses: { '200': { description: OK } }
            """;
        WithSpec(spec, path =>
            FluentActions.Invoking(() => new OpenApiValidator(path, NullLogger<OpenApiValidator>.Instance))
                .Should().Throw<InvalidOperationException>().WithMessage("*const*"));
    }

    [Fact]
    public void OpenApi31_YamlAliases_FailClosed()
    {
        const string spec = "openapi: 3.1.0\ninfo:\n  title: &name Test\n  version: '1'\npaths: {}\nx-copy: *name";
        WithSpec(spec, path =>
            FluentActions.Invoking(() => new OpenApiValidator(path, NullLogger<OpenApiValidator>.Instance))
                .Should().Throw<InvalidOperationException>().WithMessage("*aliases*"));
    }

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
