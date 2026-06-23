using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Harmony.Helpers;
using Harmony.ViewModels;

namespace Harmony.Views;

/// <summary>Spotify-style full-panel synced lyrics with ambient cover palette.</summary>
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
        {
            _vm.ActiveLineChanged -= OnActiveLineChanged;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = vm;
        if (_vm != null)
        {
            _vm.ActiveLineChanged += OnActiveLineChanged;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
        else
        {
            _lyricsScroller.Reset();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LyricsViewModel.BackgroundColor)
            or nameof(LyricsViewModel.AccentLeftColor)
            or nameof(LyricsViewModel.AccentRightColor))
        {
            PulseAmbientOrbs();
        }
    }

    private void OnActiveLineChanged(object? sender, int index) =>
        _lyricsScroller.ScrollToLine(index);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _lyricsScroller.UpdatePadding();
        StartAmbientAnimation();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        _lyricsScroller.UpdatePadding();

    private void StartAmbientAnimation()
    {
        AnimateOrb(LeftOrbShift, -18, 18, 6.2);
        AnimateOrb(RightOrbShift, 16, -16, 7.4);
        AnimateOrbScale(LeftOrbScale, 0.94, 1.06, 5.6);
        AnimateOrbScale(RightOrbScale, 1.05, 0.92, 6.8);
    }

    private void PulseAmbientOrbs()
    {
        var pulse = new DoubleAnimation(0.72, 1.0, TimeSpan.FromMilliseconds(900))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        LeftOrb.BeginAnimation(OpacityProperty, pulse);
        RightOrb.BeginAnimation(OpacityProperty, pulse.Clone());
    }

    private static void AnimateOrb(TranslateTransform target, double from, double to, double seconds)
    {
        var anim = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true
        };
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds))));
        target.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private static void AnimateOrbScale(ScaleTransform target, double from, double to, double seconds)
    {
        var anim = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true
        };
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds))));
        target.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        target.BeginAnimation(ScaleTransform.ScaleYProperty, anim.Clone());
    }
}
