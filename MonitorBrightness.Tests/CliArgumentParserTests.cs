using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class CliArgumentParserTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("/help")]
    [InlineData("-h")]
    [InlineData("-?")]
    [InlineData("/?")]
    [InlineData("--HELP")]
    public void IsHelpFlag_RecognizesSupportedHelpFlags(string flag)
    {
        Assert.True(CliArgumentParser.IsHelpFlag(flag));
    }

    [Theory]
    [InlineData("--list")]
    [InlineData("help")]
    [InlineData("/h")]
    [InlineData("")]
    public void IsHelpFlag_RejectsNonHelpFlags(string flag)
    {
        Assert.False(CliArgumentParser.IsHelpFlag(flag));
    }

    [Fact]
    public void ResolveTargetMonitors_AllTargetsOnlyBrightnessCapableMonitors()
    {
        var monitors = CreateMonitors();

        var targets = CliArgumentParser.ResolveTargetMonitors(monitors, new[] { "all" });

        Assert.Equal(new[] { 1, 3, 4 }, ToDisplayIndexes(targets));
    }

    [Fact]
    public void ResolveTargetMonitors_ParsesCommaSeparatedTargets()
    {
        var monitors = CreateMonitors();

        var targets = CliArgumentParser.ResolveTargetMonitors(monitors, new[] { "1,3" });

        Assert.Equal(new[] { 1, 3 }, ToDisplayIndexes(targets));
    }

    [Fact]
    public void ResolveTargetMonitors_ParsesSpaceSeparatedTargets()
    {
        var monitors = CreateMonitors();

        var targets = CliArgumentParser.ResolveTargetMonitors(monitors, new[] { "1", "3", "4" });

        Assert.Equal(new[] { 1, 3, 4 }, ToDisplayIndexes(targets));
    }

    [Theory]
    [InlineData("1-3")]
    [InlineData("3-1")]
    public void ResolveTargetMonitors_ParsesForwardAndReverseRanges(string range)
    {
        var monitors = CreateMonitors();

        var targets = CliArgumentParser.ResolveTargetMonitors(monitors, new[] { range });

        Assert.Equal(new[] { 1, 2, 3 }, ToDisplayIndexes(targets));
    }

    [Fact]
    public void ResolveTargetMonitors_DeduplicatesAndSortsTargets()
    {
        var monitors = CreateMonitors();

        var targets = CliArgumentParser.ResolveTargetMonitors(monitors, new[] { "4", "1,4", "3-4" });

        Assert.Equal(new[] { 1, 3, 4 }, ToDisplayIndexes(targets));
    }

    [Fact]
    public void ResolveTargetMonitors_ExplicitUnsupportedMonitorCanBeSelectedForSkipMessage()
    {
        var monitors = CreateMonitors();

        var targets = CliArgumentParser.ResolveTargetMonitors(monitors, new[] { "2" });

        Assert.Equal(new[] { 2 }, ToDisplayIndexes(targets));
    }

    [Fact]
    public void ResolveTargetMonitors_IgnoresInvalidAndOutOfRangeTargets()
    {
        var monitors = CreateMonitors();

        var targets = CliArgumentParser.ResolveTargetMonitors(monitors, new[] { "abc", "99", "1-x", "-4" });

        Assert.Empty(targets);
    }

    [Fact]
    public void ResolveTargetMonitors_LargeRangeIsConstrainedToKnownMonitors()
    {
        var monitors = CreateMonitors();

        var targets = CliArgumentParser.ResolveTargetMonitors(monitors, new[] { "1-2147483647" });

        Assert.Equal(new[] { 1, 2, 3, 4 }, ToDisplayIndexes(targets));
    }

    // ── ExtractValues ──

    [Fact]
    public void ExtractValues_CollectsValuesUntilNextFlag()
    {
        var args = new[] { "--setlevel", "--monitors", "1", "3", "4", "--level", "80" };

        var values = CliArgumentParser.ExtractValues(args, "--monitors");

        Assert.Equal(new[] { "1", "3", "4" }, values);
    }

    [Fact]
    public void ExtractValues_CollectsValuesUntilEndOfArgs()
    {
        var args = new[] { "--setlevel", "--monitors", "all" };

        var values = CliArgumentParser.ExtractValues(args, "--monitors");

        Assert.Equal(new[] { "all" }, values);
    }

    [Fact]
    public void ExtractValues_ReturnsEmptyWhenFlagAbsent()
    {
        var args = new[] { "--setlevel", "--level", "80" };

        var values = CliArgumentParser.ExtractValues(args, "--monitors");

        Assert.Empty(values);
    }

    [Fact]
    public void ExtractValues_ReturnsEmptyWhenFlagHasNoValues()
    {
        var args = new[] { "--setlevel", "--monitors", "--level", "80" };

        var values = CliArgumentParser.ExtractValues(args, "--monitors");

        Assert.Empty(values);
    }

    [Fact]
    public void ExtractValues_IsCaseInsensitive()
    {
        var args = new[] { "--setlevel", "--MONITORS", "1", "2", "--level", "50" };

        var values = CliArgumentParser.ExtractValues(args, "--monitors");

        Assert.Equal(new[] { "1", "2" }, values);
    }

    [Fact]
    public void ExtractValues_EmptyArgsReturnsEmpty()
    {
        var values = CliArgumentParser.ExtractValues(Array.Empty<string>(), "--monitors");

        Assert.Empty(values);
    }

    // ── ExtractSingleValue ──

    [Fact]
    public void ExtractSingleValue_ReturnsSingleValue()
    {
        var args = new[] { "--setlevel", "--monitors", "1", "--level", "75" };

        var result = CliArgumentParser.ExtractSingleValue(args, "--level");

        Assert.Equal("75", result);
    }

    [Fact]
    public void ExtractSingleValue_ReturnsNullWhenFlagAbsent()
    {
        var args = new[] { "--get" };

        var result = CliArgumentParser.ExtractSingleValue(args, "--monitor");

        Assert.Null(result);
    }

    [Fact]
    public void ExtractSingleValue_ReturnsNullWhenFlagIsLastArg()
    {
        var args = new[] { "--get", "--monitor" };

        var result = CliArgumentParser.ExtractSingleValue(args, "--monitor");

        Assert.Null(result);
    }

    [Fact]
    public void ExtractSingleValue_ReturnsNullWhenNextArgIsFlag()
    {
        var args = new[] { "--setlevel", "--level", "--monitors", "1" };

        var result = CliArgumentParser.ExtractSingleValue(args, "--level");

        Assert.Null(result);
    }

    [Fact]
    public void ExtractSingleValue_IsCaseInsensitive()
    {
        var args = new[] { "--get", "--MONITOR", "2" };

        var result = CliArgumentParser.ExtractSingleValue(args, "--monitor");

        Assert.Equal("2", result);
    }

    [Fact]
    public void ExtractSingleValue_EmptyArgsReturnsNull()
    {
        var result = CliArgumentParser.ExtractSingleValue(Array.Empty<string>(), "--monitor");

        Assert.Null(result);
    }

    private static List<MonitorDevice> CreateMonitors()
    {
        return new List<MonitorDevice>
        {
            new() { Index = 0, SupportsBrightness = true },
            new() { Index = 1, SupportsBrightness = false },
            new() { Index = 2, SupportsBrightness = true },
            new() { Index = 3, SupportsBrightness = true }
        };
    }

    private static int[] ToDisplayIndexes(IEnumerable<MonitorDevice> monitors)
    {
        return monitors.Select(monitor => monitor.Index + 1).ToArray();
    }
}
