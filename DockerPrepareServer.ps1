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

# JIT inlining guard: CoreCLR's tiered JIT inlines tiny property setters into their callers
# OVER Harmony detours, so AutoSync property syncs whose setter has few call sites silently
# never fire on the container (net472 hosts are unaffected; found via MapEvent.Position,
# 2026-07-08). Stamp the staged copies of the game assemblies with the NoInlining method-impl
# flag on every property setter so the JIT always calls through the detourable method entry.
# Only the image's copies are modified - the installed game is never touched. Runs before the
# Coop module is staged so our own assemblies keep normal optimization.
$cecil = Get-ChildItem "$env:USERPROFILE\.nuget\packages\mono.cecil" -Recurse -Filter Mono.Cecil.dll |
    Where-Object { $_.FullName -match '\\net40\\' } | Select-Object -First 1
if ($null -eq $cecil) {
    Write-Warning "Mono.Cecil not found in the NuGet cache - skipping the setter NoInlining stamp (property syncs may not fire on CoreCLR)."
} else {
    Add-Type -Path $cecil.FullName
    $noInline = [Mono.Cecil.MethodImplAttributes]::NoInlining
    $stampedMethods = 0
    $stampedFiles = 0
    foreach ($dll in Get-ChildItem "$TempDir\mb2" -Recurse -Filter *.dll) {
        # Cecil resolves cross-assembly references while writing; a fresh resolver per file
        # (disposed right after) keeps its cached read handles from blocking later writes to
        # the very assemblies it resolved.
        $resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
        $resolver.AddSearchDirectory("$TempDir\mb2\bin\Win64_Shipping_Client")
        foreach ($module in $GameModules) {
            if (Test-Path "$TempDir\mb2\Modules\$module\bin\Win64_Shipping_Client") {
                $resolver.AddSearchDirectory("$TempDir\mb2\Modules\$module\bin\Win64_Shipping_Client")
            }
        }
        $asm = $null
        try {
            $params = New-Object Mono.Cecil.ReaderParameters
            $params.InMemory = $true
            $params.AssemblyResolver = $resolver
            $asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll.FullName, $params)
            $count = 0
            foreach ($type in $asm.MainModule.GetTypes()) {
                foreach ($method in $type.Methods) {
                    if ($method.IsSetter -and -not $method.IsAbstract) {
                        # MethodImplAttributes is a ushort enum; PS -bor yields Int32, so cast explicitly.
                        $method.ImplAttributes = [Mono.Cecil.MethodImplAttributes]([uint16]$method.ImplAttributes -bor [uint16]$noInline)
                        $count++
                    }
                }
            }
            if ($count -gt 0) {
                $asm.Write($dll.FullName)
                $stampedMethods += $count
                $stampedFiles++
            }
        } catch {
            Write-Warning "NoInlining stamp skipped for $($dll.Name): $_"
        } finally {
            if ($null -ne $asm) { $asm.Dispose() }
            $resolver.Dispose()
        }
    }
    Write-Host "Stamped NoInlining on $stampedMethods property setters across $stampedFiles game assemblies"
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
