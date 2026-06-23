# Rezinas Music

[![Download latest release](https://img.shields.io/github/v/release/lolka213d/Rezinas-Music?label=Download&style=for-the-badge)](https://github.com/lolka213d/Rezinas-Music/releases/latest)

**Support the project** — if you enjoy Rezinas Music, you can buy me a coffee:

[![PayPal](https://img.shields.io/badge/PayPal-Donate-00457C?style=for-the-badge&logo=paypal&logoColor=white)](https://www.paypal.com/ncp/payment/PMVP42DTMTBSL)
[![Buy Me a Coffee](https://img.shields.io/badge/Buy%20Me%20a%20Coffee-Support-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black)](https://buymeacoffee.com/rezinas)
[![Boosty](https://img.shields.io/badge/Boosty-Support-F15F2C?style=for-the-badge)](https://boosty.to/moonsfh)
[![Patreon](https://img.shields.io/badge/Patreon-Support-FF424D?style=for-the-badge&logo=patreon&logoColor=white)](https://www.patreon.com/rezinas)
[![DonateAlerts](https://img.shields.io/badge/DonateAlerts-Support-F6A623?style=for-the-badge)](https://www.donationalerts.com/r/rezinas)

![Rezinas Music — home screen](docs/screenshots/home.png)

Desktop music player for Windows (.NET 8 / WPF): search, playlists, favorites, radio «My Wave», lyrics, and a bottom player bar.

**Repository:** [github.com/lolka213d/Rezinas-Music](https://github.com/lolka213d/Rezinas-Music)

**Branches:** `mine` (development, default) · `website` (GitHub Pages landing) · see [docs/BRANCHES.md](docs/BRANCHES.md)

## Requirements

- Windows 10/11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — only for building from source

## Run from source

```powershell
dotnet run --project src/Harmony/Harmony.csproj
```

## Build standalone `.exe`

```powershell
dotnet publish src/Harmony/Harmony.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o publish/win-x64
```

Output: `publish/win-x64/RezinasMusic.exe` — no separate .NET install needed.

## Build Windows installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) (script can install it via winget).

```powershell
.\installer\build-installer.ps1
```

Output: `publish/RezinasMusic-Setup-<version>.exe` (see [Releases](https://github.com/lolka213d/Rezinas-Music/releases)).

The installer lets you choose install folder and app language (default: English).

## User data (not in the repo)

Playlists, username, favorites, and settings are stored per user on each PC:

`%LOCALAPPDATA%\RezinasMusic\harmony.db`

The installer and published `.exe` do **not** include your personal library.

## Project layout

```
program/
├── Harmony.sln
├── docs/screenshots/     # README images
├── src/Harmony/          # WPF app
├── installer/            # Inno Setup script
└── tests/
```

## License

Copyright © 2026 [lolka213d](https://github.com/lolka213d). All rights reserved.

This is **not** an open-source MIT license. See [LICENSE](LICENSE) for full terms.

**You may:** use, copy, and modify the code for personal learning and experimentation on your own devices.

**You may not:** sell the software, use it commercially, or distribute/redistribute copies (modified or not) to others.

For commercial use or redistribution, contact the author.

## Privacy & data

- **Local first:** playlists, favorites, history, and settings live in `%LOCALAPPDATA%\RezinasMusic\harmony.db` on your PC.
- **API keys:** optional keys (YouTube, Spotify, SoundCloud, Last.fm) are stored locally in your settings — they are never committed to the public repository.
- **Network:** the app calls Deezer (charts/search), LRCLIB/lyrics.ovh (lyrics), and optionally YouTube/Spotify/SoundCloud when you configure keys. Last.fm scrobbling only runs if you enable it.
- **Updates:** optional check against [GitHub Releases](https://github.com/lolka213d/Rezinas-Music/releases) — no telemetry or analytics backend.
- **Discord status:** only sent when you enable it in Settings and Discord is running.
