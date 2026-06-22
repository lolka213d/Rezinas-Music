using System.IO;
using System.Text;
using Harmony.Data;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>
/// Appends timestamped lines to a daily log file under
/// %LOCALAPPDATA%\Harmony\logs\harmony-YYYYMMDD.log
/// </summary>
public sealed class FileAppLog : IAppLog
{
    private readonly object _lock = new();

    public FileAppLog()
    {
        LogsFolder = AppPaths.LogsFolder;
        LogFilePath = Path.Combine(LogsFolder, $"harmony-{DateTime.Now:yyyyMMdd}.log");
        Info("Harmony started.");
    }

    public string LogFilePath { get; }
    public string LogsFolder { get; }

    public void Info(string message) => Write("INFO", message, null);

    public void Warning(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(" [")
            .Append(level)
            .Append("] ")
            .Append(message);

        if (exception != null)
        {
            line.AppendLine()
                .Append(exception.GetType().Name)
                .Append(": ")
                .Append(exception.Message);

            if (exception.StackTrace != null)
            {
                line.AppendLine()
                    .Append(exception.StackTrace);
            }

            if (exception.InnerException != null)
            {
                line.AppendLine()
                    .Append("Inner: ")
                    .Append(exception.InnerException);
            }
        }

        lock (_lock)
        {
            try
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Never crash the app because logging failed.
            }
        }

        System.Diagnostics.Debug.WriteLine(line.ToString());
    }
}
