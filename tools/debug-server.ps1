<#
debug-server.ps1 - thin terminal entry for tools\DebugServerLauncher.

All logic lives in the C# project (tools\DebugServerLauncher): it starts the
.stage-win coop dedicated server and DTE-attaches the running Visual Studio's
CoreCLR debugger to the engine child process. This script just builds the
project when its output is missing or stale, then runs it, forwarding all
arguments (see DebugServerLauncher.exe --help).

F5 in Visual Studio does NOT use this script: source\Coop's DebugAutoConnect
profile points straight at the built exe (net472, so the Desktop CLR debug
engine F5 picks attaches to it cleanly). If F5 complains the exe is missing,
run this script once (or: dotnet build tools\DebugServerLauncher -c Release).
#>
param(
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Forwarded
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'DebugServerLauncher'
$exe = Join-Path $project 'bin\Release\net472\DebugServerLauncher.exe'

$sources = Get-ChildItem $project -Recurse -Include *.cs, *.csproj |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$stale = -not (Test-Path $exe)
if (-not $stale) {
    $built = (Get-Item $exe).LastWriteTimeUtc
    $stale = [bool]($sources | Where-Object { $_.LastWriteTimeUtc -gt $built })
}
if ($stale) {
    Write-Host '[debug-server] building tools\DebugServerLauncher...'
    & dotnet build $project -c Release -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "DebugServerLauncher build failed ($LASTEXITCODE)" }
}

& $exe @Forwarded
exit $LASTEXITCODE
