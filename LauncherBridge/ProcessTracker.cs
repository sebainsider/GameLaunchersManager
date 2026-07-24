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

        // 3. Detect or wait for process start
        string? targetProcessName = options.ProcessName;

        if (string.IsNullOrEmpty(targetProcessName))
        {
            _logger.LogInfo($"Auto-detecting target process (timeout: {options.TimeoutSeconds}s)...");
            targetProcessName = await AutoDetectProcessAsync(initialSnapshot, options.TimeoutSeconds, cancellationToken);
        }
        else
        {
            _logger.LogInfo($"Waiting for process '{targetProcessName}' to start (timeout: {options.TimeoutSeconds}s)...");
            bool started = await WaitForExplicitProcessAsync(targetProcessName, options.TimeoutSeconds, cancellationToken);
            if (!started)
            {
                targetProcessName = null;
            }
        }

        if (string.IsNullOrEmpty(targetProcessName))
        {
            _logger.LogError($"Target process failed to start within {options.TimeoutSeconds} seconds.");
            return 1;
        }

        // 4. Track target process until all instances exit
        _logger.LogInfo($"Tracking target process: '{targetProcessName}'");
        await MonitorProcessUntilExitAsync(targetProcessName, cancellationToken);

        // 5. Close launcher if --close-launcher flag is enabled OR if launcher was newly started by LauncherBridge
        bool wasLauncherRunningInitially = CheckIfLauncherWasRunningInitially(initialSnapshot, options.LaunchCommand);
        if (options.CloseLauncher || !wasLauncherRunningInitially)
        {
            _logger.LogInfo("Closing third-party launcher processes (Epic Games Launcher, Epic Online Services, etc.)...");
            _provider.CloseLauncherProcesses(options.LaunchCommand);
        }

        _logger.LogInfo($"All instances of '{targetProcessName}' have exited. LauncherBridge exiting with code 0.");
        return 0;
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

    public async Task<string?> AutoDetectProcessAsync(ProcessSnapshot initialSnapshot, int timeoutSeconds, CancellationToken cancellationToken)
    {

        var startTime = DateTime.UtcNow;
        var timeoutSpan = TimeSpan.FromSeconds(timeoutSeconds);

        while (DateTime.UtcNow - startTime < timeoutSpan)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var currentSnapshot = _provider.CaptureSnapshot();
            var newProcesses = initialSnapshot.GetNewProcesses(currentSnapshot);

            var gameCandidates = newProcesses
                .Where(p => !LauncherFilter.IsLauncherOrSystemProcess(p.ProcessName))
                .ToList();

            if (gameCandidates.Count > 0)
            {
                var detected = gameCandidates.First();
                _logger.LogInfo($"Detected new game process: '{detected.ProcessName}' (PID: {detected.Id})");
                if (gameCandidates.Count > 1)
                {
                    _logger.LogDebug($"Other new processes detected: {string.Join(", ", gameCandidates.Skip(1).Select(p => p.ProcessName))}");
                }
                return detected.ProcessName;
            }

            _logger.LogDebug("No game process detected yet, waiting 500ms...");
            await Task.Delay(500, cancellationToken);
        }

        return null;
    }

    public async Task<bool> WaitForExplicitProcessAsync(string processName, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var timeoutSpan = TimeSpan.FromSeconds(timeoutSeconds);

        while (DateTime.UtcNow - startTime < timeoutSpan)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            int count = _provider.GetRunningInstanceCount(processName);
            if (count > 0)
            {
                _logger.LogInfo($"Process '{processName}' detected ({count} running instance(s)).");
                return true;
            }

            await Task.Delay(500, cancellationToken);
        }

        return false;
    }

    public async Task MonitorProcessUntilExitAsync(string processName, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            int count = _provider.GetRunningInstanceCount(processName);
            if (count == 0)
            {
                // Double check after brief delay to avoid transient exit blips
                await Task.Delay(500, cancellationToken);
                count = _provider.GetRunningInstanceCount(processName);
                if (count == 0)
                {
                    break;
                }
            }

            _logger.LogDebug($"Monitoring '{processName}': {count} active instance(s).");
            await Task.Delay(1000, cancellationToken);
        }
    }
}

