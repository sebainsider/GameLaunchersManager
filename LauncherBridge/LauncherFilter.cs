namespace LauncherBridge;

public static class LauncherFilter
{
    private static readonly HashSet<string> KnownLauncherProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // Epic Games
        "EpicGamesLauncher",
        "EpicWebHelper",
        "EOSOverlayRenderer",
        "EOSOverlayRenderer-Win64-Shipping",
        "UnrealEngineLauncher",

        // EA / Origin
        "EADesktop",
        "EABackgroundService",
        "EAWebKit",
        "EALauncher",
        "Origin",
        "OriginClientService",
        "OriginWebHelperService",

        // Ubisoft Connect / Uplay
        "UbisoftConnect",
        "upc",
        "Uplay",
        "UplayWebCore",
        "UbisoftGameLauncher",

        // Battle.net
        "Battle.net",
        "Agent",
        "Battle.net.exe",

        // GOG Galaxy
        "GalaxyClient",
        "GalaxyClientService",
        "GalaxyCommunication",

        // Steam
        "steam",
        "steamservice",
        "steamwebhelper",

        // Riot Games
        "RiotClientServices",
        "RiotClientUx",

        // Windows Shell / System utilities
        "cmd",
        "powershell",
        "pwsh",
        "conhost",
        "wt",
        "explorer",
        "rundll32",
        "dllhost",
        "LauncherBridge"
    };

    public static bool IsLauncherOrSystemProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return true;

        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            processName = processName[..^4];
        }

        return KnownLauncherProcesses.Contains(processName);
    }
}
