using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using FluentAssertions;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class DebugDashboardAccessTests
{
    [Fact]
    public void IsActive_Disabled_ReturnsFalse()
    {
        var config = new DebugConfiguration { IsEnabled = false };

        DebugDashboardAccess.IsActive(config, "Development").Should().BeFalse();
    }

    [Fact]
    public void IsActive_DefaultConfig_DevelopmentOnly()
    {
        var config = new DebugConfiguration { IsEnabled = true };

        DebugDashboardAccess.IsActive(config, "Development").Should().BeTrue();
        DebugDashboardAccess.IsActive(config, "Production").Should().BeFalse();
        DebugDashboardAccess.IsActive(config, "Staging").Should().BeFalse();
    }

    [Fact]
    public void IsActive_WildcardAllowedEnvironments_AllowsAny()
    {
        var config = new DebugConfiguration { IsEnabled = true, AllowedEnvironments = new() { "*" } };

        DebugDashboardAccess.IsActive(config, "Production").Should().BeTrue();
    }

    [Fact]
    public void IsActive_EnvironmentNameComparison_IsCaseInsensitive()
    {
        var config = new DebugConfiguration { IsEnabled = true, AllowedEnvironments = new() { "development" } };

        DebugDashboardAccess.IsActive(config, "Development").Should().BeTrue();
    }
}
