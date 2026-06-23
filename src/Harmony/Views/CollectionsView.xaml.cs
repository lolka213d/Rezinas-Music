namespace Harmony.Views;

using System.Windows;
using System.Windows.Controls;
using Harmony.ViewModels;

public partial class CollectionsView
{
    public CollectionsView() => InitializeComponent();

    private void OpenTrackMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.ContextMenu == null) return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.DataContext = btn.DataContext;
        btn.ContextMenu.IsOpen = true;
    }

    private void OpenPlaylistMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.ContextMenu == null) return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.DataContext = DataContext;
        btn.ContextMenu.IsOpen = true;
    }

    private void EditName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is CollectionsViewModel vm && vm.RenameCommand.CanExecute(null))
            vm.RenameCommand.Execute(null);
    }
}
