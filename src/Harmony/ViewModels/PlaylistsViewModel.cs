using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Models;
using Harmony.Views;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;

namespace Harmony.ViewModels;

/// <summary>Playlists page: create / rename / delete playlists and edit tracks.</summary>
public partial class PlaylistsViewModel : ObservableObject
{
    private readonly IPlaylistService _playlists;
    private readonly PlayerViewModel _player;
    private readonly ILocalizationService _loc;

    public PlaylistsViewModel(IPlaylistService playlists, PlayerViewModel player, ILocalizationService localization)
    {
        _playlists = playlists;
        _player = player;
        _loc = localization;
    }

    public ILocalizationService Loc => _loc;
    public string TracksLabel => _loc.T("common.tracks");

    public ObservableCollection<Playlist> Playlists { get; } = new();
    public ObservableCollection<Track> Tracks { get; } = new();

    [ObservableProperty] private Playlist? _selectedPlaylist;
    [ObservableProperty] private string _newPlaylistName = string.Empty;
    [ObservableProperty] private string _editName = string.Empty;

    public async Task LoadAsync()
    {
        var selectedId = SelectedPlaylist?.Id;
        Playlists.Clear();
        foreach (var p in await _playlists.GetPlaylistsAsync())
            Playlists.Add(p);

        SelectedPlaylist = Playlists.FirstOrDefault(p => p.Id == selectedId) ?? Playlists.FirstOrDefault();
    }

    partial void OnSelectedPlaylistChanged(Playlist? value)
    {
        EditName = value?.Name ?? string.Empty;
        _ = LoadTracksAsync();
    }

    private async Task LoadTracksAsync()
    {
        Tracks.Clear();
        if (SelectedPlaylist == null) return;
        foreach (var t in await _playlists.GetTracksAsync(SelectedPlaylist.Id))
            Tracks.Add(t);
    }

    [RelayCommand]
    private async Task Create()
    {
        if (string.IsNullOrWhiteSpace(NewPlaylistName)) return;
        var created = await _playlists.CreateAsync(NewPlaylistName);
        NewPlaylistName = string.Empty;
        await LoadAsync();
        SelectedPlaylist = Playlists.FirstOrDefault(p => p.Id == created.Id);
    }

    [RelayCommand]
    private async Task Rename()
    {
        if (SelectedPlaylist == null || string.IsNullOrWhiteSpace(EditName)) return;
        await _playlists.RenameAsync(SelectedPlaylist.Id, EditName);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedPlaylist == null) return;
        var owner = Application.Current.MainWindow as Window;
        if (owner != null && !DarkConfirmDialog.Ask(owner,
                string.Format(_loc.T("collections.deletePlaylistConfirm"), SelectedPlaylist.Name)))
            return;
        await _playlists.DeleteAsync(SelectedPlaylist.Id);
        await LoadAsync();
    }

    /// <summary>Adds the track currently playing to the selected playlist.</summary>
    [RelayCommand]
    private async Task AddCurrentTrack()
    {
        if (SelectedPlaylist == null || _player.CurrentTrack == null) return;
        await _playlists.AddTrackAsync(SelectedPlaylist.Id, _player.CurrentTrack);
        await LoadTracksAsync();
    }

    [RelayCommand]
    private async Task RemoveTrack(Track? track)
    {
        if (track == null || SelectedPlaylist == null) return;
        if (track.Id > 0)
            await _playlists.RemoveTrackAsync(SelectedPlaylist.Id, track.Id);
        else
            await _playlists.RemoveTrackBySourceAsync(SelectedPlaylist.Id, track.Source, track.SourceId);
        await LoadTracksAsync();
    }

    [RelayCommand]
    private Task Play(Track track) => _player.PlayQueueAsync(Tracks, track);
}
