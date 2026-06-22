using System.IO;

namespace Harmony.Config;

/// <summary>One-time language choice written by the Windows installer.</summary>
public static class InstallLanguage
{
    private static readonly HashSet<string> Supported =
    [
        "en", "ru", "uk", "es", "de", "fr", "it", "pt", "pl", "ja"
    ];

    public static string ReadOrDefault()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "install.lang");
            if (!File.Exists(path))
                return "en";

            var code = File.ReadAllText(path).Trim().ToLowerInvariant();
            try { File.Delete(path); } catch { /* ignore */ }

            return Supported.Contains(code) ? code : "en";
        }
        catch
        {
            return "en";
        }
    }
}
