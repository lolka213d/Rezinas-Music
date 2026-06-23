using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;

namespace Harmony.ViewModels;

/// <summary>Liked songs — Aurora style.</summary>
public partial class FavoritesViewModel : ObservableObject
{
    private readonly IFavoritesService _favorites;
    private readonly PlayerViewModel _player;
    private readonly ILocalizationService _loc;
    private readonly ISettingsService _settings;

    public FavoritesViewModel(
        IFavoritesService favorites,
        PlayerViewModel player,
        ILocalizationService localization,
        ISettingsService settings)
    {
        _favorites = favorites;
        _player = player;
        _loc = localization;
        _settings = settings;
        _loc.LanguageChanged += (_, _) => RefreshLabels();
        _settings.SettingsChanged += (_, _) => _ = LoadAsync();
    }

    public ILocalizationService Loc => _loc;
    public PlayerViewModel Player => _player;

    public string TitleLabel => _loc.T("favorites.title");
    public string SubtitleLabel => _loc.T("favorites.subtitle");
    public string EmptyLabel => _loc.T("favorites.empty");
    public string EmptyTitleLabel => _loc.T("favorites.emptyTitle");
    public string EmptyDescLabel => _loc.T("favorites.emptyDesc");
    public string AddMoreTitleLabel => _loc.T("favorites.addMoreTitle");
    public string AddMoreDescLabel => _loc.T("favorites.addMoreDesc");
    public string ColumnTitleLabel => _loc.T("common.title");
    public string ColumnTimeLabel => _loc.T("common.time");
    public string PlayAllLabel => _loc.T("collections.playAll");
    public string ClearAllLabel => _loc.T("favorites.clearAll");

    public ObservableCollection<Track> Tracks { get; } = new();

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private int _trackCount;

    public bool HasTracks => Tracks.Count > 0;
    public bool ShowRecommendations => Tracks.Count > 0 && Tracks.Count <= 3;

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(TitleLabel));
        OnPropertyChanged(nameof(SubtitleLabel));
        OnPropertyChanged(nameof(EmptyLabel));
        OnPropertyChanged(nameof(EmptyTitleLabel));
        OnPropertyChanged(nameof(EmptyDescLabel));
        OnPropertyChanged(nameof(AddMoreTitleLabel));
        OnPropertyChanged(nameof(AddMoreDescLabel));
        OnPropertyChanged(nameof(ColumnTitleLabel));
        OnPropertyChanged(nameof(ColumnTimeLabel));
        OnPropertyChanged(nameof(ClearAllLabel));
    }

    public async Task LoadAsync()
    {
        Tracks.Clear();
        foreach (var t in await _favorites.GetFavoritesAsync())
            Tracks.Add(t);
        TrackCount = Tracks.Count;
        IsEmpty = Tracks.Count == 0;
        NotifyTrackListChanged();
    }

    private void NotifyTrackListChanged()
    {
        OnPropertyChanged(nameof(HasTracks));
        OnPropertyChanged(nameof(ShowRecommendations));
    }

    [RelayCommand]
    private Task Play(Track? track)
    {
        if (track == null) return Task.CompletedTask;
        return _player.PlayQueueAsync(Tracks.ToList(), track);
    }

    [RelayCommand]
    private Task PlayAll()
    {
        if (Tracks.Count == 0) return Task.CompletedTask;
        var queue = Tracks.ToList();
        return _player.PlayQueueAsync(queue, queue[0]);
    }

    [RelayCommand]
    private Task ShuffleAll()
    {
        if (Tracks.Count == 0) return Task.CompletedTask;
        var shuffled = Tracks.OrderBy(_ => Random.Shared.Next()).ToList();
        return _player.PlayQueueAsync(shuffled, shuffled[0]);
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        if (Tracks.Count == 0) return;
        var confirm = MessageBox.Show(
            _loc.T("favorites.clearAllConfirm"),
            AppBranding.Name,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        await _favorites.ClearAllAsync();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Unlike(Track track)
    {
        await _favorites.ToggleAsync(track);
        await LoadAsync();
    }
}
