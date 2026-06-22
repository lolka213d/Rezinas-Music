using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;

namespace Harmony.Views;

/// <summary>Modal: name a new playlist and optionally pick tracks to add.</summary>
public sealed class CreatePlaylistDialog : Window
{
    private readonly TextBox _nameBox;
    private readonly TextBox _searchBox;
    private readonly ListBox _resultsList;
    private readonly TextBlock _hintText;
    private readonly ObservableCollection<TrackPickItem> _results = new();
    private readonly IEnumerable<IMusicSearchService> _providers;
    private readonly ISettingsService _settings;
    private CancellationTokenSource? _searchCts;

    private CreatePlaylistDialog(IEnumerable<IMusicSearchService> providers, ISettingsService settings)
    {
        _providers = providers;
        _settings = settings;

        Title = "Новый плейлист";
        Width = 480;
        Height = 520;
        MinWidth = 400;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;

        var root = new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = (Brush)Application.Current.FindResource("AppBackgroundBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("GlassBorderBrush"),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Margin = new Thickness(20) }
        };
        var panel = (StackPanel)root.Child;

        panel.Children.Add(new TextBlock
        {
            Text = "Название плейлиста",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        _nameBox = new TextBox { Margin = new Thickness(0, 0, 0, 16) };
        _nameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                TryCreate();
        };
        panel.Children.Add(_nameBox);

        panel.Children.Add(new TextBlock
        {
            Text = "Добавить треки (необязательно)",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        _searchBox = new TextBox
        {
            Tag = "Поиск треков…",
            Margin = new Thickness(0, 0, 0, 8)
        };
        _searchBox.TextChanged += (_, _) => _ = DebouncedSearchAsync();
        panel.Children.Add(_searchBox);

        _hintText = new TextBlock
        {
            Text = "Найдите треки или создайте пустой плейлист.",
            Style = (Style)Application.Current.FindResource("Muted"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(_hintText);

        _resultsList = new ListBox
        {
            Height = 220,
            ItemsSource = _results,
            SelectionMode = SelectionMode.Multiple,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Visibility = Visibility.Collapsed
        };
        if (Application.Current.TryFindResource("GlassTrackListBox") is Style listStyle)
            _resultsList.Style = listStyle;
        _resultsList.ItemTemplate = CreateTrackTemplate();
        panel.Children.Add(_resultsList);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancel = new Button
        {
            Content = "Отмена",
            Style = (Style)Application.Current.FindResource("OutlineButton"),
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };

        var createEmpty = new Button
        {
            Content = "Создать без треков",
            Style = (Style)Application.Current.FindResource("OutlineButton"),
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 0, 8, 0)
        };
        createEmpty.Click += (_, _) => TryCreate();

        var create = new Button
        {
            Content = "Создать",
            Style = (Style)Application.Current.FindResource("PillButton"),
            Padding = new Thickness(20, 8, 20, 8)
        };
        create.Click += (_, _) => TryCreate();

        buttons.Children.Add(cancel);
        buttons.Children.Add(createEmpty);
        buttons.Children.Add(create);
        panel.Children.Add(buttons);

        Content = root;
        Loaded += (_, _) => _nameBox.Focus();
    }

    public string PlaylistName { get; private set; } = string.Empty;

    public IReadOnlyList<Track> SelectedTracks { get; private set; } = Array.Empty<Track>();

    public static async Task<CreatePlaylistDialog?> ShowAsync(
        IEnumerable<IMusicSearchService> providers,
        ISettingsService settings,
        Window owner)
    {
        var dialog = new CreatePlaylistDialog(providers, settings) { Owner = owner };
        await settings.LoadAsync();
        return dialog.ShowDialog() == true ? dialog : null;
    }

    private void TryCreate()
    {
        var name = _nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Введите название плейлиста.", AppBranding.Name,
                MessageBoxButton.OK, MessageBoxImage.Information);
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
        if (query.Length < 2)
        {
            _results.Clear();
            _resultsList.Visibility = Visibility.Collapsed;
            _hintText.Text = "Введите минимум 2 символа для поиска.";
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
        _hintText.Text = "Поиск…";
        var available = _providers.Where(p => p.IsAvailable).ToList();
        if (available.Count == 0)
        {
            _hintText.Text = "Нет доступных источников музыки.";
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
            ? "Выберите треки или оставьте пустым."
            : "Ничего не найдено — можно создать пустой плейлист.";
    }

    private static DataTemplate CreateTrackTemplate()
    {
        var template = new DataTemplate(typeof(TrackPickItem));
        var factory = new FrameworkElementFactory(typeof(StackPanel));

        var title = new FrameworkElementFactory(typeof(TextBlock));
        title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title"));
        title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);

        var artist = new FrameworkElementFactory(typeof(TextBlock));
        artist.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("ArtistName"));
        artist.SetValue(TextBlock.FontSizeProperty, 12.0);
        artist.SetValue(TextBlock.OpacityProperty, 0.7);

        factory.AppendChild(title);
        factory.AppendChild(artist);
        template.VisualTree = factory;
        return template;
    }

    private sealed class TrackPickItem
    {
        public TrackPickItem(Track track)
        {
            Track = track;
            Title = track.Title;
            ArtistName = track.ArtistName;
        }

        public Track Track { get; }
        public string Title { get; }
        public string ArtistName { get; }
    }
}
