using System.Windows;
using System.Windows.Input;
using Harmony.ViewModels;

namespace Harmony.Views;

public partial class HotkeysOverlayWindow : Window
{
    public HotkeysOverlayWindow(string body)
    {
        Title = "Keyboard shortcuts";
        Width = 420;
        Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.NoResize;
        Background = (System.Windows.Media.Brush)Application.Current.FindResource("SurfaceBrush");

        var box = new System.Windows.Controls.TextBox
        {
            Text = body,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Margin = new Thickness(20),
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            FontSize = 13
        };
        Content = box;
    }

    public static void ShowForOwner(Window owner, string body)
    {
        var w = new HotkeysOverlayWindow(body) { Owner = owner };
        w.ShowDialog();
    }
}
