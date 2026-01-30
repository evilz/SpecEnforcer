using SpecEnforcer;

var builder = WebApplication.CreateBuilder(args);

// Add SpecEnforcer with configuration
builder.Services.AddSpecEnforcer(options =>
{
    options.OpenApiSpecPath = Path.Combine(AppContext.BaseDirectory, "openapi.yaml");
    options.ValidateRequests = true;
    options.ValidateResponses = true;
    options.LogErrors = true;
    options.ThrowOnValidationError = false;
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

// Use SpecEnforcer middleware to validate requests and responses
app.UseSpecEnforcer();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)((double)TemperatureC / 0.5556);
}
