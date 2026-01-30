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

    public SpecEnforcerMiddleware(
        RequestDelegate next,
        ILogger<SpecEnforcerMiddleware> logger,
        ILogger<OpenApiValidator> validatorLogger,
        IOptions<SpecEnforcerOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;

        if (string.IsNullOrEmpty(_options.OpenApiSpecPath))
        {
            throw new ArgumentException("OpenApiSpecPath must be configured", nameof(options));
        }

        if (!File.Exists(_options.OpenApiSpecPath))
        {
            throw new FileNotFoundException($"OpenAPI specification file not found at {_options.OpenApiSpecPath}");
        }

        _validator = new OpenApiValidator(_options.OpenApiSpecPath, validatorLogger);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Validate request
        if (_options.ValidateRequests)
        {
            await ValidateRequestAsync(context);
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

    private async Task ValidateRequestAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var contentType = context.Request.ContentType;

        string? body = null;
        if (context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var error = _validator.ValidateRequest(method, path, contentType, body);

        if (error != null)
        {
            if (_options.LogErrors)
            {
                _logger.LogWarning(
                    "Request validation failed for {Method} {Path}: {Message}. Details: {Details}",
                    error.Method,
                    error.Path,
                    error.Message,
                    error.Details ?? "None");
            }

            if (_options.ThrowOnValidationError)
            {
                throw new InvalidOperationException($"Request validation failed: {error.Message}");
            }
        }
    }

    private async Task ValidateResponseAsync(HttpContext context, MemoryStream responseBody)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var statusCode = context.Response.StatusCode;
        var contentType = context.Response.ContentType;

        responseBody.Seek(0, SeekOrigin.Begin);
        string? body = null;
        if (responseBody.Length > 0)
        {
            using var reader = new StreamReader(responseBody, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);
        }

        var error = _validator.ValidateResponse(method, path, statusCode, contentType, body);

        if (error != null)
        {
            if (_options.LogErrors)
            {
                _logger.LogWarning(
                    "Response validation failed for {Method} {Path} with status {StatusCode}: {Message}. Details: {Details}",
                    error.Method,
                    error.Path,
                    error.StatusCode,
                    error.Message,
                    error.Details ?? "None");
            }

            if (_options.ThrowOnValidationError)
            {
                throw new InvalidOperationException($"Response validation failed: {error.Message}");
            }
        }
    }
}
