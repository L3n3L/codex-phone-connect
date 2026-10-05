using System.IO;
using System.Text;

namespace CodexBridge;

public static class BridgeLog
{
    private const int RetentionDays = 14;
    private static readonly object Sync = new();
    private static readonly string LogDirectory = ResolveLogDirectory();
    private static bool _initialized;

    public static string DirectoryPath => LogDirectory;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(LogDirectory);
            DeleteExpiredLogs();
            _initialized = true;
            Info("App", $"日志系统已启动，目录：{LogDirectory}");
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Codex Bridge log initialization failed: {exception}");
        }
    }

    public static void Debug(string category, string message)
    {
        Write("DEBUG", category, message);
    }

    public static void Info(string category, string message)
    {
        Write("INFO", category, message);
    }

    public static void Warning(string category, string message)
    {
        Write("WARN", category, message);
    }

    public static void Error(string category, string message, Exception? exception = null)
    {
        var detail = exception is null ? message : $"{message} | {exception}";
        Write("ERROR", category, detail);
    }

    private static void Write(string level, string category, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] [{category}] {message}{Environment.NewLine}";

        try
        {
            Directory.CreateDirectory(LogDirectory);
            lock (Sync)
            {
                var path = Path.Combine(LogDirectory, $"bridge-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(path, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Codex Bridge log write failed: {exception}");
        }

        System.Diagnostics.Debug.Write(line);
    }

    private static void DeleteExpiredLogs()
    {
        var cutoff = DateTime.Now.AddDays(-RetentionDays);
        foreach (var file in Directory.EnumerateFiles(LogDirectory, "bridge-*.log"))
        {
            if (File.GetLastWriteTime(file) < cutoff)
            {
                File.Delete(file);
            }
        }
    }

    private static string ResolveLogDirectory()
    {
        var executableDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var candidate = executableDirectory;

        for (var level = 0; level < 4 && candidate is not null; level++)
        {
            if (File.Exists(Path.Combine(candidate.FullName, "CodexBridge.Desktop.csproj")))
            {
                return Path.Combine(candidate.FullName, "logs");
            }

            candidate = candidate.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "logs");
    }
}
