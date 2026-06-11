<#
.SYNOPSIS
    LOCAL main-account update path for the Oracle Steam Workshop item.

    This is the recommended way to push updates (per the maintainer's choice):
    run it on the machine logged into the owning Steam account. It builds a clean
    /Dist, then uploads via SteamCMD's workshop_build_item using oracle.vdf.

.DESCRIPTION
    Steps:
      1. Verify SteamCMD is available (PATH or known locations). If not, print an
         install hint and stop.
      2. Verify oracle.vdf has a real publishedfileid (not the placeholder).
         (First publish must be done via PPWorkshopTool GUI - see WORKSHOP.md.)
      3. Run pack-dist.ps1 to (re)build workshop\Dist.
      4. Stamp the changenote into the vdf, then run:
             steamcmd +login <user> +workshop_build_item <abs vdf> +quit

    SECURITY: credentials are NEVER stored or hardcoded here. The username is a
    parameter; SteamCMD prompts for the password and Steam Guard / 2FA code
    interactively in its own console.

.PARAMETER ChangeNote
    Required. The change note recorded in the item's history.

.PARAMETER SteamUser
    Optional Steam account name. If omitted, you'll be prompted, and SteamCMD
    will still prompt for password + 2FA.

.PARAMETER SteamCmd
    Optional explicit path to steamcmd.exe.

.EXAMPLE
    ./workshop/update.ps1 -ChangeNote "Fix tooltip overlap" -SteamUser mysteamname
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ChangeNote,

    [string] $SteamUser,

    [string] $SteamCmd
)

$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$vdf  = Join-Path $here 'oracle.vdf'
$pack = Join-Path $here 'pack-dist.ps1'

# --- 1. Locate SteamCMD ---------------------------------------------------
function Resolve-SteamCmd {
    param([string] $Explicit)
    if ($Explicit) {
        if (Test-Path $Explicit) { return (Resolve-Path $Explicit).Path }
        throw "steamcmd not found at -SteamCmd '$Explicit'."
    }
    $cmd = Get-Command steamcmd, steamcmd.exe -ErrorAction SilentlyContinue |
           Select-Object -First 1
    if ($cmd) { return $cmd.Source }
    $candidates = @(
        'C:\steamcmd\steamcmd.exe',
        "$env:ProgramFiles\steamcmd\steamcmd.exe",
        "${env:ProgramFiles(x86)}\Steam\steamcmd.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    return $null
}

$steam = Resolve-SteamCmd -Explicit $SteamCmd
if (-not $steam) {
    Write-Error @"
SteamCMD not found.

Install one of:
  - Standalone: download https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip
    extract to C:\steamcmd, then re-run (or pass -SteamCmd C:\steamcmd\steamcmd.exe).
  - Via the Steam client: it ships steamcmd-compatible tooling; or add steamcmd.exe to PATH.
"@
    exit 1
}
Write-Host "SteamCMD: $steam" -ForegroundColor Cyan

# --- 2. Verify publishedfileid is set -------------------------------------
if (-not (Test-Path $vdf)) { throw "VDF not found: $vdf" }
$vdfText = Get-Content -Raw $vdf
if ($vdfText -match 'PUBLISHEDFILEID_PLACEHOLDER') {
    Write-Error @"
oracle.vdf still has the placeholder publishedfileid.

You must FIRST publish the item once via the PPWorkshopTool GUI (see WORKSHOP.md),
then put the real published file id into oracle.vdf:

    "publishedfileid" "1234567890"

Then re-run this script.
"@
    exit 1
}

# --- 3. Build clean /Dist -------------------------------------------------
Write-Host "Packing Dist..." -ForegroundColor Cyan
& $pack
if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
    throw "pack-dist.ps1 failed (exit $LASTEXITCODE)."
}

# --- 4. Stamp changenote into the vdf -------------------------------------
# Escape backslashes and quotes for VDF, then replace the changenote value.
$escaped = $ChangeNote -replace '\\', '\\\\' -replace '"', '\"'
$vdfText = [regex]::Replace(
    $vdfText,
    '("changenote"\s*")[^"]*(")',
    { param($m) $m.Groups[1].Value + $escaped + $m.Groups[2].Value }
)
Set-Content -Path $vdf -Value $vdfText -Encoding UTF8 -NoNewline

# --- 5. Upload via SteamCMD ----------------------------------------------
if (-not $SteamUser) {
    $SteamUser = Read-Host "Steam account name"
}
Write-Host "Uploading to Steam Workshop as '$SteamUser'..." -ForegroundColor Cyan
Write-Host "(SteamCMD will prompt for password + Steam Guard/2FA in its console.)"

& $steam +login $SteamUser +workshop_build_item $vdf +quit
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Error "SteamCMD exited with code $code. Check its output above."
    exit $code
}
Write-Host "`nDone. Verify the item on the Workshop page." -ForegroundColor Green
