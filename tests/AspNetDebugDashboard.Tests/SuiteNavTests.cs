using System.Text.Json;
using AspNetDebugDashboard.Suite;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class SuiteNavTests
{
    [Fact]
    public void BuildJson_orders_by_order_dedupes_by_route_and_sets_current()
    {
        var services = new ServiceCollection();
        services.AddSuitePanel(new SuitePanel("Vitals", "/_vitals", "<svg/>", 40));
        services.AddSuitePanel(new SuitePanel("Dashboard", "/_debug", "<svg/>", 0));
        services.AddSuitePanel(new SuitePanel("Flags", "/_flags", "<svg/>", 20));
        services.AddSuitePanel(new SuitePanel("Flags dup", "/_flags", "<svg/>", 99)); // same route
        using var sp = services.BuildServiceProvider();

        var json = SuiteNav.BuildJson(sp, "/_flags");

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("current").GetString().Should().Be("/_flags");
        var panels = doc.RootElement.GetProperty("panels").EnumerateArray().ToList();
        panels.Should().HaveCount(3); // dup route collapsed
        panels.Select(p => p.GetProperty("route").GetString())
            .Should().ContainInOrder("/_debug", "/_flags", "/_vitals"); // ordered by Order
        panels[1].GetProperty("name").GetString().Should().Be("Flags"); // first registration wins
    }

    [Fact]
    public void BuildJson_with_no_panels_is_empty()
    {
        using var sp = new ServiceCollection().BuildServiceProvider();
        var json = SuiteNav.BuildJson(sp, "/_x");
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("panels").GetArrayLength().Should().Be(0);
    }
}
