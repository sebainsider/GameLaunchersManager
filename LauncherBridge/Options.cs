namespace LauncherBridge;

public class Options
{
    public string LaunchCommand { get; set; } = string.Empty;
    public string? ProcessName { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
    public bool Verbose { get; set; }
    public bool ShowHelp { get; set; }

    public static (Options? options, string? errorMessage) Parse(string[] args)
    {
        var options = new Options();

        if (args.Length == 0)
        {
            options.ShowHelp = true;
            return (options, null);
        }

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
            {
                options.ShowHelp = true;
                return (options, null);
            }

            if (arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-v", StringComparison.OrdinalIgnoreCase))
            {
                options.Verbose = true;
                continue;
            }

            if (TryParseOption(arg, "--launch", "-l", args, ref i, out var launchValue))
            {
                options.LaunchCommand = launchValue;
                continue;
            }

            if (TryParseOption(arg, "--process", "-p", args, ref i, out var processValue))
            {
                // Remove .exe suffix if user passed it
                if (processValue.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    processValue = processValue[..^4];
                }
                options.ProcessName = processValue;
                continue;
            }

            if (TryParseOption(arg, "--timeout", "-t", args, ref i, out var timeoutValue))
            {
                if (int.TryParse(timeoutValue, out var timeout) && timeout > 0)
                {
                    options.TimeoutSeconds = timeout;
                }
                else
                {
                    return (null, $"Invalid timeout value: '{timeoutValue}'. Must be a positive integer.");
                }
                continue;
            }

            // If an unrecognized positional argument is passed and LaunchCommand is empty, treat it as launch command if not starting with -
            if (string.IsNullOrEmpty(options.LaunchCommand) && !arg.StartsWith('-'))
            {
                options.LaunchCommand = arg;
                continue;
            }

            return (null, $"Unrecognized argument: '{arg}'");
        }

        if (!options.ShowHelp && string.IsNullOrWhiteSpace(options.LaunchCommand))
        {
            return (null, "Missing required argument: --launch <command or URI>");
        }

        return (options, null);
    }

    private static bool TryParseOption(string arg, string longName, string shortName, string[] args, ref int index, out string value)
    {
        value = string.Empty;

        if (arg.Equals(longName, StringComparison.OrdinalIgnoreCase) ||
            arg.Equals(shortName, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 < args.Length && !args[index + 1].StartsWith('-'))
            {
                index++;
                value = args[index];
                return true;
            }
            return false;
        }

        if (arg.StartsWith(longName + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = arg[(longName.Length + 1)..];
            return true;
        }

        if (arg.StartsWith(shortName + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = arg[(shortName.Length + 1)..];
            return true;
        }

        return false;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("LauncherBridge - Steam Third-Party Launcher Monitor (.NET 9)");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  LauncherBridge --launch <command or URI> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --launch, -l <command/URI>   (Required) Command line or URI to launch the game.");
        Console.WriteLine("  --process, -p <name>         (Optional) Specific process name to monitor (without .exe).");
        Console.WriteLine("                               If omitted, automatically detects new processes started after launch.");
        Console.WriteLine("  --timeout, -t <seconds>      (Optional) Maximum time to wait for the target process to start.");
        Console.WriteLine("                               Default: 60 seconds.");
        Console.WriteLine("  --verbose, -v                (Optional) Enable verbose debug logging.");
        Console.WriteLine("  --help, -h                   Display this help message.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  LauncherBridge --launch \"com.epicgames.launcher://apps/Item?action=launch\"");
        Console.WriteLine("  LauncherBridge --launch \"steam://run/123456\" --process \"MyGame\"");
        Console.WriteLine("  LauncherBridge --launch \"C:\\Games\\Launcher.exe\" --timeout 90");
    }
}
