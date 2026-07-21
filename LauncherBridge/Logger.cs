namespace LauncherBridge;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public class Logger
{
    private readonly bool _verbose;

    public Logger(bool verbose)
    {
        _verbose = verbose;
    }

    public void LogDebug(string message)
    {
        if (_verbose)
        {
            Log(LogLevel.Debug, message);
        }
    }

    public void LogInfo(string message)
    {
        Log(LogLevel.Info, message);
    }

    public void LogWarning(string message)
    {
        Log(LogLevel.Warning, message);
    }

    public void LogError(string message)
    {
        Log(LogLevel.Error, message);
    }

    private void Log(LogLevel level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelString = level switch
        {
            LogLevel.Debug => "[DEBUG]",
            LogLevel.Info => "[INFO ]",
            LogLevel.Warning => "[WARN ]",
            LogLevel.Error => "[ERROR]",
            _ => "[LOG  ]"
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = level switch
        {
            LogLevel.Debug => ConsoleColor.DarkGray,
            LogLevel.Info => ConsoleColor.Cyan,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            _ => originalColor
        };

        Console.WriteLine($"{timestamp} {levelString} {message}");
        Console.ForegroundColor = originalColor;
    }
}
