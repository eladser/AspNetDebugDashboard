using AspNetJobs;
using Microsoft.AspNetCore.Mvc;

namespace SampleApp.Controllers;

[ApiController]
[Route("api/jobs-demo")]
public class JobsDemoController(IJobQueue jobs) : ControllerBase
{
    // Enqueue a fresh batch with staggered delays so /_jobs shows live transitions.
    [HttpPost("run")]
    public IActionResult Run()
    {
        jobs.Enqueue("send-welcome-email", async ct => await Task.Delay(800, ct));
        jobs.Enqueue("rebuild-search-index", async ct => await Task.Delay(2200, ct));
        jobs.Enqueue("generate-invoice-pdf", async ct => await Task.Delay(1400, ct));
        jobs.Enqueue("sync-inventory", _ => throw new InvalidOperationException("upstream returned 503"));
        jobs.Enqueue("nightly-report", async ct => await Task.Delay(3500, ct));
        return Accepted();
    }
}
