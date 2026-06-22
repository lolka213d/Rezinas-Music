using System.Windows;
using System.Windows.Input;

namespace Harmony.Views;

public partial class AlbumCard : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty PlayCommandProperty =
        DependencyProperty.Register(nameof(PlayCommand), typeof(ICommand), typeof(AlbumCard));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(AlbumCard));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(AlbumCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ArtistNameProperty =
        DependencyProperty.Register(nameof(ArtistName), typeof(string), typeof(AlbumCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ThumbnailUrlProperty =
        DependencyProperty.Register(nameof(ThumbnailUrl), typeof(string), typeof(AlbumCard));

    public AlbumCard() => InitializeComponent();

    public ICommand? PlayCommand
    {
        get => (ICommand?)GetValue(PlayCommandProperty);
        set => SetValue(PlayCommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string ArtistName
    {
        get => (string)GetValue(ArtistNameProperty);
        set => SetValue(ArtistNameProperty, value);
    }

    public string? ThumbnailUrl
    {
        get => (string?)GetValue(ThumbnailUrlProperty);
        set => SetValue(ThumbnailUrlProperty, value);
    }
}
