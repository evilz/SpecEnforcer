using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SpecEnforcer;

/// <summary>
/// Validates HTTP requests and responses against an OpenAPI specification.
/// </summary>
public class OpenApiValidator
{
    private readonly OpenApiDocument _openApiDocument;
    private readonly ILogger<OpenApiValidator> _logger;

    public OpenApiValidator(string openApiSpecPath, ILogger<OpenApiValidator> logger)
    {
        _logger = logger;

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
    public ValidationError? ValidateRequest(string method, string path, string? contentType, string? body)
    {
        try
        {
            // Find the operation in the OpenAPI spec
            var pathItem = FindPathItem(path);
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

                // Basic JSON validation if content type is application/json
                if (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                {
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
    public ValidationError? ValidateResponse(string method, string path, int statusCode, string? contentType, string? body)
    {
        try
        {
            // Find the operation in the OpenAPI spec
            var pathItem = FindPathItem(path);
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

                // Basic JSON validation if content type is application/json
                if (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                {
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
                }
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

    private OpenApiPathItem? FindPathItem(string path)
    {
        // Try exact match first
        if (_openApiDocument.Paths.TryGetValue(path, out var pathItem))
        {
            return pathItem;
        }

        // Try to match path templates
        foreach (var kvp in _openApiDocument.Paths)
        {
            if (PathMatches(kvp.Key, path))
            {
                return kvp.Value;
            }
        }

        return null;
    }

    private bool PathMatches(string template, string actualPath)
    {
        // Simple path template matching (e.g., /users/{id} matches /users/123)
        var templateParts = template.Split('/');
        var pathParts = actualPath.Split('/');

        if (templateParts.Length != pathParts.Length)
        {
            return false;
        }

        for (int i = 0; i < templateParts.Length; i++)
        {
            if (templateParts[i].StartsWith("{") && templateParts[i].EndsWith("}"))
            {
                // This is a parameter, so it matches any value
                continue;
            }

            if (!string.Equals(templateParts[i], pathParts[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
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
