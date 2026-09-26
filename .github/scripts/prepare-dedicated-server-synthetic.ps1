[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('Prepare','Restore')][string]$Action,
    [Parameter(Mandatory=$true)][string]$InputsRoot,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]{1,64}$')][string]$RunToken,
    [string]$StageRoot,
    [string]$DotNetRoot,
    [string]$ExpectedCoopHead,
    [string]$ExpectedCoopTree,
    [string]$ExpectedServerHead,
    [string]$ExpectedServerTree
)
$ErrorActionPreference = 'Stop'
$stateDirectory = Join-Path $InputsRoot 'synthetic-password-state'
$statePath = Join-Path $stateDirectory 'owner.json'
$originalPath = Join-Path $stateDirectory 'original-config'
$installedPath = Join-Path $stateDirectory 'installed-config'
function Initialize-SyntheticPassword {
    param([Parameter(Mandatory=$true)][string]$ConfigPath)
    $original = [IO.File]::ReadAllBytes($configPath)
    $configText = [IO.File]::ReadAllText($configPath)
    $passwordPattern = '"password"\s*:\s*"(?:[^"\\]|\\.)*"'
    $matches = [regex]::Matches($configText, $passwordPattern)
    if ($matches.Count -ne 1) { throw 'Expected exactly one password field in the prepared server config.' }
    $password = [Guid]::NewGuid().ToString('N')
    $updated = [regex]::Replace($configText, $passwordPattern, ('"password": "' + $password + '"'))
    New-Item -Path $stateDirectory -ItemType Directory | Out-Null
    # Restrict the backup and ephemeral test config to this runner account and SYSTEM.
    $acl = Get-Acl -LiteralPath $stateDirectory
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            $sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    }
    Set-Acl -LiteralPath $stateDirectory -AclObject $acl
    [IO.File]::WriteAllBytes($originalPath, $original)
    [IO.File]::WriteAllText($installedPath, $updated, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($statePath, ([ordered]@{runToken=$RunToken;configPath=$configPath} | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllBytes($configPath, [IO.File]::ReadAllBytes($installedPath))
    $password = $null
    $updated = $null
}
if ($Action -ceq 'Restore') {
    if (-not (Test-Path -LiteralPath $stateDirectory)) { return }
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ([string]$state.runToken -cne $RunToken) { throw 'Synthetic password state belongs to another run.' }
    $configPath = [string]$state.configPath
    $currentHash = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash
    $originalHash = (Get-FileHash -LiteralPath $originalPath -Algorithm SHA256).Hash
    $installedHash = (Get-FileHash -LiteralPath $installedPath -Algorithm SHA256).Hash
    if ($currentHash -cne $originalHash -and $currentHash -cne $installedHash) {
        throw 'Synthetic server config changed outside the owned setup; backup retained.'
    }
    [IO.File]::WriteAllBytes($configPath, [IO.File]::ReadAllBytes($originalPath))
    Remove-Item -LiteralPath $stateDirectory -Recurse -Force
    return
}
if (Test-Path -LiteralPath $stateDirectory) { throw 'Unrestored synthetic password state exists.' }
$stampPath = Join-Path $InputsRoot 'live-server-stamp.json'
$stampHash = (Get-FileHash -LiteralPath $stampPath -Algorithm SHA256).Hash.ToLowerInvariant()
$dotnet = Join-Path $DotNetRoot 'dotnet.exe'
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$project = Join-Path $sourceRoot 'source\VerificationHarness\VerificationHarness.csproj'
& $dotnet build $project -c Release -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Synthetic manifest producer build failed.' }
$harness = Join-Path $sourceRoot 'source\VerificationHarness\bin\Release\net10.0\VerificationHarness.dll'
& $dotnet $harness dedicated-server-synthetic-manifest `
    --build-stamp $stampPath --build-stamp-sha256 $stampHash --artifact-root $StageRoot `
    --head $ExpectedCoopHead --tree $ExpectedCoopTree `
    --server-head $ExpectedServerHead --server-tree $ExpectedServerTree `
    --output (Join-Path $StageRoot 'dedicated-server-synthetic-artifacts.json')
if ($LASTEXITCODE -ne 0) { throw 'Synthetic build manifest creation failed.' }
Initialize-SyntheticPassword -ConfigPath (Join-Path $StageRoot 'server-data\server-config.json')
