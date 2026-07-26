[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $InputPath,

    [Parameter(Mandatory = $true, Position = 1)]
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "CoopSessionWorkshopMigrator.csproj"
& dotnet run --project $projectPath -- $InputPath $OutputPath
exit $LASTEXITCODE
