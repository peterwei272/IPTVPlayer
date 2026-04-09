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

function Get-ArchivePrefixText([string]$Path, [int]$Count = 32) {
    if (-not (Test-Path $Path)) {
        return ""
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -eq 0) {
        return ""
    }

    $prefixLength = [Math]::Min($Count, $bytes.Length)
    return [System.Text.Encoding]::ASCII.GetString($bytes, 0, $prefixLength)
}

function Test-IsHtmlDocument([string]$Path) {
    $prefix = (Get-ArchivePrefixText -Path $Path -Count 64).TrimStart()
    return $prefix.StartsWith("<!doctype html", [System.StringComparison]::OrdinalIgnoreCase) -or
        $prefix.StartsWith("<html", [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-Is7zArchive([string]$Path) {
    if (-not (Test-Path $Path)) {
        return $false
    }

    $signature = [byte[]](0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C)
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        if ($stream.Length -lt $signature.Length) {
            return $false
        }

        $buffer = New-Object byte[] $signature.Length
        [void]$stream.Read($buffer, 0, $buffer.Length)

        for ($i = 0; $i -lt $signature.Length; $i++) {
            if ($buffer[$i] -ne $signature[$i]) {
                return $false
            }
        }

        return $true
    }
    finally {
        $stream.Dispose()
    }
}

function Use-LegacyArchiveIfPresent {
    if ((-not (Test-Path $archivePath)) -and (Test-Path $legacyArchivePath)) {
        New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
        Copy-Item $legacyArchivePath $archivePath -Force
    }
}

function Assert-ArchiveShape([string]$Path) {
    if (Test-IsHtmlDocument $Path) {
        throw "Downloaded HTML page instead of the mpv archive."
    }

    if (-not (Test-Is7zArchive $Path)) {
        throw "Downloaded file is not a valid 7z archive."
    }
}

function Download-Archive {
    New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
    Invoke-WebRequest -Uri $archiveUrl -UserAgent "Wget" -MaximumRedirection 10 -OutFile $archivePath

    Assert-ArchiveShape $archivePath
}

function Assert-ArchiveHash {
    Assert-ArchiveShape $archivePath
    $actualHash = (Get-FileHash $archivePath -Algorithm SHA256).Hash
    if ($actualHash -ne $archiveHash) {
        throw "mpv archive hash mismatch. Expected: $archiveHash Actual: $actualHash"
    }
}

function Ensure-Archive {
    Use-LegacyArchiveIfPresent

    if ($Force -and (Test-Path $archivePath)) {
        Remove-Item $archivePath -Force
    }

    if (Test-Path $archivePath) {
        try {
            Assert-ArchiveHash
            return
        }
        catch {
            Remove-Item $archivePath -Force -ErrorAction SilentlyContinue
        }
    }

    Download-Archive
    Assert-ArchiveHash
}

if ((Test-Path $targetDll) -and -not $Force) {
    Write-Host "mpv runtime is ready: $targetDll"
    exit 0
}

Assert-TarAvailable
Ensure-Archive

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
