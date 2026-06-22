using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Harmony.Helpers;

/// <summary>Dark window chrome and DWM integration for custom title bars.</summary>
public static class WindowChromeHelper
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyDarkChrome(Window window)
    {
        if (window == null) return;

        void Apply(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            var on = 1;
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref on, sizeof(int));

            var caption = unchecked((int)0xFF0B0909);
            _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
            var text = unchecked((int)0x00FFFFFF);
            _ = DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref text, sizeof(int));
        }

        if (window.IsLoaded)
            Apply(new WindowInteropHelper(window).Handle);
        else
            window.SourceInitialized += (_, _) => Apply(new WindowInteropHelper(window).Handle);
    }
}
