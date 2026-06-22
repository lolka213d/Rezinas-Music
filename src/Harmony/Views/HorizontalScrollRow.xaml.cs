using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Harmony.Views;

/// <summary>Horizontal carousel: wheel scrolls sideways; arrows sit outside content.</summary>
public partial class HorizontalScrollRow : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(HorizontalScrollRow),
            new PropertyMetadata(null, OnItemsChanged));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(HorizontalScrollRow));

    public HorizontalScrollRow()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateArrows();
        Scroller.ScrollChanged += (_, _) => UpdateArrows();
        SizeChanged += (_, _) => UpdateArrows();
    }

    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HorizontalScrollRow row)
            row.Dispatcher.BeginInvoke(row.UpdateArrows);
    }

    private void ScrollLeft(object sender, RoutedEventArgs e) =>
        Scroller.ScrollToHorizontalOffset(Math.Max(0, Scroller.HorizontalOffset - 420));

    private void ScrollRight(object sender, RoutedEventArgs e) =>
        Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset + 420);

    private void UpdateArrows()
    {
        var canScroll = Scroller.ExtentWidth > Scroller.ViewportWidth + 4;
        var max = Math.Max(0, Scroller.ExtentWidth - Scroller.ViewportWidth);
        LeftBtn.Visibility = canScroll && Scroller.HorizontalOffset > 4
            ? Visibility.Visible : Visibility.Collapsed;
        RightBtn.Visibility = canScroll && Scroller.HorizontalOffset < max - 4
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var max = Math.Max(0, Scroller.ExtentWidth - Scroller.ViewportWidth);
        if (max <= 1)
        {
            BubbleVertical(e);
            return;
        }

        var atStart = Scroller.HorizontalOffset <= 0;
        var atEnd = Scroller.HorizontalOffset >= max - 1;

        if (e.Delta > 0 && atStart)
        {
            BubbleVertical(e);
            return;
        }

        if (e.Delta < 0 && atEnd)
        {
            BubbleVertical(e);
            return;
        }

        Scroller.ScrollToHorizontalOffset(Math.Clamp(Scroller.HorizontalOffset - e.Delta, 0, max));
        e.Handled = true;
        UpdateArrows();
    }

    private void BubbleVertical(MouseWheelEventArgs e)
    {
        var current = VisualTreeHelper.GetParent(Scroller);
        while (current != null)
        {
            if (current is ScrollViewer sv && !ReferenceEquals(sv, Scroller))
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }
}
