param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "build\publish\win-x64"
$objDir = Join-Path $repoRoot "temp\installer-obj\"
$binDir = Join-Path $repoRoot "temp\installer-bin\"
$projectPath = Join-Path $repoRoot "IPTVPlayer.App\IPTVPlayer.App.csproj"
$issPath = Join-Path $PSScriptRoot "IPTVPlayer.iss"
$ensureMpvScript = Join-Path $repoRoot "scripts\ensure-mpv.ps1"
$mpvDllPath = Join-Path $repoRoot "mpv\mpv-2.dll"

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

function Find-IsccPath {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

if (-not (Test-Path $mpvDllPath)) {
    if (-not (Test-Path $ensureMpvScript)) {
        throw "mpv runtime is missing and scripts\\ensure-mpv.ps1 was not found."
    }

    & $ensureMpvScript
}

if (-not (Test-Path $mpvDllPath)) {
    throw "mpv runtime is missing: $mpvDllPath"
}

dotnet publish $projectPath `
    -c $Configuration `
    -o $publishDir `
    /p:InstallPublish=true `
    /p:BaseIntermediateOutputPath=$objDir `
    /p:BaseOutputPath=$binDir

$isccPath = Find-IsccPath
if (-not $isccPath) {
    Write-Host "Publish completed, but Inno Setup (ISCC.exe) was not found on this machine."
    Write-Host "Install Inno Setup 6, then run the following command to generate Setup.exe:"
    Write-Host "  ISCC.exe `"$issPath`""
    exit 0
}

& $isccPath $issPath
