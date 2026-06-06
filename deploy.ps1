$ErrorActionPreference = 'Stop'
$proj = "E:\DEV\PhoenixPoint\PerkOracle\PerkOracle.csproj"
$out  = "E:\DEV\PhoenixPoint\PerkOracle\bin\Release"
$dest = "D:\Steam\steamapps\common\Phoenix Point\Mods\PerkOracle"
dotnet build $proj -c Release
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item "$out\PerkOracle.dll" $dest -Force
if (Test-Path "$out\PerkOracle.pdb") { Copy-Item "$out\PerkOracle.pdb" $dest -Force }
Copy-Item "E:\DEV\PhoenixPoint\PerkOracle\meta.json" $dest -Force
$assetsSrc = Join-Path $PSScriptRoot 'Assets'
if (Test-Path $assetsSrc) { Copy-Item $assetsSrc $dest -Recurse -Force }
Write-Host "Deployed PerkOracle to $dest"
