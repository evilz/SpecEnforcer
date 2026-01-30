using SpecEnforcer;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure SpecEnforcer with all new features demonstrated
builder.Services.AddSpecEnforcer(options =>
{
    options.OpenApiSpecPath = Path.Combine(AppContext.BaseDirectory, "openapi.yaml");
    
    // Feature 1: Custom Error Response Formatter
    options.HardMode = true;
    options.HardModeStatusCode = 422;
    options.CustomErrorFormatter = (error) => new
    {
        code = "VALIDATION_FAILED",
        message = error.Message,
        timestamp = DateTime.UtcNow,
        path = error.Path,
        method = error.Method,
        errors = error.ValidationErrors,
        isStrictModeViolation = error.IsStrictModeViolation
    };

    // Feature 2: Path Exclusion Filter
    options.ExcludedPaths = new List<string>
    {
        "/health",
        "/metrics",
        "/admin/*",
        "/internal/*"
    };

    // Feature 3 & 4: Performance Metrics with Endpoint
    options.EnableMetrics = true;

    // Feature 5: Custom Validation Event Handler
    options.OnValidationError = (error) =>
    {
        // Custom logging or monitoring
        Console.WriteLine($"[VALIDATION ERROR] {error.ValidationType} - {error.Method} {error.Path}: {error.Message}");
        
        if (error.IsStrictModeViolation)
        {
            Console.WriteLine($"[STRICT MODE] Undeclared elements detected: {string.Join(", ", error.ValidationErrors)}");
        }
    };

    // Feature 6: HTTP Method Filtering
    // Only validate POST, PUT, PATCH operations (write operations)
    options.AllowedMethods = new List<string> { "POST", "PUT", "PATCH" };

    // Feature 7: Response Status Code Filtering
    // Only validate success and client error responses
    options.AllowedStatusCodes = new List<int> { 200, 201, 400, 422 };

    // Feature 8: Spec File Watching (for development)
    options.WatchSpecFile = builder.Environment.IsDevelopment();

    // Feature 9: Content-Type Filtering
    options.AllowedContentTypes = new List<string>
    {
        "application/json"
    };

    // Feature 10: Body Size Limits & Debug Options
    options.MaxBodySizeForValidation = 5 * 1024 * 1024; // 5MB
    options.IncludeBodiesInErrors = builder.Environment.IsDevelopment(); // Only in dev

    // Standard validation options
    options.ValidateRequests = true;
    options.ValidateResponses = true;
    options.LogErrors = true;
    options.StrictMode = false; // Enable to detect undeclared elements
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use SpecEnforcer middleware
app.UseSpecEnforcer();

// Feature 4: Map validation metrics endpoint
app.MapGet("/spec-enforcer/metrics", (ValidationMetrics metrics) =>
{
    return Results.Ok(new
    {
        requests = new
        {
            total = metrics.TotalRequestValidations,
            failures = metrics.TotalRequestFailures,
            averageTimeMs = metrics.AverageRequestValidationTimeMs,
            successRate = metrics.TotalRequestValidations > 0 
                ? (double)(metrics.TotalRequestValidations - metrics.TotalRequestFailures) / metrics.TotalRequestValidations * 100 
                : 100
        },
        responses = new
        {
            total = metrics.TotalResponseValidations,
            failures = metrics.TotalResponseFailures,
            averageTimeMs = metrics.AverageResponseValidationTimeMs,
            successRate = metrics.TotalResponseValidations > 0 
                ? (double)(metrics.TotalResponseValidations - metrics.TotalResponseFailures) / metrics.TotalResponseValidations * 100 
                : 100
        }
    });
});

// Health check endpoint (excluded from validation)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Internal admin endpoint (excluded from validation)
app.MapGet("/admin/stats", () => Results.Ok(new { users = 1234, requests = 56789 }));

// Sample API endpoints
app.MapGet("/users", () =>
{
    var users = new[]
    {
        new { id = 1, name = "Alice", email = "alice@example.com", role = "admin" },
        new { id = 2, name = "Bob", email = "bob@example.com", role = "user" }
    };
    return Results.Ok(users);
});

app.MapGet("/users/{id:int}", (int id) =>
{
    if (id <= 0)
    {
        return Results.BadRequest(new { error = "Invalid user ID" });
    }

    var user = new { id, name = $"User {id}", email = $"user{id}@example.com", role = "user" };
    return Results.Ok(user);
});

app.MapPost("/users", async ([FromBody] UserCreateRequest request) =>
{
    // Validation will happen automatically via SpecEnforcer
    var newUser = new
    {
        id = new Random().Next(1000, 9999),
        name = request.Name,
        email = request.Email,
        role = request.Role ?? "user",
        createdAt = DateTime.UtcNow
    };

    return Results.Created($"/users/{newUser.id}", newUser);
});

app.MapPut("/users/{id:int}", async (int id, [FromBody] UserUpdateRequest request) =>
{
    var updatedUser = new
    {
        id,
        name = request.Name,
        email = request.Email,
        role = request.Role,
        updatedAt = DateTime.UtcNow
    };

    return Results.Ok(updatedUser);
});

app.MapDelete("/users/{id:int}", (int id) =>
{
    return Results.NoContent();
});

// Endpoint to demonstrate validation errors
app.MapPost("/users/test-validation", ([FromBody] JsonElement body) =>
{
    // This will trigger validation errors if the body doesn't match the schema
    return Results.Ok(new { message = "This endpoint is for testing validation" });
});

// Endpoint to demonstrate strict mode
app.MapPost("/users/test-strict-mode", ([FromBody] JsonElement body) =>
{
    // Send extra properties not in the spec to see strict mode violations
    return Results.Ok(new { message = "Testing strict mode - send extra properties" });
});

app.Run();

// DTOs
public record UserCreateRequest(string Name, string Email, string? Role);
public record UserUpdateRequest(string Name, string Email, string Role);
