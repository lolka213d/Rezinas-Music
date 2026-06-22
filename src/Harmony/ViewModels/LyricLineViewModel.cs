using CommunityToolkit.Mvvm.ComponentModel;

namespace Harmony.ViewModels;

/// <summary>One lyric line for UI binding with active/past visual states.</summary>
public partial class LyricLineViewModel : ObservableObject
{
    public LyricLineViewModel(string text, double startSeconds)
    {
        Text = text;
        StartSeconds = startSeconds;
    }

    public string Text { get; }
    public double StartSeconds { get; }

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isPast;
}
