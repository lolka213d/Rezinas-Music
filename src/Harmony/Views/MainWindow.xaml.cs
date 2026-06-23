using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.ViewModels;

namespace Harmony.Views;

public partial class MainWindow : Window
{
    private const int WmAppCommand = 0x0319;
    private const int WmKeydown = 0x0100;
    private const int WmSyskeydown = 0x0104;
    private const int AppcmdMediaPlayPause = 14;
    private const int AppcmdMediaNextTrack = 11;
    private const int AppcmdMediaPreviousTrack = 12;
    private const int AppcmdMediaStop = 13;
    private const int VkMediaNextTrack = 0xB0;
    private const int VkMediaPrevTrack = 0xB1;
    private const int VkMediaStop = 0xB4;
    private const int VkMediaPlayPause = 0xB3;

    private readonly ISettingsService _settings;
    private readonly TrayIconService _tray;
    private readonly UiPerformanceService _uiPerf;

    public MainWindow(ISettingsService settings, TrayIconService tray, UiPerformanceService uiPerf)
    {
        _settings = settings;
        _tray = tray;
        _uiPerf = uiPerf;
        InitializeComponent();
        AppIconHelper.ApplyWindowIcon(this);
        Closing += OnClosing;
        WindowChromeHelper.ApplyDarkChrome(this);
        StateChanged += (_, _) =>
        {
            UpdateMaximizeIcon();
            ApplyWorkArea();
            PushWindowState();
        };
        IsVisibleChanged += (_, _) => PushWindowState();
        Activated += (_, _) => PushWindowState();
        Deactivated += (_, _) => PushWindowState();
        _uiPerf.Changed += (_, _) => ApplyPerformanceUi();
        Loaded += (_, _) =>
        {
            _uiPerf.ReduceGpuUsage = _settings.Current.ReduceGpuUsage;
            ApplyPerformanceUi();
        };

        InputBindings.Add(new KeyBinding { Key = Key.Space, Modifiers = ModifierKeys.None, Command = PlayerPlayPauseCommand });
        InputBindings.Add(new KeyBinding { Key = Key.MediaPlayPause, Command = PlayerPlayPauseCommand });
        InputBindings.Add(new KeyBinding { Key = Key.MediaNextTrack, Command = PlayerNextCommand });
        InputBindings.Add(new KeyBinding { Key = Key.MediaPreviousTrack, Command = PlayerPrevCommand });
        InputBindings.Add(new KeyBinding { Key = Key.MediaStop, Command = PlayerStopCommand });
        InputBindings.Add(new KeyBinding { Key = Key.Right, Modifiers = ModifierKeys.Control, Command = PlayerNextCommand });
        InputBindings.Add(new KeyBinding { Key = Key.L, Modifiers = ModifierKeys.Control, Command = GoSearchCommand });
        InputBindings.Add(new KeyBinding { Key = Key.OemQuestion, Modifiers = ModifierKeys.Shift, Command = ShowHotkeysCommand });
        InputBindings.Add(new KeyBinding { Key = Key.Left, Command = PlayerPrevCommand });
        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            Maximize_Click(sender, e);
        else
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (Harmony.App.ForceShutdown || !_settings.Current.MiniPlayerInTray)
            return;

        e.Cancel = true;
        Hide();
        _tray.IsEnabled = true;
        _tray.NotifyHiddenToTray();
    }

    private void UpdateMaximizeIcon()
    {
        if (MaximizeIcon == null) return;
        MaximizeIcon.Data = WindowState == WindowState.Maximized
            ? System.Windows.Media.Geometry.Parse("M6 8h10v10H6z M8 6h10v2H8z")
            : System.Windows.Media.Geometry.Parse("M4 6h12v12H4z");
    }

    private void ApplyWorkArea()
    {
        if (WindowState != WindowState.Maximized)
        {
            MaxHeight = double.PositiveInfinity;
            MaxWidth = double.PositiveInfinity;
            return;
        }

        var area = SystemParameters.WorkArea;
        MaxWidth = area.Width;
        MaxHeight = area.Height;
        Left = area.Left;
        Top = area.Top;
    }

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        if (Vm != null)
            await Vm.StartInitialLoadAsync();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource src)
            src.AddHook(WndProc);
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    public ICommand PlayerPlayPauseCommand => new RelayCommand(_ => Vm?.Player.PlayPauseCommand.Execute(null));
    public ICommand PlayerNextCommand => new RelayCommand(_ => Vm?.Player.NextCommand.Execute(null));
    public ICommand PlayerPrevCommand => new RelayCommand(_ => Vm?.Player.PreviousCommand.Execute(null));
    public ICommand PlayerStopCommand => new RelayCommand(_ => Vm?.Player.StopCommand.Execute(null));
    private MiniPlayerWindow? _miniPlayer;

    public ICommand GoSearchCommand => new RelayCommand(_ =>
    {
        if (Vm?.NavigationItems.FirstOrDefault(n => n.Page == AppPage.Search) is { } item)
            Vm.SelectedNavigation = item;
    });

    public ICommand ShowHotkeysCommand => new RelayCommand(_ =>
    {
        if (Vm == null) return;
        HotkeysOverlayWindow.ShowForOwner(this, Vm.Settings.HotkeysHint);
    });

    public void ShowMiniPlayer(bool show)
    {
        if (show)
        {
            _miniPlayer ??= new MiniPlayerWindow(Vm!);
            _miniPlayer.Closed += (_, _) => _uiPerf.SetMiniPlayerVisible(false);
            _miniPlayer.Show();
            _uiPerf.SetMiniPlayerVisible(true);
            return;
        }
        _miniPlayer?.Close();
        _miniPlayer = null;
        _uiPerf.SetMiniPlayerVisible(false);
    }

    private void PushWindowState() =>
        _uiPerf.SetMainWindowState(IsVisible, IsActive, WindowState == WindowState.Minimized);

    private void ApplyPerformanceUi()
    {
        AmbientLayer.Visibility = _uiPerf.ShouldHideAmbient ? Visibility.Collapsed : Visibility.Visible;

        if (FindName("PageHost") is FrameworkElement host && _uiPerf.ShouldUseLiteChrome)
            VisualTreeEffectHelper.EnableBitmapCache(host);

        if (_uiPerf.ShouldUseLiteChrome)
            VisualTreeEffectHelper.StripDropShadows(this);

        FindVisualChild<PlayerBar>(this)?.ApplyLiteChrome(_uiPerf.ShouldUseLiteChrome);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var nested = FindVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is WmKeydown or WmSyskeydown)
        {
            switch (wParam.ToInt32())
            {
                case VkMediaPlayPause:
                    Vm?.Player.PlayPauseCommand.Execute(null);
                    handled = true;
                    return IntPtr.Zero;
                case VkMediaNextTrack:
                    Vm?.Player.NextCommand.Execute(null);
                    handled = true;
                    return IntPtr.Zero;
                case VkMediaPrevTrack:
                    Vm?.Player.PreviousCommand.Execute(null);
                    handled = true;
                    return IntPtr.Zero;
                case VkMediaStop:
                    Vm?.Player.StopCommand.Execute(null);
                    handled = true;
                    return IntPtr.Zero;
            }
        }

        if (msg != WmAppCommand) return IntPtr.Zero;
        var cmd = (wParam.ToInt32() >> 16) & 0xFFF;
        switch (cmd)
        {
            case AppcmdMediaPlayPause:
                Vm?.Player.PlayPauseCommand.Execute(null);
                handled = true;
                break;
            case AppcmdMediaNextTrack:
                Vm?.Player.NextCommand.Execute(null);
                handled = true;
                break;
            case AppcmdMediaPreviousTrack:
                Vm?.Player.PreviousCommand.Execute(null);
                handled = true;
                break;
            case AppcmdMediaStop:
                Vm?.Player.StopCommand.Execute(null);
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        public RelayCommand(Action<object?> execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
    }
}
