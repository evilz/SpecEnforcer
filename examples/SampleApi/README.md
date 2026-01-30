# Sample API Example

This is a simple example demonstrating how to use SpecEnforcer middleware in an ASP.NET Core Web API.

## Running the Example

1. Navigate to the example directory:
```bash
cd examples/SampleApi
```

2. Run the application:
```bash
dotnet run
```

3. Test the API:
```bash
# Valid request - should work fine
curl http://localhost:5020/weatherforecast

# Invalid request - will log validation error
curl http://localhost:5020/invalid-endpoint
```

## What It Does

This example:
- Sets up a minimal ASP.NET Core Web API
- Configures SpecEnforcer middleware to validate requests and responses
- Uses the `openapi.yaml` file as the contract
- Logs validation errors to the console

## Observe Validation

The middleware will:
1. ✅ Allow valid requests to `/weatherforecast`
2. ❌ Log an error for requests to undefined paths
3. ✅ Validate that responses match the expected format
4. 📝 Log all validation failures with detailed information

Check the console output to see validation logs!
