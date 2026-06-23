using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;
using Harmony.Views;

namespace Harmony.ViewModels;

public sealed class HistoryGroup
{
    public required string Title { get; init; }
    public ObservableCollection<HistoryItem> Items { get; } = new();
}

/// <summary>Listening history page with per-track play counts.</summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IHistoryService _history;
    private readonly PlayerViewModel _player;
    private readonly ILocalizationService _loc;
    private List<HistoryItem> _allItems = new();

    public HistoryViewModel(IHistoryService history, PlayerViewModel player, ILocalizationService localization)
    {
        _history = history;
        _player = player;
        _loc = localization;
        _loc.LanguageChanged += (_, _) =>
        {
            RefreshLabels();
            RebuildView();
        };
    }

    public ILocalizationService Loc => _loc;

    public ObservableCollection<HistoryItem> Items { get; } = new();
    public ObservableCollection<HistoryGroup> Groups { get; } = new();

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _searchQuery = string.Empty;

    public async Task LoadAsync()
    {
        _allItems = (await _history.GetUniqueHistoryAsync()).ToList();
        RebuildView();
    }

    partial void OnSearchQueryChanged(string value) => RebuildView();

    private void RebuildView()
    {
        Items.Clear();
        Groups.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchQuery)
            ? _allItems
            : _allItems.Where(i =>
                i.Track.Title.Contains(SearchQuery.Trim(), StringComparison.OrdinalIgnoreCase)
                || i.Track.ArtistName.Contains(SearchQuery.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var item in filtered)
            Items.Add(item);

        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        var weekStart = today.AddDays(-(int)today.DayOfWeek);

        var buckets = new (string TitleKey, Func<HistoryItem, bool> Match)[]
        {
            ("date.today", i => i.PlayedAt.Date == today),
            ("date.yesterday", i => i.PlayedAt.Date == yesterday),
            ("history.groupThisWeek", i => i.PlayedAt.Date < yesterday && i.PlayedAt.Date >= weekStart),
            ("history.groupEarlier", i => i.PlayedAt.Date < weekStart)
        };

        foreach (var (titleKey, match) in buckets)
        {
            var groupItems = filtered.Where(match).ToList();
            if (groupItems.Count == 0) continue;

            var group = new HistoryGroup { Title = _loc.T(titleKey) };
            foreach (var item in groupItems)
                group.Items.Add(item);
            Groups.Add(group);
        }

        IsEmpty = filtered.Count == 0;
    }

    private void RefreshLabels() => OnPropertyChanged(nameof(Loc));

    [RelayCommand]
    private async Task Play(Track? track)
    {
        if (track == null) return;
        var tracks = Items.Select(i => i.Track).ToList();
        await _player.PlayQueueAsync(tracks, track);
    }

    [RelayCommand]
    private async Task Clear()
    {
        var owner = Application.Current.MainWindow as Window;
        if (owner != null && !DarkConfirmDialog.Ask(owner, _loc.T("history.clearConfirm")))
            return;
        await _history.ClearAsync();
        await LoadAsync();
    }
}
