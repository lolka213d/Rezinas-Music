namespace Harmony.Services.Interfaces;

/// <summary>Simple file logger for diagnostics (%LOCALAPPDATA%\Harmony\logs).</summary>
public interface IAppLog
{
    string LogFilePath { get; }
    string LogsFolder { get; }

    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}
