# Changelog

## 1.2.7

- Playback: stricter duration match (±5–8s), artist + album in YouTube search; rejects wrong versions (e.g. 2:20 instead of 2:38)
- Reverted library hub cards — Library / Playlists / Favorites / History open as separate pages again

## 1.2.6

- Playback: require artist match when resolving YouTube streams (title + artist + channel); pick best candidate instead of first partial title match
- Search: fixed filter chips — active tab uses purple accent with readable text instead of white circle
- Playlists: dark separator in ⋯ menu (no bright white line)
- Library: redesigned hub with gradient hero, section cards, and polished songs/history layout

## 1.2.5

- Playback: stricter YouTube match (title/artist/duration), Deezer metadata refresh and preview fallback — fixes wrong song audio
- History: crash fix when playing from history; no stray play arrows in list
- Playlists: dark «Add to playlist» dialog; ⋯ menu works; «Find songs» panel no longer opens on its own
- Confirm dialogs: dark themed for delete/clear actions (albums, playlists, favorites, create playlist)

## 1.2.4

- Lyrics: Spotify-style full-screen view — centered text, auto-scroll, top/bottom fade, ambient colors from album art
- Playback: fixed wrong track starting when clicking a song in lists or album view
- Library & Favorites: track row layout — duration and actions no longer overlap
- Playlists: instant empty playlist create, rename, three-dots menu; play button always visible
- Album view: fixed Back button overlapping Lyrics panel
- What's new & update dialogs: dark glass UI instead of white system windows

## 1.2.3

- Home: removed Quick access grid; featured and continue-listening no longer show two single-track rows at once
- Playlist track rows: fixed duration overlapping action buttons on hover
- Create playlist dialog: cleaner layout, search placeholder, selected track count
- Favorites: «Unlike all» button to clear every liked song
- Profile: fixed localization binding crash on open

## 1.2.2

- Full UI localization pass — lyrics, history, favorites, profile, album/artist views, player bar, and dialogs
- Track row fix: play/add-to-playlist now uses the correct track in lists
- Playlist tracks default to custom order instead of sorting by title
- Unsynced lyrics timing improved (shared sync helper, better duration scaling)
- Discord Rich Presence status text follows app language
- Landing page at GitHub Pages with app screenshots and auto-updated download

## 1.2.1

- Discord Rich Presence with app logo (`rezinas_logo`) and track artwork
- Now Playing screen: fixed mixed artist/album data and queue playback order
- Compact Now Playing layout; correct track numbering in lists
- Spotify playlist sync fix (`item` field in API responses)
- Playlist cover mosaics in browse view
- Russian radio station uses regional TikTok/RU hits playlist
- Library delete crash fix; scroll and playback fixes in library views

## 1.1.0

- Localized home recommendation mixes (Chill, Focus, Night)
- Daily mix and My Wave refresh once per calendar day
- Personal «For you» radio station from history + chart picks
- Export/import playlists (JSON) and full backup (ZIP)
- Auto-start with Windows, Discord status, mini player window
- Keyboard shortcuts overlay (Shift+/)
- Listening stats (week / month / year)
- Smart playlists on Home → For you
- Queue drag-and-drop reorder
- Gapless-style crossfade option
- Offline stream cache with size limit
- Sync folder snapshot export
- Podcast search tab
- GitHub release installer download for updates

## 1.0.0

- Initial public release
