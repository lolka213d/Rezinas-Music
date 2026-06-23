using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Harmony.Helpers;

/// <summary>Centers the active lyric line and smoothly auto-scrolls; pauses after manual scroll.</summary>
public sealed class LyricsAutoScroller
{
    private readonly ListBox _list;
    private int _lastScrolledIndex = -1;
    private DateTime _manualScrollUntilUtc = DateTime.MinValue;
    private DispatcherTimer? _scrollTimer;
    private double _scrollFrom;
    private double _scrollTo;
    private DateTime _scrollStartUtc;
    private static readonly TimeSpan ScrollDuration = TimeSpan.FromMilliseconds(480);

    public LyricsAutoScroller(ListBox list)
    {
        _list = list;
        list.PreviewMouseWheel += OnManualScroll;
        list.PreviewMouseLeftButtonDown += OnManualScroll;
        list.PreviewTouchDown += (_, _) => NotifyManualScroll();
    }

    public void ScrollToLine(int index)
    {
        if (index < 0 || DateTime.UtcNow < _manualScrollUntilUtc) return;
        if (index == _lastScrolledIndex) return;
        _lastScrolledIndex = index;

        _list.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (index >= _list.Items.Count) return;

                _list.UpdateLayout();
                var scroll = FindScrollViewer(_list);
                if (scroll == null)
                {
                    _list.ScrollIntoView(_list.Items[index]);
                    return;
                }

                var container = _list.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                if (container == null)
                {
                    _list.ScrollIntoView(_list.Items[index]);
                    _list.UpdateLayout();
                    container = _list.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                }

                if (container == null) return;

                var itemTop = container.TranslatePoint(new Point(0, 0), scroll).Y;
                var target = scroll.VerticalOffset + itemTop - (scroll.ViewportHeight * 0.42) + (container.ActualHeight * 0.5);
                AnimateScroll(scroll, Math.Max(0, target));
            }
            catch
            {
                // List may be rebuilding while lyrics reload.
            }
        }, DispatcherPriority.Background);
    }

    public void Reset()
    {
        _lastScrolledIndex = -1;
        _manualScrollUntilUtc = DateTime.MinValue;
        _scrollTimer?.Stop();
    }

    public void UpdatePadding()
    {
        var scroll = FindScrollViewer(_list);
        var height = scroll?.ViewportHeight ?? _list.ActualHeight;
        if (height <= 0) return;

        var pad = height * 0.38;
        _list.Padding = new Thickness(48, pad, 48, pad);
    }

    private void AnimateScroll(ScrollViewer scroll, double target)
    {
        _scrollTimer?.Stop();
        _scrollFrom = scroll.VerticalOffset;
        _scrollTo = target;

        if (Math.Abs(_scrollFrom - _scrollTo) < 2)
        {
            scroll.ScrollToVerticalOffset(_scrollTo);
            return;
        }

        _scrollStartUtc = DateTime.UtcNow;
        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _scrollTimer.Tick += (_, _) =>
        {
            var t = (DateTime.UtcNow - _scrollStartUtc).TotalMilliseconds / ScrollDuration.TotalMilliseconds;
            if (t >= 1)
            {
                _scrollTimer.Stop();
                scroll.ScrollToVerticalOffset(_scrollTo);
                return;
            }

            var eased = EaseInOutCubic(t);
            scroll.ScrollToVerticalOffset(_scrollFrom + (_scrollTo - _scrollFrom) * eased);
        };
        _scrollTimer.Start();
    }

    private static double EaseInOutCubic(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    private void OnManualScroll(object sender, InputEventArgs e)
    {
        if (e is MouseWheelEventArgs)
        {
            NotifyManualScroll();
            return;
        }

        if (e is MouseButtonEventArgs { ChangedButton: MouseButton.Left } && IsScrollbarSource(e.OriginalSource as DependencyObject))
            NotifyManualScroll();
    }

    private static bool IsScrollbarSource(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ScrollBar or ScrollViewer)
                return true;
            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv) return sv;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var found = FindScrollViewer(child);
            if (found != null) return found;
        }

        return null;
    }

    private void NotifyManualScroll()
    {
        _manualScrollUntilUtc = DateTime.UtcNow.AddSeconds(8);
        _lastScrolledIndex = -1;
    }
}
