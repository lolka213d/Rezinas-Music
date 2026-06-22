using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Harmony.Views;

public partial class FavoriteHeart : UserControl
{
    public static readonly DependencyProperty TrackProperty =
        DependencyProperty.Register(nameof(Track), typeof(Models.Track), typeof(FavoriteHeart));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(FavoriteHeart));

    public FavoriteHeart() => InitializeComponent();

    public Models.Track? Track
    {
        get => (Models.Track?)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}
