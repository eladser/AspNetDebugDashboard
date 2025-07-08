# Getting Started with ASP.NET Debug Dashboard

Welcome to ASP.NET Debug Dashboard! This guide will help you get up and running in minutes.

## 📋 Prerequisites

- **.NET 7.0 or 8.0 SDK** - [Download here](https://dotnet.microsoft.com/download)
- **ASP.NET Core application** - Existing or new project
- **Visual Studio 2022** or **VS Code** (recommended)

## 🚀 Quick Installation

### Step 1: Install the NuGet Package

```bash
# Using .NET CLI
dotnet add package AspNetDebugDashboard

# Using Package Manager Console (Visual Studio)
Install-Package AspNetDebugDashboard

# Using PackageReference (in .csproj)
<PackageReference Include="AspNetDebugDashboard" Version="1.0.0" />
```

### Step 2: Configure Services

Add the following to your `Program.cs`:

```csharp
using AspNetDebugDashboard.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add your existing services
builder.Services.AddControllers();

// 🎯 Add Debug Dashboard (this is all you need!)
builder.Services.AddDebugDashboard();

var app = builder.Build();

// 🎯 Enable Debug Dashboard middleware
app.UseDebugDashboard();

// Your existing middleware
app.UseRouting();
app.MapControllers();

app.Run();
```

### Step 3: Access Your Dashboard

1. **Start your application**: `dotnet run`
2. **Open your browser**: Navigate to `https://localhost:5001/_debug`
3. **Enjoy debugging!** 🎉

## 🔧 Entity Framework Integration

If you're using Entity Framework Core, add SQL query monitoring:

```csharp
builder.Services.AddDbContext<YourDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    
    // 🎯 Add this line for SQL query monitoring
    options.AddDebugDashboardInterceptor();
});
```

## ⚙️ Basic Configuration

### Development vs Production

```csharp
builder.Services.AddDebugDashboard(options =>
{
    // Automatically enabled in Development, disabled in Production
    options.IsEnabled = builder.Environment.IsDevelopment();
    
    // Optional: Force enable/disable
    // options.IsEnabled = true;
});
```

### Essential Settings

```csharp
builder.Services.AddDebugDashboard(options =>
{
    // Request/Response logging
    options.LogRequestBodies = true;
    options.LogResponseBodies = true;
    
    // SQL query monitoring
    options.LogSqlQueries = true;
    
    // Exception tracking
    options.LogExceptions = true;
    
    // Real-time updates
    options.EnableRealTimeUpdates = true;
    
    // Storage settings
    options.MaxEntries = 10000;
    options.RetentionPeriod = TimeSpan.FromDays(7);
});
```

## 💡 Your First Debug Session

### 1. Make Some Requests

```bash
# Test your API endpoints
curl https://localhost:5001/api/products
curl -X POST https://localhost:5001/api/products -d '{"name":"Test"}'
```

### 2. Generate Some Logs

```csharp
public class ProductsController : ControllerBase
{
    private readonly IDebugLogger _debugLogger;
    
    public ProductsController(IDebugLogger debugLogger)
    {
        _debugLogger = debugLogger;
    }
    
    [HttpGet]
    public IActionResult GetProducts()
    {
        _debugLogger.LogInfo("Fetching all products");
        
        // Your logic here
        var products = GetAllProducts();
        
        _debugLogger.LogSuccess($"Returned {products.Count} products");
        
        return Ok(products);
    }
}
```

### 3. Explore the Dashboard

Navigate to `/_debug` and explore:

- **📊 Dashboard**: Overview with real-time stats
- **🌐 Requests**: All HTTP requests with timing
- **🗃️ SQL Queries**: Database queries with performance
- **📝 Logs**: Your custom log messages
- **❌ Exceptions**: Any errors that occurred

## 🎨 Dashboard Features

### Real-time Updates
- **Live data refresh** - See new requests as they happen
- **Auto-refresh toggle** - Control update frequency
- **Performance indicators** - Real-time metrics

### Search & Filtering
- **Global search** - Find anything across all data
- **Date range filtering** - Focus on specific time periods
- **Status code filtering** - Find errors quickly
- **Method filtering** - GET, POST, PUT, DELETE
- **Performance filtering** - Slow requests and queries

### Themes & Customization
- **🌙 Dark mode** - Easy on the eyes for long debugging sessions
- **☀️ Light mode** - Classic clean interface
- **📱 Mobile responsive** - Debug on any device
- **⚡ Fast interface** - Optimized for developer productivity

## 🔍 Common Use Cases

### 1. API Development
```csharp
[HttpPost("orders")]
public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
{
    DebugLogger.Log("Order creation started", "info", new { 
        CustomerId = request.CustomerId 
    });
    
    try
    {
        var order = await _orderService.CreateAsync(request);
        
        DebugLogger.Log("Order created successfully", "success", new { 
            OrderId = order.Id 
        });
        
        return Ok(order);
    }
    catch (Exception ex)
    {
        DebugLogger.Log($"Order creation failed: {ex.Message}", "error");
        throw;
    }
}
```

### 2. Performance Monitoring
```csharp
public async Task<Product> GetProductWithReviews(int productId)
{
    var stopwatch = Stopwatch.StartNew();
    
    var product = await _context.Products
        .Include(p => p.Reviews)
        .FirstOrDefaultAsync(p => p.Id == productId);
    
    stopwatch.Stop();
    
    DebugLogger.Log($"Product query completed", "info", new {
        ProductId = productId,
        ExecutionTime = stopwatch.ElapsedMilliseconds,
        ReviewCount = product?.Reviews?.Count ?? 0
    });
    
    return product;
}
```

### 3. Error Debugging
```csharp
public async Task<IActionResult> ProcessPayment(PaymentRequest request)
{
    try
    {
        await _paymentService.ProcessAsync(request);
        return Ok();
    }
    catch (PaymentException ex)
    {
        // Exception will automatically be captured in the dashboard
        // with full stack trace and request context
        
        return BadRequest(new { error = ex.Message });
    }
}
```

## 📱 Mobile Debugging

The dashboard is fully responsive and works great on mobile devices:

1. **Connect your phone** to the same network as your development machine
2. **Find your local IP** (e.g., 192.168.1.100)
3. **Access the dashboard**: `http://192.168.1.100:5000/_debug`
4. **Debug on the go!** Perfect for testing mobile apps

## 🔐 Security Notes

### Development Only (Default)
```csharp
// Safe for development - automatically disabled in production
builder.Services.AddDebugDashboard();
```

### Production Considerations
```csharp
// If you need debugging in production (use with caution)
builder.Services.AddDebugDashboard(options =>
{
    options.IsEnabled = builder.Configuration.GetValue<bool>("EnableDebugDashboard");
    options.LogRequestBodies = false;  // Disable sensitive data
    options.LogResponseBodies = false;
    options.ExcludedPaths = new[] { "/admin", "/api/auth" };
    options.ExcludedHeaders = new[] { "Authorization", "Cookie" };
});
```

## 🧪 Testing Integration

Debug your tests in real-time:

```csharp
[Fact]
public async Task CreateProduct_ShouldReturnSuccess()
{
    // Your test setup
    var client = _factory.CreateClient();
    
    // Make request - will be captured in dashboard
    var response = await client.PostAsync("/api/products", content);
    
    // Navigate to /_debug to see the test request
    response.EnsureSuccessStatusCode();
}
```

## 🆘 Need Help?

### Quick Troubleshooting

1. **Dashboard not loading?**
   - Check that `UseDebugDashboard()` is called
   - Verify you're in Development environment
   - Ensure port 5001 is accessible

2. **No data appearing?**
   - Make some requests to your API
   - Check that middleware is registered
   - Verify database permissions

3. **Performance issues?**
   - Reduce `MaxEntries` configuration
   - Disable body logging if not needed
   - Check excluded paths configuration

### Getting Support

- **📚 Documentation**: [Complete guides](../docs/)
- **🐛 Issues**: [GitHub Issues](https://github.com/eladser/AspNetDebugDashboard/issues)
- **💬 Discussions**: [GitHub Discussions](https://github.com/eladser/AspNetDebugDashboard/discussions)
- **📧 Email**: Support available for enterprise users

## 🎯 Next Steps

Now that you have the basics working:

1. **📖 Read the [Configuration Guide](CONFIGURATION.md)** - Learn all available options
2. **🔒 Review [Security Best Practices](SECURITY.md)** - Secure your debugging setup
3. **🚀 Explore [Advanced Features](../README.md#features)** - Real-time updates, exports, analytics
4. **🤝 Join the Community** - Share feedback and contribute

## 🌟 Success Stories

> "ASP.NET Debug Dashboard helped us identify a performance bottleneck that was causing 2-second delays in our API. Fixed it in 10 minutes!" - *Senior Developer*

> "The real-time dashboard is amazing for debugging integration tests. We can see exactly what's happening without adding console logs everywhere." - *Team Lead*

> "Finally, a debugging tool that's as beautiful as it is functional. The dark mode is perfect for long debugging sessions." - *Full Stack Developer*

---

**Ready to transform your debugging experience?** Get started now and join thousands of developers who are debugging faster and more efficiently with ASP.NET Debug Dashboard! 🚀
