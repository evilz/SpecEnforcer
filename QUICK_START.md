# SpecEnforcer - Quick Start Guide

This guide will help you get started with the Advanced Sample Application that demonstrates all 10 new SpecEnforcer features.

## Prerequisites

- .NET 8.0 SDK or later
- Your favorite API testing tool (curl, Postman, VS Code REST Client, etc.)

## Step 1: Clone and Build

```bash
cd E:\PROJECTS\GITHUB\SpecEnforcer
dotnet build
```

## Step 2: Run the Advanced Sample

```bash
cd examples/AdvancedSampleApi
dotnet run
```

The API will start on `http://localhost:5000`

## Step 3: Test the Features

### Feature 1: Custom Error Response Formatter

Send an invalid request to see the custom error format:

```bash
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{"name":"","email":"bad-email"}'
```

**Expected Response (422):**
```json
{
  "code": "VALIDATION_FAILED",
  "message": "Request validation failed",
  "timestamp": "2026-01-30T20:30:00Z",
  "path": "/users",
  "method": "POST",
  "errors": [
    "request body.name: String length 0 is less than minimum 1",
    "request body.email: Value does not match format 'email'"
  ],
  "isStrictModeViolation": false
}
```

### Feature 2: Path Exclusion Filter

These endpoints are excluded from validation:

```bash
# Health check - always works
curl http://localhost:5000/health

# Admin endpoint - excluded
curl http://localhost:5000/admin/stats
```

### Feature 3 & 4: Performance Metrics & Metrics Endpoint

Make some requests then check metrics:

```bash
# Make a few requests
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{"name":"Alice","email":"alice@example.com"}'

# View metrics
curl http://localhost:5000/spec-enforcer/metrics
```

**Expected Response:**
```json
{
  "requests": {
    "total": 1,
    "failures": 0,
    "averageTimeMs": 2.5,
    "successRate": 100
  },
  "responses": {
    "total": 1,
    "failures": 0,
    "averageTimeMs": 1.8,
    "successRate": 100
  }
}
```

### Feature 5: Custom Validation Event Handler

Watch the console output when validation errors occur. You'll see:
```
[VALIDATION ERROR] Request - POST /users: Request validation failed
```

### Feature 6: HTTP Method Filtering

Only POST, PUT, PATCH are validated:

```bash
# This GET request is NOT validated (method not in filter)
curl http://localhost:5000/users

# This POST request IS validated
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{"name":"Bob","email":"bob@example.com"}'
```

### Feature 7: Response Status Code Filtering

Only responses with status codes 200, 201, 400, 422 are validated.

### Feature 8: Spec File Watching

In development mode, you can edit `openapi.yaml` and the changes will be picked up automatically (requires restart in current implementation).

### Feature 9: Content-Type Filtering

Only `application/json` is validated:

```bash
# This is validated
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{"name":"Charlie","email":"charlie@example.com"}'

# Other content types would be skipped (if configured)
```

### Feature 10: Body Size Limits & Debug Options

- Bodies larger than 5MB skip validation
- In development mode, request/response bodies are included in error messages

## Testing Scenarios

### ✅ Valid Request
```bash
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{"name":"John Doe","email":"john@example.com","role":"user"}'
```

### ❌ Invalid: Missing Required Field
```bash
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{"email":"john@example.com"}'
```

### ❌ Invalid: Wrong Type
```bash
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{"name":"John","email":"john@example.com","role":"invalid_role"}'
```

### ❌ Invalid: Constraint Violation
```bash
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{"name":"","email":"not-an-email"}'
```

## Enabling Strict Mode

To see strict mode in action:

1. Edit `Program.cs`, line ~48:
   ```csharp
   options.StrictMode = true; // Change from false to true
   ```

2. Restart the application

3. Send a request with extra properties:
   ```bash
   curl -X POST http://localhost:5000/users/test-validation \
     -H "Content-Type: application/json" \
     -d '{"name":"John","email":"john@example.com","extraProperty":"not in spec"}'
   ```

4. You'll see a strict mode violation in the response

## Swagger UI

Open your browser and navigate to:
```
http://localhost:5000/swagger
```

You can test all endpoints interactively!

## Tips

1. **Watch Console Output**: The custom validation handler logs all errors
2. **Monitor Metrics**: Check `/spec-enforcer/metrics` to track validation performance
3. **Use the .http File**: Open `AdvancedSampleApi.http` in VS Code for quick testing
4. **Experiment**: Try different invalid requests to see validation in action

## What's Next?

1. Read the full documentation in `examples/AdvancedSampleApi/README.md`
2. Explore `FEATURES_ADDED.md` for detailed feature descriptions
3. Customize the configuration in `Program.cs` to suit your needs
4. Integrate SpecEnforcer into your own projects!

## Common Issues

**Q: Validation isn't working**
A: Check that the endpoint method is in `AllowedMethods` (POST, PUT, PATCH)

**Q: Metrics show zero**
A: Make sure `EnableMetrics = true` in the configuration

**Q: Strict mode violations not showing**
A: Set `StrictMode = true` in Program.cs

## Support

For more information, see:
- Main README: `README.md`
- Features Documentation: `FEATURES_ADDED.md`
- Sample API README: `examples/AdvancedSampleApi/README.md`
