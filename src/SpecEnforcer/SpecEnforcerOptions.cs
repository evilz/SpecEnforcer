namespace SpecEnforcer;

/// <summary>
/// Configuration options for SpecEnforcer middleware.
/// </summary>
public class SpecEnforcerOptions
{
    /// <summary>
    /// Gets or sets the path to the OpenAPI specification file.
    /// </summary>
    public string OpenApiSpecPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to validate requests. Default is true.
    /// </summary>
    public bool ValidateRequests { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate responses. Default is true.
    /// </summary>
    public bool ValidateResponses { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log validation errors. Default is true.
    /// </summary>
    public bool LogErrors { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to throw exceptions on validation errors. Default is false.
    /// </summary>
    public bool ThrowOnValidationError { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable strict mode. When enabled, reports values that exist 
    /// in traffic but are not explicitly declared in the spec (undeclared properties, 
    /// parameters, headers, cookies). Default is false.
    /// </summary>
    public bool StrictMode { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable hard mode. When enabled, validation failures are 
    /// converted to HTTP error responses instead of just logging. Default is false.
    /// </summary>
    public bool HardMode { get; set; } = false;

    /// <summary>
    /// Gets or sets the HTTP status code to return when hard mode is enabled and 
    /// validation fails. Default is 400 (Bad Request).
    /// </summary>
    public int HardModeStatusCode { get; set; } = 400;

    /// <summary>
    /// Gets or sets a custom error response formatter for hard mode.
    /// If not set, uses the default format.
    /// </summary>
    public Func<ValidationError, object>? CustomErrorFormatter { get; set; }

    /// <summary>
    /// Gets or sets a list of path patterns to exclude from validation.
    /// Supports exact matches and wildcards (*). Default is empty.
    /// Examples: "/health", "/metrics", "/api/internal/*"
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to collect performance metrics for validation operations.
    /// Default is false.
    /// </summary>
    public bool EnableMetrics { get; set; } = false;
}
