$ErrorActionPreference = 'Stop'

$workflow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../workflows/nightly-release.yml') -Raw
if (-not $workflow.Contains('Invalidate the legacy client manifest')) { throw 'Legacy client manifest invalidation was not found' }
if (-not $workflow.Contains('nightly/client.json')) { throw 'Legacy client manifest path was not found' }
$match = [regex]::Match($workflow, '(?ms)      - name: Skip scheduled overwrite of a completed nightly publication.*?\n        run: \|\r?\n(.*?)(?=\r?\n      - name:)')
if (-not $match.Success) { throw 'Nightly publish guard was not found' }
$guardText = $match.Groups[1].Value -replace '(?m)^ {10}', ''
$guardText = $guardText.Replace("& (Join-Path `$PWD 'rclone.exe')", 'Invoke-TestRclone')
if ($guardText.Contains('rclone.exe')) { throw 'Unmocked rclone invocation in nightly publish guard' }
$guard = [scriptblock]::Create($guardText)

$invalidationMatch = [regex]::Match($workflow, '(?ms)      - name: Invalidate the legacy client manifest.*?\n        run: \|\r?\n(.*?)(?=\r?\n      - name:)')
if (-not $invalidationMatch.Success) { throw 'Legacy client manifest invalidation was not found' }
$invalidationText = $invalidationMatch.Groups[1].Value -replace '(?m)^ {10}', ''
$invalidationText = $invalidationText.Replace('& $rclone', 'Invoke-TestInvalidationRclone')
if ($invalidationText.Contains('& $rclone')) { throw 'Unmocked rclone invocation in legacy client invalidation' }
$invalidation = [scriptblock]::Create($invalidationText)

function Invoke-TestRclone {
    param([string]$Command, [string]$Source, [string]$Destination)
    $expectedSource = if ($Command -eq 'lsjson') { 'patron:test-nightlies' } else { 'patron:test-nightlies/nightly/release.json' }
    if ($Source -ne $expectedSource) { throw 'Unexpected manifest source' }
    $script:commands += $Command
    $global:LASTEXITCODE = 0
    if ($Command -eq 'lsjson') {
        $global:LASTEXITCODE = $script:scenario.LookupExit
        if ($script:scenario.Missing) { return '[]' }
        if ($null -ne $script:scenario.LookupText) { return $script:scenario.LookupText }
        $size = [Text.Encoding]::UTF8.GetByteCount($script:scenario.Manifest)
        if ($script:scenario.MetadataSize) { $size = $script:scenario.MetadataSize }
        return ((ConvertTo-Json -InputObject @(@{ Path = 'nightly/release.json'; IsDir = $false; Size = $size })) -split '\r?\n')
    }
    if ($Command -ne 'copyto') { throw "Unexpected storage command: $Command" }
    $global:LASTEXITCODE = $script:scenario.DownloadExit
    if ($script:scenario.WriteFile) {
        [IO.File]::WriteAllText($Destination, $script:scenario.Manifest, [Text.UTF8Encoding]::new($false))
    }
}

function Invoke-TestInvalidationRclone {
    param([string]$Command, [string]$Source)

    $expectedSource = if ($Command -eq 'lsjson') { 'patron:test-nightlies' } else { 'patron:test-nightlies/nightly/client.json' }
    if ($Source -ne $expectedSource) { throw 'Unexpected legacy manifest source' }
    $script:commands += $Command
    if ($Command -eq 'deletefile') {
        $global:LASTEXITCODE = $script:invalidationScenario.DeleteExit
        return
    }
    if ($Command -ne 'lsjson') { throw "Unexpected legacy storage command: $Command" }
    $global:LASTEXITCODE = $script:invalidationScenario.LookupExit
    return $script:invalidationScenario.LookupText
}

$cases = @(
    @{ Name = 'missing manifest'; Missing = $true; WriteFile = $false; Expected = 'true' },
    @{ Name = 'older nightly'; Expected = 'true' },
    @{ Name = 'already published'; Manifest = '{"releaseDate":"2026-10-01"}'; Expected = 'false' },
    @{ Name = 'manual dispatch'; Event = 'workflow_dispatch'; Expected = 'true' },
    @{ Name = 'lookup denied'; LookupExit = 3; Error = 'Published nightly manifest lookup failed' },
    @{ Name = 'lookup unavailable'; LookupExit = 10; Error = 'Published nightly manifest lookup failed' },
    @{ Name = 'empty lookup response'; LookupText = ''; Error = 'Published nightly manifest lookup failed' },
    @{ Name = 'invalid metadata'; LookupText = '{'; Error = '*' },
    @{ Name = 'unexpected metadata path'; LookupText = '[{"Path":"other.json","IsDir":false,"Size":26}]'; Error = 'Published nightly manifest lookup failed' },
    @{ Name = 'download failed'; DownloadExit = 10; WriteFile = $false; Error = 'Published nightly manifest download failed' },
    @{ Name = 'successful download without file'; WriteFile = $false; Error = 'Published nightly manifest download failed' },
    @{ Name = 'invalid JSON'; Manifest = '{'; Error = '*' },
    @{ Name = 'missing release date'; Manifest = '{}'; Error = 'Published nightly manifest release date is invalid' },
    @{ Name = 'empty manifest'; Manifest = ''; Error = 'Published nightly manifest size is invalid' },
    @{ Name = 'changed manifest size'; MetadataSize = 100; Error = 'Published nightly manifest size is invalid' },
    @{ Name = 'oversized manifest'; Manifest = ('x' * 4097); Error = 'Published nightly manifest size is invalid' }
)

$invalidationCases = @(
    @{ Name = 'deleted legacy manifest returns empty array' },
    @{ Name = 'deleted legacy manifest returns multiline empty array'; LookupText = @('[', ']') },
    @{ Name = 'already absent legacy manifest returns empty array'; DeleteExit = 1 },
    @{ Name = 'legacy manifest remains after deletion'; LookupText = '[{"Path":"nightly/client.json"}]'; Error = 'Legacy client manifest remains after invalidation' },
    @{ Name = 'failed deletion leaves legacy manifest'; DeleteExit = 1; LookupText = '[{"Path":"nightly/client.json"}]'; Error = 'Legacy client manifest invalidation failed' },
    @{ Name = 'legacy readback lookup fails'; LookupExit = 3; Error = 'Legacy client manifest remains after invalidation' },
    @{ Name = 'failed deletion lookup fails'; DeleteExit = 1; LookupExit = 3; Error = 'Legacy client manifest invalidation failed' },
    @{ Name = 'legacy readback is empty'; LookupText = ''; Error = '*' },
    @{ Name = 'failed deletion lookup is empty'; DeleteExit = 1; LookupText = ''; Error = '*' },
    @{ Name = 'legacy readback is malformed JSON'; LookupText = '['; Error = '*' },
    @{ Name = 'failed deletion lookup is malformed JSON'; DeleteExit = 1; LookupText = '['; Error = '*' },
    @{ Name = 'legacy readback is not an array'; LookupText = '{}'; Error = '*' },
    @{ Name = 'failed deletion lookup is not an array'; DeleteExit = 1; LookupText = '{}'; Error = '*' }
)

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('nightly-publish-guard-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
Push-Location $testRoot
try {
    $env:R2_ACCOUNT_ID = 'test-account'
    $env:R2_NIGHTLY_BUCKET = 'test-nightlies'
    $env:RELEASE_DATE = '2026-10-01'
    foreach ($case in $cases) {
        $script:scenario = @{ Event = 'schedule'; Missing = $false; LookupExit = 0; DownloadExit = 0; WriteFile = $true; Manifest = '{"releaseDate":"2026-09-30"}' }
        foreach ($key in $case.Keys) { $script:scenario[$key] = $case[$key] }
        $script:commands = @()
        $caseRoot = Join-Path $testRoot ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $caseRoot | Out-Null
        $env:RUNNER_TEMP = $caseRoot
        $env:GITHUB_OUTPUT = Join-Path $caseRoot 'output.txt'
        $env:GITHUB_EVENT_NAME = $script:scenario.Event
        $failure = $null
        try { & $guard } catch { $failure = $_.Exception.Message }
        if ($case.Error) {
            if (-not $failure -or ($case.Error -ne '*' -and $failure -ne $case.Error)) {
                throw "$($case.Name): expected '$($case.Error)', got '$failure'"
            }
        } else {
            if ($failure) { throw "$($case.Name): $failure" }
            $outputs = @(Get-Content -LiteralPath $env:GITHUB_OUTPUT)
            if ($outputs[-1] -ne "overwrite=$($case.Expected)") { throw "$($case.Name): unexpected overwrite decision" }
        }
        if (($case.Missing -or $case.Event -eq 'workflow_dispatch' -or $case.LookupExit -or $case.Name -eq 'oversized manifest') -and $script:commands -contains 'copyto') {
            throw "$($case.Name): unexpected download"
        }
        Write-Host "PASS: $($case.Name)"
    }
    $env:R2_ACCOUNT_ID = '00000000000000000000000000000000'
    foreach ($case in $invalidationCases) {
        $script:invalidationScenario = @{ DeleteExit = 0; LookupExit = 0; LookupText = '[]' }
        foreach ($key in $case.Keys) { $script:invalidationScenario[$key] = $case[$key] }
        $script:commands = @()
        $failure = $null
        try { & $invalidation } catch { $failure = $_.Exception.Message }
        if ($case.Error) {
            if (-not $failure -or ($case.Error -ne '*' -and $failure -ne $case.Error)) {
                throw "$($case.Name): expected '$($case.Error)', got '$failure'"
            }
        } else {
            if ($failure) { throw "$($case.Name): $failure" }
            $expectedCommands = if ($case.DeleteExit) { 'deletefile,lsjson,lsjson' } else { 'deletefile,lsjson' }
            if (($script:commands -join ',') -ne $expectedCommands) { throw "$($case.Name): unexpected storage commands" }
        }
        Write-Host "PASS: $($case.Name)"
    }
} finally {
    Pop-Location
    $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolvedRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Test cleanup escaped the temporary directory' }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    $global:LASTEXITCODE = 0
}
