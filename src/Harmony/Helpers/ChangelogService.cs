using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;
using Harmony.Views;

namespace Harmony.Helpers;

/// <summary>Shows release notes after an app update.</summary>
public static class ChangelogService
{
    public static void ShowIfUpdated(ISettingsService settings, Window owner, ILocalizationService loc)
    {
        var current = UpdateCheckService.CurrentVersion;
        var lastSeen = settings.Current.LastSeenAppVersion;
        if (string.Equals(lastSeen, current, StringComparison.OrdinalIgnoreCase)) return;

        var notes = LoadVersionNotes(current);
        if (string.IsNullOrWhiteSpace(notes))
            notes = string.Format(loc.T("whatsNew.defaultBody"), current);

        AppDialog.ShowInfo(
            owner,
            loc.T("whatsNew.title"),
            notes,
            loc.T("whatsNew.ok"),
            $"v{current}");

        settings.Current.LastSeenAppVersion = current;
        _ = settings.SaveAsync(settings.Current);
    }

    private static string? LoadVersionNotes(string version)
    {
        var full = LoadFullChangelog();
        if (string.IsNullOrWhiteSpace(full)) return null;

        var normalized = version.Trim().TrimStart('v', 'V');
        var pattern = $@"^##\s+{Regex.Escape(normalized)}\s*$";
        var match = Regex.Match(full, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!match.Success)
            return TrimExcerpt(full);

        var start = match.Index + match.Length;
        var next = Regex.Match(full[(start + 1)..], @"^##\s+\d", RegexOptions.Multiline);
        var section = next.Success
            ? full[start..(start + 1 + next.Index)].Trim()
            : full[start..].Trim();

        return string.IsNullOrWhiteSpace(section) ? null : FormatSection(section);
    }

    private static string? LoadFullChangelog()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("CHANGELOG.md", StringComparison.OrdinalIgnoreCase));
            if (name != null)
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
            }

            var path = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatSection(string section)
    {
        var lines = section.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.StartsWith("- ", StringComparison.Ordinal) ? "  •  " + l[2..] : l);
        return TrimExcerpt(string.Join(Environment.NewLine + Environment.NewLine, lines));
    }

    private static string TrimExcerpt(string text)
    {
        text = text.Trim();
        return text.Length > 1400 ? text[..1400] + "…" : text;
    }
}
