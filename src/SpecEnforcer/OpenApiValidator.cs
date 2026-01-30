using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Json.Schema;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace SpecEnforcer;

/// <summary>
/// Validates HTTP requests and responses against an OpenAPI specification.
/// </summary>
public class OpenApiValidator
{
    private readonly OpenApiDocument _openApiDocument;
    private readonly ILogger<OpenApiValidator> _logger;
    private readonly bool _strictMode;

    public OpenApiValidator(string openApiSpecPath, ILogger<OpenApiValidator> logger, bool strictMode = false)
    {
        _logger = logger;
        _strictMode = strictMode;

        try
        {
            using var stream = File.OpenRead(openApiSpecPath);
            var reader = new OpenApiStreamReader();
            _openApiDocument = reader.Read(stream, out var diagnostic);

            if (diagnostic.Errors.Count > 0)
            {
                var errors = string.Join(", ", diagnostic.Errors.Select(e => e.Message));
                throw new InvalidOperationException($"Failed to parse OpenAPI specification: {errors}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load OpenAPI specification from {Path}", openApiSpecPath);
            throw;
        }
    }

    /// <summary>
    /// Validates an HTTP request against the OpenAPI specification.
    /// </summary>
    public ValidationError? ValidateRequest(string method, string path, string? contentType, string? body, 
        IHeaderDictionary? headers = null, IQueryCollection? query = null, IDictionary<string, string>? pathParameters = null)
    {
        try
        {
            // Find the operation in the OpenAPI spec
            var (pathItem, pathTemplate, extractedPathParams) = FindPathItemWithParameters(path);
            if (pathItem == null)
            {
                return new ValidationError
                {
                    ValidationType = "Request",
                    Method = method,
                    Path = path,
                    Message = $"Path '{path}' not found in OpenAPI specification"
                };
            }

            var operation = GetOperation(pathItem, method);
            if (operation == null)
            {
                return new ValidationError
                {
                    ValidationType = "Request",
                    Method = method,
                    Path = path,
                    Message = $"Method '{method}' not allowed for path '{path}'"
                };
            }

            var validationErrors = new List<string>();

            // Validate path parameters
            var pathParamsToValidate = pathParameters ?? extractedPathParams;
            var paramError = ValidateParameters(operation, pathItem, pathParamsToValidate, query, headers, "Request");
            if (paramError != null)
            {
                validationErrors.AddRange(paramError);
            }

            // Validate request body if present
            if (!string.IsNullOrEmpty(body) && operation.RequestBody != null)
            {
                // Extract media type from content type (ignore charset and other parameters)
                var mediaType = contentType?.Split(';')[0].Trim() ?? "";
                var hasMatchingContent = operation.RequestBody.Content.ContainsKey(mediaType);
                if (!hasMatchingContent)
                {
                    return new ValidationError
                    {
                        ValidationType = "Request",
                        Method = method,
                        Path = path,
                        Message = $"Content type '{mediaType}' not supported for this operation",
                        Details = $"Expected one of: {string.Join(", ", operation.RequestBody.Content.Keys)}"
                    };
                }

                // Validate JSON body against schema
                if (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    // First check if JSON is valid
                    try
                    {
                        JsonDocument.Parse(body);
                    }
                    catch (JsonException ex)
                    {
                        return new ValidationError
                        {
                            ValidationType = "Request",
                            Method = method,
                            Path = path,
                            Message = "Invalid JSON in request body",
                            Details = ex.Message
                        };
                    }

                    // Then validate against schema
                    var schemaErrors = ValidateJsonAgainstSchema(body, operation.RequestBody.Content[mediaType].Schema, "request body");
                    if (schemaErrors.Any())
                    {
                        validationErrors.AddRange(schemaErrors);
                    }
                }
            }
            else if (operation.RequestBody?.Required == true && string.IsNullOrEmpty(body))
            {
                return new ValidationError
                {
                    ValidationType = "Request",
                    Method = method,
                    Path = path,
                    Message = "Request body is required but was not provided"
                };
            }

            // Strict mode: Check for undeclared query parameters, headers, etc.
            // In strict mode, violations are reported alongside other validation errors
            if (_strictMode)
            {
                var strictErrors = CheckStrictModeViolations(operation, pathItem, query, headers, body, contentType, "Request");
                if (strictErrors.Any())
                {
                    validationErrors.AddRange(strictErrors);
                }
            }

            if (validationErrors.Any())
            {
                // Check if any errors are strict mode violations
                var isStrictMode = _strictMode && validationErrors.Any(e => 
                    e.Contains("Undeclared") || e.Contains("undeclared"));

                return new ValidationError
                {
                    ValidationType = "Request",
                    Method = method,
                    Path = path,
                    Message = isStrictMode ? "Strict mode violations detected" : "Request validation failed",
                    ValidationErrors = validationErrors,
                    IsStrictModeViolation = isStrictMode && !validationErrors.Any(e => !e.Contains("Undeclared") && !e.Contains("undeclared"))
                };
            }

            return null; // Validation passed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating request {Method} {Path}", method, path);
            return new ValidationError
            {
                ValidationType = "Request",
                Method = method,
                Path = path,
                Message = "Validation error occurred",
                Details = ex.Message
            };
        }
    }

    /// <summary>
    /// Validates an HTTP response against the OpenAPI specification.
    /// </summary>
    public ValidationError? ValidateResponse(string method, string path, int statusCode, string? contentType, string? body,
        IHeaderDictionary? headers = null)
    {
        try
        {
            // Find the operation in the OpenAPI spec
            var (pathItem, pathTemplate, extractedPathParams) = FindPathItemWithParameters(path);
            if (pathItem == null)
            {
                return new ValidationError
                {
                    ValidationType = "Response",
                    Method = method,
                    Path = path,
                    StatusCode = statusCode,
                    Message = $"Path '{path}' not found in OpenAPI specification"
                };
            }

            var operation = GetOperation(pathItem, method);
            if (operation == null)
            {
                return new ValidationError
                {
                    ValidationType = "Response",
                    Method = method,
                    Path = path,
                    StatusCode = statusCode,
                    Message = $"Method '{method}' not allowed for path '{path}'"
                };
            }

            // Check if the status code is defined in the spec
            var statusCodeStr = statusCode.ToString();
            var hasResponse = operation.Responses.ContainsKey(statusCodeStr) ||
                            operation.Responses.ContainsKey("default");

            if (!hasResponse)
            {
                return new ValidationError
                {
                    ValidationType = "Response",
                    Method = method,
                    Path = path,
                    StatusCode = statusCode,
                    Message = $"Status code {statusCode} not defined in OpenAPI specification for this operation",
                    Details = $"Expected one of: {string.Join(", ", operation.Responses.Keys)}"
                };
            }

            // Get the response definition
            var response = operation.Responses.ContainsKey(statusCodeStr)
                ? operation.Responses[statusCodeStr]
                : operation.Responses["default"];

            var validationErrors = new List<string>();

            // Validate response headers
            if (headers != null && response.Headers.Count > 0)
            {
                var headerErrors = ValidateResponseHeaders(response.Headers, headers);
                if (headerErrors.Any())
                {
                    validationErrors.AddRange(headerErrors);
                }
            }

            // Validate content type if body is present
            if (!string.IsNullOrEmpty(body) && response.Content.Count > 0)
            {
                // Extract media type from content type (ignore charset and other parameters)
                var mediaType = contentType?.Split(';')[0].Trim() ?? "";
                var hasMatchingContent = response.Content.ContainsKey(mediaType);
                if (!hasMatchingContent)
                {
                    return new ValidationError
                    {
                        ValidationType = "Response",
                        Method = method,
                        Path = path,
                        StatusCode = statusCode,
                        Message = $"Content type '{mediaType}' not defined for status {statusCode}",
                        Details = $"Expected one of: {string.Join(", ", response.Content.Keys)}"
                    };
                }

                // Validate JSON response against schema
                if (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    // First check if JSON is valid
                    try
                    {
                        JsonDocument.Parse(body);
                    }
                    catch (JsonException ex)
                    {
                        return new ValidationError
                        {
                            ValidationType = "Response",
                            Method = method,
                            Path = path,
                            StatusCode = statusCode,
                            Message = "Invalid JSON in response body",
                            Details = ex.Message
                        };
                    }

                    // Then validate against schema
                    var schemaErrors = ValidateJsonAgainstSchema(body, response.Content[mediaType].Schema, "response body");
                    if (schemaErrors.Any())
                    {
                        validationErrors.AddRange(schemaErrors);
                    }
                }
            }

            // Strict mode: Check for undeclared response headers and body properties
            if (_strictMode && headers != null)
            {
                var strictErrors = CheckStrictModeResponseViolations(response, headers, body, contentType);
                if (strictErrors.Any())
                {
                    validationErrors.AddRange(strictErrors);
                }
            }

            if (validationErrors.Any())
            {
                // Check if any errors are strict mode violations
                var isStrictMode = _strictMode && validationErrors.Any(e => 
                    e.Contains("Undeclared") || e.Contains("undeclared"));

                return new ValidationError
                {
                    ValidationType = "Response",
                    Method = method,
                    Path = path,
                    StatusCode = statusCode,
                    Message = isStrictMode ? "Strict mode violations detected in response" : "Response validation failed",
                    ValidationErrors = validationErrors,
                    IsStrictModeViolation = isStrictMode && !validationErrors.Any(e => !e.Contains("Undeclared") && !e.Contains("undeclared"))
                };
            }

            return null; // Validation passed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating response {Method} {Path} {StatusCode}", method, path, statusCode);
            return new ValidationError
            {
                ValidationType = "Response",
                Method = method,
                Path = path,
                StatusCode = statusCode,
                Message = "Validation error occurred",
                Details = ex.Message
            };
        }
    }

    private (OpenApiPathItem?, string?, IDictionary<string, string>) FindPathItemWithParameters(string path)
    {
        // Try exact match first
        if (_openApiDocument.Paths.TryGetValue(path, out var pathItem))
        {
            return (pathItem, path, new Dictionary<string, string>());
        }

        // Try to match path templates
        foreach (var kvp in _openApiDocument.Paths)
        {
            var (matches, parameters) = PathMatchesWithParameters(kvp.Key, path);
            if (matches)
            {
                return (kvp.Value, kvp.Key, parameters);
            }
        }

        return (null, null, new Dictionary<string, string>());
    }

    private OpenApiPathItem? FindPathItem(string path)
    {
        var (pathItem, _, _) = FindPathItemWithParameters(path);
        return pathItem;
    }

    private (bool, IDictionary<string, string>) PathMatchesWithParameters(string template, string actualPath)
    {
        var parameters = new Dictionary<string, string>();
        
        // Simple path template matching (e.g., /users/{id} matches /users/123)
        var templateParts = template.Split('/');
        var pathParts = actualPath.Split('/');

        if (templateParts.Length != pathParts.Length)
        {
            return (false, parameters);
        }

        for (int i = 0; i < templateParts.Length; i++)
        {
            if (templateParts[i].StartsWith("{") && templateParts[i].EndsWith("}"))
            {
                // This is a parameter, extract the parameter name and value
                var paramName = templateParts[i].Trim('{', '}');
                parameters[paramName] = pathParts[i];
                continue;
            }

            if (!string.Equals(templateParts[i], pathParts[i], StringComparison.OrdinalIgnoreCase))
            {
                return (false, parameters);
            }
        }

        return (true, parameters);
    }

    private List<string> ValidateParameters(OpenApiOperation operation, OpenApiPathItem pathItem,
        IDictionary<string, string>? pathParameters, IQueryCollection? query, IHeaderDictionary? headers, string context)
    {
        var errors = new List<string>();
        
        // Combine operation parameters with path-level parameters
        var allParameters = new List<OpenApiParameter>();
        if (pathItem.Parameters != null)
        {
            allParameters.AddRange(pathItem.Parameters);
        }
        if (operation.Parameters != null)
        {
            allParameters.AddRange(operation.Parameters);
        }

        foreach (var param in allParameters)
        {
            var paramValue = param.In switch
            {
                ParameterLocation.Path => pathParameters?.TryGetValue(param.Name, out var pv) == true ? pv : null,
                ParameterLocation.Query => query?[param.Name].ToString(),
                ParameterLocation.Header => headers?[param.Name].ToString(),
                ParameterLocation.Cookie => null, // Not yet supported
                _ => null
            };

            // Check if required parameter is missing
            if (param.Required && string.IsNullOrEmpty(paramValue))
            {
                errors.Add($"Required {param.In} parameter '{param.Name}' is missing");
                continue;
            }

            // Validate parameter against schema if value is present
            if (!string.IsNullOrEmpty(paramValue) && param.Schema != null)
            {
                var paramErrors = ValidateParameterValue(paramValue, param.Schema, param.Name, param.In.ToString()!);
                errors.AddRange(paramErrors);
            }
        }

        return errors;
    }

    private List<string> ValidateParameterValue(string value, OpenApiSchema schema, string paramName, string paramLocation)
    {
        var errors = new List<string>();

        try
        {
            // Type validation
            switch (schema.Type?.ToLowerInvariant())
            {
                case "integer":
                    if (!int.TryParse(value, out _))
                    {
                        errors.Add($"{paramLocation} parameter '{paramName}' must be an integer, got '{value}'");
                    }
                    break;
                case "number":
                    if (!double.TryParse(value, out _))
                    {
                        errors.Add($"{paramLocation} parameter '{paramName}' must be a number, got '{value}'");
                    }
                    break;
                case "boolean":
                    if (!bool.TryParse(value, out _))
                    {
                        errors.Add($"{paramLocation} parameter '{paramName}' must be a boolean, got '{value}'");
                    }
                    break;
                case "string":
                    // Enum validation
                    if (schema.Enum != null && schema.Enum.Count > 0)
                    {
                        var enumValues = schema.Enum
                            .Select(e => e is Microsoft.OpenApi.Any.OpenApiString str ? str.Value : e?.ToString())
                            .Where(v => v != null)
                            .ToList();
                        if (!enumValues.Contains(value))
                        {
                            errors.Add($"{paramLocation} parameter '{paramName}' must be one of [{string.Join(", ", enumValues)}], got '{value}'");
                        }
                    }
                    
                    // Pattern validation
                    if (!string.IsNullOrEmpty(schema.Pattern))
                    {
                        if (!Regex.IsMatch(value, schema.Pattern))
                        {
                            errors.Add($"{paramLocation} parameter '{paramName}' does not match pattern '{schema.Pattern}'");
                        }
                    }
                    
                    // Min/Max length
                    if (schema.MinLength.HasValue && value.Length < schema.MinLength.Value)
                    {
                        errors.Add($"{paramLocation} parameter '{paramName}' must be at least {schema.MinLength.Value} characters");
                    }
                    if (schema.MaxLength.HasValue && value.Length > schema.MaxLength.Value)
                    {
                        errors.Add($"{paramLocation} parameter '{paramName}' must be at most {schema.MaxLength.Value} characters");
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating {paramLocation} parameter '{paramName}': {ex.Message}");
        }

        return errors;
    }

    private List<string> ValidateJsonAgainstSchema(string jsonBody, OpenApiSchema? schema, string context)
    {
        var errors = new List<string>();

        if (schema == null)
        {
            return errors;
        }

        try
        {
            // Parse JSON
            using var jsonDoc = JsonDocument.Parse(jsonBody);
            
            // First try basic manual validation which works with references
            var manualErrors = ValidateJsonElementAgainstSchema(jsonDoc.RootElement, schema, context);
            if (manualErrors.Any())
            {
                errors.AddRange(manualErrors);
                return errors;
            }

            // If manual validation passes, try JSON Schema validation for more complex rules
            // This may fail if schema has unresolved references, but manual validation already passed
            try
            {
                var jsonSchema = ConvertOpenApiSchemaToJsonSchema(schema);
                if (jsonSchema != null)
                {
                    var validationResult = jsonSchema.Evaluate(jsonDoc.RootElement);
                    
                    if (!validationResult.IsValid)
                    {
                        CollectValidationErrors(validationResult, errors, context);
                    }
                }
            }
            catch
            {
                // JSON Schema validation failed (likely due to unresolved refs), but manual validation passed
                // So we'll accept the result
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON in {context}: {ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating {context}: {ex.Message}");
        }

        return errors;
    }

    private List<string> ValidateJsonElementAgainstSchema(JsonElement element, OpenApiSchema schema, string path)
    {
        var errors = new List<string>();

        // Type validation
        if (schema.Type != null)
        {
            var isValidType = schema.Type.ToLowerInvariant() switch
            {
                "object" => element.ValueKind == JsonValueKind.Object,
                "array" => element.ValueKind == JsonValueKind.Array,
                "string" => element.ValueKind == JsonValueKind.String,
                "number" => element.ValueKind == JsonValueKind.Number,
                "integer" => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out _),
                "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
                "null" => element.ValueKind == JsonValueKind.Null,
                _ => true
            };

            if (!isValidType)
            {
                errors.Add($"{path}: Expected type '{schema.Type}', got '{element.ValueKind}'");
                return errors; // Don't continue validation if type is wrong
            }
        }

        // Object validation
        if (element.ValueKind == JsonValueKind.Object && schema.Properties != null)
        {
            // Check required properties
            if (schema.Required != null)
            {
                foreach (var requiredProp in schema.Required)
                {
                    if (!element.TryGetProperty(requiredProp, out _))
                    {
                        errors.Add($"{path}: Required property '{requiredProp}' is missing");
                    }
                }
            }

            // Validate each property
            foreach (var property in element.EnumerateObject())
            {
                if (schema.Properties.TryGetValue(property.Name, out var propSchema))
                {
                    var propErrors = ValidateJsonElementAgainstSchema(property.Value, propSchema, $"{path}.{property.Name}");
                    errors.AddRange(propErrors);
                }
            }
        }

        // Array validation
        if (element.ValueKind == JsonValueKind.Array && schema.Items != null)
        {
            int index = 0;
            foreach (var item in element.EnumerateArray())
            {
                var itemErrors = ValidateJsonElementAgainstSchema(item, schema.Items, $"{path}[{index}]");
                errors.AddRange(itemErrors);
                index++;
            }
        }

        // String validation
        if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString() ?? "";
            
            if (schema.MinLength.HasValue && str.Length < schema.MinLength.Value)
            {
                errors.Add($"{path}: String length {str.Length} is less than minimum {schema.MinLength.Value}");
            }
            
            if (schema.MaxLength.HasValue && str.Length > schema.MaxLength.Value)
            {
                errors.Add($"{path}: String length {str.Length} is greater than maximum {schema.MaxLength.Value}");
            }

            if (schema.Enum != null && schema.Enum.Count > 0)
            {
                var enumValues = schema.Enum
                    .Select(e => e is Microsoft.OpenApi.Any.OpenApiString openApiStr ? openApiStr.Value : e?.ToString())
                    .Where(v => v != null)
                    .ToList();
                if (!enumValues.Contains(str))
                {
                    errors.Add($"{path}: Value '{str}' is not one of the allowed values: [{string.Join(", ", enumValues)}]");
                }
            }

            if (!string.IsNullOrEmpty(schema.Pattern))
            {
                if (!Regex.IsMatch(str, schema.Pattern))
                {
                    errors.Add($"{path}: Value does not match pattern '{schema.Pattern}'");
                }
            }
        }

        // Number validation
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetDecimal(out var numValue))
            {
                if (schema.Minimum.HasValue && numValue < schema.Minimum.Value)
                {
                    errors.Add($"{path}: Value {numValue} is less than minimum {schema.Minimum.Value}");
                }

                if (schema.Maximum.HasValue && numValue > schema.Maximum.Value)
                {
                    errors.Add($"{path}: Value {numValue} is greater than maximum {schema.Maximum.Value}");
                }
            }
        }

        return errors;
    }

    private JsonSchema? ConvertOpenApiSchemaToJsonSchema(OpenApiSchema openApiSchema)
    {
        try
        {
            // Serialize OpenAPI schema to JSON
            var jsonWriter = new System.IO.StringWriter();
            openApiSchema.SerializeAsV3(new Microsoft.OpenApi.Writers.OpenApiJsonWriter(jsonWriter));
            var schemaJson = jsonWriter.ToString();
            
            // Parse as JSON Schema
            // Note: References ($ref) in the schema may not resolve correctly if they point to
            // components in the parent document. For now, we'll rely on basic validation.
            var schema = JsonSchema.FromText(schemaJson);
            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to convert OpenAPI schema to JSON Schema, validation may be limited");
            // Return null to skip schema validation rather than failing
            return null;
        }
    }

    private void CollectValidationErrors(EvaluationResults result, List<string> errors, string context)
    {
        if (result.HasErrors && result.Errors != null)
        {
            foreach (var error in result.Errors)
            {
                errors.Add($"{context}: {error.Key} - {error.Value}");
            }
        }

        // Recursively collect errors from nested results
        if (result.Details != null)
        {
            foreach (var detail in result.Details)
            {
                CollectValidationErrors(detail, errors, context);
            }
        }
    }

    private List<string> ValidateResponseHeaders(IDictionary<string, OpenApiHeader> specHeaders, IHeaderDictionary actualHeaders)
    {
        var errors = new List<string>();

        foreach (var specHeader in specHeaders)
        {
            var headerName = specHeader.Key;
            var headerSpec = specHeader.Value;

            if (headerSpec.Required && !actualHeaders.ContainsKey(headerName))
            {
                errors.Add($"Required response header '{headerName}' is missing");
                continue;
            }

            if (actualHeaders.TryGetValue(headerName, out var headerValue))
            {
                var value = headerValue.ToString();
                if (headerSpec.Schema != null)
                {
                    var headerErrors = ValidateParameterValue(value, headerSpec.Schema, headerName, "response header");
                    errors.AddRange(headerErrors);
                }
            }
        }

        return errors;
    }

    private List<string> CheckStrictModeViolations(OpenApiOperation operation, OpenApiPathItem pathItem,
        IQueryCollection? query, IHeaderDictionary? headers, string? body, string? contentType, string context)
    {
        var violations = new List<string>();

        // Get all declared parameters
        var declaredParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allParameters = new List<OpenApiParameter>();
        if (pathItem.Parameters != null)
        {
            allParameters.AddRange(pathItem.Parameters);
        }
        if (operation.Parameters != null)
        {
            allParameters.AddRange(operation.Parameters);
        }

        foreach (var param in allParameters)
        {
            declaredParams.Add(param.Name);
        }

        // Add security scheme headers as declared
        var securityHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization", "X-API-Key", "Api-Key"
        };

        // Check for undeclared query parameters
        if (query != null)
        {
            foreach (var queryParam in query)
            {
                if (!declaredParams.Contains(queryParam.Key))
                {
                    violations.Add($"Undeclared query parameter: '{queryParam.Key}'");
                }
            }
        }

        // Check for undeclared headers
        if (headers != null)
        {
            var standardHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Host", "User-Agent", "Accept", "Accept-Encoding", "Accept-Language",
                "Connection", "Content-Length", "Content-Type", "Cache-Control"
            };

            foreach (var header in headers)
            {
                if (!declaredParams.Contains(header.Key) &&
                    !securityHeaders.Contains(header.Key) &&
                    !standardHeaders.Contains(header.Key))
                {
                    violations.Add($"Undeclared request header: '{header.Key}'");
                }
            }
        }

        // Check for undeclared JSON properties
        if (!string.IsNullOrEmpty(body) && operation.RequestBody != null)
        {
            var mediaType = contentType?.Split(';')[0].Trim() ?? "";
            
            if (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) &&
                operation.RequestBody.Content.TryGetValue(mediaType, out var content) &&
                content.Schema != null)
            {
                var propertyViolations = CheckUndeclaredJsonProperties(body, content.Schema, "request body");
                violations.AddRange(propertyViolations);
            }
        }

        return violations;
    }

    private List<string> CheckStrictModeResponseViolations(OpenApiResponse response, IHeaderDictionary headers, string? body, string? contentType)
    {
        var violations = new List<string>();

        // Get declared response headers
        var declaredHeaders = new HashSet<string>(response.Headers.Keys, StringComparer.OrdinalIgnoreCase);
        
        var standardHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Date", "Server", "Content-Length", "Content-Type", "Transfer-Encoding",
            "Connection", "Cache-Control", "Vary", "ETag", "Last-Modified"
        };

        // Check for undeclared response headers
        foreach (var header in headers)
        {
            if (!declaredHeaders.Contains(header.Key) && !standardHeaders.Contains(header.Key))
            {
                violations.Add($"Undeclared response header: '{header.Key}'");
            }
        }

        // Check for undeclared JSON properties in response
        if (!string.IsNullOrEmpty(body) && response.Content.Count > 0)
        {
            var mediaType = contentType?.Split(';')[0].Trim() ?? "";
            
            if (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) &&
                response.Content.TryGetValue(mediaType, out var content) &&
                content.Schema != null)
            {
                var propertyViolations = CheckUndeclaredJsonProperties(body, content.Schema, "response body");
                violations.AddRange(propertyViolations);
            }
        }

        return violations;
    }

    private List<string> CheckUndeclaredJsonProperties(string jsonBody, OpenApiSchema schema, string context)
    {
        var violations = new List<string>();

        try
        {
            using var jsonDoc = JsonDocument.Parse(jsonBody);
            CheckJsonElementForUndeclaredProperties(jsonDoc.RootElement, schema, context, violations);
        }
        catch (JsonException)
        {
            // Invalid JSON, will be caught by other validation
        }

        return violations;
    }

    private void CheckJsonElementForUndeclaredProperties(JsonElement element, OpenApiSchema schema,
        string path, List<string> violations)
    {
        if (element.ValueKind == JsonValueKind.Object && schema.Properties != null)
        {
            var declaredProperties = new HashSet<string>(schema.Properties.Keys, StringComparer.OrdinalIgnoreCase);
            
            foreach (var property in element.EnumerateObject())
            {
                if (!declaredProperties.Contains(property.Name))
                {
                    // In strict mode, we want to flag undeclared properties regardless of additionalProperties setting
                    // because strict mode is about governance - finding things in traffic not in spec
                    violations.Add($"Undeclared property in {path}: '{property.Name}'");
                }
                else if (schema.Properties.TryGetValue(property.Name, out var propSchema))
                {
                    // Recursively check nested objects
                    CheckJsonElementForUndeclaredProperties(property.Value, propSchema, $"{path}.{property.Name}", violations);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array && schema.Items != null)
        {
            int index = 0;
            foreach (var item in element.EnumerateArray())
            {
                CheckJsonElementForUndeclaredProperties(item, schema.Items, $"{path}[{index}]", violations);
                index++;
            }
        }
    }

    private OpenApiOperation? GetOperation(OpenApiPathItem pathItem, string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => pathItem.Operations.TryGetValue(OperationType.Get, out var op) ? op : null,
            "POST" => pathItem.Operations.TryGetValue(OperationType.Post, out var op) ? op : null,
            "PUT" => pathItem.Operations.TryGetValue(OperationType.Put, out var op) ? op : null,
            "DELETE" => pathItem.Operations.TryGetValue(OperationType.Delete, out var op) ? op : null,
            "PATCH" => pathItem.Operations.TryGetValue(OperationType.Patch, out var op) ? op : null,
            "OPTIONS" => pathItem.Operations.TryGetValue(OperationType.Options, out var op) ? op : null,
            "HEAD" => pathItem.Operations.TryGetValue(OperationType.Head, out var op) ? op : null,
            "TRACE" => pathItem.Operations.TryGetValue(OperationType.Trace, out var op) ? op : null,
            _ => null
        };
    }
}
