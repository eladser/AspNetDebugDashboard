using AspNetFlags;
using FluentAssertions;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class FlagsTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"flags-test-{Guid.NewGuid():N}.db");

    private FeatureFlags New(bool autoDiscover = true) =>
        new(new FlagsOptions { DatabasePath = _db, AutoDiscover = autoDiscover });

    [Fact]
    public void IsEnabled_auto_discovers_unknown_flag_as_off()
    {
        using var flags = New();
        flags.IsEnabled("new-checkout").Should().BeFalse();
        flags.All().Should().ContainSingle(f => f.Name == "new-checkout" && !f.Enabled);
    }

    [Fact]
    public void Set_then_IsEnabled_round_trips_and_persists()
    {
        using (var flags = New())
        {
            flags.Set("dark-mode", true);
            flags.IsEnabled("dark-mode").Should().BeTrue();
        }
        // reopen the same file: state survived
        using var reopened = New();
        reopened.IsEnabled("dark-mode").Should().BeTrue();
    }

    [Fact]
    public void Remove_deletes_the_flag()
    {
        using var flags = New();
        flags.Set("temp", true);
        flags.Remove("temp").Should().BeTrue();
        flags.All().Should().NotContain(f => f.Name == "temp");
        flags.Remove("temp").Should().BeFalse(); // already gone
    }

    [Fact]
    public void AutoDiscover_off_does_not_create_unknown_flags()
    {
        using var flags = New(autoDiscover: false);
        flags.IsEnabled("ghost").Should().BeFalse();
        flags.All().Should().BeEmpty();
    }

    public void Dispose()
    {
        if (File.Exists(_db)) File.Delete(_db);
    }
}
