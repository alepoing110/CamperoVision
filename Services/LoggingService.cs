using System.IO;
using Serilog;

namespace CamperoDesktop.Services;

public static class LoggingService
{
    private static string LogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "campero-.log");

    public static void Configure()
    {
        string logDir = Path.GetDirectoryName(LogPath)!;
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: LogPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 30)
            .WriteTo.Debug()
            .CreateLogger();
    }

    public static Serilog.ILogger Instance => Log.Logger;
}
