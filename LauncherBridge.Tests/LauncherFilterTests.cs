using LauncherBridge;
using Xunit;

namespace LauncherBridge.Tests;

public class LauncherFilterTests
{
    [Theory]
    [InlineData("EpicGamesLauncher")]
    [InlineData("EpicGamesLauncher.exe")]
    [InlineData("EpicWebHelper")]
    [InlineData("EpicOnlineServicesHost")]
    [InlineData("EpicOnlineServicesHost.exe")]
    [InlineData("EOSOverlayRenderer-Win64-Shipping")]
    [InlineData("EOSOverlayRenderer-Win32-Shipping.exe")]
    [InlineData("EOSSDK-Win64-Shipping")]
    [InlineData("CrashReportClient")]
    [InlineData("CrashReportClient-Win64-Shipping.exe")]
    [InlineData("EasyAntiCheat_EOS")]
    [InlineData("EasyAntiCheat_x64.exe")]
    [InlineData("BEService")]
    [InlineData("EADesktop")]
    [InlineData("EADesktop.exe")]
    [InlineData("Origin")]
    [InlineData("UbisoftConnect")]
    [InlineData("upc.exe")]
    [InlineData("Battle.net")]
    [InlineData("GalaxyClient")]
    [InlineData("steam")]
    [InlineData("cmd.exe")]
    [InlineData("powershell")]
    [InlineData("LauncherBridge")]
    public void IsLauncherOrSystemProcess_ReturnsTrue_ForKnownLaunchers(string processName)
    {
        Assert.True(LauncherFilter.IsLauncherOrSystemProcess(processName));
    }

    [Theory]
    [InlineData("AlanWake2")]
    [InlineData("AlanWake2.exe")]
    [InlineData("Cyberpunk2077")]
    [InlineData("GTA5")]
    [InlineData("EASportsFC24")]
    [InlineData("DiabloIV.exe")]
    [InlineData("FortniteClient-Win64-Shipping")]
    public void IsLauncherOrSystemProcess_ReturnsFalse_ForGameProcesses(string processName)
    {
        Assert.False(LauncherFilter.IsLauncherOrSystemProcess(processName));
    }
}

