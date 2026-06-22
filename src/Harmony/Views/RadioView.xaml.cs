using System.ComponentModel;
using System.Windows;
using Harmony.ViewModels;

namespace Harmony.Views;

public partial class RadioView
{
    private RadioViewModel? _vm;

    public RadioView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.Player.PropertyChanged -= OnPlayerPropertyChanged;

        _vm = e.NewValue as RadioViewModel;
        if (_vm != null)
            _vm.Player.PropertyChanged += OnPlayerPropertyChanged;
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlayerViewModel.CurrentTrack) || _vm?.Player.CurrentTrack is not { } current)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            var match = _vm.Tracks.FirstOrDefault(t => t.Matches(current));
            if (match != null)
                TrackList.ScrollIntoView(match);
        });
    }
}
