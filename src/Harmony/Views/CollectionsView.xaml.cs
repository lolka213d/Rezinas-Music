namespace Harmony.Views;

using System.Windows;
using System.Windows.Controls;
using Harmony.ViewModels;

public partial class CollectionsView
{
    public CollectionsView() => InitializeComponent();

    private void OpenTrackMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || DataContext is not CollectionsViewModel vm) return;
        if (btn.DataContext is not CollectionTrackRow row) return;

        var menu = new ContextMenu { PlacementTarget = btn };
        menu.Items.Add(new MenuItem
        {
            Header = vm.RemoveFromPlaylistTip,
            Command = vm.RemoveTrackCommand,
            CommandParameter = row.Track
        });
        menu.Items.Add(new MenuItem
        {
            Header = vm.AddToPlaylistTip,
            Command = vm.AddTrackElsewhereCommand,
            CommandParameter = row.Track
        });

        menu.IsOpen = true;
        e.Handled = true;
    }

    private void OpenPlaylistMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || DataContext is not CollectionsViewModel vm) return;

        var menu = new ContextMenu { PlacementTarget = btn };
        menu.Items.Add(new MenuItem
        {
            Header = vm.FindSongsTitle,
            Command = vm.ToggleAddPanelCommand
        });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = vm.Loc.T("common.delete"),
            Command = vm.DeleteCommand
        });
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void EditName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is CollectionsViewModel vm && vm.RenameCommand.CanExecute(null))
            vm.RenameCommand.Execute(null);
    }
}
