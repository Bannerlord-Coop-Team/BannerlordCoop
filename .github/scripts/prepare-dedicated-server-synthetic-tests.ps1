[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'prepare-dedicated-server-synthetic.ps1'
$tokens=$null; $errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($scriptPath,[ref]$tokens,[ref]$errors)
if ($errors.Count) { throw 'Synthetic preparation script does not parse.' }
$function=$ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Initialize-SyntheticPassword'},$true)
. ([ScriptBlock]::Create($function.Extent.Text))
$testRoot=Join-Path $env:TEMP ('synthetic-config-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -Path $testRoot -ItemType Directory | Out-Null
try {
    $InputsRoot=$testRoot
    $RunToken='synthetic-config-test'
    $stateDirectory=Join-Path $InputsRoot 'synthetic-password-state'
    $statePath=Join-Path $stateDirectory 'owner.json'
    $originalPath=Join-Path $stateDirectory 'original-config'
    $installedPath=Join-Path $stateDirectory 'installed-config'
    $configPath=Join-Path $testRoot 'server-config.json'
    $original=[Text.Encoding]::UTF8.GetBytes('{"password":"original-test-value","port":4200}')
    $seedConfig=Join-Path $testRoot 'server-config.default.json'
    $seed=[Text.Encoding]::UTF8.GetBytes('{"password":"","port":4200,"traceTick":false,"tracePublish":false,"traceBandits":false}')
    [IO.File]::WriteAllBytes($seedConfig,$seed)
    [IO.File]::WriteAllBytes($configPath,$original)
    Initialize-SyntheticPassword -ConfigPath $configPath -SeedConfigPath $seedConfig
    $configured=Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    if ($configured.password -notmatch '^[a-f0-9]{32}$' -or $configured.port -ne 4200) { throw 'Owned test password was not configured.' }
    foreach ($key in @('traceTick','tracePublish','traceBandits')) {
        if ($null -eq $configured.$key -or $configured.$key -ne $false) { throw 'Launcher migration would change the installed config.' }
    }
    $rejected=$false
    try { & $scriptPath -Action Restore -InputsRoot $InputsRoot -RunToken 'another-run' } catch { $rejected=$true }
    if (-not $rejected) { throw 'Wrong owner restored password state.' }
    & $scriptPath -Action Restore -InputsRoot $InputsRoot -RunToken $RunToken
    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($configPath)) -cne [Convert]::ToBase64String($original)) { throw 'Original config bytes were not restored.' }
    if (Test-Path -LiteralPath $stateDirectory) { throw 'Password backup remains after restoration.' }
    # A fresh pinned stage has no config; use its shipped template, not the repository dev config.
    $stageRoot=Join-Path $testRoot '.stage-win'
    $stageData=Join-Path $stageRoot 'server-data'
    New-Item -Path $stageData -ItemType Directory | Out-Null
    $freshConfig=Join-Path $stageData 'server-config.json'
    Initialize-SyntheticPassword -ConfigPath $freshConfig -SeedConfigPath $seedConfig
    $configured=Get-Content -LiteralPath $freshConfig -Raw | ConvertFrom-Json
    if ($configured.password -notmatch '^[a-f0-9]{32}$' -or $configured.port -ne 4200) { throw 'Fresh stage config was not seeded.' }
    & $scriptPath -Action Restore -InputsRoot $InputsRoot -RunToken $RunToken
    if (Test-Path -LiteralPath $freshConfig) { throw 'Fresh stage cleanup did not restore config absence.' }
    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($seedConfig)) -cne [Convert]::ToBase64String($seed)) { throw 'Config seed was changed.' }
    Initialize-SyntheticPassword -ConfigPath $freshConfig -SeedConfigPath $seedConfig
    [IO.File]::AppendAllText($freshConfig,' ')
    $rejected=$false
    try { & $scriptPath -Action Restore -InputsRoot $InputsRoot -RunToken $RunToken } catch { $rejected=$true }
    if (-not $rejected -or -not (Test-Path -LiteralPath $stateDirectory)) { throw 'Changed fresh config lost its recovery state.' }
    [IO.File]::WriteAllBytes($freshConfig,[IO.File]::ReadAllBytes($installedPath))
    & $scriptPath -Action Restore -InputsRoot $InputsRoot -RunToken $RunToken
    Initialize-SyntheticPassword -ConfigPath $configPath -SeedConfigPath $seedConfig
    [IO.File]::AppendAllText($configPath,' ')
    $rejected=$false
    try { & $scriptPath -Action Restore -InputsRoot $InputsRoot -RunToken $RunToken } catch { $rejected=$true }
    if (-not $rejected -or -not (Test-Path -LiteralPath $originalPath)) { throw 'Unexpected config mutation did not preserve recovery state.' }
    Write-Output 'Synthetic password ownership and restoration tests passed.'
} finally { Remove-Item -LiteralPath $testRoot -Recurse -Force }
