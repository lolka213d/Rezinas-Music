using Harmony.ViewModels;



namespace Harmony.Services;



/// <summary>Sidebar navigation with back-stack for detail pages.</summary>

public sealed class NavigationService

{

    private readonly Stack<AppPage> _backStack = new();



    public event Action<AppPage>? NavigateRequested;



    public AppPage? CurrentPage { get; private set; }

    public AlbumNavigationContext? PendingAlbum { get; private set; }

    public ArtistNavigationContext? PendingArtist { get; private set; }

    public int? PendingPlaylistId { get; private set; }



    public void Navigate(AppPage page, bool rememberBack = true)

    {

        if (rememberBack && CurrentPage is AppPage cur

            && cur is not (AppPage.AlbumDetail or AppPage.ArtistDetail))

        {

            _backStack.Push(cur);

        }



        CurrentPage = page;

        NavigateRequested?.Invoke(page);

    }



    public void GoBack()

    {

        PendingAlbum = null;

        PendingArtist = null;



        if (_backStack.Count > 0)

        {

            var prev = _backStack.Pop();

            CurrentPage = prev;

            NavigateRequested?.Invoke(prev);

            return;

        }



        CurrentPage = AppPage.Home;

        NavigateRequested?.Invoke(AppPage.Home);

    }



    public void OpenAlbum(AlbumNavigationContext context)

    {

        PendingAlbum = context;

        PendingArtist = null;

        Navigate(AppPage.AlbumDetail);

    }



    public void OpenArtist(ArtistNavigationContext context)

    {

        PendingArtist = context;

        PendingAlbum = null;

        Navigate(AppPage.ArtistDetail);

    }



    public void OpenPlaylist(Models.Playlist playlist)

    {

        PendingPlaylistId = playlist.Id;

        Navigate(AppPage.Playlists);

    }



    public int? ConsumePendingPlaylistId()

    {

        var id = PendingPlaylistId;

        PendingPlaylistId = null;

        return id;

    }



    public void ResetTo(AppPage page)

    {

        _backStack.Clear();

        CurrentPage = page;

    }

}


