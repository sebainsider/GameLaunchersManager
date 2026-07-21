using LauncherBridge;
using Xunit;

namespace LauncherBridge.Tests;

public class OptionsTests
{
    [Fact]
    public void Parse_WithNoArgs_ShowsHelp()
    {
        var (options, error) = Options.Parse(Array.Empty<string>());

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.ShowHelp);
    }

    [Fact]
    public void Parse_WithLaunchCommand_ReturnsOptions()
    {
        var args = new[] { "--launch", "com.epicgames.launcher://apps/Item?action=launch" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("com.epicgames.launcher://apps/Item?action=launch", options.LaunchCommand);
        Assert.Equal(60, options.TimeoutSeconds);
        Assert.Null(options.ProcessName);
        Assert.False(options.Verbose);
    }

    [Fact]
    public void Parse_WithShortLaunchArg_ReturnsOptions()
    {
        var args = new[] { "-l", "com.epicgames.launcher://apps/Item" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("com.epicgames.launcher://apps/Item", options.LaunchCommand);
    }

    [Fact]
    public void Parse_WithProcessName_StripsExeExtension()
    {
        var args = new[] { "--launch", "myuri://", "--process", "AlanWake2.exe" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("AlanWake2", options.ProcessName);
    }

    [Fact]
    public void Parse_WithCustomTimeoutAndVerbose_ParsesCorrectly()
    {
        var args = new[] { "--launch", "myuri://", "--timeout", "120", "--verbose" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(120, options.TimeoutSeconds);
        Assert.True(options.Verbose);
    }

    [Fact]
    public void Parse_InvalidTimeout_ReturnsError()
    {
        var args = new[] { "--launch", "myuri://", "--timeout", "invalid" };
        var (options, error) = Options.Parse(args);

        Assert.NotNull(error);
        Assert.Null(options);
        Assert.Contains("Invalid timeout value", error);
    }

    [Fact]
    public void Parse_MissingLaunchCommand_ReturnsError()
    {
        var args = new[] { "--process", "AlanWake2" };
        var (options, error) = Options.Parse(args);

        Assert.NotNull(error);
        Assert.Null(options);
        Assert.Contains("Missing required argument", error);
    }
}
