using LauncherBridge;
using Xunit;

namespace LauncherBridge.Tests;

public class ProcessSnapshotTests
{
    [Fact]
    public void GetNewProcesses_DetectsNewlyAddedPids()
    {
        var initialProcs = new List<ProcessInfo>
        {
            new(100, "system"),
            new(101, "explorer"),
            new(102, "EpicGamesLauncher")
        };

        var initialSnapshot = new ProcessSnapshot(initialProcs);

        var updatedProcs = new List<ProcessInfo>
        {
            new(100, "system"),
            new(101, "explorer"),
            new(102, "EpicGamesLauncher"),
            new(200, "EpicWebHelper"),
            new(300, "AlanWake2")
        };

        var updatedSnapshot = new ProcessSnapshot(updatedProcs);

        var newProcs = initialSnapshot.GetNewProcesses(updatedSnapshot);

        Assert.Equal(2, newProcs.Count);
        Assert.Contains(newProcs, p => p.Id == 200 && p.ProcessName == "EpicWebHelper");
        Assert.Contains(newProcs, p => p.Id == 300 && p.ProcessName == "AlanWake2");
    }

    [Fact]
    public void GetNewProcesses_ReturnsEmpty_WhenNoNewProcessesExist()
    {
        var procs = new List<ProcessInfo>
        {
            new(100, "system"),
            new(101, "explorer")
        };

        var snapshot1 = new ProcessSnapshot(procs);
        var snapshot2 = new ProcessSnapshot(procs);

        var newProcs = snapshot1.GetNewProcesses(snapshot2);

        Assert.Empty(newProcs);
    }
}
