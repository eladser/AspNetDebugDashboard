    [HttpGet("performance")]
    public async Task<ActionResult> GetPerformanceMetrics()
    {
        if (!_config.IsEnabled) return NotFound();

        var stats = await _storage.GetStatsAsync();
        
        // Get requests for performance analysis
        var recentRequests = await _storage.GetRequestsAsync(new DebugFilter 
        { 
            PageSize = 1000,
            SortBy = "timestamp",
            SortDescending = true
        });

        var requests = recentRequests.Items.Where(r => r.Timestamp > DateTime.UtcNow.AddHours(-1)).ToList();
        
        var performanceMetrics = new
        {
            TotalRequests = requests.Count,
            AverageResponseTime = requests.Any() ? requests.Average(r => r.ExecutionTimeMs) : 0,
            MedianResponseTime = requests.Any() ? GetMedian(requests.Select(r => (double)r.ExecutionTimeMs).ToList()) : 0,
            P95ResponseTime = requests.Any() ? GetPercentile(requests.Select(r => (double)r.ExecutionTimeMs).ToList(), 95) : 0,
            P99ResponseTime = requests.Any() ? GetPercentile(requests.Select(r => (double)r.ExecutionTimeMs).ToList(), 99) : 0,
            ErrorRate = requests.Any() ? (double)requests.Count(r => r.StatusCode >= 400) / requests.Count * 100 : 0,
            RequestsPerMinute = requests.Any() ? requests.Count / 60.0 : 0,
            SlowestEndpoints = requests
                .GroupBy(r => $"{r.Method} {r.Path}")
                .Select(g => new 
                {
                    Endpoint = g.Key,
                    AverageTime = g.Average(r => r.ExecutionTimeMs),
                    RequestCount = g.Count()
                })
                .OrderByDescending(e => e.AverageTime)
                .Take(10)
                .ToList(),
            StatusCodeDistribution = requests
                .GroupBy(r => r.StatusCode)
                .Select(g => new 
                {
                    StatusCode = g.Key,
                    Count = g.Count(),
                    Percentage = (double)g.Count() / requests.Count * 100
                })
                .OrderBy(s => s.StatusCode)
                .ToList()
        };

        return Ok(performanceMetrics);
    }