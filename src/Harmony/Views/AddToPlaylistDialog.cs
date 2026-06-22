using System.Windows;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Views;

/// <summary>Pick a playlist and add a track.</summary>
public static class AddToPlaylistDialog
{
    public static async Task<bool> ShowAsync(IPlaylistService playlists, Track track, Window owner)
    {
        if (track == null) return false;

        var list = (await playlists.GetPlaylistsAsync()).ToList();
        if (list.Count == 0)
        {
            DarkAlertDialog.Show(owner, "Create a playlist first (Playlists page).");
            return false;
        }

        var names = list.Select(p => p.Name).ToArray();
        var pick = PlaylistPickerWindow.Show(owner, names, track.Title ?? "track");
        if (pick < 0) return false;

        await playlists.AddTrackAsync(list[pick].Id, track);
        DarkAlertDialog.Show(owner, $"Added to «{list[pick].Name}».");
        return true;
    }
}
