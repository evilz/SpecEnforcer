using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace SpecEnforcer;

/// <summary>
/// Middleware that validates HTTP requests and responses against an OpenAPI specification.
/// </summary>
public class SpecEnforcerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SpecEnforcerMiddleware> _logger;
    private readonly SpecEnforcerOptions _options;
    private readonly OpenApiValidator _validator;
    private readonly ValidationMetrics? _metrics;

    public SpecEnforcerMiddleware(
        RequestDelegate next,
        ILogger<SpecEnforcerMiddleware> logger,
        ILogger<OpenApiValidator> validatorLogger,
        IOptions<SpecEnforcerOptions> options,
        ValidationMetrics? metrics = null)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;

        if (string.IsNullOrEmpty(_options.OpenApiSpecPath))
        {
            throw new ArgumentException("OpenApiSpecPath must be configured", nameof(options));
        }

        if (!File.Exists(_options.OpenApiSpecPath))
        {
            throw new FileNotFoundException($"OpenAPI specification file not found at {_options.OpenApiSpecPath}");
        }

        _validator = new OpenApiValidator(_options.OpenApiSpecPath, validatorLogger, _options.StrictMode);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if path should be excluded from validation
        var path = context.Request.Path.Value ?? "/";
        if (IsPathExcluded(path))
        {
            await _next(context);
            return;
        }

        // Validate request
        if (_options.ValidateRequests)
        {
            var shouldContinue = await ValidateRequestAsync(context);
            if (!shouldContinue)
            {
                // Hard mode returned an error response, don't continue pipeline
                return;
            }
        }

        // Validate response by intercepting the response stream
        if (_options.ValidateResponses)
        {
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await _next(context);

                // Validate the response
                await ValidateResponseAsync(context, responseBody);

                // Copy the response back to the original stream
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }
        else
        {
            await _next(context);
        }
    }

    private async Task<bool> ValidateRequestAsync(HttpContext context)
    {
        var stopwatch = _options.EnableMetrics ? System.Diagnostics.Stopwatch.StartNew() : null;

        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        
        // Check if method should be validated
        if (_options.AllowedMethods.Count > 0 && !_options.AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return true; // Skip validation for this method
        }
        
        var contentType = context.Request.ContentType;

        string? body = null;
        if (context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var error = _validator.ValidateRequest(method, path, contentType, body,
            context.Request.Headers, context.Request.Query, null);

        if (_options.EnableMetrics && _metrics != null)
        {
            stopwatch?.Stop();
            _metrics.RecordRequestValidation(stopwatch?.ElapsedMilliseconds ?? 0, error != null);
        }

        if (error != null)
        {
            // Invoke custom callback if provided
            _options.OnValidationError?.Invoke(error);

            // Handle hard mode - return error response instead of proceeding
            if (_options.HardMode)
            {
                await WriteErrorResponse(context, error);
                return false; // Don't continue pipeline
            }

            if (_options.LogErrors)
            {
                LogValidationError("Request", error);
            }

            if (_options.ThrowOnValidationError)
            {
                throw new InvalidOperationException($"Request validation failed: {error.Message}");
            }
        }

        return true; // Continue pipeline
    }

    private async Task WriteErrorResponse(HttpContext context, ValidationError error)
    {
        // Check if response has already started
        if (context.Response.HasStarted)
        {
            // Cannot modify response headers after they've been sent
            _logger.LogError(
                "Cannot write hard mode error response - response has already started. {ValidationType} validation failed for {Method} {Path}",
                error.ValidationType,
                error.Method,
                error.Path);
            return;
        }

        context.Response.StatusCode = _options.HardModeStatusCode;
        context.Response.ContentType = "application/json";
        
        // Use custom formatter if provided, otherwise use default format
        object errorResponse;
        if (_options.CustomErrorFormatter != null)
        {
            errorResponse = _options.CustomErrorFormatter(error);
        }
        else
        {
            errorResponse = new
            {
                error = error.Message,
                details = error.Details,
                validationType = error.ValidationType,
                method = error.Method,
                path = error.Path,
                statusCode = error.StatusCode,
                validationErrors = error.ValidationErrors,
                isStrictModeViolation = error.IsStrictModeViolation,
                timestamp = error.Timestamp
            };
        }

        var json = System.Text.Json.JsonSerializer.Serialize(errorResponse);
        await context.Response.WriteAsync(json);

        if (_options.LogErrors)
        {
            LogValidationError("Hard Mode", error);
        }
    }

    private void LogValidationError(string context, ValidationError error)
    {
        var errorDetails = error.Details ?? "None";
        if (error.ValidationErrors.Any())
        {
            errorDetails += $" | Validation Errors: {string.Join(", ", error.ValidationErrors)}";
        }

        if (error.IsStrictModeViolation)
        {
            _logger.LogWarning(
                "{Context} - Strict mode violation for {Method} {Path}: {Message}. Details: {Details}",
                context,
                error.Method,
                error.Path,
                error.Message,
                errorDetails);
        }
        else if (error.ValidationType == "Request")
        {
            _logger.LogWarning(
                "{Context} validation failed for {Method} {Path}: {Message}. Details: {Details}",
                context,
                error.Method,
                error.Path,
                error.Message,
                errorDetails);
        }
        else
        {
            _logger.LogWarning(
                "{Context} validation failed for {Method} {Path} with status {StatusCode}: {Message}. Details: {Details}",
                context,
                error.Method,
                error.Path,
                error.StatusCode,
                error.Message,
                errorDetails);
        }
    }

    private bool IsPathExcluded(string path)
    {
        foreach (var pattern in _options.ExcludedPaths)
        {
            // Support exact match
            if (pattern == path)
            {
                return true;
            }

            // Support wildcard matching
            if (pattern.Contains('*'))
            {
                var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace("\\*", ".*") + "$";
                if (System.Text.RegularExpressions.Regex.IsMatch(path, regex))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task ValidateResponseAsync(HttpContext context, MemoryStream responseBody)
    {
        var stopwatch = _options.EnableMetrics ? System.Diagnostics.Stopwatch.StartNew() : null;

        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var statusCode = context.Response.StatusCode;
        
        // Check if status code should be validated
        if (_options.AllowedStatusCodes.Count > 0 && !_options.AllowedStatusCodes.Contains(statusCode))
        {
            return; // Skip validation for this status code
        }
        
        var contentType = context.Response.ContentType;

        responseBody.Seek(0, SeekOrigin.Begin);
        string? body = null;
        if (responseBody.Length > 0)
        {
            using var reader = new StreamReader(responseBody, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);
        }

        var error = _validator.ValidateResponse(method, path, statusCode, contentType, body, 
            context.Response.Headers);

        if (_options.EnableMetrics && _metrics != null)
        {
            stopwatch?.Stop();
            _metrics.RecordResponseValidation(stopwatch?.ElapsedMilliseconds ?? 0, error != null);
        }

        if (error != null)
        {
            // Invoke custom callback if provided
            _options.OnValidationError?.Invoke(error);

            if (_options.LogErrors)
            {
                LogValidationError("Response", error);
            }

            if (_options.ThrowOnValidationError)
            {
                throw new InvalidOperationException($"Response validation failed: {error.Message}");
            }
        }
    }
}
