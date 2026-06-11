<#
.SYNOPSIS
    Builds Oracle in Release and assembles a clean Steam Workshop content
    folder at workshop/Dist/ containing exactly:
        Oracle.dll + meta.json + Assets/
    (no .pdb, no extra files). Idempotent: cleans Dist/ then re-copies.

.NOTES
    Mirrors deploy.ps1's build/copy logic, but targets workshop/Dist/ instead of
    the live game Mods/ folder. The generated Dist/ is gitignored.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param()

$ErrorActionPreference = 'Stop'

# --- Paths (anchored to the repo root, one level above this script) ---
$repoRoot = Split-Path -Parent $PSScriptRoot          # ...\Oracle
$proj     = Join-Path $repoRoot 'Oracle.csproj'
$relOut   = Join-Path $repoRoot 'bin\Release'
$meta     = Join-Path $repoRoot 'meta.json'
$assets   = Join-Path $repoRoot 'Assets'
$dist     = Join-Path $PSScriptRoot 'Dist'            # workshop\Dist

Write-Host "Building Oracle (Release)..." -ForegroundColor Cyan
if ($PSCmdlet.ShouldProcess($proj, 'dotnet build -c Release')) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }
}

$dll = Join-Path $relOut 'Oracle.dll'

# --- Clean Dist/ ---
if (Test-Path $dist) {
    if ($PSCmdlet.ShouldProcess($dist, 'Remove existing Dist')) {
        Remove-Item $dist -Recurse -Force
    }
}
if ($PSCmdlet.ShouldProcess($dist, 'Create Dist')) {
    New-Item -ItemType Directory -Force -Path $dist | Out-Null
}

# --- Copy exactly: DLL + meta.json + Assets\ ---
if (-not (Test-Path $dll))  { throw "Built DLL not found: $dll" }
if (-not (Test-Path $meta)) { throw "meta.json not found: $meta" }

if ($PSCmdlet.ShouldProcess($dll, "Copy to Dist")) {
    Copy-Item $dll  $dist -Force
}
if ($PSCmdlet.ShouldProcess($meta, "Copy to Dist")) {
    Copy-Item $meta $dist -Force
}
if (Test-Path $assets) {
    if ($PSCmdlet.ShouldProcess($assets, "Copy Assets\ to Dist")) {
        Copy-Item $assets $dist -Recurse -Force
    }
} else {
    Write-Warning "Assets folder not found at $assets - skipping."
}

Write-Host "`nDist assembled at: $dist" -ForegroundColor Green
if (Test-Path $dist) {
    Get-ChildItem -Path $dist -Recurse |
        ForEach-Object { $_.FullName.Substring($dist.Length).TrimStart('\') } |
        Sort-Object |
        ForEach-Object { Write-Host "  $_" }
}
