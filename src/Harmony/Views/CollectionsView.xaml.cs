namespace Harmony.Views;

using System.Windows;
using System.Windows.Controls;

public partial class CollectionsView
{
    public CollectionsView() => InitializeComponent();

    private void OpenTrackMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.ContextMenu == null) return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.IsOpen = true;
    }
}
