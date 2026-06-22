using System.Collections.ObjectModel;

using System.ComponentModel;

using System.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using Harmony.Models;

using Harmony.Services;

using Harmony.Services.Interfaces;



namespace Harmony.ViewModels;



/// <summary>Album / now-playing page with inline lyrics.</summary>

public partial class AlbumDetailViewModel : ObservableObject

{

    private readonly DeezerHomeService _deezer;

    private readonly IAlbumService _albums;

    private readonly PlayerViewModel _player;

    private readonly NavigationService _navigation;

    private readonly IFavoritesService _favorites;

    private readonly ILyricsService _lyricsService;

    private readonly ISettingsService _settings;

    private CancellationTokenSource? _lyricsCts;

    private int _lyricsGeneration;

    private IReadOnlyList<LyricLineViewModel> _lyricLineList = Array.Empty<LyricLineViewModel>();

    private double _lyricSyncScale = 1.0;



    public AlbumDetailViewModel(

        DeezerHomeService deezer,

        IAlbumService albums,

        PlayerViewModel player,

        NavigationService navigation,

        IFavoritesService favorites,

        ILyricsService lyricsService,

        ISettingsService settings)

    {

        _deezer = deezer;

        _albums = albums;

        _player = player;

        _navigation = navigation;

        _favorites = favorites;

        _lyricsService = lyricsService;

        _settings = settings;

        _player.PropertyChanged += OnPlayerChanged;

    }



    public PlayerViewModel Player => _player;



    public ObservableCollection<Track> Tracks { get; } = new();

    public ObservableCollection<LyricLineViewModel> LyricLines { get; } = new();



    [ObservableProperty] private string _title = string.Empty;

    [ObservableProperty] private string _artistName = string.Empty;

    [ObservableProperty] private string? _thumbnailUrl;

    [ObservableProperty] private string _metaLine = string.Empty;

    [ObservableProperty] private string _descriptionLine = string.Empty;
    [ObservableProperty] private string? _artistPictureUrl;
    [ObservableProperty] private string _artistBio = string.Empty;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _hasTracks;

    [ObservableProperty] private bool _isUserAlbum;

    [ObservableProperty] private bool _isTrackList;

    [ObservableProperty] private Track? _focusedTrack;

    [ObservableProperty] private bool _isFocusedFavorite;

    [ObservableProperty] private bool _isLyricsLoading;

    [ObservableProperty] private string _lyricsStatus = string.Empty;

    [ObservableProperty] private int _activeLyricIndex = -1;

    /// <summary>Raised when the active lyric line changes so the view can scroll.</summary>
    public event EventHandler<int>? ActiveLyricLineChanged;

    public string LyricsTitle => "Текст песни";

    public bool ShowLyricsEmpty => !IsLyricsLoading && LyricLines.Count == 0 && !string.IsNullOrWhiteSpace(LyricsStatus);



    public string PageKindLabel => IsTrackList ? "NOW PLAYING" : "ALBUM";

    public string? HeroCoverUrl => FocusedTrack?.ThumbnailUrl ?? ThumbnailUrl ?? ArtistPictureUrl;

    public string FocusedTitle => FocusedTrack?.Title ?? "Select a track";

    public string FocusedArtistName => FocusedTrack?.ArtistName ?? ArtistName;

    public string FocusedInfoLine => FocusedTrack == null
        ? string.Empty
        : $"{FocusedTrack.AlbumName ?? "—"} · {FocusedTrack.DurationDisplay}";



    public bool IsAlbumPlaying =>

        _player.IsPlaying && _player.CurrentTrack != null &&

        Tracks.Any(t => t.Matches(_player.CurrentTrack));



    public bool IsFocusedTrackPlaying =>

        FocusedTrack != null && _player.IsPlaying &&

        _player.CurrentTrack?.Matches(FocusedTrack) == true;



    public async Task LoadAsync(AlbumNavigationContext context)

    {

        Title = context.Title;

        ArtistName = context.ArtistName;

        ThumbnailUrl = context.ThumbnailUrl;

        IsUserAlbum = context.UserAlbumId != null;

        IsTrackList = context.PreloadedTracks is { Count: > 0 };

        DescriptionLine = string.Empty;
        ArtistPictureUrl = null;
        ArtistBio = string.Empty;

        FocusedTrack = context.InitialTrack;



        IsLoading = true;

        Tracks.Clear();

        HasTracks = false;



        try

        {

            IReadOnlyList<Track> tracks;

            AlbumInfo? info = null;

            var deezerId = context.DeezerAlbumId;



            if (context.PreloadedTracks is { Count: > 0 } preloaded)

            {

                tracks = preloaded;

                DescriptionLine = $"{Title} · {tracks.Count} tracks";

            }

            else if (context.UserAlbumId is int userId)

            {

                tracks = await _albums.GetTracksAsync(userId);

                DescriptionLine = "Your personal album — songs you imported or added.";

            }

            else

            {

                if (string.IsNullOrWhiteSpace(deezerId))

                    deezerId = await _deezer.FindAlbumIdAsync(context.Title, context.ArtistName);



                if (!string.IsNullOrWhiteSpace(deezerId))

                {

                    info = await _deezer.GetAlbumInfoAsync(deezerId);

                    tracks = await _deezer.GetAlbumTracksAsync(deezerId);



                    if (info != null)

                    {

                        Title = info.Title;

                        ArtistName = info.ArtistName;

                        ThumbnailUrl ??= info.CoverUrl;

                    }

                }

                else if (context.FallbackTrack != null)

                    tracks = new[] { context.FallbackTrack };

                else

                    tracks = Array.Empty<Track>();



                DescriptionLine = BuildDescription(info, tracks);

            }



            foreach (var t in tracks)

                Tracks.Add(t);



            HasTracks = Tracks.Count > 0;

            var totalSec = Tracks.Sum(t => t.DurationSeconds);

            var year = context.Year ?? info?.Year;

            var yearPart = year is int y ? $"{y} · " : "";

            MetaLine = $"{yearPart}{Tracks.Count} songs · {FormatDuration(totalSec)}";



            if (FocusedTrack == null || !Tracks.Any(t => t.Matches(FocusedTrack)))

                SyncFocusedTrack();

            else

            {

                OnPropertyChanged(nameof(HeroCoverUrl));

                OnPropertyChanged(nameof(FocusedTitle));

                OnPropertyChanged(nameof(FocusedArtistName));

                OnPropertyChanged(nameof(FocusedInfoLine));

                OnPropertyChanged(nameof(IsFocusedTrackPlaying));

            }



            await UpdateFocusedFavoriteAsync();
            _ = LoadArtistMetadataAsync();
            _ = LoadLyricsAsync();

        }

        finally

        {

            IsLoading = false;

        }

    }



    private async Task LoadArtistMetadataAsync()
    {
        ArtistPictureUrl = null;
        ArtistBio = string.Empty;
        if (string.IsNullOrWhiteSpace(ArtistName)) return;

        try
        {
            var artistId = await _deezer.FindArtistIdAsync(ArtistName);
            if (string.IsNullOrWhiteSpace(artistId))
            {
                ArtistBio = ArtistName;
                return;
            }

            var data = await _deezer.GetArtistPageAsync(artistId);
            if (data == null)
            {
                ArtistBio = ArtistName;
                return;
            }

            ArtistPictureUrl = data.PictureUrl;
            if (!string.IsNullOrWhiteSpace(data.Bio))
                ArtistBio = data.Bio;
            else if (data.Fans > 0)
                ArtistBio = $"{data.Name} · {data.Fans:N0} поклонников";
            else
                ArtistBio = data.Name;
        }
        catch
        {
            ArtistBio = ArtistName;
        }
    }

    private async Task LoadLyricsAsync()

    {

        var track = FocusedTrack;

        if (track == null)

        {

            LyricLines.Clear();

            LyricsStatus = "Выберите трек.";

            OnPropertyChanged(nameof(ShowLyricsEmpty));

            return;

        }



        _lyricsCts?.Cancel();

        _lyricsCts = new CancellationTokenSource();

        var token = _lyricsCts.Token;

        var generation = Interlocked.Increment(ref _lyricsGeneration);



        IsLyricsLoading = true;

        LyricLines.Clear();

        LyricsStatus = string.Empty;

        ActiveLyricIndex = -1;

        OnPropertyChanged(nameof(ShowLyricsEmpty));



        try

        {

            var duration = track.DurationSeconds > 0 ? track.DurationSeconds : _player.DurationSeconds;

            var data = await _lyricsService.GetLyricsAsync(track.ArtistName, track.Title, duration, token);

            if (token.IsCancellationRequested) return;



            if (data == null || data.Lines.Count == 0)

            {

                LyricsStatus = "Текст не найден.";

                OnPropertyChanged(nameof(ShowLyricsEmpty));

                return;

            }



            foreach (var line in data.Lines)

                LyricLines.Add(new LyricLineViewModel(line.Text, line.StartSeconds));



            _lyricLineList = LyricLines.ToList();

            RecalculateLyricSyncScale();

            UpdateActiveLyricLine(_player.PositionSeconds);

            OnPropertyChanged(nameof(ShowLyricsEmpty));

        }

        finally

        {

            if (generation == Volatile.Read(ref _lyricsGeneration))

                IsLyricsLoading = false;

        }

    }



    private void RecalculateLyricSyncScale()

    {

        if (_lyricLineList.Count == 0)

        {

            _lyricSyncScale = 1.0;

            return;

        }



        var duration = _player.DurationSeconds > 0

            ? _player.DurationSeconds

            : FocusedTrack?.DurationSeconds ?? 0;

        var last = _lyricLineList[^1].StartSeconds;

        _lyricSyncScale = duration > 30 && last > 15 && Math.Abs(last - duration) > 8

            ? duration / last

            : 1.0;

    }



    private void UpdateActiveLyricLine(double positionSeconds)

    {

        if (_lyricLineList.Count == 0) return;



        var adjusted = positionSeconds / _lyricSyncScale + _settings.Current.LyricsOffsetSeconds;

        var idx = -1;

        for (var i = _lyricLineList.Count - 1; i >= 0; i--)

        {

            if (adjusted >= _lyricLineList[i].StartSeconds - 0.08)

            {

                idx = i;

                break;

            }

        }



        if (idx == ActiveLyricIndex) return;



        for (var i = 0; i < _lyricLineList.Count; i++)

        {

            _lyricLineList[i].IsActive = i == idx;

            _lyricLineList[i].IsPast = i < idx;

        }



        ActiveLyricIndex = idx;

        if (idx >= 0)
            ActiveLyricLineChanged?.Invoke(this, idx);

    }



    private async Task UpdateFocusedFavoriteAsync()

    {

        if (FocusedTrack == null)

        {

            IsFocusedFavorite = false;

            return;

        }



        IsFocusedFavorite = await _favorites.IsFavoriteAsync(FocusedTrack.Source, FocusedTrack.SourceId);

    }



    partial void OnFocusedTrackChanged(Track? value)

    {

        OnPropertyChanged(nameof(HeroCoverUrl));

        OnPropertyChanged(nameof(FocusedTitle));

        OnPropertyChanged(nameof(FocusedArtistName));

        OnPropertyChanged(nameof(FocusedInfoLine));

        OnPropertyChanged(nameof(IsFocusedTrackPlaying));

        _ = UpdateFocusedFavoriteAsync();

        _ = LoadLyricsAsync();

    }



    partial void OnArtistNameChanged(string value) => OnPropertyChanged(nameof(FocusedArtistName));

    partial void OnThumbnailUrlChanged(string? value) => OnPropertyChanged(nameof(HeroCoverUrl));
    partial void OnArtistPictureUrlChanged(string? value) => OnPropertyChanged(nameof(HeroCoverUrl));

    partial void OnIsLyricsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowLyricsEmpty));

    partial void OnLyricsStatusChanged(string value) => OnPropertyChanged(nameof(ShowLyricsEmpty));



    private void OnPlayerChanged(object? sender, PropertyChangedEventArgs e)

    {

        if (e.PropertyName is nameof(PlayerViewModel.CurrentTrack) or nameof(PlayerViewModel.IsPlaying))

        {

            SyncFocusedTrack();

            OnPropertyChanged(nameof(IsAlbumPlaying));

            OnPropertyChanged(nameof(IsFocusedTrackPlaying));

            return;

        }



        if (e.PropertyName is nameof(PlayerViewModel.PositionSeconds) or nameof(PlayerViewModel.DurationSeconds))

        {

            if (e.PropertyName == nameof(PlayerViewModel.DurationSeconds))

                RecalculateLyricSyncScale();

            UpdateActiveLyricLine(_player.PositionSeconds);

        }

    }



    private void SyncFocusedTrack()

    {

        if (_player.CurrentTrack != null)

        {

            var match = Tracks.FirstOrDefault(t => t.Matches(_player.CurrentTrack));

            if (match != null)

                FocusedTrack = match;

        }



        FocusedTrack ??= Tracks.FirstOrDefault();

    }



    private static string BuildDescription(AlbumInfo? info, IReadOnlyList<Track> tracks)
    {
        if (info == null)
        {
            if (tracks.Count == 1)
                return $"Сингл · {tracks[0].DurationDisplay}";
            return tracks.Count > 0
                ? $"Альбом · {tracks.Count} треков"
                : "Альбом не найден.";
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(info.RecordType))
            parts.Add(char.ToUpper(info.RecordType[0]) + info.RecordType[1..]);
        if (info.Year is int y)
            parts.Add(y.ToString());
        if (!string.IsNullOrWhiteSpace(info.Label))
            parts.Add(info.Label);
        if (info.Fans > 0)
            parts.Add($"{info.Fans:N0} поклонников");
        return parts.Count > 0 ? string.Join(" · ", parts) : $"{tracks.Count} треков";
    }



    [RelayCommand]

    private Task Play() => Tracks.Count > 0 ? PlayTrack(Tracks[0]) : Task.CompletedTask;



    [RelayCommand]

    private Task Shuffle()

    {

        if (Tracks.Count == 0) return Task.CompletedTask;

        var shuffled = Tracks.OrderBy(_ => Random.Shared.Next()).ToList();

        return _player.PlayQueueAsync(shuffled, shuffled[0]);

    }



    [RelayCommand]

    private Task PlayTrack(Track track)

    {

        FocusedTrack = track;

        return _player.PlayQueueAsync(Tracks, track);

    }



    [RelayCommand]

    private async Task ToggleFocusedFavorite()

    {

        if (FocusedTrack == null) return;

        await _favorites.ToggleAsync(FocusedTrack);

        await UpdateFocusedFavoriteAsync();

    }



    [RelayCommand]

    private void GoBack() => _navigation.GoBack();



    private static string FormatDuration(int totalSeconds)

    {

        if (totalSeconds <= 0) return "0 min";

        var ts = TimeSpan.FromSeconds(totalSeconds);

        return ts.TotalHours >= 1

            ? $"{(int)ts.TotalHours} hr {ts.Minutes} min"

            : $"{Math.Max(1, ts.Minutes)} min";

    }

}


