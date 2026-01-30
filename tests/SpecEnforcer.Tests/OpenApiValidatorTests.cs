using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace SpecEnforcer.Tests;

public class OpenApiValidatorTests
{
    private readonly string _testSpecPath;
    private readonly ILogger<OpenApiValidator> _logger;

    public OpenApiValidatorTests()
    {
        _testSpecPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-api.yaml");
        
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<OpenApiValidator>();
    }

    [Fact]
    public void ValidateRequest_ValidGetRequest_ReturnsNull()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);

        // Act
        var error = validator.ValidateRequest("GET", "/users", null, null);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateRequest_ValidPostRequestWithBody_ReturnsNull()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var body = "{\"name\":\"John\",\"email\":\"john@example.com\"}";

        // Act
        var error = validator.ValidateRequest("POST", "/users", "application/json", body);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateRequest_InvalidPath_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);

        // Act
        var error = validator.ValidateRequest("GET", "/invalid-path", null, null);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationType.Should().Be("Request");
        error.Message.Should().Contain("not found in OpenAPI specification");
    }

    [Fact]
    public void ValidateRequest_InvalidMethod_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);

        // Act
        var error = validator.ValidateRequest("PUT", "/users", null, null);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationType.Should().Be("Request");
        error.Message.Should().Contain("not allowed");
    }

    [Fact]
    public void ValidateRequest_MissingRequiredBody_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);

        // Act
        var error = validator.ValidateRequest("POST", "/users", null, null);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationType.Should().Be("Request");
        error.Message.Should().Contain("required");
    }

    [Fact]
    public void ValidateRequest_InvalidJsonBody_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var invalidJson = "{invalid json";

        // Act
        var error = validator.ValidateRequest("POST", "/users", "application/json", invalidJson);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationType.Should().Be("Request");
        error.Message.Should().Contain("Invalid JSON");
    }

    [Fact]
    public void ValidateRequest_PathWithParameter_ReturnsNull()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);

        // Act
        var error = validator.ValidateRequest("GET", "/users/123", null, null);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateResponse_ValidResponse_ReturnsNull()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var body = "[]";

        // Act
        var error = validator.ValidateResponse("GET", "/users", 200, "application/json", body);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateResponse_InvalidStatusCode_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);

        // Act
        var error = validator.ValidateResponse("GET", "/users", 500, "application/json", null);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationType.Should().Be("Response");
        error.StatusCode.Should().Be(500);
        error.Message.Should().Contain("not defined");
    }

    [Fact]
    public void ValidateResponse_ValidStatusCodeWithBody_ReturnsNull()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var body = "{\"id\":1,\"name\":\"John\",\"email\":\"john@example.com\"}";

        // Act
        var error = validator.ValidateResponse("POST", "/users", 201, "application/json", body);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateResponse_InvalidJsonInResponse_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var invalidJson = "{invalid}";

        // Act
        var error = validator.ValidateResponse("GET", "/users", 200, "application/json", invalidJson);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationType.Should().Be("Response");
        error.Message.Should().Contain("Invalid JSON");
    }
}
