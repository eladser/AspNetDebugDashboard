# API Documentation

This document describes the API endpoints and programming interfaces provided by AspNetDebugDashboard.

## REST API Endpoints

The debug dashboard exposes several REST API endpoints for programmatic access to debug data.

### Base URL

All API endpoints are available under the configured base path (default: `/_debug/api`).

### Authentication

The API endpoints are currently unauthenticated and should only be used in development environments.

## Statistics API

### GET `/_debug/api/stats`

Returns overall statistics about the debug data.

**Response:**
```json
{
  "totalRequests": 150,
  "totalSqlQueries": 45,
  "totalExceptions": 2,
  "totalLogs": 89,
  "averageResponseTime": 245.6,
  "averageSqlTime": 12.3,
  "statusCodeDistribution": {
    "200": 120,
    "404": 25,
    "500": 5
  },
  "requestMethodDistribution": {
    "GET": 100,
    "POST": 30,
    "PUT": 15,
    "DELETE": 5
  },
  "exceptionTypeDistribution": {
    "ArgumentException": 1,
    "InvalidOperationException": 1
  },
  "slowestRequests": [
    {
      "id": "req-123",
      "method": "GET",
      "path": "/api/slow-endpoint",
      "executionTimeMs": 2500,
      "timestamp": "2025-01-07T10:30:00Z"
    }
  ],
  "slowestQueries": [
    {
      "id": "sql-456",
      "query": "SELECT * FROM Products WHERE...",
      "executionTimeMs": 150,
      "timestamp": "2025-01-07T10:25:00Z"
    }
  ],
  "lastUpdated": "2025-01-07T10:35:00Z"
}
```

## Requests API

### GET `/_debug/api/requests`

Returns a paginated list of HTTP requests.

**Query Parameters:**
- `page` (int): Page number (default: 1)
- `pageSize` (int): Items per page (default: 50)
- `method` (string): Filter by HTTP method
- `path` (string): Filter by path (contains)
- `statusCode` (int): Filter by status code
- `fromDate` (datetime): Filter from date
- `toDate` (datetime): Filter to date
- `sortBy` (string): Sort field (default: "timestamp")
- `sortDescending` (bool): Sort direction (default: true)

**Response:**
```json
{
  "items": [
    {
      "id": "req-123",
      "type": "Request",
      "method": "GET",
      "url": "https://localhost:5001/api/products",
      "path": "/api/products",
      "queryString": "?page=1&size=10",
      "statusCode": 200,
      "executionTimeMs": 125,
      "headers": {
        "User-Agent": "Mozilla/5.0...",
        "Accept": "application/json"
      },
      "requestBody": null,
      "responseBody": "[{\"id\": 1, \"name\": \"Product 1\"}]",
      "contentType": "application/json",
      "userAgent": "Mozilla/5.0...",
      "ipAddress": "127.0.0.1",
      "timestamp": "2025-01-07T10:30:00Z",
      "sqlQueries": [],
      "logs": [],
      "exception": null
    }
  ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 50,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

### GET `/_debug/api/requests/{id}`

Returns a specific request by ID.

**Response:**
```json
{
  "id": "req-123",
  "type": "Request",
  "method": "GET",
  "url": "https://localhost:5001/api/products",
  "path": "/api/products",
  "queryString": "?page=1&size=10",
  "statusCode": 200,
  "executionTimeMs": 125,
  "headers": {
    "User-Agent": "Mozilla/5.0...",
    "Accept": "application/json"
  },
  "requestBody": null,
  "responseBody": "[{\"id\": 1, \"name\": \"Product 1\"}]",
  "contentType": "application/json",
  "userAgent": "Mozilla/5.0...",
  "ipAddress": "127.0.0.1",
  "timestamp": "2025-01-07T10:30:00Z",
  "sqlQueries": [
    {
      "id": "sql-456",
      "query": "SELECT * FROM Products",
      "executionTimeMs": 25,
      "parameters": {},
      "isSuccessful": true
    }
  ],
  "logs": [
    {
      "id": "log-789",
      "level": "Info",
      "message": "Fetching products",
      "tag": "ProductService"
    }
  ],
  "exception": null
}
```

## SQL Queries API

### GET `/_debug/api/queries`

Returns a paginated list of SQL queries.

**Query Parameters:**
- `page` (int): Page number (default: 1)
- `pageSize` (int): Items per page (default: 50)
- `search` (string): Search in query text
- `fromDate` (datetime): Filter from date
- `toDate` (datetime): Filter to date
- `sortBy` (string): Sort field (default: "timestamp")
- `sortDescending` (bool): Sort direction (default: true)

**Response:**
```json
{
  "items": [
    {
      "id": "sql-456",
      "type": "SqlQuery",
      "query": "SELECT * FROM Products WHERE IsActive = @p0",
      "parameters": {
        "@p0": true
      },
      "executionTimeMs": 25,
      "rowsAffected": 0,
      "requestId": "req-123",
      "database": "SampleDb",
      "connectionString": "Server=localhost;Database=SampleDb;Trusted_Connection=true",
      "isSuccessful": true,
      "error": null,
      "timestamp": "2025-01-07T10:30:00Z"
    }
  ],
  "totalCount": 45,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

### GET `/_debug/api/queries/{id}`

Returns a specific SQL query by ID.

## Logs API

### GET `/_debug/api/logs`

Returns a paginated list of log entries.

**Query Parameters:**
- `page` (int): Page number (default: 1)
- `pageSize` (int): Items per page (default: 50)
- `level` (string): Filter by log level
- `tag` (string): Filter by tag
- `search` (string): Search in log message
- `fromDate` (datetime): Filter from date
- `toDate` (datetime): Filter to date
- `sortBy` (string): Sort field (default: "timestamp")
- `sortDescending` (bool): Sort direction (default: true)

**Response:**
```json
{
  "items": [
    {
      "id": "log-789",
      "type": "Log",
      "level": "Info",
      "message": "Fetching products from database",
      "category": null,
      "tag": "ProductService",
      "requestId": "req-123",
      "properties": {
        "UserId": 123,
        "Action": "GetProducts"
      },
      "stackTrace": null,
      "timestamp": "2025-01-07T10:30:00Z"
    }
  ],
  "totalCount": 89,
  "page": 1,
  "pageSize": 50,
  "totalPages": 2,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

### GET `/_debug/api/logs/{id}`

Returns a specific log entry by ID.

### POST `/_debug/api/logs`

Creates a new log entry.

**Request Body:**
```json
{
  "message": "Custom log message",
  "level": "Info",
  "tag": "CustomTag",
  "category": "CustomCategory",
  "properties": {
    "CustomProperty": "CustomValue"
  }
}
```

**Response:**
```json
{
  "id": "log-new-123"
}
```

## Exceptions API

### GET `/_debug/api/exceptions`

Returns a paginated list of exceptions.

**Query Parameters:**
- `page` (int): Page number (default: 1)
- `pageSize` (int): Items per page (default: 50)
- `search` (string): Search in exception message or type
- `fromDate` (datetime): Filter from date
- `toDate` (datetime): Filter to date
- `sortBy` (string): Sort field (default: "timestamp")
- `sortDescending` (bool): Sort direction (default: true)

**Response:**
```json
{
  "items": [
    {
      "id": "exc-123",
      "type": "Exception",
      "message": "Object reference not set to an instance of an object",
      "stackTrace": "at MyApp.Controllers.ProductsController.GetById(Int32 id)...",
      "source": "MyApp",
      "requestId": "req-456",
      "route": "/api/products/{id}",
      "method": "GET",
      "path": "/api/products/999",
      "exceptionType": "NullReferenceException",
      "innerException": null,
      "data": {},
      "timestamp": "2025-01-07T10:35:00Z"
    }
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

### GET `/_debug/api/exceptions/{id}`

Returns a specific exception by ID.

## Management API

### DELETE `/_debug/api/clear`

Clears all debug data.

**Response:**
```json
{
  "message": "All debug data cleared"
}
```

### POST `/_debug/api/cleanup`

Triggers manual cleanup of old entries.

**Response:**
```json
{
  "message": "Cleanup completed"
}
```

### GET `/_debug/api/config`

Returns current configuration.

**Response:**
```json
{
  "isEnabled": true,
  "maxEntries": 1000,
  "logRequestBodies": true,
  "logResponseBodies": false,
  "logSqlQueries": true,
  "logExceptions": true,
  "enableRealTimeUpdates": true
}
```

## Programming Interface

### IDebugLogger Interface

```csharp
public interface IDebugLogger
{
    Task LogAsync(string message, string level = "Info", string? tag = null, Dictionary<string, object>? properties = null);
    Task LogInfoAsync(string message, string? tag = null, Dictionary<string, object>? properties = null);
    Task LogWarningAsync(string message, string? tag = null, Dictionary<string, object>? properties = null);
    Task LogErrorAsync(string message, string? tag = null, Dictionary<string, object>? properties = null);
    Task LogSuccessAsync(string message, string? tag = null, Dictionary<string, object>? properties = null);
}
```

### Static DebugLogger Class

```csharp
public static class DebugLogger
{
    public static async Task LogAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    public static async Task LogAsync(string message, string level, string? tag = null, Dictionary<string, object>? properties = null)
    public static async Task InfoAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    public static async Task WarningAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    public static async Task ErrorAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
    public static async Task SuccessAsync(string message, string? tag = null, Dictionary<string, object>? properties = null)
}
```

### IDebugStorage Interface

```csharp
public interface IDebugStorage : IDisposable
{
    Task<string> StoreRequestAsync(RequestEntry request);
    Task<string> StoreSqlQueryAsync(SqlQueryEntry query);
    Task<string> StoreLogAsync(LogEntry log);
    Task<string> StoreExceptionAsync(ExceptionEntry exception);
    
    Task<PagedResult<RequestEntry>> GetRequestsAsync(DebugFilter filter);
    Task<PagedResult<SqlQueryEntry>> GetSqlQueriesAsync(DebugFilter filter);
    Task<PagedResult<LogEntry>> GetLogsAsync(DebugFilter filter);
    Task<PagedResult<ExceptionEntry>> GetExceptionsAsync(DebugFilter filter);
    
    Task<RequestEntry?> GetRequestAsync(string id);
    Task<SqlQueryEntry?> GetSqlQueryAsync(string id);
    Task<LogEntry?> GetLogAsync(string id);
    Task<ExceptionEntry?> GetExceptionAsync(string id);
    
    Task<DebugStats> GetStatsAsync();
    Task CleanupAsync(int maxEntries);
    Task ClearAllAsync();
}
```

### Configuration Classes

```csharp
public class DebugConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public string DatabasePath { get; set; } = "debug-dashboard.db";
    public string BasePath { get; set; } = "/_debug";
    public int MaxEntries { get; set; } = 1000;
    public bool LogRequestBodies { get; set; } = true;
    public bool LogResponseBodies { get; set; } = false;
    public bool LogSqlQueries { get; set; } = true;
    public bool LogExceptions { get; set; } = true;
    public bool EnableRealTimeUpdates { get; set; } = true;
    public List<string> ExcludedPaths { get; set; } = new();
    public List<string> ExcludedHeaders { get; set; } = new();
    public int MaxBodySize { get; set; } = 1024 * 1024; // 1MB
}
```

## Usage Examples

### Custom Logging

```csharp
// Using static logger
await DebugLogger.InfoAsync("User logged in", "Authentication", 
    new Dictionary<string, object> { { "UserId", 123 } });

// Using dependency injection
public class UserService
{
    private readonly IDebugLogger _logger;
    
    public UserService(IDebugLogger logger)
    {
        _logger = logger;
    }
    
    public async Task<User> GetUserAsync(int id)
    {
        await _logger.LogInfoAsync($"Fetching user {id}", "UserService");
        // Implementation
    }
}
```

### Custom Storage Implementation

```csharp
public class CustomDebugStorage : IDebugStorage
{
    public async Task<string> StoreRequestAsync(RequestEntry request)
    {
        // Custom storage logic
        return request.Id;
    }
    
    // Implement other methods...
}

// Register custom storage
builder.Services.AddSingleton<IDebugStorage, CustomDebugStorage>();
```

### API Client Example

```csharp
public class DebugDashboardClient
{
    private readonly HttpClient _httpClient;
    
    public DebugDashboardClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<DebugStats> GetStatsAsync()
    {
        var response = await _httpClient.GetAsync("/_debug/api/stats");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<DebugStats>(json);
    }
    
    public async Task<PagedResult<RequestEntry>> GetRequestsAsync(DebugFilter filter)
    {
        var query = BuildQueryString(filter);
        var response = await _httpClient.GetAsync($"/_debug/api/requests?{query}");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PagedResult<RequestEntry>>(json);
    }
}
```
