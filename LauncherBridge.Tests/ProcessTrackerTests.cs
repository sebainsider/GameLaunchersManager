using LauncherBridge;
using Xunit;

namespace LauncherBridge.Tests;

public class MockProcessProvider : IProcessProvider
{
    public List<ProcessSnapshot> SnapshotsToReturn { get; set; } = new();
    public Dictionary<string, int> InstanceCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool LaunchResult { get; set; } = true;
    public string? LastLaunchedCommand { get; set; private get; }
    public bool CloseLauncherCalled { get; set; }
    public string? CloseLauncherCommandPassed { get; set; }

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

    public void CloseLauncherProcesses(string launchCommand)
    {
        CloseLauncherCalled = true;
        CloseLauncherCommandPassed = launchCommand;
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

    [Fact]
    public async Task RunAsync_WithCloseLauncher_CallsCloseLauncherProcesses()
    {
        var provider = new MockProcessProvider();
        provider.InstanceCounts["AlanWake2"] = 1;

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            ProcessName = "AlanWake2",
            CloseLauncher = true,
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(600);
        provider.InstanceCounts["AlanWake2"] = 0;

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
        Assert.True(provider.CloseLauncherCalled);
        Assert.Equal("com.epicgames.launcher://apps/Item?action=launch", provider.CloseLauncherCommandPassed);
    }

    [Fact]
    public async Task RunAsync_TracksMultiStageGameBootstrapper_WithoutEarlyExit()
    {
        var provider = new MockProcessProvider();

        var initialProcs = new List<ProcessInfo>
        {
            new(10, "system")
        };

        // Step 1: Launcher opens bootstrapper (PID 100)
        var step1Procs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(100, "AlanWake2Launcher")
        };

        // Step 2: Bootstrapper spawns real game (PID 101)
        var step2Procs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(100, "AlanWake2Launcher"),
            new(101, "AlanWake2-Win64-Shipping")
        };

        // Step 3: Bootstrapper (PID 100) exits, but real game (PID 101) remains
        var step3Procs = new List<ProcessInfo>
        {
            new(10, "system"),
            new(20, "EpicGamesLauncher"),
            new(101, "AlanWake2-Win64-Shipping")
        };

        provider.SnapshotsToReturn = new List<ProcessSnapshot>
        {
            new(initialProcs),
            new(step1Procs),
            new(step2Procs),
            new(step3Procs)
        };

        var tracker = new ProcessTracker(provider, _logger);
        var options = new Options
        {
            LaunchCommand = "com.epicgames.launcher://apps/Item?action=launch",
            TimeoutSeconds = 5
        };

        var trackerTask = tracker.RunAsync(options);
        await Task.Delay(1200);

        // After 1.2s, step 3 is reached. Real game (PID 101) is still alive.
        // Signal real game exit by setting empty snapshot
        provider.SnapshotsToReturn.Add(new ProcessSnapshot(new[] { new ProcessInfo(10, "system"), new ProcessInfo(20, "EpicGamesLauncher") }));

        int exitCode = await trackerTask;

        Assert.Equal(0, exitCode);
    }
}



