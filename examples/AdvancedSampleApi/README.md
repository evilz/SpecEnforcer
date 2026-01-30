# Advanced Sample API

This sample application demonstrates **all 10 new features** added to SpecEnforcer.

## Features Demonstrated

### 1. Custom Error Response Formatter
The app uses a custom error formatter that returns a consistent error format with code, message, and timestamp.

### 2. Path Exclusion Filter
The following paths are excluded from validation:
- `/health` - Health check endpoint
- `/metrics` - Metrics endpoint
- `/admin/*` - All admin endpoints
- `/internal/*` - All internal endpoints

### 3. Performance Metrics
Metrics are enabled and tracked for all validation operations.

### 4. Validation Metrics Endpoint
Access validation statistics at: `GET /spec-enforcer/metrics`

### 5. Custom Validation Event Handler
A custom callback logs all validation errors to the console with detailed information.

### 6. HTTP Method Filtering
Only POST, PUT, and PATCH operations are validated (write operations only).

### 7. Response Status Code Filtering
Only responses with status codes 200, 201, 400, and 422 are validated.

### 8. Spec File Watching
In development mode, the OpenAPI spec file is watched for changes and reloaded automatically.

### 9. Content-Type Filtering
Only `application/json` content is validated.

### 10. Body Size Limits & Debug Options
- Request/response bodies larger than 5MB skip validation
- Bodies are included in error messages in development mode only

## Running the Application

```bash
cd examples/AdvancedSampleApi
dotnet run
```

The application will start on `http://localhost:5000`.

## Testing the Features

### 1. Check Health Endpoint (Excluded from Validation)
```bash
curl http://localhost:5000/health
```

### 2. View Validation Metrics
```bash
curl http://localhost:5000/spec-enforcer/metrics
```

### 3. Create a Valid User (POST - Validated)
```bash
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{
    "name": "John Doe",
    "email": "john@example.com",
    "role": "user"
  }'
```

### 4. Create an Invalid User (Trigger Validation Error)
```bash
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{
    "name": "",
    "email": "invalid-email"
  }'
```

Expected response (422 with custom error format):
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

### 5. Test Strict Mode (Enable in Program.cs)
To enable strict mode, change `options.StrictMode = false;` to `true` in Program.cs, then:

```bash
curl -X POST http://localhost:5000/users/test-validation \
  -H "Content-Type: application/json" \
  -d '{
    "name": "John Doe",
    "email": "john@example.com",
    "role": "user",
    "extraProperty": "not in spec"
  }'
```

This will trigger a strict mode violation for the undeclared `extraProperty`.

### 6. Get All Users (GET - Not Validated due to method filter)
```bash
curl http://localhost:5000/users
```

This won't be validated because AllowedMethods only includes POST, PUT, PATCH.

### 7. Update a User (PUT - Validated)
```bash
curl -X PUT http://localhost:5000/users/1 \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Jane Doe",
    "email": "jane@example.com",
    "role": "admin"
  }'
```

### 8. View Swagger UI
Open your browser and navigate to:
```
http://localhost:5000/swagger
```

## Configuration

All features are configured in `Program.cs`. You can modify the options to:
- Enable/disable strict mode
- Change excluded paths
- Modify allowed methods and status codes
- Adjust body size limits
- Customize the error formatter

## Key Files

- `Program.cs` - Main application with all feature configurations
- `openapi.yaml` - OpenAPI specification
- `appsettings.json` - Application settings

## Monitoring

Watch the console output to see:
- Custom validation error callbacks in action
- Validation statistics
- Strict mode violations (when enabled)

## Performance Testing

Use the metrics endpoint to monitor validation performance:
```bash
# Make several requests
for i in {1..10}; do
  curl -X POST http://localhost:5000/users \
    -H "Content-Type: application/json" \
    -d '{"name":"User'$i'","email":"user'$i'@example.com"}' &
done

# Check metrics
curl http://localhost:5000/spec-enforcer/metrics
```

This will show:
- Total validations performed
- Failure counts
- Average validation time
- Success rates

## Notes

- The custom error formatter provides a consistent API error format
- Path exclusions prevent validation overhead on health/metrics endpoints
- Method filtering focuses validation on write operations where it matters most
- Performance metrics help identify validation bottlenecks
- Strict mode helps enforce API governance and detect undocumented behavior
