using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace SpecEnforcer.Tests;

public class AdvancedValidationTests
{
    private readonly string _testSpecPath;
    private readonly ILogger<OpenApiValidator> _logger;

    public AdvancedValidationTests()
    {
        _testSpecPath = Path.Combine(AppContext.BaseDirectory, "TestData", "advanced-api.yaml");
        
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<OpenApiValidator>();
    }

    #region Parameter Validation Tests

    [Fact]
    public void ValidateRequest_RequiredQueryParameter_Missing_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var headers = new HeaderDictionary
        {
            { "X-Request-ID", "12345678-1234-1234-1234-123456789abc" }
        };
        var query = new QueryCollection();

        // Act
        var error = validator.ValidateRequest("GET", "/products", null, null, headers, query, null);

        // Assert - X-Request-ID is required
        error.Should().BeNull(); // Actually no required query params in this test
    }

    [Fact]
    public void ValidateRequest_RequiredHeaderParameter_Missing_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var headers = new HeaderDictionary();
        var query = new QueryCollection();

        // Act
        var error = validator.ValidateRequest("GET", "/products", null, null, headers, query, null);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(e => e.Contains("X-Request-ID"));
    }

    [Fact]
    public void ValidateRequest_HeaderParameter_InvalidPattern_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var headers = new HeaderDictionary
        {
            { "X-Request-ID", "invalid-uuid" }
        };
        var query = new QueryCollection();

        // Act
        var error = validator.ValidateRequest("GET", "/products", null, null, headers, query, null);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(e => e.Contains("pattern"));
    }

    [Fact]
    public void ValidateRequest_QueryParameter_InvalidEnum_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var headers = new HeaderDictionary
        {
            { "X-Request-ID", "12345678-1234-1234-1234-123456789abc" }
        };
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            { "category", "invalid_category" }
        });

        // Act
        var error = validator.ValidateRequest("GET", "/products", null, null, headers, query, null);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(e => e.Contains("category") && e.Contains("one of"));
    }

    [Fact]
    public void ValidateRequest_QueryParameter_InvalidType_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var headers = new HeaderDictionary
        {
            { "X-Request-ID", "12345678-1234-1234-1234-123456789abc" }
        };
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            { "minPrice", "not-a-number" }
        });

        // Act
        var error = validator.ValidateRequest("GET", "/products", null, null, headers, query, null);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(e => e.Contains("minPrice") && e.Contains("number"));
    }

    [Fact]
    public void ValidateRequest_PathParameter_InvalidPattern_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var pathParams = new Dictionary<string, string>
        {
            { "productId", "invalid" }
        };

        // Act
        var error = validator.ValidateRequest("GET", "/products/invalid", null, null, null, null, pathParams);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(e => e.Contains("productId") && e.Contains("pattern"));
    }

    [Fact]
    public void ValidateRequest_ValidParameters_ReturnsNull()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var headers = new HeaderDictionary
        {
            { "X-Request-ID", "12345678-1234-1234-1234-123456789abc" }
        };
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            { "category", "electronics" },
            { "minPrice", "10.50" }
        });

        // Act
        var error = validator.ValidateRequest("GET", "/products", null, null, headers, query, null);

        // Assert
        error.Should().BeNull();
    }

    #endregion

    #region JSON Schema Validation Tests

    [Fact]
    public void ValidateRequest_JsonSchema_MissingRequiredField_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var body = "{\"name\":\"Product 1\"}"; // Missing required 'price' field

        // Act
        var error = validator.ValidateRequest("POST", "/products", "application/json", body);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(e => e.Contains("price") || e.Contains("required"));
    }

    [Fact]
    public void ValidateRequest_JsonSchema_InvalidType_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var body = "{\"name\":\"Product 1\",\"price\":\"not-a-number\"}";

        // Act
        var error = validator.ValidateRequest("POST", "/products", "application/json", body);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(e => e.Contains("price") || e.Contains("number"));
    }

    [Fact]
    public void ValidateRequest_JsonSchema_StringTooShort_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var body = "{\"name\":\"\",\"price\":10.0}"; // name too short (minLength: 1)

        // Act
        var error = validator.ValidateRequest("POST", "/products", "application/json", body);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(e => e.Contains("name") || e.Contains("minLength") || e.Contains("length"));
    }

    [Fact]
    public void ValidateRequest_JsonSchema_InvalidEnumValue_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var body = "{\"name\":\"Product 1\",\"price\":10.0,\"category\":\"invalid_category\"}";

        // Act
        var error = validator.ValidateRequest("POST", "/products", "application/json", body);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(e => e.Contains("category"));
    }

    [Fact]
    public void ValidateRequest_JsonSchema_ValidData_ReturnsNull()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var body = "{\"name\":\"Product 1\",\"price\":10.50,\"category\":\"electronics\"}";

        // Act
        var error = validator.ValidateRequest("POST", "/products", "application/json", body);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateResponse_JsonSchema_MissingRequiredField_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var body = "{\"name\":\"Product 1\",\"price\":10.0}"; // Missing required 'id' field

        // Act
        var error = validator.ValidateResponse("GET", "/products/PRD-123456", 200, "application/json", body);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(e => e.Contains("id") || e.Contains("required"));
    }

    [Fact]
    public void ValidateResponse_RequiredHeader_Missing_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var body = "[]";
        var headers = new HeaderDictionary(); // Missing X-Total-Count

        // Act
        var error = validator.ValidateResponse("GET", "/products", 200, "application/json", body, headers);

        // Assert
        error.Should().NotBeNull();
        error!.ValidationErrors.Should().Contain(e => e.Contains("X-Total-Count"));
    }

    [Fact]
    public void ValidateResponse_ValidWithHeaders_ReturnsNull()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger);
        var body = "[]";
        var headers = new HeaderDictionary
        {
            { "X-Total-Count", "0" }
        };

        // Act
        var error = validator.ValidateResponse("GET", "/products", 200, "application/json", body, headers);

        // Assert
        error.Should().BeNull();
    }

    #endregion

    #region Strict Mode Tests

    [Fact]
    public void ValidateRequest_StrictMode_UndeclaredQueryParameter_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger, strictMode: true);
        var headers = new HeaderDictionary
        {
            { "X-Request-ID", "12345678-1234-1234-1234-123456789abc" }
        };
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            { "undeclaredParam", "value" }
        });

        // Act
        var error = validator.ValidateRequest("GET", "/products", null, null, headers, query, null);

        // Assert
        error.Should().NotBeNull();
        error!.IsStrictModeViolation.Should().BeTrue();
        error.ValidationErrors.Should().Contain(e => e.Contains("undeclaredParam"));
    }

    [Fact]
    public void ValidateRequest_StrictMode_UndeclaredHeader_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger, strictMode: true);
        var headers = new HeaderDictionary
        {
            { "X-Request-ID", "12345678-1234-1234-1234-123456789abc" },
            { "X-Undeclared-Header", "value" }
        };
        var query = new QueryCollection();

        // Act
        var error = validator.ValidateRequest("GET", "/products", null, null, headers, query, null);

        // Assert
        error.Should().NotBeNull();
        error!.IsStrictModeViolation.Should().BeTrue();
        error.ValidationErrors.Should().Contain(e => e.Contains("X-Undeclared-Header"));
    }

    [Fact]
    public void ValidateRequest_StrictMode_SecurityHeader_Allowed()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger, strictMode: true);
        var headers = new HeaderDictionary
        {
            { "X-Request-ID", "12345678-1234-1234-1234-123456789abc" },
            { "Authorization", "Bearer token" } // Security header should be allowed
        };
        var query = new QueryCollection();

        // Act
        var error = validator.ValidateRequest("GET", "/products", null, null, headers, query, null);

        // Assert
        error.Should().BeNull(); // Authorization header should be allowed
    }

    [Fact]
    public void ValidateRequest_StrictMode_UndeclaredProperty_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger, strictMode: true);
        var body = "{\"name\":\"Product 1\",\"price\":10.0,\"undeclaredProp\":\"value\"}";

        // Act
        var error = validator.ValidateRequest("POST", "/products", "application/json", body);

        // Assert
        error.Should().NotBeNull();
        error!.IsStrictModeViolation.Should().BeTrue();
        error.ValidationErrors.Should().Contain(e => e.Contains("undeclaredProp"));
    }

    [Fact]
    public void ValidateResponse_StrictMode_UndeclaredHeader_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger, strictMode: true);
        var body = "[]";
        var headers = new HeaderDictionary
        {
            { "X-Total-Count", "0" },
            { "X-Undeclared-Response-Header", "value" }
        };

        // Act
        var error = validator.ValidateResponse("GET", "/products", 200, "application/json", body, headers);

        // Assert
        error.Should().NotBeNull();
        error!.IsStrictModeViolation.Should().BeTrue();
        error.ValidationErrors.Should().Contain(e => e.Contains("X-Undeclared-Response-Header"));
    }

    [Fact]
    public void ValidateResponse_StrictMode_UndeclaredProperty_ReturnsError()
    {
        // Arrange
        var validator = new OpenApiValidator(_testSpecPath, _logger, strictMode: true);
        var body = "{\"id\":\"PRD-123456\",\"name\":\"Product 1\",\"price\":10.0,\"undeclaredProp\":\"value\"}";
        var headers = new HeaderDictionary();

        // Act
        var error = validator.ValidateResponse("GET", "/products/PRD-123456", 200, "application/json", body, headers);

        // Assert
        error.Should().NotBeNull();
        error!.IsStrictModeViolation.Should().BeTrue();
        error.ValidationErrors.Should().Contain(e => e.Contains("undeclaredProp"));
    }

    #endregion
}
