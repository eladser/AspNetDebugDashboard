using AspNetDebugDashboard;
using Microsoft.AspNetCore.Mvc;

namespace SampleApp.Controllers;

// Calls a real external API so Outgoing HTTP capture has something to show in the demo.
[ApiController]
[Route("api/[controller]")]
public class ExternalApiController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ExternalApiController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("zen")]
    public async Task<ActionResult<string>> GetZen()
    {
        var client = _httpClientFactory.CreateClient();
        try
        {
            var zen = await client.GetStringAsync("https://api.github.com/zen");
            return Ok(zen);
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"External API call failed: {ex.Message}", "ExternalApiController");
            return StatusCode(502, "Upstream call failed");
        }
    }
}
