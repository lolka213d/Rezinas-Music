namespace Harmony.Services;

/// <summary>Coordinates lower GPU/CPU UI work while gaming or when the window is in the background.</summary>
public sealed class UiPerformanceService
{
    private bool _reduceGpuUsage;
    private bool _mainVisible = true;
    private bool _mainActive = true;
    private bool _minimized;
    private bool _lyricsOpen;
    private bool _miniPlayerVisible;

    public event EventHandler? Changed;

    public bool ReduceGpuUsage
    {
        get => _reduceGpuUsage;
        set
        {
            if (_reduceGpuUsage == value) return;
            _reduceGpuUsage = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool LyricsOpen
    {
        get => _lyricsOpen;
        set
        {
            if (_lyricsOpen == value) return;
            _lyricsOpen = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetMainWindowState(bool isVisible, bool isActive, bool isMinimized)
    {
        if (_mainVisible == isVisible && _mainActive == isActive && _minimized == isMinimized)
            return;

        _mainVisible = isVisible;
        _mainActive = isActive;
        _minimized = isMinimized;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetMiniPlayerVisible(bool visible)
    {
        if (_miniPlayerVisible == visible) return;
        _miniPlayerVisible = visible;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool ShouldHideAmbient => ReduceGpuUsage;

    public bool ShouldUseLiteChrome => ReduceGpuUsage;

    /// <summary>Stop position ticks entirely (audio keeps playing).</summary>
    public bool ShouldPausePositionUi =>
        !_mainVisible && !_miniPlayerVisible;

    public int PositionUpdateIntervalMs
    {
        get
        {
            if (ShouldPausePositionUi) return 0;
            if (LyricsOpen) return 250;
            if (ReduceGpuUsage || _minimized || !_mainActive) return 1000;
            return 250;
        }
    }
}
