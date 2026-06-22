using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Harmony.Helpers;

namespace Harmony.Views;

/// <summary>Dark-themed OK alert (replaces system MessageBox for info).</summary>
public sealed class DarkAlertDialog : Window
{
    private DarkAlertDialog(string message, string title)
    {
        Title = title;
        Width = 400;
        MinHeight = 140;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;

        var root = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = (Brush)Application.Current.FindResource("AppBackgroundBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("GlassBorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(22, 20, 22, 18),
            Child = new StackPanel()
        };
        var panel = (StackPanel)root.Child!;

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            FontSize = 16,
            Margin = new Thickness(0, 0, 0, 10)
        });
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush"),
            Margin = new Thickness(0, 0, 0, 18)
        });

        var ok = new Button
        {
            Content = "OK",
            Style = (Style)Application.Current.FindResource("PillButton"),
            Padding = new Thickness(24, 8, 24, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true,
            IsCancel = true
        };
        ok.Click += (_, _) => Close();
        panel.Children.Add(ok);
        Content = root;
    }

    public static void Show(Window owner, string message, string? title = null)
    {
        var dialog = new DarkAlertDialog(message, title ?? AppBranding.Name) { Owner = owner };
        dialog.ShowDialog();
    }
}
