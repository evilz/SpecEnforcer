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
}
