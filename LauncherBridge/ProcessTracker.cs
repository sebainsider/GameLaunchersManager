using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LauncherBridge;

public interface IProcessProvider
{
    ProcessSnapshot CaptureSnapshot();
    bool Launch(string commandOrUri);
    int GetRunningInstanceCount(string processName);
    void CloseLauncherProcesses(string launchCommand);
}

public class DefaultProcessProvider : IProcessProvider
{
    private readonly Logger _logger;

    public DefaultProcessProvider(Logger logger)
    {
        _logger = logger;
    }

    public ProcessSnapshot CaptureSnapshot()
    {
        return ProcessSnapshot.Capture();
    }

    public bool Launch(string commandOrUri)
    {
        try
        {
            string cleanCommand = commandOrUri.Trim('"', '\'');
            _logger.LogInfo($"Launching: '{cleanCommand}'");

            var startInfo = new ProcessStartInfo
            {
                FileName = cleanCommand,
                UseShellExecute = true
            };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = $"\"{cleanCommand}\"",
                        UseShellExecute = false
                    };
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = $"\"{cleanCommand}\"",
                        UseShellExecute = false
                    };
                }
            }

            using var proc = Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to launch command/URI '{commandOrUri}': {ex.Message}");
            return false;
        }
    }


    public int GetRunningInstanceCount(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            int count = processes.Length;

            foreach (var p in processes)
            {
                p.Dispose();
            }

            return count;
        }
        catch
        {
            return 0;
        }
    }

    public void CloseLauncherProcesses(string launchCommand)
    {
        var targetProcesses = GetLauncherProcessNames(launchCommand);

        foreach (var procName in targetProcesses)
        {
            try
            {
                var processes = Process.GetProcessesByName(procName);
                foreach (var p in processes)
                {
                    try
                    {
                        _logger.LogInfo($"Terminating launcher process '{p.ProcessName}' (PID: {p.Id})...");
                        p.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"Could not terminate process '{procName}' (PID: {p.Id}): {ex.Message}");
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch
            {
                // Ignore process enum errors
            }
        }
    }

    public static string[] GetLauncherProcessNames(string launchCommand)
    {
        if (string.IsNullOrWhiteSpace(launchCommand))
            return Array.Empty<string>();

        if (launchCommand.Contains("epic", StringComparison.OrdinalIgnoreCase) ||
            launchCommand.Contains("epicgames", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                "EpicGamesLauncher",
                "EpicWebHelper",
                "EpicOnlineServicesHost",
                "EpicOnlineServices",
                "EpicOnlineServicesUser",
                "EpicInstaller",
                "EOSOverlayRenderer",
                "EOSOverlayRenderer-Win64-Shipping",
                "EOSOverlayRenderer-Win32-Shipping",
                "EOSSDK-Win64-Shipping",
                "EOSSDK-Win32-Shipping",
                "CrashReportClient",
                "CrashReportClient-Win64-Shipping",
                "CrashReportClient-Win32-Shipping",
                "EasyAntiCheat_EOS"
            };
        }
        if (launchCommand.Contains("origin", StringComparison.OrdinalIgnoreCase) ||
            launchCommand.Contains("ea", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "EADesktop", "EABackgroundService", "EAWebKit", "EALauncher", "EACrashReporter", "EACoreServer", "Origin", "OriginClientService", "OriginWebHelperService" };
        }
        if (launchCommand.Contains("uplay", StringComparison.OrdinalIgnoreCase) ||
            launchCommand.Contains("ubisoft", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "UbisoftConnect", "upc", "Uplay", "UplayWebCore", "UbisoftGameLauncher", "UbisoftGameLauncher64", "UplayService" };
        }
        if (launchCommand.Contains("battlenet", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "Battle.net", "Agent" };
        }
        if (launchCommand.Contains("gog", StringComparison.OrdinalIgnoreCase) ||
            launchCommand.Contains("galaxy", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "GalaxyClient", "GalaxyClientService", "GalaxyCommunication", "GalaxyOverlay" };
        }

        return new[] { "EpicGamesLauncher", "EpicWebHelper", "EpicOnlineServicesHost", "EpicOnlineServices", "EADesktop", "UbisoftConnect", "GalaxyClient", "Battle.net" };
    }
}

public class ProcessTracker
{
    private readonly IProcessProvider _provider;
    private readonly Logger _logger;

    public ProcessTracker(IProcessProvider provider, Logger logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task<int> RunAsync(Options options, CancellationToken cancellationToken = default)
    {
        // 1. Take initial process snapshot
        _logger.LogDebug("Capturing initial process snapshot...");
        var initialSnapshot = _provider.CaptureSnapshot();
        _logger.LogDebug($"Snapshot recorded with {initialSnapshot.Processes.Count} running processes.");

        // 2. Launch target command/URI
        if (!_provider.Launch(options.LaunchCommand))
        {
            return 1;
        }

        // 3. Track game session lifetime
        _logger.LogInfo($"Monitoring game session (timeout waiting for start: {options.TimeoutSeconds}s)...");
        bool gameStarted = await TrackGameSessionAsync(initialSnapshot, options, cancellationToken);

        if (!gameStarted)
        {
            _logger.LogError($"Target process failed to start within {options.TimeoutSeconds} seconds.");
            return 1;
        }

        // 4. Close launcher if --close-launcher flag is enabled OR if launcher was newly started by LauncherBridge
        bool wasLauncherRunningInitially = CheckIfLauncherWasRunningInitially(initialSnapshot, options.LaunchCommand);
        if (options.CloseLauncher || !wasLauncherRunningInitially)
        {
            _logger.LogInfo("Closing third-party launcher processes (Epic Games Launcher, Epic Online Services, etc.)...");
            _provider.CloseLauncherProcesses(options.LaunchCommand);
        }

        _logger.LogInfo("Game session ended. LauncherBridge exiting with code 0.");
        return 0;
    }

    public async Task<bool> TrackGameSessionAsync(ProcessSnapshot initialSnapshot, Options options, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var timeoutSpan = TimeSpan.FromSeconds(options.TimeoutSeconds);

        var trackedGamePids = new HashSet<int>();
        string? primaryProcessName = options.ProcessName;

        while (!cancellationToken.IsCancellationRequested)
        {
            var currentSnapshot = _provider.CaptureSnapshot();
            var newProcesses = initialSnapshot.GetNewProcesses(currentSnapshot);

            var gameCandidates = newProcesses
                .Where(p => !LauncherFilter.IsLauncherOrSystemProcess(p.ProcessName))
                .ToList();

            if (!string.IsNullOrEmpty(options.ProcessName))
            {
                gameCandidates = gameCandidates
                    .Where(p => p.ProcessName.Equals(options.ProcessName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (var p in gameCandidates)
            {
                if (trackedGamePids.Add(p.Id))
                {
                    _logger.LogInfo($"Detected active game process: '{p.ProcessName}' (PID: {p.Id})");
                    primaryProcessName ??= p.ProcessName;
                }
            }

            // Waiting for initial game process to start
            if (trackedGamePids.Count == 0)
            {
                if (DateTime.UtcNow - startTime > timeoutSpan)
                {
                    return false;
                }

                _logger.LogDebug("Waiting for game process to start...");
                await Task.Delay(500, cancellationToken);
                continue;
            }

            // Count how many tracked game processes are currently active
            int activeCount = 0;
            foreach (var pid in trackedGamePids)
            {
                if (currentSnapshot.Processes.ContainsKey(pid))
                {
                    activeCount++;
                }
            }

            if (!string.IsNullOrEmpty(primaryProcessName))
            {
                int nameCount = _provider.GetRunningInstanceCount(primaryProcessName);
                if (nameCount > activeCount)
                {
                    activeCount = nameCount;
                }
            }

            if (activeCount == 0)
            {
                // Brief pause and re-check to ensure no sub-process transitions
                await Task.Delay(1500, cancellationToken);
                var recheckSnapshot = _provider.CaptureSnapshot();
                var recheckNew = initialSnapshot.GetNewProcesses(recheckSnapshot);
                var recheckGame = recheckNew
                    .Where(p => !LauncherFilter.IsLauncherOrSystemProcess(p.ProcessName))
                    .ToList();

                if (!string.IsNullOrEmpty(options.ProcessName))
                {
                    recheckGame = recheckGame
                        .Where(p => p.ProcessName.Equals(options.ProcessName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                int recheckActiveCount = 0;
                foreach (var p in recheckGame)
                {
                    if (trackedGamePids.Contains(p.Id) || (primaryProcessName != null && p.ProcessName.Equals(primaryProcessName, StringComparison.OrdinalIgnoreCase)))
                    {
                        recheckActiveCount++;
                    }
                }

                if (recheckActiveCount == 0)
                {
                    _logger.LogInfo("All game processes have exited.");
                    return true;
                }
            }

            _logger.LogDebug($"Monitoring game session: {activeCount} active process(es)");
            await Task.Delay(1000, cancellationToken);
        }

        return trackedGamePids.Count > 0;
    }

    private static bool CheckIfLauncherWasRunningInitially(ProcessSnapshot initialSnapshot, string launchCommand)
    {
        var targetProcesses = DefaultProcessProvider.GetLauncherProcessNames(launchCommand);
        foreach (var procName in targetProcesses)
        {
            if (initialSnapshot.Processes.Values.Any(name => name.Equals(procName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }
}


