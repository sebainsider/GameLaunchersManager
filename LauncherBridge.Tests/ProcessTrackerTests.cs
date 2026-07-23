using LauncherBridge;
using Xunit;

namespace LauncherBridge.Tests;

public class MockProcessProvider : IProcessProvider
{
    public List<ProcessSnapshot> SnapshotsToReturn { get; set; } = new();
    public Dictionary<string, int> InstanceCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool LaunchResult { get; set; } = true;
    public string? LastLaunchedCommand { get; set; private get; }

    private int _snapshotIndex = 0;

    public ProcessSnapshot CaptureSnapshot()
    {
        if (SnapshotsToReturn.Count == 0)
        {
            return new ProcessSnapshot(Array.Empty<ProcessInfo>());
        }

        var snapshot = SnapshotsToReturn[Math.Min(_snapshotIndex, SnapshotsToReturn.Count - 1)];
        _snapshotIndex++;
        return snapshot;
    }

    public bool Launch(string commandOrUri)
    {
        LastLaunchedCommand = commandOrUri;
        return LaunchResult;
    }

    public int GetRunningInstanceCount(string processName)
    {
        if (InstanceCounts.TryGetValue(processName, out var count))
        {
            return count;
        }
        return 0;
    }
}

public class ProcessTrackerTests
{
    private readonly Logger _logger = new Logger(verbose: false);

    [Fact]
    public async Task RunAsync_AutoDetectsNewGameProcess_AndMonitorsUntilExit()
    {
        var provider = new MockProcessProvider();

        // Initial snapshot before launch
        var initialProcs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher")
        };

        // Snapshot after launch: Epic helper and new game process start
        var afterLaunchProcs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(21, "EpicWebHelper"),
            new(100, "AlanWake2")
        };

        provider.SnapshotsToReturn = new List<ProcessSnapshot>
        {
            new(initialProcs),
            new(afterLaunchProcs)
        };

        // Initially AlanWake2 is running (1 instance), then exits (0 instances)
        provider.InstanceCounts["AlanWake2"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            TimeoutSeconds = 5
        };

        // Simulate process exiting after 600ms
        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(600);
        provider.InstanceCounts["AlanWake2"] = 0;

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_TimesOut_WhenNoGameProcessStarts()
    {
        var provider = new MockProcessProvider();

        var initialProcs = new List<ProcessInfo>
        {
            new(10, "system")
        };

        provider.SnapshotsToReturn = new List<ProcessSnapshot>
        {
            new(initialProcs)
        };

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            TimeoutSeconds = 1
        };

        int exitCode = await tracker.RunAsync(options);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_ExplicitProcessMode_WaitsForProcessAndSucceeds()
    {
        var provider = new MockProcessProvider();
        provider.InstanceCounts["Cyberpunk2077"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "steam://run/1091500",
            ProcessName = "Cyberpunk2077",
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(600);
        provider.InstanceCounts["Cyberpunk2077"] = 0;

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_IgnoresEpicServicesAndDetectsGame()
    {
        var provider = new MockProcessProvider();

        var initialProcs = new List<ProcessInfo>
        {
            new(10, "system")
        };

        // Epic Launcher and EOS services start alongside game
        var afterLaunchProcs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(21, "EpicWebHelper"),
            new(22, "EpicOnlineServicesHost"),
            new(23, "EOSOverlayRenderer-Win64-Shipping"),
            new(24, "CrashReportClient-Win64-Shipping"),
            new(25, "EasyAntiCheat_EOS"),
            new(100, "AlanWake2")
        };

        provider.SnapshotsToReturn = new List<ProcessSnapshot>
        {
            new(initialProcs),
            new(afterLaunchProcs)
        };

        provider.InstanceCounts["AlanWake2"] = 1;
        provider.InstanceCounts["EpicOnlineServicesHost"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(600);
        // Game exits, but EpicOnlineServicesHost is still running!
        provider.InstanceCounts["AlanWake2"] = 0;

        int exitCode = await trackerTask;

        // LauncherBridge must succeed when AlanWake2 exits, even if EpicOnlineServicesHost is still running
        Assert.Equal(0, exitCode);
    }
}

