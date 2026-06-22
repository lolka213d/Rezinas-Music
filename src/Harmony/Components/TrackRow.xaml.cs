using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Harmony.Models;

namespace Harmony.Components;

public partial class TrackRow : UserControl
{
    public static readonly DependencyProperty TrackProperty =
        DependencyProperty.Register(nameof(Track), typeof(Track), typeof(TrackRow));

    public static readonly DependencyProperty CurrentTrackProperty =
        DependencyProperty.Register(nameof(CurrentTrack), typeof(Track), typeof(TrackRow));

    public static readonly DependencyProperty PlayCommandProperty =
        DependencyProperty.Register(nameof(PlayCommand), typeof(ICommand), typeof(TrackRow));

    public static readonly DependencyProperty LikeCommandProperty =
        DependencyProperty.Register(nameof(LikeCommand), typeof(ICommand), typeof(TrackRow));

    public static readonly DependencyProperty PlaylistCommandProperty =
        DependencyProperty.Register(nameof(PlaylistCommand), typeof(ICommand), typeof(TrackRow));

    public static readonly DependencyProperty RemoveCommandProperty =
        DependencyProperty.Register(nameof(RemoveCommand), typeof(ICommand), typeof(TrackRow));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(TrackRow));

    public static readonly DependencyProperty ShowIndexProperty =
        DependencyProperty.Register(nameof(ShowIndex), typeof(bool), typeof(TrackRow), new PropertyMetadata(false));

    public static readonly DependencyProperty ShowPlayButtonProperty =
        DependencyProperty.Register(nameof(ShowPlayButton), typeof(bool), typeof(TrackRow), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowAlbumProperty =
        DependencyProperty.Register(nameof(ShowAlbum), typeof(bool), typeof(TrackRow), new PropertyMetadata(false));

    public static readonly DependencyProperty ShowDurationProperty =
        DependencyProperty.Register(nameof(ShowDuration), typeof(bool), typeof(TrackRow), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowMetaProperty =
        DependencyProperty.Register(nameof(ShowMeta), typeof(bool), typeof(TrackRow), new PropertyMetadata(false));

    public static readonly DependencyProperty MetaTextProperty =
        DependencyProperty.Register(nameof(MetaText), typeof(string), typeof(TrackRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IndexProperty =
        DependencyProperty.Register(nameof(Index), typeof(string), typeof(TrackRow), new PropertyMetadata(string.Empty));

    public TrackRow() => InitializeComponent();

    public Track? Track
    {
        get => (Track?)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public Track? CurrentTrack
    {
        get => (Track?)GetValue(CurrentTrackProperty);
        set => SetValue(CurrentTrackProperty, value);
    }

    public ICommand? PlayCommand
    {
        get => (ICommand?)GetValue(PlayCommandProperty);
        set => SetValue(PlayCommandProperty, value);
    }

    public ICommand? LikeCommand
    {
        get => (ICommand?)GetValue(LikeCommandProperty);
        set => SetValue(LikeCommandProperty, value);
    }

    public ICommand? PlaylistCommand
    {
        get => (ICommand?)GetValue(PlaylistCommandProperty);
        set => SetValue(PlaylistCommandProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty) ?? Track;
        set => SetValue(CommandParameterProperty, value);
    }

    public bool ShowIndex
    {
        get => (bool)GetValue(ShowIndexProperty);
        set => SetValue(ShowIndexProperty, value);
    }

    public bool ShowPlayButton
    {
        get => (bool)GetValue(ShowPlayButtonProperty);
        set => SetValue(ShowPlayButtonProperty, value);
    }

    public bool ShowAlbum
    {
        get => (bool)GetValue(ShowAlbumProperty);
        set => SetValue(ShowAlbumProperty, value);
    }

    public bool ShowDuration
    {
        get => (bool)GetValue(ShowDurationProperty);
        set => SetValue(ShowDurationProperty, value);
    }

    public bool ShowMeta
    {
        get => (bool)GetValue(ShowMetaProperty);
        set => SetValue(ShowMetaProperty, value);
    }

    public string MetaText
    {
        get => (string)GetValue(MetaTextProperty);
        set => SetValue(MetaTextProperty, value);
    }

    public string Index
    {
        get => (string)GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }
}
