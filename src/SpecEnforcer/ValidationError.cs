namespace SpecEnforcer;

/// <summary>
/// Represents a validation error that occurred during request or response validation.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Gets or sets the type of validation (Request or Response).
    /// </summary>
    public string ValidationType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP method (GET, POST, etc.).
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status code (for response validation).
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional details about the error.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
