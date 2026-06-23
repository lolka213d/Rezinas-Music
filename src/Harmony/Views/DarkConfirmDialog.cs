using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Harmony.Helpers;
using Harmony.Services.Localization;

namespace Harmony.Views;

/// <summary>Dark-themed Yes/No confirmation (replaces system MessageBox).</summary>
public sealed class DarkConfirmDialog : Window
{
    private DarkConfirmDialog(string message, string title)
    {
        Title = title;
        Width = 400;
        MinHeight = 160;
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

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var loc = LocalizationService.Instance;
        var no = new Button
        {
            Content = loc.T("common.no"),
            Style = (Style)Application.Current.FindResource("OutlineButton"),
            Padding = new Thickness(18, 8, 18, 8),
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        no.Click += (_, _) => { DialogResult = false; Close(); };
        var yes = new Button
        {
            Content = loc.T("common.yes"),
            Style = (Style)Application.Current.FindResource("PillButton"),
            Padding = new Thickness(20, 8, 20, 8),
            IsDefault = true
        };
        yes.Click += (_, _) => { DialogResult = true; Close(); };
        buttons.Children.Add(no);
        buttons.Children.Add(yes);
        panel.Children.Add(buttons);
        Content = root;
    }

    public static bool Ask(Window owner, string message, string? title = null)
    {
        var dialog = new DarkConfirmDialog(message, title ?? AppBranding.Name) { Owner = owner };
        return dialog.ShowDialog() == true;
    }
}
