# SpecEnforcer

[![Build Status](https://github.com/evilz/SpecEnforcer/actions/workflows/build.yml/badge.svg)](https://github.com/evilz/SpecEnforcer/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SpecEnforcer.svg)](https://www.nuget.org/packages/SpecEnforcer/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SpecEnforcer.svg)](https://www.nuget.org/packages/SpecEnforcer/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET middleware tool to validate HTTP requests and responses against OpenAPI contracts with comprehensive logging.

## 🎉 New Features!

SpecEnforcer now includes **10 powerful new features** to make validation easier and more flexible:

1. **Custom Error Response Formatter** - Customize error responses in hard mode
2. **Path Exclusion Filter** - Exclude health checks, metrics, and internal endpoints
3. **Performance Metrics** - Track validation performance and failure rates
4. **Validation Metrics Endpoint** - HTTP endpoint exposing validation statistics
5. **Custom Validation Event Handlers** - React to validation errors with callbacks
6. **HTTP Method Filtering** - Validate only specific HTTP methods
7. **Response Status Code Filtering** - Validate only specific response codes
8. **OpenAPI Spec File Watching** - Auto-reload specs on file changes
9. **Content-Type Filtering** - Validate only specific content types
10. **Body Size Limits & Debug Options** - Control validation scope and debugging

👉 **[See all new features in detail](FEATURES_ADDED.md)**

👉 **[Try the Advanced Sample Application](examples/AdvancedSampleApi/README.md)**

👉 **[Quick Start Guide](QUICK_START.md)**

## Features

- ✅ **Request Validation**: Validates incoming HTTP requests against OpenAPI specifications
- ✅ **Response Validation**: Validates outgoing HTTP responses against OpenAPI specifications
- ✅ **Comprehensive Parameter Validation**: Path, query, header parameters with type, enum, pattern, and range validation
- ✅ **JSON Schema Validation**: Full JSON schema validation for request/response bodies (required fields, types, enums, formats, min/max, patterns, nested objects)
- ✅ **Path Template Matching**: Supports parameterized paths (e.g., `/users/{id}`)
- ✅ **Response Header Validation**: Validates response headers against declared schemas
- ✅ **Strict Mode**: Detects undeclared elements in traffic (properties, parameters, headers) for API governance
- ✅ **Hard Mode**: Converts validation failures into HTTP error responses for fail-fast scenarios
- ✅ **Comprehensive Logging**: Logs validation errors with detailed information
- ✅ **Configurable**: Flexible options to enable/disable validation modes and customize behavior

## Installation

```bash
dotnet add package SpecEnforcer
```

## Quick Start

### 1. Add SpecEnforcer to your ASP.NET Core application

```csharp
using SpecEnforcer;

var builder = WebApplication.CreateBuilder(args);

// Add SpecEnforcer services
builder.Services.AddSpecEnforcer(options =>
{
    options.OpenApiSpecPath = "path/to/your/openapi.yaml";
    options.ValidateRequests = true;
    options.ValidateResponses = true;
    options.LogErrors = true;
    options.ThrowOnValidationError = false; // Set to true to throw exceptions on validation errors
    
    // Optional: Enable strict mode to detect undeclared elements
    options.StrictMode = false;
    
    // Optional: Enable hard mode to return error responses on validation failures
    options.HardMode = false;
    options.HardModeStatusCode = 400; // Customize error status code
});

var app = builder.Build();

// Use SpecEnforcer middleware
app.UseSpecEnforcer();

// Your other middleware and endpoints
app.MapControllers();

app.Run();
```

### 2. Create your OpenAPI specification

```yaml
openapi: 3.0.0
info:
  title: My API
  version: 1.0.0
paths:
  /users:
    get:
      summary: Get all users
      responses:
        '200':
          description: Success
          content:
            application/json:
              schema:
                type: array
    post:
      summary: Create a user
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              properties:
                name:
                  type: string
                email:
                  type: string
      responses:
        '201':
          description: Created
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `OpenApiSpecPath` | string | *required* | Path to the OpenAPI specification file (YAML or JSON) |
| `ValidateRequests` | bool | `true` | Enable/disable request validation |
| `ValidateResponses` | bool | `true` | Enable/disable response validation |
| `LogErrors` | bool | `true` | Enable/disable logging of validation errors |
| `ThrowOnValidationError` | bool | `false` | Throw exceptions when validation fails |
| `StrictMode` | bool | `false` | Enable strict mode to detect undeclared elements in traffic |
| `HardMode` | bool | `false` | Convert validation failures into HTTP error responses |
| `HardModeStatusCode` | int | `400` | HTTP status code to return when hard mode is enabled |

## Validation Modes

### Standard Mode (Default)

Validates requests and responses against your OpenAPI specification:
- Ensures paths and methods exist in the spec
- Validates required parameters and request bodies
- Validates JSON against schema definitions (types, required fields, enums, formats, min/max, patterns, etc.)
- Validates response status codes and content types
- Validates response headers against declared schemas

### Strict Mode

When enabled via `StrictMode = true`, SpecEnforcer reports elements that exist in traffic but are not declared in your OpenAPI specification:

- **Undeclared JSON properties** in request/response bodies
- **Undeclared query parameters**
- **Undeclared request headers** (excluding standard HTTP headers and security scheme headers like Authorization, X-API-Key)
- **Undeclared response headers** (excluding standard HTTP headers)

This helps enforce API governance by ensuring all API elements are properly documented.

### Hard Mode

When enabled via `HardMode = true`, validation failures are converted into HTTP error responses instead of just logging:

- Failed request validation returns an error response immediately (configured via `HardModeStatusCode`, default 400)
- The response includes detailed validation error information in JSON format
- Useful for CI/CD pipelines and fail-fast scenarios

## Comprehensive Validation Features

SpecEnforcer provides comprehensive OpenAPI compliance validation:

### Path & Operation Matching
- ✅ Path template matching (e.g., `/users/{id}`)
- ✅ HTTP method validation
- ✅ Path parameter extraction and validation

### Parameter Validation
- ✅ Required parameter presence (path, query, header)
- ✅ Type validation (string, integer, number, boolean)
- ✅ Enum validation
- ✅ Pattern matching (regex)
- ✅ Min/max length for strings
- ✅ Min/max values for numbers

### Request Body Validation
- ✅ Required body presence
- ✅ Content-Type matching
- ✅ JSON schema validation:
  - Required fields
  - Type checking
  - Enum values
  - String formats, min/max length, patterns
  - Number ranges
  - Array items
  - Nested object validation

### Response Validation
- ✅ Status code matching
- ✅ Content-Type validation
- ✅ Response header validation (required headers, schema compliance)
- ✅ JSON schema validation for response bodies

## Validation Errors

When validation errors occur, they are logged with the following information:

- **Validation Type**: Request or Response
- **HTTP Method**: GET, POST, PUT, DELETE, etc.
- **Path**: The request path
- **Status Code**: (for response validation)
- **Error Message**: Description of the validation error
- **Validation Errors**: List of specific validation failures
- **Is Strict Mode Violation**: Whether this is a strict mode governance issue
- **Timestamp**: When the error occurred

### Example Log Output

```
[Warning] Request validation failed for POST /products: Request validation failed. Details: None | Validation Errors: request body.name: Required property 'price' is missing
[Warning] Strict mode violations detected for GET /products: Strict mode violations detected. Details: None | Validation Errors: Undeclared query parameter: 'debug'
[Warning] Hard Mode - Request validation failed for POST /products: Request validation failed. Details: None | Validation Errors: Path parameter 'productId' does not match pattern '^PRD-[0-9]{6}$'
```

### Hard Mode Error Response Example

When hard mode is enabled, validation failures return JSON error responses:

```json
{
  "error": "Request validation failed",
  "details": null,
  "validationType": "Request",
  "method": "POST",
  "path": "/products",
  "statusCode": null,
  "validationErrors": [
    "request body.price: Required property 'price' is missing"
  ],
  "isStrictModeViolation": false,
  "timestamp": "2026-01-30T17:50:00.000Z"
}
```

## How It Works

1. **Request Validation**: The middleware intercepts incoming requests and validates:
   - Path exists in the OpenAPI specification
   - HTTP method is allowed for the path
   - Path parameters match their schema (type, format, pattern, enum)
   - Query and header parameters are present (if required) and match their schema
   - Request body is present when required
   - Content type matches the specification
   - JSON payload validates against schema (required fields, types, enums, formats, min/max constraints, etc.)
   - In strict mode: detects undeclared parameters, headers, and JSON properties

2. **Response Validation**: The middleware captures outgoing responses and validates:
   - Status code is defined in the specification
   - Content type matches the specification
   - Response headers match declared headers (presence and schema)
   - JSON payload validates against response schema
   - In strict mode: detects undeclared response headers and JSON properties

3. **Logging**: All validation errors are logged using `ILogger` with detailed information to help diagnose issues.

4. **Hard Mode**: When enabled, validation failures immediately return error responses instead of continuing request processing.

## Building from Source

```bash
git clone https://github.com/evilz/SpecEnforcer.git
cd SpecEnforcer
dotnet build
dotnet test
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
