using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;

namespace Harmony.Views;

/// <summary>Modal: name a new playlist and optionally pick tracks to add.</summary>
public sealed class CreatePlaylistDialog : Window
{
    private readonly TextBox _nameBox;
    private readonly TextBox _searchBox;
    private readonly ListBox _resultsList;
    private readonly TextBlock _hintText;
    private readonly TextBlock _selectedCountText;
    private readonly ObservableCollection<TrackPickItem> _results = new();
    private readonly IEnumerable<IMusicSearchService> _providers;
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _loc;
    private CancellationTokenSource? _searchCts;

    private CreatePlaylistDialog(IEnumerable<IMusicSearchService> providers, ISettingsService settings, ILocalizationService localization)
    {
        _providers = providers;
        _settings = settings;
        _loc = localization;

        Title = _loc.T("common.newPlaylist");
        Width = 560;
        Height = 580;
        MinWidth = 480;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;

        var root = new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = (Brush)Application.Current.FindResource("AppBackgroundBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("GlassBorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24)
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new TextBlock
        {
            Text = _loc.T("createPlaylist.dialogTitle"),
            FontSize = 22,
            FontWeight = FontWeights.Bold
        });

        var closeBtn = new Button
        {
            Content = "✕",
            Style = (Style)Application.Current.FindResource("IconButton"),
            Width = 32,
            Height = 32,
            FontSize = 14
        };
        closeBtn.Click += (_, _) => { DialogResult = false; Close(); };
        Grid.SetColumn(closeBtn, 1);
        header.Children.Add(closeBtn);
        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        var nameLabel = new TextBlock
        {
            Text = _loc.T("createPlaylist.nameLabel"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(nameLabel, 1);
        layout.Children.Add(nameLabel);

        _nameBox = new TextBox { Margin = new Thickness(0, 0, 0, 16), Padding = new Thickness(12, 10, 12, 10) };
        _nameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                TryCreate();
        };
        Grid.SetRow(_nameBox, 2);
        layout.Children.Add(_nameBox);

        var tracksHeader = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        tracksHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tracksHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tracksHeader.Children.Add(new TextBlock
        {
            Text = _loc.T("createPlaylist.addTracksLabel"),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        _selectedCountText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Muted"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_selectedCountText, 1);
        tracksHeader.Children.Add(_selectedCountText);
        Grid.SetRow(tracksHeader, 3);
        layout.Children.Add(tracksHeader);

        _searchBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(12, 10, 12, 10)
        };
        _searchBox.GotFocus += (_, _) =>
        {
            if (string.IsNullOrEmpty(_searchBox.Text))
                _searchBox.Text = string.Empty;
        };
        _searchBox.TextChanged += (_, _) => _ = DebouncedSearchAsync();
        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(_searchBox.Text))
                _searchBox.Text = _loc.T("createPlaylist.searchPlaceholder");
        };
        _searchBox.GotFocus += (_, _) =>
        {
            if (_searchBox.Text == _loc.T("createPlaylist.searchPlaceholder"))
                _searchBox.Text = string.Empty;
        };
        _searchBox.LostFocus += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_searchBox.Text))
                _searchBox.Text = _loc.T("createPlaylist.searchPlaceholder");
        };

        _hintText = new TextBlock
        {
            Text = _loc.T("createPlaylist.initialHint"),
            Style = (Style)Application.Current.FindResource("Muted"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };

        _resultsList = new ListBox
        {
            ItemsSource = _results,
            SelectionMode = SelectionMode.Multiple,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Visibility = Visibility.Collapsed
        };
        if (Application.Current.TryFindResource("GlassTrackListBox") is Style listStyle)
            _resultsList.Style = listStyle;
        _resultsList.ItemTemplate = CreateTrackTemplate();
        _resultsList.SelectionChanged += (_, _) => UpdateSelectedCount();

        var resultsPanel = new Grid();
        resultsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        resultsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        resultsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        resultsPanel.Children.Add(_searchBox);
        Grid.SetRow(_hintText, 1);
        resultsPanel.Children.Add(_hintText);
        Grid.SetRow(_resultsList, 2);
        resultsPanel.Children.Add(_resultsList);
        Grid.SetRow(resultsPanel, 4);
        layout.Children.Add(resultsPanel);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        var cancel = new Button
        {
            Content = _loc.T("common.cancel"),
            Style = (Style)Application.Current.FindResource("OutlineButton"),
            Padding = new Thickness(18, 10, 18, 10),
            Margin = new Thickness(0, 0, 10, 0)
        };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };

        var create = new Button
        {
            Content = _loc.T("common.create"),
            Style = (Style)Application.Current.FindResource("PillButton"),
            Padding = new Thickness(24, 10, 24, 10),
            MinWidth = 120
        };
        create.Click += (_, _) => TryCreate();

        buttons.Children.Add(cancel);
        buttons.Children.Add(create);
        Grid.SetRow(buttons, 6);
        layout.Children.Add(buttons);

        root.Child = layout;
        Content = root;
        Loaded += (_, _) => _nameBox.Focus();
        UpdateSelectedCount();
    }

    public string PlaylistName { get; private set; } = string.Empty;

    public IReadOnlyList<Track> SelectedTracks { get; private set; } = Array.Empty<Track>();

    public static async Task<CreatePlaylistDialog?> ShowAsync(
        IEnumerable<IMusicSearchService> providers,
        ISettingsService settings,
        ILocalizationService localization,
        Window owner)
    {
        var dialog = new CreatePlaylistDialog(providers, settings, localization) { Owner = owner };
        await settings.LoadAsync();
        return dialog.ShowDialog() == true ? dialog : null;
    }

    private void UpdateSelectedCount()
    {
        var count = _resultsList.SelectedItems.Count;
        _selectedCountText.Text = count > 0
            ? string.Format(_loc.T("createPlaylist.selectedCount"), count)
            : string.Empty;
    }

    private void TryCreate()
    {
        var name = _nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            DarkAlertDialog.Show(this, _loc.T("createPlaylist.nameRequired"));
            _nameBox.Focus();
            return;
        }

        PlaylistName = name;
        SelectedTracks = _resultsList.SelectedItems
            .OfType<TrackPickItem>()
            .Select(i => i.Track)
            .ToList();
        DialogResult = true;
        Close();
    }

    private async Task DebouncedSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        var query = _searchBox.Text.Trim();
        var placeholder = _loc.T("createPlaylist.searchPlaceholder");
        if (query == placeholder) query = string.Empty;

        if (query.Length < 2)
        {
            _results.Clear();
            _resultsList.Visibility = Visibility.Collapsed;
            _hintText.Text = _loc.T("createPlaylist.searchMinChars");
            UpdateSelectedCount();
            return;
        }

        try
        {
            await Task.Delay(350, token);
            await SearchAsync(query, token);
        }
        catch (OperationCanceledException) { }
    }

    private async Task SearchAsync(string query, CancellationToken token)
    {
        _hintText.Text = _loc.T("createPlaylist.searching");
        var available = _providers.Where(p => p.IsAvailable).ToList();
        if (available.Count == 0)
        {
            _hintText.Text = _loc.T("createPlaylist.noProviders");
            return;
        }

        var tasks = available.Select(async p =>
        {
            try
            {
                var tracks = await p.SearchAsync(query, token);
                return tracks;
            }
            catch
            {
                return Array.Empty<Track>();
            }
        });

        var merged = TrackDedup.Merge((await Task.WhenAll(tasks)).SelectMany(t => t)).Take(30).ToList();
        if (token.IsCancellationRequested) return;

        _results.Clear();
        foreach (var track in merged)
            _results.Add(new TrackPickItem(track));

        _resultsList.Visibility = merged.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _hintText.Text = merged.Count > 0
            ? _loc.T("createPlaylist.hintSelect")
            : _loc.T("createPlaylist.hintEmpty");
        UpdateSelectedCount();
    }

    private static DataTemplate CreateTrackTemplate()
    {
        var template = new DataTemplate(typeof(TrackPickItem));
        var factory = new FrameworkElementFactory(typeof(StackPanel));

        var title = new FrameworkElementFactory(typeof(TextBlock));
        title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title"));
        title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);

        var meta = new FrameworkElementFactory(typeof(TextBlock));
        meta.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("MetaLine"));
        meta.SetValue(TextBlock.FontSizeProperty, 12.0);
        meta.SetValue(TextBlock.OpacityProperty, 0.7);

        factory.AppendChild(title);
        factory.AppendChild(meta);
        template.VisualTree = factory;
        return template;
    }

    private sealed class TrackPickItem
    {
        public TrackPickItem(Track track)
        {
            Track = track;
            Title = track.Title;
            MetaLine = string.IsNullOrWhiteSpace(track.DurationDisplay)
                ? track.ArtistName
                : $"{track.ArtistName} · {track.DurationDisplay}";
        }

        public Track Track { get; }
        public string Title { get; }
        public string MetaLine { get; }
    }
}
