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
            var handled = CliHandler.TryHandle(new[] { "--set", "all", "50", "/?" });

            Assert.True(handled);
            var text = output.ToString();
            Assert.Contains("Monitor Brightness Control - Command Line Interface", text);
            Assert.Contains("all other flags and arguments are ignored", text);
            Assert.Contains("--set command is ignored", text);
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }
}
