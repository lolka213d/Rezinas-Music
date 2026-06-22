using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Harmony.Helpers;

/// <summary>Forwards mouse wheel from non-scrollable lists to the parent page ScrollViewer.</summary>
public static class ScrollWheelBubble
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ScrollWheelBubble),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        if (e.NewValue is true)
            element.PreviewMouseWheel += OnPreviewMouseWheel;
        else
            element.PreviewMouseWheel -= OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            var inner = FindVisualChild<ScrollViewer>(listBox);
            if (inner != null && CanScrollVertical(inner, e.Delta))
                return;
        }

        var current = VisualTreeHelper.GetParent(sender as DependencyObject);
        while (current != null)
        {
            if (current is ScrollViewer sv && CanScrollVertical(sv, e.Delta))
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private static bool CanScrollVertical(ScrollViewer sv, int delta)
    {
        if (delta > 0)
            return sv.VerticalOffset > 0.5;
        return sv.VerticalOffset < sv.ScrollableHeight - 0.5;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var found = FindVisualChild<T>(child);
            if (found != null)
                return found;
        }

        return null;
    }
}
