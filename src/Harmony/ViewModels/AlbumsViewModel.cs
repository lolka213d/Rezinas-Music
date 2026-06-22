using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;
using Microsoft.Win32;

namespace Harmony.ViewModels;

/// <summary>User-created albums + import songs from PC.</summary>
public partial class AlbumsViewModel : ObservableObject
{
    private readonly IAlbumService _albums;
    private readonly ILocalImportService _import;
    private readonly PlayerViewModel _player;
    private readonly NavigationService _navigation;
    private readonly ILocalizationService _loc;

    public AlbumsViewModel(
        IAlbumService albums,
        ILocalImportService import,
        PlayerViewModel player,
        NavigationService navigation,
        ILocalizationService localization)
    {
        _albums = albums;
        _import = import;
        _player = player;
        _navigation = navigation;
        _loc = localization;
    }

    public ILocalizationService Loc => _loc;

    public ObservableCollection<Album> UserAlbums { get; } = new();
    public ObservableCollection<Track> Tracks { get; } = new();

    [ObservableProperty] private string _newAlbumName = string.Empty;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private Album? _selectedAlbum;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isEmpty;

    public async Task LoadAsync()
    {
        UserAlbums.Clear();
        foreach (var a in await _albums.GetUserAlbumsAsync())
            UserAlbums.Add(a);

        IsEmpty = UserAlbums.Count == 0;
        StatusMessage = string.Empty;
    }

    partial void OnSelectedAlbumChanged(Album? value)
    {
        EditName = value?.Name ?? string.Empty;
        _ = LoadTracksAsync();
    }

    private async Task LoadTracksAsync()
    {
        Tracks.Clear();
        if (SelectedAlbum == null) return;

        foreach (var t in await _albums.GetTracksAsync(SelectedAlbum.Id))
            Tracks.Add(t);
    }

    [RelayCommand]
    private async Task Create()
    {
        var album = await _albums.CreateAsync(NewAlbumName);
        NewAlbumName = string.Empty;
        await LoadAsync();
        SelectedAlbum = UserAlbums.FirstOrDefault(a => a.Id == album.Id);
        StatusMessage = $"Album \"{album.Name}\" created.";
    }

    [RelayCommand]
    private async Task Rename()
    {
        if (SelectedAlbum == null) return;
        await _albums.RenameAsync(SelectedAlbum.Id, EditName);
        await LoadAsync();
        SelectedAlbum = UserAlbums.FirstOrDefault(a => a.Id == SelectedAlbum?.Id);
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedAlbum == null) return;
        if (MessageBox.Show($"Delete album «{SelectedAlbum.Name}»?", AppBranding.Name,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        var id = SelectedAlbum.Id;
        await _albums.DeleteAsync(id);
        SelectedAlbum = null;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task AddCurrentTrack()
    {
        if (SelectedAlbum == null || _player.CurrentTrack == null) return;
        await _albums.AddTrackAsync(SelectedAlbum.Id, _player.CurrentTrack);
        await LoadTracksAsync();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RemoveTrack(Track track)
    {
        if (SelectedAlbum == null) return;
        await _albums.RemoveTrackAsync(SelectedAlbum.Id, track.Id);
        await LoadTracksAsync();
        await LoadAsync();
    }

    [RelayCommand]
    private Task Play(Track track) => _player.PlayQueueAsync(Tracks, track);

    [RelayCommand]
    private void OpenAlbum()
    {
        if (SelectedAlbum == null) return;
        _navigation.OpenAlbum(AlbumNavigationContext.FromUserAlbum(SelectedAlbum));
    }

    [RelayCommand]
    private async Task ImportFromPc()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import songs from your PC",
            Filter = "Audio files|*.mp3;*.m4a;*.aac;*.wav;*.flac;*.ogg;*.wma|All files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;

        if (SelectedAlbum == null)
        {
            var auto = await _albums.CreateAsync($"Imported {DateTime.Now:MMM d, yyyy}");
            await LoadAsync();
            SelectedAlbum = UserAlbums.FirstOrDefault(a => a.Id == auto.Id);
            StatusMessage = $"Created album \"{auto.Name}\" and importing…";
        }

        var imported = await _import.ImportFilesAsync(dialog.FileNames, SelectedAlbum?.Id);
        StatusMessage = imported.Count > 0
            ? $"Imported {imported.Count} song(s) into \"{SelectedAlbum?.Name}\"."
            : "No files imported.";

        if (SelectedAlbum != null)
            await LoadTracksAsync();

        await LoadAsync();
    }
}
