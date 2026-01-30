# SpecEnforcer

A .NET middleware tool to validate HTTP requests and responses against OpenAPI contracts with comprehensive logging.

## Features

- ✅ **Request Validation**: Validates incoming HTTP requests against OpenAPI specifications
- ✅ **Response Validation**: Validates outgoing HTTP responses against OpenAPI specifications
- ✅ **Comprehensive Logging**: Logs validation errors with detailed information
- ✅ **Path Template Matching**: Supports parameterized paths (e.g., `/users/{id}`)
- ✅ **JSON Validation**: Validates JSON payloads for correct syntax
- ✅ **Content Type Validation**: Ensures content types match the specification
- ✅ **Configurable**: Flexible options to enable/disable validation and logging

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

## Validation Errors

When validation errors occur, they are logged with the following information:

- **Validation Type**: Request or Response
- **HTTP Method**: GET, POST, PUT, DELETE, etc.
- **Path**: The request path
- **Status Code**: (for response validation)
- **Error Message**: Description of the validation error
- **Details**: Additional context about the error
- **Timestamp**: When the error occurred

### Example Log Output

```
[Warning] Request validation failed for POST /users: Request body is required but was not provided
[Warning] Response validation failed for GET /users with status 500: Status code 500 not defined in OpenAPI specification for this operation. Details: Expected one of: 200
```

## How It Works

1. **Request Validation**: The middleware intercepts incoming requests and validates:
   - Path exists in the OpenAPI specification
   - HTTP method is allowed for the path
   - Request body is present when required
   - Content type matches the specification
   - JSON payload is well-formed

2. **Response Validation**: The middleware captures outgoing responses and validates:
   - Status code is defined in the specification
   - Content type matches the specification
   - JSON payload is well-formed

3. **Logging**: All validation errors are logged using `ILogger` with detailed information to help diagnose issues.

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
