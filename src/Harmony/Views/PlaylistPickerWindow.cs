using System.Windows;
using System.Windows.Controls;

namespace Harmony.Views;

public sealed class PlaylistPickerWindow : Window
{
    private readonly ListBox _list;

    private PlaylistPickerWindow(string trackTitle, string[] playlistNames)
    {
        Title = "Add to playlist";
        Width = 360;
        Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (System.Windows.Media.Brush)Application.Current.FindResource("SurfaceBrush");

        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(new TextBlock
        {
            Text = $"Add «{trackTitle ?? "track"}» to:",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        });

        _list = new ListBox { ItemsSource = playlistNames, Height = 180 };
        root.Children.Add(_list);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var ok = new Button { Content = "Add", Padding = new Thickness(14, 6, 14, 6) };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        root.Children.Add(buttons);

        Content = root;
    }

    public static int Show(Window owner, string[] playlistNames, string trackTitle)
    {
        var w = new PlaylistPickerWindow(trackTitle, playlistNames) { Owner = owner };
        if (w.ShowDialog() != true) return -1;
        return w._list.SelectedIndex;
    }
}
