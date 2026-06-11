$ErrorActionPreference = 'Stop'
$proj = "E:\DEV\PhoenixPoint\PerkOracle\Oracle.csproj"
$out  = "E:\DEV\PhoenixPoint\PerkOracle\bin\Release"
$dest = "D:\Steam\steamapps\common\Phoenix Point\Mods\Oracle"
dotnet build $proj -c Release
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item "$out\Oracle.dll" $dest -Force
if (Test-Path "$out\Oracle.pdb") { Copy-Item "$out\Oracle.pdb" $dest -Force }
Copy-Item "E:\DEV\PhoenixPoint\PerkOracle\meta.json" $dest -Force
$assetsSrc = Join-Path $PSScriptRoot 'Assets'
if (Test-Path $assetsSrc) { Copy-Item $assetsSrc $dest -Recurse -Force }
Write-Host "Deployed Oracle to $dest"
