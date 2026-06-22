using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services.Localization;
using Harmony.ViewModels;
using Application = System.Windows.Application;

namespace Harmony.Services;

/// <summary>System tray icon with rich dark context menu and navigation shortcuts.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly PlayerViewModel _player;
    private readonly NavigationService _navigation;
    private readonly ILocalizationService _loc;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _nowPlayingItem;
    private readonly ToolStripMenuItem _playPauseItem;

    public TrayIconService(
        PlayerViewModel player,
        NavigationService navigation,
        ILocalizationService localization)
    {
        _player = player;
        _navigation = navigation;
        _loc = localization;

        _icon = new NotifyIcon
        {
            Text = AppBranding.Name,
            Visible = false,
            Icon = AppIconHelper.LoadTrayIcon() ?? SystemIcons.Application
        };

        _menu = new ContextMenuStrip
        {
            Renderer = new DarkTrayMenuRenderer(),
            ShowImageMargin = true,
            ImageScalingSize = new System.Drawing.Size(16, 16)
        };

        _nowPlayingItem = MakeHeader(_loc.T("tray.nowPlaying"));
        _playPauseItem = MakeItem("play", _loc.T("tray.playPause"), (_, _) => Run(() => _player.PlayPauseCommand.Execute(null)));

        _menu.Items.Add(_nowPlayingItem);
        _menu.Items.Add(_playPauseItem);
        _menu.Items.Add(MakeItem("prev", _loc.T("tray.previous"), (_, _) => Run(() => _player.PreviousCommand.Execute(null))));
        _menu.Items.Add(MakeItem("next", _loc.T("tray.next"), (_, _) => Run(() => _player.NextCommand.Execute(null))));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(MakeItem("home", _loc.NavLabel(AppPage.Home), (_, _) => Navigate(AppPage.Home)));
        _menu.Items.Add(MakeItem("search", _loc.NavLabel(AppPage.Search), (_, _) => Navigate(AppPage.Search)));
        _menu.Items.Add(MakeItem("library", _loc.NavLabel(AppPage.Library), (_, _) => Navigate(AppPage.Library)));
        _menu.Items.Add(MakeItem("favorites", _loc.NavLabel(AppPage.Favorites), (_, _) => Navigate(AppPage.Favorites)));
        _menu.Items.Add(MakeItem("settings", _loc.NavLabel(AppPage.Settings), (_, _) => Navigate(AppPage.Settings)));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(MakeItem("show", string.Format(_loc.T("tray.showApp"), AppBranding.Name), (_, _) => ShowMainWindow()));
        _menu.Items.Add(MakeItem("exit", _loc.T("tray.exit"), (_, _) =>
        {
            Harmony.App.ForceShutdown = true;
            Application.Current.Shutdown();
        }));

        _icon.ContextMenuStrip = _menu;
        _icon.DoubleClick += (_, _) => ShowMainWindow();

        _player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PlayerViewModel.CurrentTrack) or nameof(PlayerViewModel.IsPlaying))
                RefreshNowPlaying();
        };

        _loc.LanguageChanged += (_, _) => RefreshLabels();
        RefreshLabels();
        RefreshNowPlaying();
    }

    public bool IsEnabled
    {
        get => _icon.Visible;
        set => _icon.Visible = value;
    }

    public void NotifyHiddenToTray()
    {
        if (!_icon.Visible) return;
        try
        {
            _icon.ShowBalloonTip(
                2500,
                AppBranding.Name,
                _loc.T("tray.hiddenHint"),
                ToolTipIcon.Info);
        }
        catch { }
    }

    private void RefreshLabels()
    {
        _nowPlayingItem.Text = _loc.T("tray.nowPlaying");
        _playPauseItem.Text = _loc.T("tray.playPause");
        if (_menu.Items.Count >= 12)
        {
            _menu.Items[2].Text = _loc.T("tray.previous");
            _menu.Items[3].Text = _loc.T("tray.next");
            _menu.Items[5].Text = _loc.NavLabel(AppPage.Home);
            _menu.Items[6].Text = _loc.NavLabel(AppPage.Search);
            _menu.Items[7].Text = _loc.NavLabel(AppPage.Library);
            _menu.Items[8].Text = _loc.NavLabel(AppPage.Favorites);
            _menu.Items[9].Text = _loc.NavLabel(AppPage.Settings);
            _menu.Items[11].Text = string.Format(_loc.T("tray.showApp"), AppBranding.Name);
            _menu.Items[12].Text = _loc.T("tray.exit");
        }
    }

    private void RefreshNowPlaying()
    {
        var track = _player.CurrentTrack;
        var title = track?.Title;
        _nowPlayingItem.Text = string.IsNullOrWhiteSpace(title)
            ? _loc.T("tray.nothingPlaying")
            : $"{_loc.T("tray.nowPlaying")}: {title}";
        _icon.Text = string.IsNullOrWhiteSpace(title) ? AppBranding.Name : $"{title} — {AppBranding.Name}";
        _playPauseItem.Image = TrayMenuIcons.Get(_player.IsPlaying ? "pause" : "play");
    }

    private static ToolStripMenuItem MakeHeader(string text) =>
        new(text) { Enabled = false, Tag = "header", Image = TrayMenuIcons.Get("music") };

    private static ToolStripMenuItem MakeItem(string iconKey, string text, EventHandler onClick) =>
        new(text, TrayMenuIcons.Get(iconKey), onClick);

    private void Navigate(AppPage page) => Run(() => _navigation.Navigate(page));

    private static void Run(Action action) =>
        Application.Current.Dispatcher.Invoke(action);

    private static void ShowMainWindow()
    {
        if (Application.Current.MainWindow is { } w)
        {
            w.Show();
            w.WindowState = WindowState.Normal;
            w.Activate();
        }
    }

    public void Dispose()
    {
        _icon.Dispose();
        foreach (ToolStripItem item in _menu.Items)
            item.Image?.Dispose();
        _menu.Dispose();
    }
}
