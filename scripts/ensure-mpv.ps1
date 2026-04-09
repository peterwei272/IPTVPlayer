param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$mpvDir = Join-Path $repoRoot "mpv"
$targetDll = Join-Path $mpvDir "mpv-2.dll"
$downloadDir = Join-Path $repoRoot "temp\downloads"
$extractDir = Join-Path $repoRoot "temp\mpv-extract"
$archiveName = "mpv-dev-x86_64-20260301-git-05fac7f.7z"
$archiveUrl = "https://sourceforge.net/projects/mpv-player-windows/files/libmpv/$archiveName/download"
$archiveHash = "DC991B2077F9B899FC022B92B3CAEDF1A70916C06560B6216F1AB09B5C808258"
$archivePath = Join-Path $downloadDir $archiveName
$legacyArchivePath = Join-Path $repoRoot "build\downloads\$archiveName"

function Assert-TarAvailable {
    $tar = Get-Command tar.exe -ErrorAction SilentlyContinue
    if (-not $tar) {
        throw "tar.exe was not found. Enable tar on Windows or place mpv\\mpv-2.dll manually."
    }
}

function Use-LegacyArchiveIfPresent {
    if ((-not (Test-Path $archivePath)) -and (Test-Path $legacyArchivePath)) {
        New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
        Copy-Item $legacyArchivePath $archivePath -Force
    }
}

function Download-Archive {
    New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
    Invoke-WebRequest -Uri $archiveUrl -OutFile $archivePath
}

function Assert-ArchiveHash {
    $actualHash = (Get-FileHash $archivePath -Algorithm SHA256).Hash
    if ($actualHash -ne $archiveHash) {
        throw "mpv archive hash mismatch. Expected: $archiveHash Actual: $actualHash"
    }
}

if ((Test-Path $targetDll) -and -not $Force) {
    Write-Host "mpv runtime is ready: $targetDll"
    exit 0
}

Assert-TarAvailable
Use-LegacyArchiveIfPresent

if ($Force -or -not (Test-Path $archivePath)) {
    Download-Archive
}

Assert-ArchiveHash

if (Test-Path $extractDir) {
    Remove-Item $extractDir -Recurse -Force
}

New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
New-Item -ItemType Directory -Path $mpvDir -Force | Out-Null

tar -xf $archivePath -C $extractDir

$sourceDll = Join-Path $extractDir "libmpv-2.dll"
if (-not (Test-Path $sourceDll)) {
    throw "libmpv-2.dll was not found in the extracted archive."
}

Copy-Item $sourceDll $targetDll -Force

Write-Host "Prepared mpv runtime: $targetDll"
