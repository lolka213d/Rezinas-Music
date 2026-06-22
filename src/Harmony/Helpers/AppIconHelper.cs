using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Harmony.Helpers;

/// <summary>Loads branded icons for the window and system tray.</summary>
public static class AppIconHelper
{
    private static readonly string IcoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.ico");

    public static Icon? LoadTrayIcon()
    {
        try
        {
            if (File.Exists(IcoPath))
                return new Icon(IcoPath);
        }
        catch { }

        try
        {
            return Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        }
        catch
        {
            return null;
        }
    }

    public static void ApplyWindowIcon(Window window)
    {
        try
        {
            window.Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/app-icon.ico", UriKind.Absolute));
            return;
        }
        catch { }

        try
        {
            if (File.Exists(IcoPath))
            {
                window.Icon = BitmapFrame.Create(new Uri(IcoPath, UriKind.Absolute));
                return;
            }

            var tray = LoadTrayIcon();
            if (tray != null)
                window.Icon = Imaging.CreateBitmapSourceFromHIcon(
                    tray.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
        }
        catch { }
    }
}
