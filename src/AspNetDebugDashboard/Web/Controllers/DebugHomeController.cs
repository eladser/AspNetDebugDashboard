using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AspNetDebugDashboard.Core.Models;
using Microsoft.Extensions.FileProviders;
using System.Reflection;

namespace AspNetDebugDashboard.Web.Controllers;

[Route("/_debug")]
public class DebugHomeController : Controller
{
    private readonly DebugConfiguration _config;

    public DebugHomeController(IOptions<DebugConfiguration> config)
    {
        _config = config.Value;
    }

    [HttpGet]
    [HttpGet("{*path}")]
    public IActionResult Index()
    {
        if (!_config.IsEnabled) return NotFound();
        
        return View();
    }

    [HttpGet("assets/{*path}")]
    public IActionResult Assets(string path)
    {
        if (!_config.IsEnabled) return NotFound();
        
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"AspNetDebugDashboard.wwwroot.{path.Replace('/', '.')}".Replace("-", "_");
        
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return NotFound();
        
        var contentType = GetContentType(path);
        return File(stream, contentType);
    }

    private string GetContentType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".html" => "text/html",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".eot" => "application/vnd.ms-fontobject",
            _ => "application/octet-stream"
        };
    }
}
