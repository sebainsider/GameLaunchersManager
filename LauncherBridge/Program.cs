namespace LauncherBridge;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var (options, errorMessage) = Options.Parse(args);

        if (!string.IsNullOrEmpty(errorMessage))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {errorMessage}");
            Console.ResetColor();
            Console.WriteLine();
            Options.PrintHelp();
            return 1;
        }

        if (options == null || options.ShowHelp)
        {
            Options.PrintHelp();
            return 0;
        }

        var logger = new Logger(options.Verbose);
        logger.LogInfo("LauncherBridge starting...");
        logger.LogDebug($"Launch Command: {options.LaunchCommand}");
        if (!string.IsNullOrEmpty(options.ProcessName))
        {
            logger.LogDebug($"Explicit Process Name: {options.ProcessName}");
        }
        logger.LogDebug($"Timeout: {options.TimeoutSeconds}s");

        var provider = new DefaultProcessProvider(logger);
        var tracker = new ProcessTracker(provider, logger);

        try
        {
            return await tracker.RunAsync(options);
        }
        catch (Exception ex)
        {
            logger.LogError($"Unhandled exception: {ex.Message}");
            if (options.Verbose)
            {
                logger.LogError(ex.ToString());
            }
            return 1;
        }
    }
}
