using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AspNetDebugDashboard.Core.Models;
using Microsoft.Extensions.FileProviders;
using System.Reflection;

namespace AspNetDebugDashboard.Web.Controllers;

public class DebugHomeController : Controller
{
    private readonly DebugConfiguration _config;

    public DebugHomeController(IOptions<DebugConfiguration> config)
    {
        _config = config.Value;
    }

    [HttpGet("/_debug")]
    [HttpGet("/_debug/{*path}")]
    [HttpGet("/_custom-debug")]  
    [HttpGet("/_custom-debug/{*path}")]
    public IActionResult Index()
    {
        if (!_config.IsEnabled) return NotFound();
        
        // Only validate path if we have an HttpContext (skip for unit tests)
        if (HttpContext?.Request?.Path.Value != null)
        {
            var requestPath = HttpContext.Request.Path.Value;
            if (!requestPath.StartsWith(_config.BasePath))
            {
                return NotFound();
            }
        }
        
        // Return HTML content directly instead of using views
        var html = GetDashboardHtml();
        return Content(html, "text/html");
    }

    [HttpGet("/_debug/assets/{*path}")]
    [HttpGet("/_custom-debug/assets/{*path}")]
    public IActionResult Assets(string path)
    {
        if (!_config.IsEnabled) return NotFound();
        
        // Only validate path if we have an HttpContext (skip for unit tests)
        if (HttpContext?.Request?.Path.Value != null)
        {
            var requestPath = HttpContext.Request.Path.Value;
            if (!requestPath.StartsWith(_config.BasePath))
            {
                return NotFound();
            }
        }
        
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"AspNetDebugDashboard.wwwroot.{path.Replace('/', '.')}" .Replace("-", "_");
        
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return NotFound();
        
        var contentType = GetContentType(path);
        return File(stream, contentType);
    }

    private string GetDashboardHtml()
    {
        // Use the configured base path for API calls
        var basePath = _config.BasePath;
        
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>ASP.NET Debug Dashboard</title>
    <script src=""https://cdn.tailwindcss.com""></script>
    <script src=""https://unpkg.com/react@18/umd/react.development.js""></script>
    <script src=""https://unpkg.com/react-dom@18/umd/react-dom.development.js""></script>
    <script src=""https://unpkg.com/@babel/standalone/babel.min.js""></script>
    <link href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css"" rel=""stylesheet"">
    <style>
        .debug-dashboard {{
            font-family: 'Inter', system-ui, -apple-system, sans-serif;
        }}
        
        .code-block {{
            background: #1a1a1a;
            color: #e5e7eb;
            border-radius: 12px;
            padding: 1.25rem;
            overflow-x: auto;
            font-family: 'JetBrains Mono', 'Consolas', monospace;
            font-size: 0.875rem;
            line-height: 1.6;
            border: 1px solid #374151;
        }}
        
        .status-200 {{ @apply bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-300; }}
        .status-300 {{ @apply bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300; }}
        .status-400 {{ @apply bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300; }}
        .status-500 {{ @apply bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300; }}
        
        .method-get {{ @apply bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300; }}
        .method-post {{ @apply bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300; }}
        .method-put {{ @apply bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300; }}
        .method-delete {{ @apply bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300; }}
        .method-patch {{ @apply bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300; }}
        
        .level-info {{ @apply bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300; }}
        .level-warning {{ @apply bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300; }}
        .level-error {{ @apply bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300; }}
        .level-success {{ @apply bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300; }}
    </style>
    <script>
        tailwind.config = {{
            darkMode: 'class',
            theme: {{
                extend: {{
                    colors: {{
                        primary: {{
                            50: '#eff6ff',
                            500: '#3b82f6',
                            600: '#2563eb',
                            700: '#1d4ed8',
                            900: '#1e3a8a'
                        }}
                    }}
                }}
            }}
        }}
    </script>
</head>
<body class=""bg-gray-50 dark:bg-gray-900 debug-dashboard transition-colors duration-300"">
    <div id=""root""></div>
    
    <script type=""text/babel"">
        const {{ useState, useEffect, useCallback }} = React;
        
        const API_BASE = '{basePath}/api';
        
        function DebugDashboard() {{
            const [activeTab, setActiveTab] = useState('dashboard');
            const [darkMode, setDarkMode] = useState(localStorage.getItem('darkMode') === 'true');
            const [stats, setStats] = useState(null);
            const [loading, setLoading] = useState(false);
            
            // Apply dark mode
            useEffect(() => {{
                if (darkMode) {{
                    document.documentElement.classList.add('dark');
                }} else {{
                    document.documentElement.classList.remove('dark');
                }}
                localStorage.setItem('darkMode', darkMode);
            }}, [darkMode]);
            
            const fetchStats = useCallback(async () => {{
                try {{
                    setLoading(true);
                    const response = await fetch(`${{API_BASE}}/stats`);
                    if (response.ok) {{
                        const data = await response.json();
                        setStats(data);
                    }}
                }} catch (error) {{
                    console.error('Error fetching stats:', error);
                }} finally {{
                    setLoading(false);
                }}
            }}, []);
            
            useEffect(() => {{
                fetchStats();
            }}, [fetchStats]);
            
            const StatsCard = ({{ title, value, icon, color }}) => (
                <div className=""bg-white dark:bg-gray-800 rounded-xl shadow-lg p-6 border-l-4 border-blue-500"">
                    <div className=""flex items-center justify-between"">
                        <div>
                            <p className=""text-sm font-medium text-gray-600 dark:text-gray-400"">{{title}}</p>
                            <p className=""text-3xl font-bold text-gray-900 dark:text-white"">{{value || 0}}</p>
                        </div>
                        <div className=""p-3 rounded-full bg-blue-100 dark:bg-blue-900/30"">
                            <i className={{`fas ${{icon}} text-blue-600 dark:text-blue-400 text-xl`}}></i>
                        </div>
                    </div>
                </div>
            );
            
            const LoadingSpinner = () => (
                <div className=""flex items-center justify-center p-8"">
                    <div className=""animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500""></div>
                </div>
            );
            
            return (
                <div className=""min-h-screen bg-gray-50 dark:bg-gray-900 transition-colors duration-300"">
                    {{/* Header */}}
                    <div className=""bg-white dark:bg-gray-800 shadow-sm border-b border-gray-200 dark:border-gray-700"">
                        <div className=""max-w-7xl mx-auto px-4 sm:px-6 lg:px-8"">
                            <div className=""flex justify-between items-center py-4"">
                                <div>
                                    <h1 className=""text-2xl font-bold text-gray-900 dark:text-white"">ASP.NET Debug Dashboard</h1>
                                    <p className=""text-gray-600 dark:text-gray-400"">Real-time debugging and monitoring</p>
                                </div>
                                
                                <div className=""flex items-center space-x-4"">
                                    <button
                                        onClick={{() => setDarkMode(!darkMode)}}
                                        className=""p-2 rounded-lg text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-500""
                                    >
                                        <i className={{`fas ${{darkMode ? 'fa-sun' : 'fa-moon'}}`}}></i>
                                    </button>
                                    
                                    <button
                                        onClick={{fetchStats}}
                                        className=""bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors""
                                    >
                                        <i className=""fas fa-refresh mr-2""></i>Refresh
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                    
                    <div className=""max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8"">
                        {{/* Dashboard Content */}}
                        {{loading ? (
                            <LoadingSpinner />
                        ) : (
                            <div className=""grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6"">
                                <StatsCard
                                    title=""Total Requests""
                                    value={{stats?.totalRequests}}
                                    icon=""fa-globe""
                                    color=""blue""
                                />
                                <StatsCard
                                    title=""SQL Queries""
                                    value={{stats?.totalSqlQueries}}
                                    icon=""fa-database""
                                    color=""green""
                                />
                                <StatsCard
                                    title=""Log Entries""
                                    value={{stats?.totalLogs}}
                                    icon=""fa-file-alt""
                                    color=""yellow""
                                />
                                <StatsCard
                                    title=""Exceptions""
                                    value={{stats?.totalExceptions}}
                                    icon=""fa-exclamation-triangle""
                                    color=""red""
                                />
                            </div>
                        )}}
                        
                        {{/* Success Message */}}
                        <div className=""mt-8 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg p-6"">
                            <div className=""flex items-center"">
                                <i className=""fas fa-check-circle text-green-500 text-xl mr-3""></i>
                                <div>
                                    <h3 className=""text-lg font-medium text-green-800 dark:text-green-200"">Debug Dashboard is Active</h3>
                                    <p className=""text-green-700 dark:text-green-300"">The dashboard is successfully running and collecting debug data.</p>
                                </div>
                            </div>
                        </div>
                        
                        {{/* API Links */}}
                        <div className=""mt-8 bg-white dark:bg-gray-800 rounded-xl shadow-lg p-6"">
                            <h3 className=""text-lg font-semibold text-gray-900 dark:text-white mb-4"">
                                <i className=""fas fa-link mr-2 text-blue-500""></i>
                                Available API Endpoints
                            </h3>
                            <div className=""grid grid-cols-1 md:grid-cols-2 gap-4"">
                                <a href=""{basePath}/api/stats"" className=""block p-3 bg-gray-50 dark:bg-gray-700 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"">
                                    <div className=""font-medium text-gray-900 dark:text-white"">Statistics</div>
                                    <div className=""text-sm text-gray-600 dark:text-gray-400"">{basePath}/api/stats</div>
                                </a>
                                <a href=""{basePath}/api/requests"" className=""block p-3 bg-gray-50 dark:bg-gray-700 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"">
                                    <div className=""font-medium text-gray-900 dark:text-white"">HTTP Requests</div>
                                    <div className=""text-sm text-gray-600 dark:text-gray-400"">{basePath}/api/requests</div>
                                </a>
                                <a href=""{basePath}/api/queries"" className=""block p-3 bg-gray-50 dark:bg-gray-700 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"">
                                    <div className=""font-medium text-gray-900 dark:text-white"">SQL Queries</div>
                                    <div className=""text-sm text-gray-600 dark:text-gray-400"">{basePath}/api/queries</div>
                                </a>
                                <a href=""{basePath}/api/logs"" className=""block p-3 bg-gray-50 dark:bg-gray-700 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"">
                                    <div className=""font-medium text-gray-900 dark:text-white"">Logs</div>
                                    <div className=""text-sm text-gray-600 dark:text-gray-400"">{basePath}/api/logs</div>
                                </a>
                                <a href=""{basePath}/api/exceptions"" className=""block p-3 bg-gray-50 dark:bg-gray-700 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"">
                                    <div className=""font-medium text-gray-900 dark:text-white"">Exceptions</div>
                                    <div className=""text-sm text-gray-600 dark:text-gray-400"">{basePath}/api/exceptions</div>
                                </a>
                                <a href=""{basePath}/api/health"" className=""block p-3 bg-gray-50 dark:bg-gray-700 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"">
                                    <div className=""font-medium text-gray-900 dark:text-white"">Health Check</div>
                                    <div className=""text-sm text-gray-600 dark:text-gray-400"">{basePath}/api/health</div>
                                </a>
                            </div>
                        </div>
                    </div>
                </div>
            );
        }}
        
        ReactDOM.render(<DebugDashboard />, document.getElementById('root'));
    </script>
</body>
</html>";
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
