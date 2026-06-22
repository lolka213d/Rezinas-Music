using System.Windows.Controls;
using Harmony.Helpers;
using Harmony.ViewModels;

namespace Harmony.Views;

/// <summary>Spotify-style full-panel synced lyrics. Scrolls to the active line.</summary>
public partial class LyricsOverlay : UserControl
{
    private readonly LyricsAutoScroller _lyricsScroller;
    private LyricsViewModel? _vm;

    public LyricsOverlay()
    {
        InitializeComponent();
        _lyricsScroller = new LyricsAutoScroller(LyricsList);
        DataContextChanged += (_, _) => BindViewModel(DataContext as LyricsViewModel);
    }

    private void BindViewModel(LyricsViewModel? vm)
    {
        if (_vm != null)
            _vm.ActiveLineChanged -= OnActiveLineChanged;

        _vm = vm;
        if (_vm != null)
            _vm.ActiveLineChanged += OnActiveLineChanged;
        else
            _lyricsScroller.Reset();
    }

    private void OnActiveLineChanged(object? sender, int index) =>
        _lyricsScroller.ScrollToLine(index);
}
