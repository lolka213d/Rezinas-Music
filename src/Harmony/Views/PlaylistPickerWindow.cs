using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Harmony.Views;

public sealed class PlaylistPickerWindow : Window
{
    private readonly ListBox _list;

    private PlaylistPickerWindow(string trackTitle, string[] playlistNames)
    {
        Title = "Add to playlist";
        Width = 400;
        Height = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;

        var shell = new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = (Brush)Application.Current.FindResource("AppBackgroundBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("GlassBorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(22, 20, 22, 18),
            Effect = new DropShadowEffect
            {
                BlurRadius = 32,
                ShadowDepth = 0,
                Opacity = 0.5,
                Color = Color.FromRgb(124, 58, 237)
            }
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        layout.Children.Add(new TextBlock
        {
            Text = $"Add «{trackTitle ?? "track"}» to:",
            FontWeight = FontWeights.SemiBold,
            FontSize = 16,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });

        _list = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 255, 255, 255)),
            Padding = new Thickness(4)
        };

        if (Application.Current.TryFindResource("RzTrackRowItem") is Style itemStyle)
            _list.ItemContainerStyle = itemStyle;

        _list.ItemTemplate = CreateItemTemplate();
        _list.ItemsSource = playlistNames;
        if (playlistNames.Length > 0)
            _list.SelectedIndex = 0;

        Grid.SetRow(_list, 1);
        layout.Children.Add(_list);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancel = new Button
        {
            Content = "Cancel",
            Style = (Style)Application.Current.FindResource("GhostButton"),
            MinWidth = 96,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(16, 8, 16, 8)
        };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };

        var ok = new Button
        {
            Content = "Add",
            Style = (Style)Application.Current.FindResource("PillButton"),
            MinWidth = 96,
            Padding = new Thickness(18, 8, 18, 8)
        };
        ok.Click += (_, _) => { DialogResult = true; Close(); };

        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        Grid.SetRow(buttons, 2);
        layout.Children.Add(buttons);

        shell.Child = layout;
        Content = shell;
    }

    private static DataTemplate CreateItemTemplate()
    {
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetValue(TextBlock.FontSizeProperty, 14.0);
        factory.SetValue(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2));
        factory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
        factory.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        template.VisualTree = factory;
        return template;
    }

    public static int Show(Window owner, string[] playlistNames, string trackTitle)
    {
        var w = new PlaylistPickerWindow(trackTitle, playlistNames) { Owner = owner };
        if (w.ShowDialog() != true) return -1;
        return w._list.SelectedIndex;
    }
}
