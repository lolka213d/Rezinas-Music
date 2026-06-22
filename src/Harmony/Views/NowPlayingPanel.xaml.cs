using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Harmony.Models;
using Harmony.ViewModels;

namespace Harmony.Views;

public partial class NowPlayingPanel : UserControl
{
    private Point _dragStart;
    private int _dragFrom = -1;

    public NowPlayingPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (FindName("QueueList") is ListBox list)
        {
            list.PreviewMouseLeftButtonDown += OnQueueMouseDown;
            list.PreviewMouseMove += OnQueueMouseMove;
            list.Drop += OnQueueDrop;
            list.AllowDrop = true;
        }
        else
        {
            // Find ListBox in visual tree for queue tab
            var listBox = FindVisualChild<ListBox>(this);
            if (listBox == null) return;
            listBox.Name = "QueueList";
            listBox.PreviewMouseLeftButtonDown += OnQueueMouseDown;
            listBox.PreviewMouseMove += OnQueueMouseMove;
            listBox.Drop += OnQueueDrop;
            listBox.AllowDrop = true;
        }
    }

    private void OnQueueMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        if (sender is ListBox { SelectedIndex: >= 0 } list)
            _dragFrom = list.SelectedIndex;
    }

    private void OnQueueMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragFrom < 0) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is ListBox list && list.SelectedItem is Track track)
            DragDrop.DoDragDrop(list, track, DragDropEffects.Move);
    }

    private void OnQueueDrop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox list) return;
        if (DataContext is not NowPlayingViewModel vm) return;
        if (_dragFrom < 0) return;

        var toIndex = list.SelectedIndex;
        if (toIndex < 0) toIndex = list.Items.Count - 1;
        vm.Player.MoveUpNext(_dragFrom, toIndex);
        _dragFrom = -1;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var nested = FindVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }
}
