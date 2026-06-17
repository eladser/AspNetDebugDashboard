using AspNetVitals;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class VitalsTests
{
    [Fact]
    public async Task Collect_returns_sane_process_metrics_and_no_health_when_unregistered()
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns("Test");
        var collector = new VitalsCollector(env.Object, new VitalsOptions(), health: null);

        var snap = await collector.Collect(CancellationToken.None);

        snap.Environment.Should().Be("Test");
        snap.ProcessorCount.Should().BeGreaterThan(0);
        snap.ManagedMemoryBytes.Should().BeGreaterThan(0);
        snap.AssemblyCount.Should().BeGreaterThan(0);
        snap.Runtime.Should().NotBeNullOrEmpty();
        snap.Os.Should().NotBeNullOrEmpty();
        snap.OverallHealth.Should().BeNull();        // AddHealthChecks not called
        snap.HealthChecks.Should().BeEmpty();
        snap.CpuPercent.Should().BeInRange(0, 100);
    }
}
