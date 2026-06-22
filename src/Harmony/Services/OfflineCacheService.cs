using System.IO;
using Harmony.Data;
using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>Caches resolved stream URLs on disk with a configurable size limit.</summary>
public sealed class OfflineCacheService
{
    private readonly ISettingsService _settings;
    private readonly IAppLog _log;

    public OfflineCacheService(ISettingsService settings, IAppLog log)
    {
        _settings = settings;
        _log = log;
    }

    public bool IsEnabled => _settings.Current.OfflineCacheLimitMb > 0;

    public string? TryGetCachedPath(MusicSource source, string sourceId)
    {
        if (!IsEnabled) return null;
        var path = CachePathFor(source, sourceId);
        return File.Exists(path) ? path : null;
    }

    public async Task<string?> StoreAsync(MusicSource source, string sourceId, Stream stream, CancellationToken ct = default)
    {
        if (!IsEnabled) return null;
        try
        {
            await EnforceLimitAsync();
            var path = CachePathFor(source, sourceId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var file = File.Create(path);
            await stream.CopyToAsync(file, ct);
            return path;
        }
        catch (Exception ex)
        {
            _log.Warning($"Offline cache store failed: {ex.Message}");
            return null;
        }
    }

    public async Task EnforceLimitAsync()
    {
        var limitBytes = _settings.Current.OfflineCacheLimitMb * 1024L * 1024L;
        if (limitBytes <= 0) return;

        var dir = OfflineFolder;
        if (!Directory.Exists(dir)) return;

        var files = new DirectoryInfo(dir)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .OrderBy(f => f.LastWriteTimeUtc)
            .ToList();

        long total = files.Sum(f => f.Length);
        foreach (var file in files)
        {
            if (total <= limitBytes) break;
            total -= file.Length;
            try { file.Delete(); } catch { /* ignore */ }
        }

        await Task.CompletedTask;
    }

    private static string OfflineFolder => Path.Combine(AppPaths.StreamCacheFolder, "offline");

    private static string CachePathFor(MusicSource source, string sourceId)
    {
        var safeId = string.Join("_", sourceId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(OfflineFolder, source.ToString(), safeId + ".cache");
    }
}
