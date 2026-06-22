using System.Windows.Threading;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>Runs Spotify library sync on a timer when auto-sync is enabled.</summary>
public sealed class SpotifySyncScheduler : IDisposable
{
    private readonly SpotifyLibrarySyncService _sync;
    private readonly SpotifyAuthService _auth;
    private readonly ISettingsService _settings;
    private readonly IAppLog _log;
    private readonly DispatcherTimer _timer;
    private bool _isRunning;

    public SpotifySyncScheduler(
        SpotifyLibrarySyncService sync,
        SpotifyAuthService auth,
        ISettingsService settings,
        IAppLog log)
    {
        _sync = sync;
        _auth = auth;
        _settings = settings;
        _log = log;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        _timer.Tick += async (_, _) => await MaybeSyncAsync();
    }

    public void Start()
    {
        _timer.Start();
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(8));
            await MaybeSyncAsync();
        });
    }

    public async Task MaybeSyncAsync()
    {
        if (_isRunning || !_auth.IsConnected) return;

        var s = _settings.Current;
        if (!s.SpotifyAutoSyncEnabled) return;

        var intervalHours = Math.Max(1, s.SpotifyAutoSyncIntervalHours);
        if (s.SpotifyLastSyncUtc.HasValue
            && DateTime.UtcNow - s.SpotifyLastSyncUtc.Value < TimeSpan.FromHours(intervalHours))
            return;

        _isRunning = true;
        try
        {
            var result = await _sync.SyncAsync();
            if (result.Error != null)
                _log.Info($"Spotify auto-sync: {result.Error}");
        }
        catch (Exception ex)
        {
            _log.Warning($"Spotify auto-sync failed: {ex.Message}");
        }
        finally
        {
            _isRunning = false;
        }
    }

    public void Dispose() => _timer.Stop();
}
