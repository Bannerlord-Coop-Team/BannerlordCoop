$ErrorActionPreference = 'Stop'

$subject = Join-Path $PSScriptRoot 'live-test-engine-profile.ps1'
. $subject

$testRoot = Join-Path `
    ([IO.Path]::GetTempPath()) `
    ("BannerlordCoop.EngineProfileTests.$PID.$([Guid]::NewGuid().ToString('N'))")

function Assert-True {
    param(
        [Parameter(Mandatory = $true)] [bool] $Condition,
        [Parameter(Mandatory = $true)] [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
    $configPath = Join-Path $testRoot 'engine_config.txt'
    $evidencePath = Join-Path $testRoot 'evidence'
    $originalText = @(
        'number_of_ragdolls = 0'
        'max_framerate = 30'
        'display_width = 1280'
        'display_height = 960'
        'display_mode = 0'
        'resolution_scale = 100.0000'
        ''
    ) -join "`r`n"
    $originalBytes = [Text.UTF8Encoding]::new($false).GetBytes($originalText)
    [IO.File]::WriteAllBytes($configPath, $originalBytes)

    $lease = Enter-LiveTestEngineProfile `
        -Profile functional `
        -RunToken engine-profile-test `
        -EvidenceDirectory $evidencePath `
        -EngineConfigPath $configPath `
        -LockTimeoutSeconds 0
    $appliedText = [IO.File]::ReadAllText($configPath)
    Assert-True `
        -Condition ($appliedText -match '(?m)^max_framerate = 15\r?$') `
        -Message 'The functional profile did not set 15 FPS.'
    Assert-True `
        -Condition ($appliedText -match '(?m)^display_width = 960\r?$') `
        -Message 'The functional profile did not set width 960.'
    Assert-True `
        -Condition ($appliedText -match '(?m)^display_height = 540\r?$') `
        -Message 'The functional profile did not set height 540.'
    Assert-True `
        -Condition ($appliedText -match '(?m)^resolution_scale = 100\.0000\r?$') `
        -Message 'The functional profile changed an unrelated setting.'

    $appliedText = $appliedText.Replace("`r`n", "`n") + "safely_exited = 1`n"
    [IO.File]::WriteAllText($configPath, $appliedText)
    $borrowed = Enter-LiveTestEngineProfile -Profile functional -RunToken engine-profile-test -EvidenceDirectory (Join-Path $testRoot 'borrower') -EngineConfigPath $configPath -LockTimeoutSeconds 0
    Assert-True -Condition ([bool]$borrowed.Borrowed) -Message 'Same run did not reuse its applied profile.'
    Exit-LiveTestEngineProfile -Lease $borrowed
    Assert-True -Condition ([IO.File]::ReadAllText($configPath) -ceq $appliedText) -Message 'Borrower restored the owner profile.'

    [IO.File]::WriteAllText($configPath, $appliedText.Replace('max_framerate = 15', 'max_framerate = 60'))
    $changedProfileRejected = $false
    try { $null = Enter-LiveTestEngineProfile -Profile functional -RunToken engine-profile-test -EvidenceDirectory $evidencePath -EngineConfigPath $configPath -LockTimeoutSeconds 0 }
    catch { $changedProfileRejected = $_.Exception.Message -like '*engine-profile lease*' }
    Assert-True -Condition $changedProfileRejected -Message 'Same-run borrow accepted changed profile settings.'
    [IO.File]::WriteAllText($configPath, $appliedText)

    $contentionFailed = $false
    try {
        $null = Enter-LiveTestEngineProfile `
            -Profile visual `
            -RunToken engine-profile-contender `
            -EvidenceDirectory (Join-Path $testRoot 'contender') `
            -EngineConfigPath $configPath `
            -LockTimeoutSeconds 0
    }
    catch {
        $contentionFailed = $_.Exception.Message -like '*engine-profile lease*'
    }
    Assert-True `
        -Condition $contentionFailed `
        -Message 'A concurrent profile mutation acquired the shared lease.'

    Exit-LiveTestEngineProfile -Lease $lease
    $restoredBytes = [IO.File]::ReadAllBytes($configPath)
    Assert-True `
        -Condition ((Get-LiveTestBytesSha256 -Bytes $restoredBytes) -ceq (Get-LiveTestBytesSha256 -Bytes $originalBytes)) `
        -Message 'The original engine config bytes were not restored.'
    $receipt = Get-Content `
        -LiteralPath (Join-Path $evidencePath 'engine-profile.json') `
        -Raw | ConvertFrom-Json
    Assert-True `
        -Condition (
            [string]$receipt.profile -ceq 'functional' -and
            [string]$receipt.state -ceq 'restored' -and
            [bool]$receipt.exactOriginalRestored) `
        -Message 'The engine-profile receipt did not prove exact restoration.'
    $recoveryPath = Get-LiveTestEngineProfileRecoveryPath `
        -EngineConfigPath $configPath
    Assert-True `
        -Condition (-not (Test-Path -LiteralPath $recoveryPath)) `
        -Message 'A completed profile left its crash-recovery journal behind.'

    $interruptedEvidencePath = Join-Path $testRoot 'interrupted-evidence'
    $interruptedLease = Enter-LiveTestEngineProfile `
        -Profile functional `
        -RunToken engine-profile-interrupted `
        -EvidenceDirectory $interruptedEvidencePath `
        -EngineConfigPath $configPath `
        -LockTimeoutSeconds 0
    Assert-True `
        -Condition (Test-Path -LiteralPath $recoveryPath -PathType Leaf) `
        -Message 'An applied profile did not persist its crash-recovery journal.'
    $interruptedLease.LockStream.Dispose()
    $interruptedLease.LockStream = $null

    $recoveredEvidencePath = Join-Path $testRoot 'recovered-evidence'
    $recoveredLease = Enter-LiveTestEngineProfile `
        -Profile visual `
        -RunToken engine-profile-recovered `
        -EvidenceDirectory $recoveredEvidencePath `
        -EngineConfigPath $configPath `
        -LockTimeoutSeconds 0
    Assert-True `
        -Condition ($null -ne $recoveredLease.InterruptedRecovery) `
        -Message 'The next profile did not report recovery of the interrupted profile.'
    Assert-True `
        -Condition (
            [string]$recoveredLease.InterruptedRecovery.RunToken -ceq
            'engine-profile-interrupted') `
        -Message 'The interrupted recovery reported the wrong run token.'
    $interruptedReceipt = Get-Content `
        -LiteralPath (Join-Path $interruptedEvidencePath 'engine-profile.json') `
        -Raw | ConvertFrom-Json
    Assert-True `
        -Condition (
            [string]$interruptedReceipt.state -ceq 'recovered-after-interruption' -and
            [bool]$interruptedReceipt.exactOriginalRestored -and
            [bool]$interruptedReceipt.recoveredAfterInterruption) `
        -Message 'The interrupted receipt did not prove crash recovery.'
    Exit-LiveTestEngineProfile -Lease $recoveredLease
    $recoveredLease = $null
    $recoveredBytes = [IO.File]::ReadAllBytes($configPath)
    Assert-True `
        -Condition ((Get-LiveTestBytesSha256 -Bytes $recoveredBytes) -ceq (Get-LiveTestBytesSha256 -Bytes $originalBytes)) `
        -Message 'The recovered profile did not restore the pre-interruption config.'
    Assert-True `
        -Condition (-not (Test-Path -LiteralPath $recoveryPath)) `
        -Message 'The recovered profile left its crash-recovery journal behind.'

    $blockedEvidencePath = Join-Path $testRoot 'blocked-evidence'
    [IO.File]::WriteAllText($blockedEvidencePath, 'not-a-directory')
    $receiptFailureObserved = $false
    try {
        $null = Enter-LiveTestEngineProfile `
            -Profile visual `
            -RunToken engine-profile-rollback `
            -EvidenceDirectory $blockedEvidencePath `
            -EngineConfigPath $configPath `
            -LockTimeoutSeconds 0
    }
    catch {
        $receiptFailureObserved = $true
    }
    Assert-True `
        -Condition $receiptFailureObserved `
        -Message 'The receipt failure test did not fail.'
    $rollbackBytes = [IO.File]::ReadAllBytes($configPath)
    Assert-True `
        -Condition ((Get-LiveTestBytesSha256 -Bytes $rollbackBytes) -ceq (Get-LiveTestBytesSha256 -Bytes $originalBytes)) `
        -Message 'A failed profile admission did not roll back the engine config.'

    Write-Output 'live-test-engine-profile tests passed'
}
finally {
    if ($null -ne $lease -and $null -ne $lease.LockStream) {
        $lease.LockStream.Dispose()
    }
    if ($null -ne $interruptedLease -and $null -ne $interruptedLease.LockStream) {
        $interruptedLease.LockStream.Dispose()
    }
    if ($null -ne $recoveredLease -and $null -ne $recoveredLease.LockStream) {
        $recoveredLease.LockStream.Dispose()
    }
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}
