using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Harmony.Helpers;

namespace Harmony.Views;

public enum AppDialogChoice
{
    None,
    Primary,
    Secondary,
    Cancel
}

/// <summary>Dark glass modal dialogs — replaces system MessageBox for in-app prompts.</summary>
public sealed class AppDialog : Window
{
    private AppDialog(Window owner, string title, string body, string? versionBadge,
        string? primary, string? secondary, string? cancel)
    {
        Owner = owner;
        Title = title;
        Width = 480;
        MaxWidth = 520;
        MinWidth = 400;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;

        var shell = new Border
        {
            CornerRadius = new CornerRadius(18),
            Background = (Brush)Application.Current.FindResource("AppBackgroundBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("GlassBorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0),
            Effect = new DropShadowEffect
            {
                BlurRadius = 36,
                ShadowDepth = 0,
                Opacity = 0.55,
                Color = Color.FromRgb(124, 58, 237)
            }
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var accent = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(18, 18, 0, 0),
            Background = new LinearGradientBrush(
                Color.FromRgb(139, 92, 246),
                Color.FromRgb(56, 189, 248),
                new Point(0, 0),
                new Point(1, 0))
        };
        Grid.SetRow(accent, 0);
        layout.Children.Add(accent);

        var header = new Grid { Margin = new Thickness(24, 20, 24, 0) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBorder = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Background = new LinearGradientBrush(
                Color.FromRgb(139, 92, 246),
                Color.FromRgb(56, 189, 248),
                new Point(0, 0),
                new Point(1, 1))
        };
        iconBorder.Child = new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/app-icon.png")),
            Stretch = Stretch.UniformToFill
        };
        Grid.SetColumn(iconBorder, 0);
        header.Children.Add(iconBorder);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(versionBadge))
        {
            var badge = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(0x33, 139, 92, 246)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 139, 92, 246)),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = versionBadge,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.FindResource("AccentBrush")
                }
            };
            titleStack.Children.Add(badge);
        }

        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        if (cancel != null)
        {
            var closeBtn = new Button
            {
                Content = "✕",
                Style = (Style)Application.Current.FindResource("IconButton"),
                Width = 32,
                Height = 32,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Top
            };
            closeBtn.Click += (_, _) => { Choice = AppDialogChoice.Cancel; Close(); };
            Grid.SetColumn(closeBtn, 2);
            header.Children.Add(closeBtn);
        }

        Grid.SetRow(header, 1);
        layout.Children.Add(header);

        var bodyBlock = new TextBlock
        {
            Text = body,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            LineHeight = 22,
            Margin = new Thickness(24, 16, 24, 0),
            Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush")
        };
        Grid.SetRow(bodyBlock, 2);
        layout.Children.Add(bodyBlock);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(24, 20, 24, 22)
        };

        if (cancel != null)
        {
            var cancelBtn = new Button
            {
                Content = cancel,
                Style = (Style)Application.Current.FindResource("GhostButton"),
                MinWidth = 96,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(16, 8, 16, 8)
            };
            cancelBtn.Click += (_, _) => { Choice = AppDialogChoice.Cancel; Close(); };
            buttons.Children.Add(cancelBtn);
        }

        if (secondary != null)
        {
            var secBtn = new Button
            {
                Content = secondary,
                Style = (Style)Application.Current.FindResource("OutlineButton"),
                MinWidth = 110,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(16, 8, 16, 8)
            };
            secBtn.Click += (_, _) => { Choice = AppDialogChoice.Secondary; Close(); };
            buttons.Children.Add(secBtn);
        }

        if (primary != null)
        {
            var primBtn = new Button
            {
                Content = primary,
                Style = (Style)Application.Current.FindResource("PillButton"),
                MinWidth = 110,
                Padding = new Thickness(18, 8, 18, 8)
            };
            primBtn.Click += (_, _) => { Choice = AppDialogChoice.Primary; Close(); };
            buttons.Children.Add(primBtn);
        }

        Grid.SetRow(buttons, 3);
        layout.Children.Add(buttons);

        shell.Child = layout;
        Content = shell;
    }

    public AppDialogChoice Choice { get; private set; } = AppDialogChoice.None;

    public static AppDialogChoice ShowInfo(Window owner, string title, string body, string okLabel, string? versionBadge = null)
    {
        var dlg = new AppDialog(owner, title, body, versionBadge, okLabel, null, null);
        dlg.ShowDialog();
        return dlg.Choice == AppDialogChoice.None ? AppDialogChoice.Primary : dlg.Choice;
    }

    public static AppDialogChoice ShowChoice(Window owner, string title, string body,
        string primary, string secondary, string cancel, string? versionBadge = null)
    {
        var dlg = new AppDialog(owner, title, body, versionBadge, primary, secondary, cancel);
        dlg.ShowDialog();
        return dlg.Choice;
    }
}
