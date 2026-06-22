using System.Windows;
using System.Windows.Media.Animation;
using Harmony.Helpers;
using Harmony.ViewModels;

namespace Harmony.Views;

public partial class AlbumDetailView
{
    private readonly LyricsAutoScroller _lyricsScroller;
    private AlbumDetailViewModel? _vm;

    public AlbumDetailView()
    {
        InitializeComponent();
        _lyricsScroller = new LyricsAutoScroller(LyricsList);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.ActiveLyricLineChanged -= OnActiveLyricLineChanged;

        _vm = e.NewValue as AlbumDetailViewModel;
        if (_vm != null)
            _vm.ActiveLyricLineChanged += OnActiveLyricLineChanged;
    }

    private void OnActiveLyricLineChanged(object? sender, int index) =>
        _lyricsScroller.ScrollToLine(index);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var fade = (Storyboard)FindResource("FadeSlideInFast");
        fade.Begin(BackButton);
        fade.Begin(MainContent);
    }
}
