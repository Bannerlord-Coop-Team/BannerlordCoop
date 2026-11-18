$ErrorActionPreference = 'Stop'

node --test (Join-Path $PSScriptRoot 'test-nightly-workflow.cjs')
if ($LASTEXITCODE -ne 0) { throw 'Nightly workflow regression tests failed' }

$workflow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../workflows/nightly-release.yml') -Raw
if (-not $workflow.Contains('Invalidate the legacy client manifest')) { throw 'Legacy client manifest invalidation was not found' }
if (-not $workflow.Contains('nightly/client.json')) { throw 'Legacy client manifest path was not found' }
$match = [regex]::Match($workflow, '(?ms)      - name: Skip scheduled overwrite of a completed nightly publication.*?\n        run: \|\r?\n(.*?)(?=\r?\n      - name:)')
if (-not $match.Success) { throw 'Nightly publish guard was not found' }
$guardText = $match.Groups[1].Value -replace '(?m)^ {10}', ''
# The guard calls & $rclone, so naming the mock there routes both lsf and copyto to Invoke-TestRclone.
$guardText = $guardText.Replace("Join-Path `$PWD 'rclone.exe'", "'Invoke-TestRclone'")
$guardText = $guardText.Replace('Invoke-WebRequest', 'Invoke-TestWebRequest')
if ($guardText.Contains('rclone.exe')) { throw 'Unmocked rclone invocation in nightly publish guard' }
if (-not $guardText.Contains('Invoke-TestWebRequest')) { throw 'Unmocked pointer request in nightly publish guard' }
$guard = [scriptblock]::Create($guardText)

$invalidationMatch = [regex]::Match($workflow, '(?ms)      - name: Invalidate the legacy client manifest.*?\n        run: \|\r?\n(.*?)(?=\r?\n      - name:)')
if (-not $invalidationMatch.Success) { throw 'Legacy client manifest invalidation was not found' }
$invalidationText = $invalidationMatch.Groups[1].Value -replace '(?m)^ {10}', ''
if ($invalidationText.Contains('deletefile')) { throw 'Legacy client manifest must not be deleted before the replacement is published' }
if (-not $invalidationText.Contains('Retaining the previous legacy client manifest')) {
    throw 'Legacy client retention guard was not found'
}

function Invoke-TestWebRequest {
    param([string]$Uri, [switch]$SkipHttpErrorCheck, [int]$TimeoutSec, [int]$MaximumRetryCount, [int]$RetryIntervalSec)
    if ($Uri -ne 'https://pointer.test/nightly/release.json' -or -not $SkipHttpErrorCheck) { throw 'Unexpected pointer request' }
    $script:commands += 'pointer'
    return [pscustomobject]@{ StatusCode = $script:scenario.PointerStatus; Content = $script:scenario.Pointer }
}

function Invoke-TestRclone {
    param([string]$Command, [string]$Source, [string]$Destination)
    if ($Source -ne "patron:test-nightlies/$($script:scenario.PointerKey)") { throw 'Unexpected manifest source' }
    $script:commands += $Command
    if ($Command -eq 'lsf') {
        $global:LASTEXITCODE = $script:scenario.ListExit
        if ($script:scenario.Expired) { return }
        return 'release.json'
    }
    if ($Command -ne 'copyto') { throw "Unexpected storage command: $Command" }
    $global:LASTEXITCODE = $script:scenario.DownloadExit
    if ($script:scenario.WriteFile) {
        [IO.File]::WriteAllText($Destination, $script:scenario.Manifest, [Text.UTF8Encoding]::new($false))
    }
}

function Get-TestSha256([string]$Text) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    return ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($bytes)) -replace '-', '').ToLowerInvariant()
}

# Each case overrides these; Sha256 and PointerKey follow the manifest unless a case sets them.
$defaultScenario = @{ Event = 'schedule'; PointerStatus = 200; PointerVersion = 1; ListExit = 0; DownloadExit = 0; WriteFile = $true; Manifest = '{"releaseDate":"2026-09-30"}' }

$cases = @(
    @{ Name = 'no paired publication'; PointerStatus = 404; StopsBeforeDownload = $true; Expected = 'true' },
    @{ Name = 'older nightly'; Expected = 'true' },
    @{ Name = 'already published'; Manifest = '{"releaseDate":"2026-10-01"}'; Expected = 'false' },
    @{ Name = 'manual dispatch'; Event = 'workflow_dispatch'; StopsBeforeDownload = $true; Expected = 'true' },
    @{ Name = 'expired manifest'; Expired = $true; StopsBeforeDownload = $true; Expected = 'true' },
    @{ Name = 'expired manifest reported as not found'; Expired = $true; ListExit = 3; StopsBeforeDownload = $true; Expected = 'true' },
    @{ Name = 'pointer unavailable'; PointerStatus = 503; StopsBeforeDownload = $true; Error = 'Published nightly pointer lookup failed with HTTP 503' },
    @{ Name = 'pointer with a malformed checksum'; Sha256 = ('z' * 64); StopsBeforeDownload = $true; Error = 'Published nightly pointer is invalid' },
    @{ Name = 'pointer of another version'; PointerVersion = 2; StopsBeforeDownload = $true; Error = 'Published nightly pointer is invalid' },
    @{ Name = 'pointer to another key'; PointerKey = 'nightly/release.json'; StopsBeforeDownload = $true; Error = 'Published nightly pointer is invalid' },
    @{ Name = 'manifest lookup failed'; Expired = $true; ListExit = 10; StopsBeforeDownload = $true; Error = 'Published nightly manifest lookup failed' },
    @{ Name = 'download failed'; DownloadExit = 10; WriteFile = $false; Error = 'Published nightly manifest download failed' },
    @{ Name = 'successful download without file'; WriteFile = $false; Error = 'Published nightly manifest download failed' },
    @{ Name = 'changed manifest'; Sha256 = ('0' * 64); Error = 'Published nightly manifest checksum mismatch' },
    @{ Name = 'invalid JSON'; Manifest = '{'; Error = '*' },
    @{ Name = 'missing release date'; Manifest = '{}'; Error = 'Published nightly manifest release date is invalid' }
)

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('nightly-publish-guard-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
Push-Location $testRoot
try {
    $env:R2_ACCOUNT_ID = 'test-account'
    $env:R2_NIGHTLY_BUCKET = 'test-nightlies'
    $env:RELEASE_DATE = '2026-10-01'
    $env:NIGHTLY_POINTER_URL = 'https://pointer.test/nightly/release.json'
    foreach ($case in $cases) {
        $script:scenario = $defaultScenario.Clone()
        foreach ($key in $case.Keys) { $script:scenario[$key] = $case[$key] }
        if (-not $script:scenario.Sha256) { $script:scenario.Sha256 = Get-TestSha256 $script:scenario.Manifest }
        if (-not $script:scenario.PointerKey) { $script:scenario.PointerKey = "nightly/publications/$($script:scenario.Sha256)/release.json" }
        $script:scenario.Pointer = @{
            version = $script:scenario.PointerVersion; kind = 'bannerlordcoop-nightly-pointer'
            manifest = @{ key = $script:scenario.PointerKey; sha256 = $script:scenario.Sha256 }
        } | ConvertTo-Json -Compress
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
        if ($case.Event -eq 'workflow_dispatch' -and $script:commands.Count -gt 0) { throw "$($case.Name): unexpected lookup" }
        if ($case.StopsBeforeDownload -and $script:commands -contains 'copyto') { throw "$($case.Name): unexpected download" }
        Write-Host "PASS: $($case.Name)"
    }
    Write-Host 'PASS: legacy client manifest is retained until replacement publication'
} finally {
    Pop-Location
    $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolvedRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Test cleanup escaped the temporary directory' }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    $global:LASTEXITCODE = 0
}
