using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AspNetDebugDashboard.Core.Models;
using System.Reflection;

namespace AspNetDebugDashboard.Web.Controllers;

public class DebugHomeController : Controller
{
    private readonly DebugConfiguration _config;

    // The dashboard is a single self-contained HTML file built from /dashboard
    // and embedded in the assembly. Cached after the first read; only the
    // base-path token differs per configuration.
    private static string? _html;
    private static readonly object _lock = new();

    private readonly IServiceProvider _services;

    public DebugHomeController(IOptions<DebugConfiguration> config, IServiceProvider services)
    {
        _config = config.Value;
        _services = services;
    }

    [HttpGet("/_debug")]
    [HttpGet("/_debug/{*path}")]
    [HttpGet("/_custom-debug")]
    [HttpGet("/_custom-debug/{*path}")]
    public IActionResult Index()
    {
        if (!_config.IsEnabled) return NotFound();

        if (HttpContext?.Request?.Path.Value is { } requestPath &&
            !requestPath.StartsWith(_config.BasePath))
        {
            return NotFound();
        }

        var html = LoadHtml();
        if (html == null) return NotFound();

        var nav = Suite.SuiteNav.BuildJson(_services, _config.BasePath);
        return Content(
            html.Replace("__BASE_PATH__", _config.BasePath).Replace("__SUITE_NAV__", nav),
            "text/html");
    }

    private static string? LoadHtml()
    {
        if (_html != null) return _html;
        lock (_lock)
        {
            if (_html != null) return _html;
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("AspNetDebugDashboard.wwwroot.index.html");
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            _html = reader.ReadToEnd();
            return _html;
        }
    }
}
