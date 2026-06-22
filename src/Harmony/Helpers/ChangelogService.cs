using System.IO;
using System.Reflection;
using System.Windows;
using Harmony.Services;
using Harmony.Services.Interfaces;

namespace Harmony.Helpers;
/// <summary>Shows release notes after an app update.</summary>
public static class ChangelogService
{
    public static void ShowIfUpdated(ISettingsService settings, Window owner)
    {
        var current = UpdateCheckService.CurrentVersion;
        var lastSeen = settings.Current.LastSeenAppVersion;
        if (string.Equals(lastSeen, current, StringComparison.OrdinalIgnoreCase)) return;

        var notes = LoadChangelogExcerpt();
        if (string.IsNullOrWhiteSpace(notes)) notes = $"Version {current}";

        MessageBox.Show(
            owner,
            notes,
            $"What's new — {current}",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        settings.Current.LastSeenAppVersion = current;
        _ = settings.SaveAsync(settings.Current);
    }

    private static string? LoadChangelogExcerpt()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("CHANGELOG.md", StringComparison.OrdinalIgnoreCase));
            if (name == null)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
                if (File.Exists(path))
                    return Trim(File.ReadAllText(path));
                return null;
            }

            using var stream = asm.GetManifestResourceStream(name);
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            return Trim(reader.ReadToEnd());
        }
        catch
        {
            return null;
        }
    }

    private static string Trim(string text)
    {
        text = text.Trim();
        return text.Length > 1200 ? text[..1200] + "…" : text;
    }
}
