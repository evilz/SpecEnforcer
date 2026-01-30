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
    /// <summary>
    /// Gets or sets a custom callback to invoke when a validation error occurs.
    /// Allows custom logging, monitoring, or other side effects.
    /// </summary>
    public Action<ValidationError>? OnValidationError { get; set; }
    /// <summary>
    /// Gets or sets HTTP methods to validate. If empty, all methods are validated.
    /// Default is empty (validate all methods).
    /// </summary>
    public List<string> AllowedMethods { get; set; } = new();
    /// <summary>
    /// Gets or sets response status codes to validate. If empty, all responses are validated.
    /// Default is empty (validate all status codes).
    /// </summary>
    public List<int> AllowedStatusCodes { get; set; } = new();
    /// <summary>
    /// Gets or sets whether to watch the OpenAPI spec file for changes and reload automatically.
    /// Default is false.
    /// </summary>
    public bool WatchSpecFile { get; set; } = false;
    /// <summary>
    /// Gets or sets content types to validate. If empty, all content types are validated.
    /// Default is empty (validate all).
    /// Example: ["application/json", "application/xml"]
    /// </summary>
    public List<string> AllowedContentTypes { get; set; } = new();
    /// <summary>
    /// Gets or sets the maximum request body size to validate in bytes.
    /// Bodies larger than this will skip validation. Default is 10MB.
    /// Set to 0 for unlimited.
    /// </summary>
    public long MaxBodySizeForValidation { get; set; } = 10 * 1024 * 1024; // 10MB
    /// <summary>
    /// Gets or sets whether to include request/response bodies in validation error messages.
    /// Useful for debugging but may expose sensitive data in logs. Default is false.
    /// </summary>
    public bool IncludeBodiesInErrors { get; set; } = false;
}
