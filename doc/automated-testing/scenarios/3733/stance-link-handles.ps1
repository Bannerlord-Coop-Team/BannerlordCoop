[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]{1,64}$')][string]$RunToken,
    [Parameter(Mandatory=$true)][string]$ArtifactDirectory,
    [Parameter(Mandatory=$true)][string]$LiveTestClientScript,
    [Parameter(Mandatory=$true)][string]$RuntimeScript,
    [Parameter(Mandatory=$true)][string]$ExpectedHead,
    [ValidateSet('Scenario','Cleanup')][string]$Mode = 'Scenario'
)
$ErrorActionPreference = 'Stop'
. $LiveTestClientScript
. $RuntimeScript
New-Item -ItemType Directory -Path $ArtifactDirectory -Force | Out-Null
function Save-Json([string]$Name, $Value) {
    $path = Join-Path $ArtifactDirectory $Name
    if (Test-Path -LiteralPath $path) { throw "Refusing to replace evidence: $path" }
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    [IO.File]::WriteAllText($path, (($Value | ConvertTo-Json -Depth 64) + "`n"), [Text.UTF8Encoding]::new($false))
}
if ($Mode -eq 'Cleanup') {
    $cleanup = Stop-RemoteLiveRunScopedProcessesVerified -RunToken $RunToken -TimeoutSeconds 60
    Save-Json 'cleanup.json' $cleanup
    if (-not $cleanup.verifiedAbsent) { throw 'Owned processes remain after cleanup.' }
    exit 0
}
$clients = @()
$prepared = @()
$images = @()
$failure = $null
$restoreErrors = @()
$ordinal = 0
$raw = Join-Path ([IO.Path]::GetDirectoryName($ArtifactDirectory)) "captures-$RunToken"
if ($raw -notmatch '^[A-Za-z]:\\' -or (Test-Path -LiteralPath $raw)) { throw 'Capture directory must be fresh Windows-local storage.' }
New-Item -ItemType Directory -Path $raw | Out-Null
function Command($Endpoint, [string]$Action) {
    $script:ordinal++
    $argsJson = ConvertTo-Json -InputObject @($Action,'Kingdom_vlandia','Kingdom_empire') -Compress
    $reply = Invoke-LiveTestClientAction -RequestedAction Command -TargetProcessId ([int]$Endpoint.process.pid) `
        -RequestedCommandName 'coop.debug.stance_link.handle_fixture' -RequestedArgumentsJson $argsJson -RequestTimeoutMilliseconds 30000
    Save-Json ("commands/{0:D3}-{1}-{2}.json" -f $script:ordinal,$Endpoint.process.pid,$Action) $reply
    $output = [string]$reply.Response.result.output
    $pattern = if ($Action -ceq 'send') {
        '^STANCE_HANDLE_SENT id=StanceLink_vlandia_empire handle=[0-9]+$'
    } else {
        '^STANCE_HANDLE_STATE action=' + [regex]::Escape($Action) + ' id=StanceLink_vlandia_empire handle=[0-9]+ sameString=True sameNumeric=(True|False) samePrepared=(True|False) received=[0-9]+$'
    }
    $succeeded = $reply.Response.result.PSObject.Properties['succeeded']
    if ($reply.ExitCode -ne 0 -or -not $reply.Response.ok -or -not $reply.Response.result.found -or
        $Endpoint.process.role -cnotin @('server','client') -or
        ($Endpoint.process.role -cne 'server' -and $reply.Response.result.succeeded -ne $true) -or
        ($null -ne $succeeded -and $succeeded.Value -ne $true) -or $output -cnotmatch $pattern) {
        throw "Fixture $Action failed for $($Endpoint.process.pid): $($reply.Response.result.output)"
    }
    $fields = @{}
    foreach ($part in ([string]$reply.Response.result.output -split ' ')) {
        $pair = $part -split '=',2
        if ($pair.Count -eq 2) { $fields[$pair[0]] = $pair[1] }
    }
    return $fields
}
function Capture([string]$Checkpoint) {
    if ($clients.Count -eq 0) { throw 'No renderable client endpoints are available.' }
    $captures = @($clients | ForEach-Object {
        [pscustomobject]@{ ProcessId=[int]$_.process.pid; Path=(Join-Path $raw "$Checkpoint-$($_.process.platformId).bmp") }
    })
    $batch = @(Invoke-LiveTestScreenshotBatch -Captures $captures -TimeoutMilliseconds 60000)
    Save-Json "screenshots/$Checkpoint-batch.json" $batch
    foreach ($capture in $captures) {
        $entry = @($batch | Where-Object { [int]$_.processId -eq $capture.ProcessId })[0]
        if ($null -eq $entry -or $entry.exitCode -ne 0 -or -not $entry.response.ok -or -not $entry.response.result.captureComplete -or
            $entry.response.result.captureId -cne $entry.response.result.completionCaptureId) { throw "Incomplete $Checkpoint screenshot." }
        $endpoint = @($clients | Where-Object { [int]$_.process.pid -eq $capture.ProcessId })[0]
        $relative = "screenshots/$Checkpoint-$($endpoint.process.platformId).png"
        $png = Join-Path $ArtifactDirectory $relative
        $completedPath = [IO.Path]::GetFullPath([string]$entry.path)
        if (-not [string]::Equals([IO.Path]::GetDirectoryName($completedPath), $raw, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Completed capture is outside the owned raw directory.'
        }
        $converted = Convert-LiveTestScreenshotToPng -BmpPath $completedPath -PngPath $png
        $bitmap = [Drawing.Bitmap]::new($png)
        try {
            $visible = $false
            for ($x=0; $x -lt $bitmap.Width; $x += [Math]::Max(1,[int]($bitmap.Width/8))) {
                for ($y=0; $y -lt $bitmap.Height; $y += [Math]::Max(1,[int]($bitmap.Height/8))) {
                    $pixel=$bitmap.GetPixel($x,$y)
                    if ($pixel.R -gt 8 -or $pixel.G -gt 8 -or $pixel.B -gt 8) { $visible=$true }
                }
            }
            if (-not $visible) { throw 'Screenshot is blank.' }
        } finally { $bitmap.Dispose() }
        $script:images += [ordered]@{
            role=[string]$endpoint.process.platformId; checkpoint=$Checkpoint; processId=$capture.ProcessId
            captureId=[string]$entry.response.result.captureId; completionObservedUtc=[string]$entry.response.result.completionObservedUtc
            path=$relative; sha256=$converted.sha256; width=$converted.width; height=$converted.height
        }
    }
}
function Require-State($State, [uint32]$Handle, [int]$Received, [bool]$Prepared) {
    if ([uint32]$State.handle -ne $Handle -or $State.sameString -cne 'True' -or
        $State.sameNumeric -cne $(if ($Handle -eq 0) {'False'} else {'True'}) -or
        $State.samePrepared -cne $Prepared.ToString() -or [int]$State.received -lt $Received) {
        throw "Unexpected stance identity: $($State | ConvertTo-Json -Compress)"
    }
}
try {
    $endpoints = @(Wait-LiveTestReadiness -ExpectedRunToken $RunToken -ClientCount 2 -ExpectedClientPlatformIds @('testclient','testclient2') `
        -ReadyFor Campaign -RegisteredPlayerCount 2 -ConnectedPlayerCount 2 -ClientsOnly -AllowLegacyServerConnectionProbe `
        -EndpointStartupTimeoutMilliseconds 600000 -TimeoutMilliseconds 180000)
    Save-Json 'readiness.json' $endpoints
    $clients = @($endpoints | Where-Object { $_.process.role -ceq 'client' } | Sort-Object { $_.process.platformId })
    if ($clients.Count -ne 2) { throw 'Two real clients are required.' }
    foreach ($client in $clients) {
        Save-Json "client-$($client.process.platformId)-status.json" $client
        if ([string]$client.result.buildVersion -notmatch [regex]::Escape($ExpectedHead)) { throw 'Client build source differs.' }
    }
    Capture 'baseline'
    $servers = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" | Where-Object {
        $_.CommandLine -like '*TaleWorlds.Starter.DotNetCore.dll*' -and $_.CommandLine -like '*DedicatedServer.Windows*' -and
        $_.CommandLine -match ('(?i)(?:^|\s)' + [regex]::Escape($RunToken) + '(?:\s|$|\")')
    })
    if ($servers.Count -ne 1) { throw 'One exact token-bound dedicated server is required.' }
    $reply = Invoke-LiveTestClientAction -RequestedAction Status -TargetProcessId ([int]$servers[0].ProcessId) -RequestTimeoutMilliseconds 30000
    $server = $reply.Response
    Save-Json 'server-status.json' $reply
    if ($reply.ExitCode -ne 0 -or -not $server.ok -or $server.process.runToken -cne $RunToken -or -not $server.result.readyForCampaignTests) {
        throw 'Authoritative server endpoint is unavailable.'
    }
    $baseline = Command $server 'state'
    $handle = [uint32]$baseline.handle
    if ($handle -eq 0) { throw 'Authoritative baseline handle is missing.' }
    foreach ($client in $clients) {
        $before = Command $client 'state'
        Require-State $before $handle 0 $false
        if ($before.id -cne $baseline.id) { throw 'Client/server stance string ids differ.' }
        # Retain even a partial preparation so failure restoration can be attempted.
        $prepared += $client
        $setup = Command $client 'prepare'
        Require-State $setup 0 0 $true
    }
    foreach ($client in $clients) { Require-State (Command $client 'conflicts') 0 0 $true }
    foreach ($minimumReceipts in @(1,2)) {
        $sent = Command $server 'send'
        if ([uint32]$sent.handle -ne $handle -or $sent.id -cne $baseline.id) { throw 'Server sent a different stance identity.' }
        foreach ($client in $clients) {
            $deadline = [DateTime]::UtcNow.AddSeconds(60)
            do {
                $state = Command $client 'state'
                if ([int]$state.received -ge $minimumReceipts) { break }
                Start-Sleep -Milliseconds 200
            } while ([DateTime]::UtcNow -lt $deadline)
            Require-State $state $handle $minimumReceipts $true
        }
    }
    foreach ($client in $clients) { Require-State (Command $client 'conflicts') $handle 2 $true }
    Capture 'decisive'
}
catch {
    $failure = $_.Exception.Message
    Save-Json 'first-failure.json' @{ message=$failure; utc=[DateTime]::UtcNow.ToString('o') }
    if ($clients.Count -eq 0) {
        try {
            $partial = Invoke-Discover -ExpectedRunToken $RunToken -SerialStatusRequests -RequestTimeoutMilliseconds 30000 -PerProcessTimeoutMilliseconds 1000
            Save-Json 'partial-readiness.json' $partial
            $clients = @($partial.result.endpoints | Where-Object { $_.process.role -ceq 'client' })
        } catch { Save-Json 'partial-readiness-error.json' @{ message=$_.Exception.Message } }
    }
    try { Capture 'failure' } catch { Save-Json 'failure-capture-error.json' @{ message=$_.Exception.Message } }
}
finally {
    foreach ($client in $prepared) {
        try { Require-State (Command $client 'restore') $handle 0 $false }
        catch { $restoreErrors += $_.Exception.Message }
    }
    if ($prepared.Count -eq 2 -and $restoreErrors.Count -eq 0) {
        try { Capture 'restored' } catch { $restoreErrors += $_.Exception.Message }
    }
    try {
        # Retain all raw captures before deleting the exact fresh directory.
        Copy-Item -LiteralPath $raw -Destination (Join-Path $ArtifactDirectory 'raw-captures') -Recurse -ErrorAction Stop
        foreach ($file in @(Get-ChildItem -LiteralPath $raw -File)) {
            $copy = Join-Path (Join-Path $ArtifactDirectory 'raw-captures') $file.Name
            if ((Get-FileHash $file.FullName).Hash -cne (Get-FileHash $copy).Hash) { throw 'Raw capture retention hash differs.' }
        }
        Remove-Item -LiteralPath $raw -Recurse -ErrorAction Stop
    } catch { $restoreErrors += "Raw capture retention: $($_.Exception.Message)" }
    Save-Json 'scenario-result.json' @{
        outcome=$(if ($null -eq $failure -and $restoreErrors.Count -eq 0) {'passed'} else {'failed'})
        sourceHead=$ExpectedHead; runToken=$RunToken; firstFailure=$failure; restoreErrors=$restoreErrors
        screenshots=$images; claims=@('production zero-to-nonzero binding','same-instance numeric lookup','received identical replay','rejected conflicts on both clients')
        coverageLimit='Native input and UI wiring are outside this registry change.'
    }
}
if ($null -ne $failure -or $restoreErrors.Count -ne 0) { exit 1 }
