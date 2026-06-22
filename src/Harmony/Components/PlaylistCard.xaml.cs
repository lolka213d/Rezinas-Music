using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Harmony.Components;

public partial class PlaylistCard : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PlaylistCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(PlaylistCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(PlaylistCard));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(PlaylistCard));

    public static readonly DependencyProperty Thumbnail0Property =
        DependencyProperty.Register(nameof(Thumbnail0), typeof(string), typeof(PlaylistCard), new PropertyMetadata(null, OnThumbnailsChanged));

    public static readonly DependencyProperty Thumbnail1Property =
        DependencyProperty.Register(nameof(Thumbnail1), typeof(string), typeof(PlaylistCard), new PropertyMetadata(null, OnThumbnailsChanged));

    public static readonly DependencyProperty Thumbnail2Property =
        DependencyProperty.Register(nameof(Thumbnail2), typeof(string), typeof(PlaylistCard), new PropertyMetadata(null, OnThumbnailsChanged));

    public static readonly DependencyProperty Thumbnail3Property =
        DependencyProperty.Register(nameof(Thumbnail3), typeof(string), typeof(PlaylistCard), new PropertyMetadata(null, OnThumbnailsChanged));

    public PlaylistCard() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public string? Thumbnail0
    {
        get => (string?)GetValue(Thumbnail0Property);
        set => SetValue(Thumbnail0Property, value);
    }

    public string? Thumbnail1
    {
        get => (string?)GetValue(Thumbnail1Property);
        set => SetValue(Thumbnail1Property, value);
    }

    public string? Thumbnail2
    {
        get => (string?)GetValue(Thumbnail2Property);
        set => SetValue(Thumbnail2Property, value);
    }

    public string? Thumbnail3
    {
        get => (string?)GetValue(Thumbnail3Property);
        set => SetValue(Thumbnail3Property, value);
    }

    public bool HasSingleCover => !string.IsNullOrWhiteSpace(Thumbnail0) && string.IsNullOrWhiteSpace(Thumbnail3);
    public bool HasMosaic => !string.IsNullOrWhiteSpace(Thumbnail3);
    public bool HasDefaultCover => string.IsNullOrWhiteSpace(Thumbnail0);

    public void SetThumbnails(IReadOnlyList<string?> thumbnails)
    {
        Thumbnail0 = thumbnails.Count > 0 ? thumbnails[0] : null;
        Thumbnail1 = thumbnails.Count > 1 ? thumbnails[1] : null;
        Thumbnail2 = thumbnails.Count > 2 ? thumbnails[2] : null;
        Thumbnail3 = thumbnails.Count > 3 ? thumbnails[3] : null;
    }

    private static void OnThumbnailsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PlaylistCard card)
            card.NotifyCoverChanged();
    }

    private void NotifyCoverChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSingleCover)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMosaic)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasDefaultCover)));
    }
}
