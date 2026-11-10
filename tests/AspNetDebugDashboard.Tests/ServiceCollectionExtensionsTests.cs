using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Reflection;
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

    // Guards against the EmitActivities-goes-missing-in-2.3.0 bug happening again: every
    // public settable property on DebugConfiguration has to make it through the manual
    // options.X = config.X copy in AddDebugDashboard, or this fails.
    [Fact]
    public void AddDebugDashboard_CopiesEveryPublicProperty()
    {
        var props = typeof(DebugConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToList();

        var source = new DebugConfiguration();
        foreach (var prop in props)
        {
            prop.SetValue(source, MutatedValue(prop));
        }

        var services = new ServiceCollection();
        services.Configure<HealthCheckServiceOptions>(o => o.Registrations.Clear());
        services.AddDebugDashboard(o =>
        {
            foreach (var prop in props)
            {
                prop.SetValue(o, prop.GetValue(source));
            }
        });

        var result = services.BuildServiceProvider().GetRequiredService<IOptions<DebugConfiguration>>().Value;

        foreach (var prop in props)
        {
            var expected = prop.GetValue(source);
            var actual = prop.GetValue(result);

            if (expected is Delegate)
            {
                actual.Should().BeSameAs(expected, $"{prop.Name} should be carried over by AddDebugDashboard");
            }
            else
            {
                actual.Should().BeEquivalentTo(expected, $"{prop.Name} should be carried over by AddDebugDashboard");
            }
        }
    }

    private static object? MutatedValue(PropertyInfo prop)
    {
        var current = prop.GetValue(new DebugConfiguration());

        if (prop.PropertyType == typeof(bool)) return !(bool)current!;
        if (prop.PropertyType == typeof(int)) return (int)current! + 1;
        if (prop.PropertyType == typeof(long)) return (long)current! + 1;
        if (prop.PropertyType == typeof(string)) return "mutated-" + prop.Name;
        if (prop.PropertyType == typeof(TimeSpan)) return TimeSpan.FromMinutes(42);
        if (prop.PropertyType == typeof(TimeSpan?)) return TimeSpan.FromMinutes(43);
        if (prop.PropertyType == typeof(List<string>)) return new List<string> { "mutated-" + prop.Name };
        if (prop.PropertyType == typeof(Func<HttpContext, bool>)) return (Func<HttpContext, bool>)(_ => true);

        throw new NotSupportedException($"add a mutator for {prop.PropertyType} ({prop.Name}) in this test");
    }
}
