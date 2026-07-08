# Stages everything the headless co-op server image needs into .\DockerServerTemp:
#   app\  - the ServerHeadless net6.0 build output (the Linux-capable target)
#   mb2\  - the minimal game subset: managed engine DLLs, the four base modules' metadata +
#           module data + managed DLLs, and the deployed Coop module
#
# Prerequisites (both on Windows, BEFORE running this):
#   1. Build the Coop solution (deploys the Coop module into .\mb2\Modules\Coop).
#   2. Build source\ServerHeadless (produces the net6.0 output).
#
# Then: docker build -f Dockerfile.server -t bannerlordcoop-server .
param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$TempDir = ".\DockerServerTemp"
$ServerBin = ".\source\ServerHeadless\bin\x64\$Configuration\net6.0"
$MBBinDir = ".\mb2\bin\Win64_Shipping_Client"
$MBModulesDir = ".\mb2\Modules"

# The game modules the headless server activates (must match HeadlessBootstrap.RequiredModules).
$GameModules = @("Native", "SandBoxCore", "SandBox", "StoryMode")

if (-not (Test-Path "$ServerBin\ServerHeadless.dll")) {
    throw "ServerHeadless net6.0 output not found at $ServerBin - build source\ServerHeadless first."
}
if (-not (Test-Path "$MBModulesDir\Coop\bin\Win64_Shipping_Client\GameInterface.dll")) {
    throw "Deployed Coop module not found - build the Coop solution first."
}

# Recreate the temp folder
Remove-Item $TempDir -Recurse -ErrorAction Ignore
New-Item -Force -ItemType Directory -Path $TempDir | Out-Null

# Server app (self-contained w.r.t. NuGet deps; game DLLs resolve from mb2 at runtime)
Copy-Item $ServerBin -Destination "$TempDir\app" -Recurse

# Managed engine assemblies (the headless bootstrap patches away all native calls, so
# TaleWorlds.Native.dll is only ever used as a directory marker, never loaded)
New-Item -Force -ItemType Directory -Path "$TempDir\mb2\bin\Win64_Shipping_Client" | Out-Null
Copy-Item "$MBBinDir\TaleWorlds*.dll" -Destination "$TempDir\mb2\bin\Win64_Shipping_Client"

# XML schemas: MBObjectManager validates merged module XML against <game root>\XmlSchemas\*.xsd
Copy-Item ".\mb2\XmlSchemas" -Destination "$TempDir\mb2\XmlSchemas" -Recurse

# Base game modules: module metadata + game data XMLs + managed DLLs
foreach ($module in $GameModules) {
    $src = "$MBModulesDir\$module"
    $dst = "$TempDir\mb2\Modules\$module"
    New-Item -Force -ItemType Directory -Path $dst | Out-Null

    Copy-Item "$src\SubModule.xml" -Destination $dst
    if (Test-Path "$src\ModuleData") {
        Copy-Item "$src\ModuleData" -Destination "$dst\ModuleData" -Recurse
    }
    if (Test-Path "$src\bin\Win64_Shipping_Client") {
        New-Item -Force -ItemType Directory -Path "$dst\bin\Win64_Shipping_Client" | Out-Null
        Copy-Item "$src\bin\Win64_Shipping_Client\*.dll" -Destination "$dst\bin\Win64_Shipping_Client"
    }
}

# The deployed Coop module, as-is
Copy-Item "$MBModulesDir\Coop" -Destination "$TempDir\mb2\Modules\Coop" -Recurse

# Optional per-map operator artifacts, baked next to the app so containers ship ready-to-host:
# the blank day-0 campaign ("new game" loads it) and the exported nav grid (real terrain).
$docs = Join-Path ([Environment]::GetFolderPath('MyDocuments')) "Mount and Blade II Bannerlord"
$defaultSave = Join-Path $docs "Game Saves\default_new_game.sav"
if (Test-Path $defaultSave) {
    Copy-Item $defaultSave "$TempDir\app\"
    Write-Host "Staged default_new_game.sav"
} else {
    Write-Warning "No default_new_game.sav in '$docs\Game Saves' - containers will need one on /data to start fresh campaigns."
}
$navGrids = Join-Path $docs "CoopMapData"
if (Test-Path $navGrids) {
    New-Item -Force -ItemType Directory -Path "$TempDir\app\CoopMapData" | Out-Null
    Copy-Item "$navGrids\*.navgrid" "$TempDir\app\CoopMapData\" -ErrorAction SilentlyContinue
    Write-Host "Staged nav grids: $((Get-ChildItem "$TempDir\app\CoopMapData" -ErrorAction SilentlyContinue).Count)"
} else {
    Write-Warning "No CoopMapData exports in '$docs' - containers will path terrain-blind without a nav grid."
}

$size = [math]::Round((Get-ChildItem $TempDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "Staged $TempDir ($size MB)"
