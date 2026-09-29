using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class ServiceCollectionExtensionsTests
{
    private static DebugConfiguration Configure(Action<DebugConfiguration> configure)
    {
        var services = new ServiceCollection();
        services.Configure<HealthCheckServiceOptions>(o => o.Registrations.Clear());
        services.AddDebugDashboard(configure);
        return services.BuildServiceProvider().GetRequiredService<IOptions<DebugConfiguration>>().Value;
    }

    [Fact]
    public void AddDebugDashboard_EmitActivitiesFalse_IsCarriedOver()
    {
        var config = Configure(o => o.EmitActivities = false);

        config.EmitActivities.Should().BeFalse();
    }

    [Fact]
    public void AddDebugDashboard_EmitActivitiesDefaultsTrue()
    {
        var config = Configure(_ => { });

        config.EmitActivities.Should().BeTrue();
    }

    [Fact]
    public void AddDebugDashboard_AllowedEnvironments_IsCarriedOver()
    {
        var config = Configure(o => o.AllowedEnvironments = new() { "Staging" });

        config.AllowedEnvironments.Should().ContainSingle().Which.Should().Be("Staging");
    }

    [Fact]
    public void AddDebugDashboard_RedactedBodyFields_IsCarriedOver()
    {
        var config = Configure(o => o.RedactedBodyFields = new() { "ssn" });

        config.RedactedBodyFields.Should().ContainSingle().Which.Should().Be("ssn");
    }

    [Fact]
    public void AddDebugDashboard_AuthorizationFilter_IsCarriedOver()
    {
        Func<Microsoft.AspNetCore.Http.HttpContext, bool> filter = _ => false;
        var config = Configure(o => o.AuthorizationFilter = filter);

        config.AuthorizationFilter.Should().BeSameAs(filter);
    }
}
