# Spotify OAuth (for developers only)

Copy this file to `SpotifyAppCredentials.cs` in the same folder:

```csharp
namespace Harmony.Config;

internal static class SpotifyAppCredentials
{
    internal const string ClientId = "YOUR_CLIENT_ID";
    internal const string ClientSecret = "YOUR_CLIENT_SECRET";
}
```

Users click **«Войти через Spotify»** in Settings — they never enter these keys.

Register redirect URI in Spotify Developer Dashboard:
`http://127.0.0.1:4567/callback`

`SpotifyAppCredentials.cs` is gitignored — do not commit real secrets to GitHub.
