# Troubleshooting Guide

This guide covers common issues and their solutions when using AspNetDebugDashboard.

## Common Issues

### Dashboard Not Accessible

#### Problem: Cannot access `/_debug` endpoint

**Possible Causes:**
1. Middleware not registered
2. Not running in development environment
3. Dashboard disabled in configuration
4. Port conflicts

**Solutions:**

1. **Check Middleware Registration:**
   ```csharp
   // Ensure this is called
   app.UseDebugDashboard();
   ```

2. **Environment Check:**
   ```csharp
   // Force enable for testing
   app.UseDebugDashboard(forceEnable: true);
   ```

3. **Configuration Check:**
   ```csharp
   builder.Services.AddDebugDashboard(config =>
   {
       config.IsEnabled = true; // Ensure this is true
   });
   ```

4. **Port Conflicts:**
   - Check if another service is using the same port
   - Try a different port: `dotnet run --urls="https://localhost:5002"`

### SQL Queries Not Appearing

#### Problem: EF Core queries not being captured

**Possible Causes:**
1. Interceptor not registered
2. DbContext not configured correctly
3. SQL logging disabled
4. Using raw SQL queries

**Solutions:**

1. **Register Interceptor:**
   ```csharp
   builder.Services.AddDbContext<YourDbContext>(options =>
   {
       options.UseSqlServer(connectionString);
       options.AddDebugDashboard(builder.Services.BuildServiceProvider());
   });
   ```

2. **Alternative Registration:**
   ```csharp
   builder.Services.AddDebugDashboardEntityFramework();
   ```

3. **Enable SQL Logging:**
   ```csharp
   builder.Services.AddDebugDashboard(config =>
   {
       config.LogSqlQueries = true;
   });
   ```

4. **Check EF Core Version:**
   - Ensure you're using EF Core 7.0+
   - Update packages if necessary

### High Memory Usage

#### Problem: Application consuming too much memory

**Possible Causes:**
1. Too many entries stored
2. Large request/response bodies
3. Cleanup service not running
4. Memory leaks in storage

**Solutions:**

1. **Reduce Max Entries:**
   ```csharp
   builder.Services.AddDebugDashboard(config =>
   {
       config.MaxEntries = 500; // Reduce from default 1000
   });
   ```

2. **Disable Body Logging:**
   ```csharp
   builder.Services.AddDebugDashboard(config =>
   {
       config.LogRequestBodies = false;
       config.LogResponseBodies = false;
   });
   ```

3. **Limit Body Size:**
   ```csharp
   builder.Services.AddDebugDashboard(config =>
   {
       config.MaxBodySize = 10 * 1024; // 10KB instead of 1MB
   });
   ```

4. **Manual Cleanup:**
   ```csharp
   // Call cleanup API endpoint
   POST /_debug/api/cleanup
   ```

### Performance Issues

#### Problem: Application running slowly

**Possible Causes:**
1. Too much data being logged
2. Synchronous operations blocking
3. Database performance issues
4. Large SQL queries being logged

**Solutions:**

1. **Exclude Heavy Endpoints:**
   ```csharp
   builder.Services.AddDebugDashboard(config =>
   {
       config.ExcludedPaths = new List<string>
       {
           "/_debug",
           "/api/heavy-operation",
           "/api/file-upload"
       };
   });
   ```

2. **Optimize Database Path:**
   ```csharp
   builder.Services.AddDebugDashboard(config =>
   {
       config.DatabasePath = "debug-dashboard.db"; // Use faster storage
   });
   ```

3. **Reduce Logging:**
   ```csharp
   builder.Services.AddDebugDashboard(config =>
   {
       config.LogSqlQueries = false; // Disable if not needed
   });
   ```

### Build Errors

#### Problem: Compilation errors when using the package

**Common Errors:**

1. **Missing Dependencies:**
   ```
   Error: Package 'LiteDB' is not found
   ```
   **Solution:** Update to latest package version

2. **Target Framework Issues:**
   ```
   Error: Package 'AspNetDebugDashboard' is not compatible with net6.0
   ```
   **Solution:** Upgrade to .NET 7.0+

3. **Namespace Conflicts:**
   ```
   Error: The type 'DebugLogger' exists in both...
   ```
   **Solution:** Use fully qualified names

### Runtime Errors

#### Problem: Exceptions during application startup

**Common Errors:**

1. **Service Registration Error:**
   ```
   InvalidOperationException: Unable to resolve service for type 'IDebugStorage'
   ```
   **Solution:**
   ```csharp
   builder.Services.AddDebugDashboard(); // Ensure this is called
   ```

2. **Database Access Error:**
   ```
   UnauthorizedAccessException: Access to the path 'debug-dashboard.db' is denied
   ```
   **Solution:**
   ```csharp
   builder.Services.AddDebugDashboard(config =>
   {
       config.DatabasePath = Path.Combine(Path.GetTempPath(), "debug-dashboard.db");
   });
   ```

3. **Middleware Order Error:**
   ```
   InvalidOperationException: The middleware order is incorrect
   ```
   **Solution:**
   ```csharp
   // Ensure correct order
   app.UseDebugDashboard(); // Should be early in pipeline
   app.UseRouting();
   app.UseAuthorization();
   app.MapControllers();
   ```

### Browser Issues

#### Problem: Dashboard not loading in browser

**Possible Causes:**
1. JavaScript disabled
2. HTTPS certificate issues
3. Browser cache issues
4. CORS problems

**Solutions:**

1. **Enable JavaScript:**
   - Ensure JavaScript is enabled in your browser
   - Check browser console for errors

2. **HTTPS Certificate:**
   ```bash
   dotnet dev-certs https --trust
   ```

3. **Clear Browser Cache:**
   - Hard refresh: Ctrl+F5 (Windows) or Cmd+Shift+R (Mac)
   - Clear browser cache and cookies

4. **Check Network Tab:**
   - Open browser dev tools
   - Check Network tab for failed requests
   - Look for 404 or 500 errors

## Debugging Steps

### 1. Enable Detailed Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "AspNetDebugDashboard": "Debug"
    }
  }
}
```

### 2. Check Configuration

```csharp
// Add this to see current configuration
app.MapGet("/debug-config", (IOptions<DebugConfiguration> config) => 
{
    return Results.Ok(config.Value);
});
```

### 3. Verify Services Registration

```csharp
// Check if services are registered
app.MapGet("/debug-services", (IServiceProvider services) => 
{
    var storage = services.GetService<IDebugStorage>();
    var logger = services.GetService<IDebugLogger>();
    
    return Results.Ok(new 
    {
        StorageRegistered = storage != null,
        LoggerRegistered = logger != null
    });
});
```

### 4. Test API Endpoints

```bash
# Test if API is accessible
curl https://localhost:5001/_debug/api/stats

# Test configuration endpoint
curl https://localhost:5001/_debug/api/config
```

## Getting Help

If you're still experiencing issues:

1. **Check Existing Issues:** Search [GitHub issues](https://github.com/eladser/AspNetDebugDashboard/issues)
2. **Create New Issue:** Use the bug report template
3. **Provide Details:** Include:
   - .NET version
   - Package version
   - Operating system
   - Browser version
   - Minimal reproduction code
   - Error messages and stack traces

## Diagnostic Information

When reporting issues, please include:

```csharp
// Add this endpoint to collect diagnostic info
app.MapGet("/debug-diagnostics", (IServiceProvider services) => 
{
    var config = services.GetRequiredService<IOptions<DebugConfiguration>>().Value;
    var env = services.GetRequiredService<IWebHostEnvironment>();
    
    return Results.Ok(new 
    {
        Environment = env.EnvironmentName,
        Configuration = config,
        DotNetVersion = Environment.Version.ToString(),
        OS = Environment.OSVersion.ToString(),
        MachineName = Environment.MachineName,
        CurrentDirectory = Environment.CurrentDirectory
    });
});
```

## Performance Monitoring

To monitor the dashboard's performance impact:

```csharp
// Add performance monitoring
app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await next();
    sw.Stop();
    
    if (sw.ElapsedMilliseconds > 100) // Log slow requests
    {
        Console.WriteLine($"Slow request: {context.Request.Path} took {sw.ElapsedMilliseconds}ms");
    }
});
```
