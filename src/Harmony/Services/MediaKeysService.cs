using System.Runtime.InteropServices;
using System.Windows.Threading;
using Harmony.ViewModels;

namespace Harmony.Services;

/// <summary>
/// Global media keys (headphones, keyboard) — play/pause, next, previous, stop.
/// </summary>
public sealed class MediaKeysService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmSyskeydown = 0x0104;
    private const int VkMediaNextTrack = 0xB0;
    private const int VkMediaPrevTrack = 0xB1;
    private const int VkMediaStop = 0xB4;
    private const int VkMediaPlayPause = 0xB3;

    private readonly Dispatcher _dispatcher;
    private readonly PlayerViewModel _player;
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hook;
    private bool _enabled = true;

    public MediaKeysService(PlayerViewModel player)
    {
        _player = player;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _proc = HookCallback;
        _hook = SetHook();
    }

    public bool IsEnabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    private IntPtr SetHook()
    {
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        return SetWindowsHookEx(WhKeyboardLl, _proc,
            GetModuleHandle(curModule.ModuleName), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WmKeydown || wParam == (IntPtr)WmSyskeydown))
        {
            if (!_enabled)
                return CallNextHookEx(_hook, nCode, wParam, lParam);

            var vk = Marshal.ReadInt32(lParam);
            switch (vk)
            {
                case VkMediaPlayPause:
                    _dispatcher.BeginInvoke(() => _player.PlayPauseCommand.Execute(null));
                    return (IntPtr)1;
                case VkMediaNextTrack:
                    _dispatcher.BeginInvoke(() => _player.NextCommand.Execute(null));
                    return (IntPtr)1;
                case VkMediaPrevTrack:
                    _dispatcher.BeginInvoke(() => _player.PreviousCommand.Execute(null));
                    return (IntPtr)1;
                case VkMediaStop:
                    _dispatcher.BeginInvoke(() => _player.StopCommand.Execute(null));
                    return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
