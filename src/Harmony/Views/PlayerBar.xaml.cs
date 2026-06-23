using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Harmony.ViewModels;

namespace Harmony.Views;

/// <summary>Bottom player bar — seek/volume drag wiring.</summary>
public partial class PlayerBar : UserControl
{
    private bool _progressDragging;

    public PlayerBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ProgressSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnProgressDragStarted), true);
        ProgressSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnProgressDragCompleted), true);
        ProgressSlider.PreviewMouseLeftButtonUp += OnProgressMouseUp;
    }

    private PlayerViewModel? Vm => DataContext as PlayerViewModel;

    private void OnProgressDragStarted(object sender, DragStartedEventArgs e)
    {
        _progressDragging = true;
        Vm?.BeginSeek();
    }

    private void OnProgressDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _progressDragging = false;
        Vm?.EndSeek(ProgressSlider.Value);
    }

    private void OnProgressMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_progressDragging && Vm?.HasTrack == true)
            Vm.EndSeek(ProgressSlider.Value);
    }

    public void ApplyLiteChrome(bool lite)
    {
        ChromeBorder.Style = (Style)FindResource(lite ? "GlassPlayerBarLite" : "GlassPlayerBar");
        PlayButton.Style = (Style)FindResource(lite ? "RzPlayerPlayButtonLite" : "RzPlayerPlayButton");
    }
}
