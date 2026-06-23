# Git branches

| Branch | Purpose |
|--------|---------|
| **`mine`** | Default development branch — day-to-day work, features, fixes. |
| **`website`** | GitHub Pages landing page only (`website/` folder). Auto-synced from `mine`. |
| **`main`** | Stable snapshots / release tags (optional merge target before GitHub Release). |

## Website updates

The public site loads the **latest release** from [GitHub Releases](https://github.com/lolka213d/Rezinas-Music/releases) via the GitHub API — when you publish `v1.2.2`, the download button and release notes update automatically.

Workflow:

1. Work on **`mine`**
2. Edit files in `website/` if needed
3. Push to `mine` → action updates **`website`** branch
4. Create a GitHub Release with the installer `.exe` → site shows the new version

## Enable GitHub Pages (once)

1. Repo **Settings → Pages**
2. Source: **GitHub Actions**
3. After first push to `website`, the site URL appears in Actions → Deploy website

## Set default branch to `mine`

```powershell
gh api repos/lolka213d/Rezinas-Music -X PATCH -f default_branch=mine
```

Or: GitHub → Settings → General → Default branch → **mine**.
