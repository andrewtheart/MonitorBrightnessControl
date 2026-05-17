using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class CliHandlerTests
{
    [Fact]
    public void TryHandle_WithNoArguments_ReturnsFalseSoGuiCanLaunch()
    {
        Assert.False(CliHandler.TryHandle(Array.Empty<string>()));
    }

    [Fact]
    public void TryHandle_WithUnknownCommand_ReturnsFalseSoGuiCanLaunch()
    {
        Assert.False(CliHandler.TryHandle(new[] { "--not-a-command" }));
    }

    [Fact]
    public void TryHandle_WithHelpFlag_PrintsHelpAndIgnoresOtherCommands()
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            var handled = CliHandler.TryHandle(new[] { "--setlevel", "--monitors", "all", "--level", "50", "/?" });

            Assert.True(handled);
            var text = output.ToString();
            Assert.Contains("Monitor Brightness Control - Command Line Interface", text);
            Assert.Contains("all other flags and arguments are ignored", text);
            Assert.Contains("--setlevel command is ignored", text);
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    [Fact]
    public void TryHandle_OldSetCommandNoLongerRecognized()
    {
        Assert.False(CliHandler.TryHandle(new[] { "--set", "1", "75" }));
    }

    [Fact]
    public void TryHandle_OldShortSetAliasNoLongerRecognized()
    {
        Assert.False(CliHandler.TryHandle(new[] { "-s", "1", "75" }));
    }

    [Fact]
    public void TryHandle_SetlevelShortAliasIsRecognized()
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            // --sl without --monitors triggers the error path (no hardware calls)
            var handled = CliHandler.TryHandle(new[] { "--sl" });

            Assert.True(handled);
            Assert.Contains("--setlevel requires --monitors", output.ToString());
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    [Fact]
    public void TryHandle_SetlevelWithoutMonitorsFlagShowsError()
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            var handled = CliHandler.TryHandle(new[] { "--setlevel", "--level", "80" });

            Assert.True(handled);
            var text = output.ToString();
            Assert.Contains("--setlevel requires --monitors", text);
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    [Fact]
    public void TryHandle_SetlevelWithoutLevelFlagShowsError()
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            var handled = CliHandler.TryHandle(new[] { "--setlevel", "--monitors", "1" });

            Assert.True(handled);
            var text = output.ToString();
            Assert.Contains("--setlevel requires --level", text);
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    [Fact]
    public void TryHandle_SetlevelWithInvalidLevelShowsError()
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            var handled = CliHandler.TryHandle(new[] { "--setlevel", "--monitors", "1", "--level", "abc" });

            Assert.True(handled);
            Assert.Contains("--setlevel requires --level", output.ToString());
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    [Fact]
    public void TryHandle_SetlevelWithOutOfRangeLevelShowsError()
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            var handled = CliHandler.TryHandle(new[] { "--setlevel", "--monitors", "1", "--level", "150" });

            Assert.True(handled);
            Assert.Contains("--setlevel requires --level", output.ToString());
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    [Fact]
    public void TryHandle_HelpTextContainsNewSetlevelSyntax()
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            CliHandler.TryHandle(new[] { "--help" });

            var text = output.ToString();
            Assert.Contains("--setlevel --monitors <targets> --level <value>", text);
            Assert.Contains("--get [--monitor <n>]", text);
            Assert.DoesNotContain("--set, -s", text);
            Assert.DoesNotContain("--get, -g", text);
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    [Fact]
    public void TryHandle_SetlevelWithNonExistentMonitorShowsNoMatchError()
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            // Monitor 999 doesn't exist, so ResolveTargetMonitors returns empty
            var handled = CliHandler.TryHandle(new[] { "--setlevel", "--monitors", "999", "--level", "50" });

            Assert.True(handled);
            Assert.Contains("No matching monitors found", output.ToString());
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    [Fact]
    public void TryHandle_GetWithNonExistentMonitorShowsError()
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            var handled = CliHandler.TryHandle(new[] { "--get", "--monitor", "999" });

            Assert.True(handled);
            Assert.Contains("Monitor 999 not found", output.ToString());
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    [Fact]
    public void TryHandle_GetWithoutMonitorFlagListsAllMonitors()
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            var handled = CliHandler.TryHandle(new[] { "--get" });

            Assert.True(handled);
            // Should not error — either prints monitors or prints nothing for an empty list
            Assert.DoesNotContain("Error", output.ToString());
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    [Fact]
    public void TryHandle_GetShortAliasIsRecognized()
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            var handled = CliHandler.TryHandle(new[] { "-g" });

            Assert.True(handled);
            Assert.DoesNotContain("Error", output.ToString());
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    [Theory]
    [InlineData("--setlevel")]
    [InlineData("--sl")]
    [InlineData("--list")]
    [InlineData("-l")]
    [InlineData("--get")]
    [InlineData("-g")]
    [InlineData("--identify")]
    [InlineData("--id")]
    public void TryHandle_AllKnownCommandsReturnTrue(string command)
    {
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            Assert.True(CliHandler.TryHandle(new[] { command }));
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }
}
