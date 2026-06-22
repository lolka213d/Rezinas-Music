using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Harmony.Helpers;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;

namespace Harmony.Services;

public sealed class UpdateCheckResult
{
    public required string Version { get; init; }
    public required string DownloadUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    public bool IsNewer { get; init; }
}

/// <summary>Checks GitHub Releases for a newer app version.</summary>
public sealed class UpdateCheckService
{
    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly IAppLog _log;
    private readonly ILocalizationService _loc;

    public UpdateCheckService(HttpClient http, ISettingsService settings, IAppLog log, ILocalizationService loc)
    {
        _http = http;
        _settings = settings;
        _log = log;
        _loc = loc;
    }

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "1.0.0";

    public async Task<UpdateCheckResult?> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(AppBranding.GitHubOwner) ||
            AppBranding.GitHubOwner.StartsWith("YOUR_", StringComparison.Ordinal))
            return null;

        try
        {
            var url = $"https://api.github.com/repos/{AppBranding.GitHubOwner}/{AppBranding.GitHubRepo}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("RezinasMusic", CurrentVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log.Info($"Update check: GitHub returned {(int)response.StatusCode}");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, JsonOpts, cancellationToken);
            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                return null;

            var latest = NormalizeVersion(release.TagName);
            var current = NormalizeVersion(CurrentVersion);
            if (!TryParseVersion(latest, out var latestV) || !TryParseVersion(current, out var currentV))
                return null;

            var download = release.HtmlUrl
                ?? $"https://github.com/{AppBranding.GitHubOwner}/{AppBranding.GitHubRepo}/releases/latest";

            return new UpdateCheckResult
            {
                Version = latest,
                DownloadUrl = download,
                ReleaseNotes = TrimNotes(release.Body),
                IsNewer = latestV > currentV
            };
        }
        catch (Exception ex)
        {
            _log.Info($"Update check failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CheckAndPromptAsync(bool force = false)
    {
        await _settings.LoadAsync();
        var s = _settings.Current;
        if (!force && !s.CheckForUpdates)
            return false;

        var result = await CheckAsync();
        if (result is not { IsNewer: true })
            return false;

        if (!force && string.Equals(s.SkippedUpdateVersion, result.Version, StringComparison.OrdinalIgnoreCase))
            return false;

        return await Application.Current.Dispatcher.InvokeAsync(() => ShowPrompt(result));
    }

    private bool ShowPrompt(UpdateCheckResult result)
    {
        var notes = string.IsNullOrWhiteSpace(result.ReleaseNotes)
            ? string.Empty
            : $"\n\n{result.ReleaseNotes}";

        var message = string.Format(_loc.T("update.availableBody"), result.Version, CurrentVersion) + notes;

        var answer = MessageBox.Show(
            message,
            _loc.T("update.availableTitle"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Information);

        if (answer == MessageBoxResult.Yes)
        {
            try
            {
                Process.Start(new ProcessStartInfo(result.DownloadUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _log.Error("Failed to open update URL", ex);
            }
            return true;
        }

        if (answer == MessageBoxResult.No)
        {
            var s = _settings.Current;
            s.SkippedUpdateVersion = result.Version;
            _ = _settings.SaveAsync(s);
        }

        return false;
    }

    private static string? TrimNotes(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        body = body.Trim();
        return body.Length > 600 ? body[..600] + "…" : body;
    }

    private static string NormalizeVersion(string tag) =>
        tag.Trim().TrimStart('v', 'V');

    private static bool TryParseVersion(string text, out Version version)
    {
        version = new Version(0, 0);
        var parts = text.Split('.', '-', '+')[0];
        if (Version.TryParse(parts, out var v))
        {
            version = v;
            return true;
        }

        var digits = new string(text.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        if (!Version.TryParse(digits, out var parsed))
            return false;

        version = parsed;
        return true;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }
    }
}
