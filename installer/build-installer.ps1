$ErrorActionPreference = "Stop"

$InstallerDir = $PSScriptRoot
$Root = Split-Path -Parent $InstallerDir
$Project = Join-Path $Root "src\Harmony\Harmony.csproj"
$PublishDir = Join-Path $Root "publish\win-x64"
$IssFile = Join-Path $InstallerDir "RezinasMusic.iss"

Write-Host "Publishing Rezinas Music (win-x64, self-contained)..." -ForegroundColor Cyan
dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $PublishDir

if (-not (Test-Path (Join-Path $PublishDir "RezinasMusic.exe"))) {
    throw "Publish failed: RezinasMusic.exe not found."
}

function Find-InnoCompiler {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }
    return $null
}

$Iscc = Find-InnoCompiler
if (-not $Iscc) {
    Write-Host "Inno Setup 6 not found. Installing via winget..." -ForegroundColor Yellow
    winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements
    $Iscc = Find-InnoCompiler
}

if (-not $Iscc) {
    throw @"
Inno Setup compiler (ISCC.exe) was not found.
Install Inno Setup 6 from https://jrsoftware.org/isinfo.php
or run: winget install JRSoftware.InnoSetup
Then run this script again.
"@
}

Write-Host "Building installer with: $Iscc" -ForegroundColor Cyan
& $Iscc $IssFile

$setup = Get-ChildItem (Join-Path $Root "publish") -Filter "RezinasMusic-Setup-*.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($setup) {
    Write-Host ""
    Write-Host "Installer ready:" -ForegroundColor Green
    Write-Host $setup.FullName
} else {
    throw "Installer build finished but setup exe was not found in publish folder."
}
