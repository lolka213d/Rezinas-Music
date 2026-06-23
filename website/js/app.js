const REPO = 'lolka213d/Rezinas-Music';
const API = `https://api.github.com/repos/${REPO}/releases/latest`;

function normalizeVersion(tag) {
  return (tag || '').trim().replace(/^v/i, '');
}

function pickInstaller(assets) {
  if (!assets?.length) return null;
  return (
    assets.find(a => /setup.*\.exe$/i.test(a.name)) ||
    assets.find(a => /\.exe$/i.test(a.name)) ||
    null
  );
}

function trimNotes(body, max = 1200) {
  if (!body) return 'No release notes.';
  const text = body.trim();
  return text.length > max ? `${text.slice(0, max)}…` : text;
}

async function loadLatestRelease() {
  const statusEl = document.getElementById('update-status');
  const badgeEl = document.getElementById('release-badge');
  const notesEl = document.getElementById('release-notes');
  const heroBtn = document.getElementById('download-btn');
  const heroLabel = document.getElementById('download-label');
  const heroVersion = document.getElementById('download-version');
  const panelBtn = document.getElementById('release-download');

  try {
    const res = await fetch(API, {
      headers: { Accept: 'application/vnd.github+json' }
    });
    if (!res.ok) throw new Error(`GitHub API ${res.status}`);

    const release = await res.json();
    const version = normalizeVersion(release.tag_name);
    const asset = pickInstaller(release.assets);
    const url = asset?.browser_download_url || release.html_url;

    badgeEl.textContent = `v${version}`;
    notesEl.textContent = trimNotes(release.body);
    heroLabel.textContent = asset ? 'Download for Windows' : 'View release';
    heroVersion.textContent = `v${version}`;
    heroBtn.href = url;

    panelBtn.href = url;
    panelBtn.textContent = asset
      ? `Download ${asset.name}`
      : 'Open on GitHub';
    panelBtn.hidden = false;

    statusEl.textContent = `Latest public release: v${version} — synced from GitHub Releases.`;
    statusEl.classList.add('status--ok');
  } catch (err) {
    statusEl.textContent = 'Could not load release info. Open GitHub Releases manually.';
    statusEl.classList.add('status--err');
    notesEl.textContent = 'Visit https://github.com/lolka213d/Rezinas-Music/releases to download.';
    heroBtn.href = 'https://github.com/lolka213d/Rezinas-Music/releases/latest';
    console.error(err);
  }
}

loadLatestRelease();
