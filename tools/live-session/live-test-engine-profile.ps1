function Get-DefaultLiveTestEngineConfigPath {
    $documents = [Environment]::GetFolderPath(
        [Environment+SpecialFolder]::MyDocuments)
    if ([string]::IsNullOrWhiteSpace($documents)) {
        throw 'Could not resolve the Windows Documents directory.'
    }

    return Join-Path `
        $documents `
        'Mount and Blade II Bannerlord\Configs\engine_config.txt'
}

function Get-LiveTestEngineProfileSettings {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('functional', 'visual')]
        [string] $Profile
    )

    if ($Profile -ceq 'functional') {
        return [ordered]@{
            max_framerate = 15
            display_width = 960
            display_height = 540
        }
    }

    return [ordered]@{
        max_framerate = 30
        display_width = 1280
        display_height = 960
    }
}

function Get-LiveTestBytesSha256 {
    param([Parameter(Mandatory = $true)] [byte[]] $Bytes)

    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha256.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Set-LiveTestEngineConfigBytes {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [byte[]] $Bytes
    )

    $temporaryPath = "$Path.$PID.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        [IO.File]::WriteAllBytes($temporaryPath, $Bytes)
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

function Write-LiveTestEngineProfileReceipt {
    param(
        [Parameter(Mandatory = $true)] [string] $EvidenceDirectory,
        [Parameter(Mandatory = $true)] $Receipt
    )

    New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
    $receiptPath = Join-Path $EvidenceDirectory 'engine-profile.json'
    $temporaryPath = "$receiptPath.$PID.tmp"
    try {
        $Receipt |
            ConvertTo-Json -Depth 8 |
            Set-Content -LiteralPath $temporaryPath -Encoding UTF8
        Move-Item -LiteralPath $temporaryPath -Destination $receiptPath -Force
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
    return $receiptPath
}

function Get-LiveTestEngineProfileRecoveryPath {
    param([Parameter(Mandatory = $true)] [string] $EngineConfigPath)

    return "$EngineConfigPath.codex-live-test.recovery.json"
}

function Write-LiveTestEngineProfileRecoveryJournal {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] $Journal
    )

    $temporaryPath = "$Path.$PID.tmp"
    try {
        $Journal |
            ConvertTo-Json -Depth 8 |
            Set-Content -LiteralPath $temporaryPath -Encoding UTF8
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

function Restore-InterruptedLiveTestEngineProfile {
    param(
        [Parameter(Mandatory = $true)] [string] $EngineConfigPath,
        [Parameter(Mandatory = $true)] [string] $RecoveryPath
    )

    if (-not (Test-Path -LiteralPath $RecoveryPath -PathType Leaf)) {
        return $null
    }

    try {
        $journal = Get-Content -LiteralPath $RecoveryPath -Raw | ConvertFrom-Json
        if ([int]$journal.schemaVersion -ne 1 -or
            [string]::IsNullOrWhiteSpace([string]$journal.originalBytesBase64) -or
            [string]::IsNullOrWhiteSpace([string]$journal.originalSha256)) {
            throw 'The recovery journal is incomplete.'
        }
        $journalConfigPath = [IO.Path]::GetFullPath([string]$journal.engineConfigPath)
        if (-not $journalConfigPath.Equals(
                $EngineConfigPath,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The recovery journal belongs to a different engine config.'
        }

        $originalBytes = [Convert]::FromBase64String(
            [string]$journal.originalBytesBase64)
        $originalSha256 = Get-LiveTestBytesSha256 -Bytes $originalBytes
        if ($originalSha256 -cne [string]$journal.originalSha256) {
            throw 'The recovery journal original-byte hash does not match.'
        }

        Set-LiveTestEngineConfigBytes `
            -Path $EngineConfigPath `
            -Bytes $originalBytes
        $restoredBytes = [IO.File]::ReadAllBytes($EngineConfigPath)
        $restoredSha256 = Get-LiveTestBytesSha256 -Bytes $restoredBytes
        if ($restoredBytes.Length -ne $originalBytes.Length -or
            $restoredSha256 -cne $originalSha256) {
            throw 'The interrupted engine profile did not restore to its original bytes.'
        }

        if (-not [string]::IsNullOrWhiteSpace([string]$journal.evidenceDirectory)) {
            $receiptPath = Join-Path `
                ([string]$journal.evidenceDirectory) `
                'engine-profile.json'
            if (Test-Path -LiteralPath $receiptPath -PathType Leaf) {
                $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
                $receipt.state = 'recovered-after-interruption'
                $receipt.restoredUtc = [DateTime]::UtcNow.ToString('o')
                $receipt.restoredSha256 = $restoredSha256
                $receipt.exactOriginalRestored = $true
                $receipt | Add-Member `
                    -NotePropertyName recoveredAfterInterruption `
                    -NotePropertyValue $true `
                    -Force
                $null = Write-LiveTestEngineProfileReceipt `
                    -EvidenceDirectory ([string]$journal.evidenceDirectory) `
                    -Receipt $receipt
            }
        }

        Remove-Item -LiteralPath $RecoveryPath -Force
        return [pscustomobject]@{
            RunToken = [string]$journal.runToken
            Profile = [string]$journal.profile
            OriginalSha256 = $originalSha256
        }
    }
    catch {
        throw "Could not recover an interrupted Bannerlord engine profile: $($_.Exception.Message)"
    }
}

function Enter-LiveTestEngineProfile {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('functional', 'visual')]
        [string] $Profile,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^[A-Za-z0-9_-]{1,64}$')]
        [string] $RunToken,

        [Parameter(Mandatory = $true)]
        [string] $EvidenceDirectory,

        [string] $EngineConfigPath,

        [ValidateRange(0, 60)]
        [int] $LockTimeoutSeconds = 30
    )

    if ([string]::IsNullOrWhiteSpace($EngineConfigPath)) {
        $EngineConfigPath = Get-DefaultLiveTestEngineConfigPath
    }
    $EngineConfigPath = [IO.Path]::GetFullPath($EngineConfigPath)
    if (-not (Test-Path -LiteralPath $EngineConfigPath -PathType Leaf)) {
        throw "Bannerlord engine config was not found: $EngineConfigPath"
    }

    $lockPath = "$EngineConfigPath.codex-live-test.lock"
    $recoveryPath = Get-LiveTestEngineProfileRecoveryPath `
        -EngineConfigPath $EngineConfigPath
    $lockDeadline = [DateTime]::UtcNow.AddSeconds($LockTimeoutSeconds)
    $lockStream = $null
    do {
        try {
            $lockStream = [IO.File]::Open(
                $lockPath,
                [IO.FileMode]::OpenOrCreate,
                [IO.FileAccess]::ReadWrite,
                [IO.FileShare]::None)
        }
        catch [IO.IOException] {
            if (Test-Path -LiteralPath $recoveryPath -PathType Leaf) {
                $owner = Get-Content -LiteralPath $recoveryPath -Raw | ConvertFrom-Json
                $currentHash = Get-LiveTestBytesSha256 -Bytes ([IO.File]::ReadAllBytes($EngineConfigPath))
                if ([string]$owner.runToken -ceq $RunToken -and
                    [string]$owner.profile -ceq $Profile -and
                    [string]$owner.engineConfigPath -ieq $EngineConfigPath -and
                    [string]$owner.appliedSha256 -ceq $currentHash) {
                    return [pscustomobject]@{
                        Borrowed = $true
                        LockStream = $null
                        InterruptedRecovery = $null
                        ReceiptPath = (Join-Path ([string]$owner.evidenceDirectory) 'engine-profile.json')
                        Receipt = $owner
                    }
                }
            }
            if ([DateTime]::UtcNow -ge $lockDeadline) {
                throw "Timed out waiting for the Bannerlord engine-profile lease: $lockPath"
            }
            Start-Sleep -Milliseconds 100
        }
    } while ($null -eq $lockStream)

    $originalBytes = $null
    $configApplied = $false
    try {
        $interruptedRecovery = Restore-InterruptedLiveTestEngineProfile `
            -EngineConfigPath $EngineConfigPath `
            -RecoveryPath $recoveryPath
        $originalBytes = [IO.File]::ReadAllBytes($EngineConfigPath)
        $originalText = [Text.UTF8Encoding]::new($false, $true).GetString($originalBytes)
        $settings = Get-LiveTestEngineProfileSettings -Profile $Profile
        $appliedText = $originalText
        foreach ($entry in $settings.GetEnumerator()) {
            $pattern = '(?m)^' + [Regex]::Escape([string]$entry.Key) + '[ \t]*=[ \t]*[^\r\n]*'
            $matches = [Regex]::Matches($appliedText, $pattern)
            if ($matches.Count -ne 1) {
                throw "Bannerlord engine config must contain exactly one $($entry.Key) setting."
            }
            $appliedText = [Regex]::Replace(
                $appliedText,
                $pattern,
                "$($entry.Key) = $($entry.Value)")
        }

        $appliedBytes = [Text.UTF8Encoding]::new($false).GetBytes($appliedText)
        $originalSha256 = Get-LiveTestBytesSha256 -Bytes $originalBytes
        $appliedSha256 = Get-LiveTestBytesSha256 -Bytes $appliedBytes
        $recoveryJournal = [ordered]@{
            schemaVersion = 1
            runToken = $RunToken
            profile = $Profile
            state = 'prepared'
            engineConfigPath = $EngineConfigPath
            evidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
            preparedUtc = [DateTime]::UtcNow.ToString('o')
            originalSha256 = $originalSha256
            appliedSha256 = $appliedSha256
            originalBytesBase64 = [Convert]::ToBase64String($originalBytes)
        }
        Write-LiveTestEngineProfileRecoveryJournal `
            -Path $recoveryPath `
            -Journal $recoveryJournal
        Set-LiveTestEngineConfigBytes -Path $EngineConfigPath -Bytes $appliedBytes
        $configApplied = $true
        $receipt = [ordered]@{
            schemaVersion = 1
            runToken = $RunToken
            profile = $Profile
            state = 'applied'
            engineConfigPath = $EngineConfigPath
            leasePath = $lockPath
            appliedUtc = [DateTime]::UtcNow.ToString('o')
            originalSha256 = $originalSha256
            appliedSha256 = $appliedSha256
            settings = $settings
            restoredUtc = $null
            restoredSha256 = $null
            exactOriginalRestored = $false
        }
        $receiptPath = Write-LiveTestEngineProfileReceipt `
            -EvidenceDirectory $EvidenceDirectory `
            -Receipt $receipt
        return [pscustomobject]@{
            ConfigPath = $EngineConfigPath
            EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
            InterruptedRecovery = $interruptedRecovery
            LockStream = $lockStream
            OriginalBytes = $originalBytes
            RecoveryPath = $recoveryPath
            Receipt = $receipt
            ReceiptPath = $receiptPath
        }
    }
    catch {
        if ($configApplied -and $null -ne $originalBytes) {
            try {
                Set-LiveTestEngineConfigBytes `
                    -Path $EngineConfigPath `
                    -Bytes $originalBytes
            }
            catch {
            }
        }
        if ($null -ne $originalBytes) {
            try {
                $currentBytes = [IO.File]::ReadAllBytes($EngineConfigPath)
                if ((Get-LiveTestBytesSha256 -Bytes $currentBytes) -ceq
                    (Get-LiveTestBytesSha256 -Bytes $originalBytes)) {
                    Remove-Item `
                        -LiteralPath $recoveryPath `
                        -Force `
                        -ErrorAction SilentlyContinue
                }
            }
            catch {
            }
        }
        if ($null -ne $lockStream) {
            $lockStream.Dispose()
        }
        throw
    }
}

function Exit-LiveTestEngineProfile {
    param([Parameter(Mandatory = $true)] $Lease)

    if ($Lease.PSObject.Properties['Borrowed'] -and $Lease.Borrowed) { return }

    $restoreFailure = $null
    try {
        Set-LiveTestEngineConfigBytes `
            -Path ([string]$Lease.ConfigPath) `
            -Bytes ([byte[]]$Lease.OriginalBytes)
        $restoredBytes = [IO.File]::ReadAllBytes([string]$Lease.ConfigPath)
        $restoredSha256 = Get-LiveTestBytesSha256 -Bytes $restoredBytes
        $Lease.Receipt.state = 'restored'
        $Lease.Receipt.restoredUtc = [DateTime]::UtcNow.ToString('o')
        $Lease.Receipt.restoredSha256 = $restoredSha256
        $Lease.Receipt.exactOriginalRestored = (
            $restoredBytes.Length -eq $Lease.OriginalBytes.Length -and
            $restoredSha256 -ceq [string]$Lease.Receipt.originalSha256)
        if (-not [bool]$Lease.Receipt.exactOriginalRestored) {
            throw 'The Bannerlord engine config did not restore to its original bytes.'
        }
        $receiptPath = Write-LiveTestEngineProfileReceipt `
            -EvidenceDirectory ([string]$Lease.EvidenceDirectory) `
            -Receipt $Lease.Receipt
        Remove-Item -LiteralPath ([string]$Lease.RecoveryPath) -Force
        Write-Output (
            "LIVE_TEST_ENGINE_PROFILE_RESTORED runToken=$($Lease.Receipt.runToken) " +
            "profile=$($Lease.Receipt.profile) receipt=$receiptPath")
    }
    catch {
        $restoreFailure = $_
        try {
            $Lease.Receipt.state = 'restore-failed'
            $Lease.Receipt.restoreError = $_.Exception.Message
            $Lease.Receipt.restoredUtc = [DateTime]::UtcNow.ToString('o')
            $null = Write-LiveTestEngineProfileReceipt `
                -EvidenceDirectory ([string]$Lease.EvidenceDirectory) `
                -Receipt $Lease.Receipt
        }
        catch {
        }
    }
    finally {
        if ($null -ne $Lease.LockStream) {
            $Lease.LockStream.Dispose()
        }
    }

    if ($null -ne $restoreFailure) {
        throw $restoreFailure
    }
}
