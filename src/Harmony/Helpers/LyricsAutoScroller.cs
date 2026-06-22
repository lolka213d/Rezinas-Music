using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Harmony.Helpers;

/// <summary>Scrolls a lyrics ListBox to the active line; pauses after manual scroll.</summary>
public sealed class LyricsAutoScroller
{
    private readonly ListBox _list;
    private int _lastScrolledIndex = -1;
    private DateTime _manualScrollUntilUtc = DateTime.MinValue;

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
                _list.ScrollIntoView(_list.Items[index]);
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

    private void NotifyManualScroll() =>
        _manualScrollUntilUtc = DateTime.UtcNow.AddSeconds(8);
}
