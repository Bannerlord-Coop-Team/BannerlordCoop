[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]{1,64}$')][string]$RunToken,
    [Parameter(Mandatory=$true)][string]$ArtifactDirectory,
    [Parameter(Mandatory=$true)][string]$LiveTestClientScript,
    [Parameter(Mandatory=$true)][string]$RuntimeScript,
    [Parameter(Mandatory=$true)][string]$ExpectedHead,
    [Parameter(Mandatory=$true)][string]$ServerReceipt,
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
function Command($Endpoint, [string]$Name, [string[]]$Arguments = @()) {
    $script:ordinal++
    $reply = Invoke-LiveTestClientAction -RequestedAction Command -TargetProcessId ([int]$Endpoint.process.pid) `
        -RequestedCommandName $Name -RequestedArgumentsJson (ConvertTo-LiveTestArgumentsJson -Arguments $Arguments) -RequestTimeoutMilliseconds 30000
    Save-Json ("commands/{0:D3}-{1}-{2}.json" -f $script:ordinal,$Endpoint.process.pid,$Name) $reply
    $succeeded = $reply.Response.result.PSObject.Properties['succeeded']
    if ($reply.ExitCode -ne 0 -or -not $reply.Response.ok -or -not $reply.Response.result.found -or
        ($null -ne $succeeded -and $succeeded.Value -ne $true)) { throw "Command $Name failed: $($reply.Response.result.output)" }
    return [string]$reply.Response.result.output
}
function Read-Crime($Endpoint) {
    $output = Command $Endpoint 'coop.debug.crime.read' @($faction)
    $rows = @{}
    foreach ($line in ($output -split "`n")) {
        if ($line.Trim() -cmatch '^hero=([^;]+);faction=([^;]+);rating=([^;]+);war=(True|False)$') {
            if ($matches[2] -cne $faction) { throw 'Crime observation has a different faction.' }
            $rows[$matches[1]] = @{ rating=[double]::Parse($matches[3],[Globalization.CultureInfo]::InvariantCulture); war=($matches[4] -ceq 'True') }
        }
    }
    if ($rows.Count -ne 2) { throw "Expected two player crime rows: $output" }
    return $rows
}
function Assert-Crime([string]$Checkpoint, [double]$First, [double]$Second, [bool]$RequireWar=$false) {
    foreach ($endpoint in @($server)+$clients) {
        $deadline=[DateTime]::UtcNow.AddSeconds(60)
        do {
            $rows=Read-Crime $endpoint
            $ok=[Math]::Abs($rows[$hero1].rating-$First) -lt 0.001 -and [Math]::Abs($rows[$hero2].rating-$Second) -lt 0.001
            if ($RequireWar) { $ok=$ok -and $rows[$hero1].war }
            if ($ok) { break }
            Start-Sleep -Milliseconds 250
        } while ([DateTime]::UtcNow -lt $deadline)
        if (-not $ok) { throw "Crime convergence failed at $Checkpoint on $($endpoint.process.pid)." }
        Save-Json "observations/$Checkpoint-$($endpoint.process.pid).json" @{ sourceHead=$ExpectedHead; endpoint=$endpoint.process; faction=$faction; players=$rows }
    }
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
    $preflight=Command $server 'coop.debug.crime.preflight' | ConvertFrom-Json
    Save-Json 'preflight.json' $preflight
    $faction=[string]$preflight.faction
    if (@($preflight.caravans).Count -eq 0 -or @($preflight.players).Count -ne 2 -or
        @($preflight.players | Where-Object { $_.war -or $_.sameFaction }).Count -ne 0) { throw 'Preflight needs two peaceful foreign players and a Danustica-faction caravan.' }
    $heroIds=@()
    foreach ($client in $clients) {
        $identity=Command $client 'coop.debug.alley.my_hero_id'
        if ($identity -notmatch 'registry id: (\S+)') { throw 'Player hero registry id was not resolved.' }
        $heroIds += $matches[1]
    }
    $hero1=$heroIds[0]; $hero2=$heroIds[1]
    if ($hero1 -ceq $hero2) { throw 'Clients must have distinct heroes.' }
    $initial=Read-Crime $server
    foreach ($hero in $heroIds) { Command $server 'coop.debug.crime.apply' @($hero,$faction,(-$initial[$hero].rating).ToString([Globalization.CultureInfo]::InvariantCulture)) | Out-Null }
    Assert-Crime 'zero' 0 0
    Command $server 'coop.debug.crime.apply' @($hero1,$faction,'10') | Out-Null
    Assert-Crime 'isolation' 10 0
    Command $server 'coop.debug.crime.coerce' @($hero1,[string]$preflight.caravans[0]) | Out-Null
    $coerced=Read-Crime $server
    if ($coerced[$hero1].rating -le 10 -or $coerced[$hero2].rating -ne 0) { throw 'Production caravan coercion did not increase only the acting player.' }
    Assert-Crime 'coercion' $coerced[$hero1].rating 0
    Command $server 'coop.debug.crime.apply' @($hero1,$faction,(-$coerced[$hero1].rating).ToString([Globalization.CultureInfo]::InvariantCulture)) | Out-Null
    Command $server 'coop.debug.crime.apply' @($hero2,$faction,'20') | Out-Null
    Assert-Crime 'before-daily' 0 20
    $alleys=Command $server 'coop.debug.alley.list' @('town_ES1')
    Save-Json 'alley-baseline.json' @{ output=$alleys }
    if ($alleys -notmatch '\[0\].*owner=(.+)') { throw 'Danustica alley zero is unavailable.' }
    $alleyOwner=$matches[1].Trim()
    if ($alleyOwner -cne 'none') { throw 'Alley zero must be unowned for disposable verification.' }
    Command $server 'coop.debug.alley.set_owner' @('town_ES1','0',$hero1) | Out-Null
    $deadline=[DateTime]::UtcNow.AddSeconds(60)
    do {
        $daily=Command $server 'coop.debug.crime.preflight' | ConvertFrom-Json
        $firstDaily=@($daily.players | Where-Object { $_.hero -ceq $hero1 })[0].daily
        $secondDaily=@($daily.players | Where-Object { $_.hero -ceq $hero2 })[0].daily
        if ($firstDaily -gt 0 -and $secondDaily -lt 0) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($firstDaily -le 0 -or $secondDaily -ge 0) { throw 'Daily model must predict owned-alley gain and ordinary decay.' }
    Save-Json 'daily-model.json' $daily
    Command $server 'coop.debug.crime.daily_tick' | Out-Null
    Assert-Crime 'daily-alley-and-decay' $firstDaily (20+$secondDaily)
    Command $server 'coop.debug.alley.abandon' @('town_ES1','0') | Out-Null
    Command $server 'coop.debug.crime.apply' @($hero1,$faction,([double]$preflight.threshold+1).ToString([Globalization.CultureInfo]::InvariantCulture)) | Out-Null
    $threshold=Read-Crime $server
    Assert-Crime 'threshold-war' $threshold[$hero1].rating $threshold[$hero2].rating $true
    Capture 'decisive'
    $receipt=Get-Content -LiteralPath $ServerReceipt -Raw | ConvertFrom-Json
    $data=[IO.Path]::GetFullPath([string]$receipt.serverData)
    if ($receipt.runToken -cne $RunToken -or $data -notmatch '^[A-Za-z]:\\' -or
        (Split-Path -Leaf $data) -cne "server-data-trace-$RunToken") { throw 'Save data is outside the exact disposable server receipt.' }
    $config=Get-Content -LiteralPath (Join-Path $data 'server-config.json') -Raw | ConvertFrom-Json
    $saveName=[string]$config.SaveName
    if ([string]::IsNullOrWhiteSpace($saveName)) { $saveName='saveauto1' }
    if ($saveName -match '[\\/]' -or $saveName -in @('.','..')) { throw 'Invalid configured save name.' }
    $sav=Join-Path $data "Game Saves/$saveName.sav"
    $json=Join-Path $data "Game Saves/$saveName.json"
    $saveStarted=[DateTime]::UtcNow
    if ((Command $server 'coop.debug.dedicated_server.save') -cne 'Dedicated-server save started.') { throw 'Dedicated save did not start.' }
    $deadline=[DateTime]::UtcNow.AddSeconds(120)
    do {
        $saved=$false
        if ((Test-Path -LiteralPath $sav) -and (Test-Path -LiteralPath $json)) {
            $saved=(Get-Item $sav).Length -gt 0 -and (Get-Item $json).Length -gt 0 -and
                (Get-Item $sav).LastWriteTimeUtc -ge $saveStarted -and (Get-Item $json).LastWriteTimeUtc -ge $saveStarted
        }
        if ($saved) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $saved) { throw 'Fresh paired campaign/session save was not written.' }
    $session=Get-Content -LiteralPath $json -Raw | ConvertFrom-Json
    foreach ($hero in $heroIds) {
        $player=@($session.Players | Where-Object { $_.HeroId -ceq $hero })
        if ($player.Count -ne 1 -or [Math]::Abs($player[0].CrimeRatings.$faction-$threshold[$hero].rating) -ge 0.001) { throw 'Saved player crime dictionary differs.' }
    }
    New-Item -ItemType Directory -Path (Join-Path $ArtifactDirectory 'save') | Out-Null
    Copy-Item $sav (Join-Path $ArtifactDirectory 'save/campaign.sav')
    Copy-Item $json (Join-Path $ArtifactDirectory 'save/session-original.json')
    Save-Json 'save/receipt.json' @{ serverReceipt=$receipt; saveName=$saveName; startedUtc=$saveStarted.ToString('o'); savSha256=(Get-FileHash $sav).Hash; jsonSha256=(Get-FileHash $json).Hash }
    foreach ($client in $clients) {
        Command $client 'coop.debug.connection.disconnect' | Out-Null
        $deadline=[DateTime]::UtcNow.AddSeconds(60)
        $left=$false
        do {
            $status=Invoke-LiveTestClientAction -RequestedAction Status -TargetProcessId ([int]$client.process.pid) -RequestTimeoutMilliseconds 30000
            Save-Json ("disconnect-{0:D3}-{1}.json" -f (++$script:ordinal),$client.process.pid) $status
            $left=$status.ExitCode -eq 0 -and $status.Response.ok -and $status.Response.process.runToken -ceq $RunToken -and
                [int]$status.Response.process.pid -eq [int]$client.process.pid -and
                $status.Response.result.campaignLoaded -eq $false -and $status.Response.result.coopRunning -eq $false -and
                $status.Response.result.activeState -match '(^|\.)InitialState$'
            if ($left) { break }
            Start-Sleep -Milliseconds 250
        } while ([DateTime]::UtcNow -lt $deadline)
        if (-not $left) { throw 'Client teardown did not reach its token-bound initial state.' }
    }
    $shutdown=Invoke-LiveTestClientAction -RequestedAction Shutdown -TargetProcessId ([int]$server.process.pid) -RequestTimeoutMilliseconds 30000
    Save-Json 'save/server-shutdown.json' $shutdown
    if ($shutdown.ExitCode -ne 0 -or -not $shutdown.Response.ok) { throw 'Saved authority shutdown failed.' }
    $deadline=[DateTime]::UtcNow.AddSeconds(60)
    do {
        $remaining=@(Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -eq [int]$receipt.pid -or $_.ProcessId -eq [int]$server.process.pid })
        if ($remaining.Count -eq 0) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($remaining.Count -ne 0) { throw 'Original authority remains before saved reload.' }
    # A stale saved clan id exercises the production registered-player replacement branch.
    $replacement=@($session.Players | Where-Object { $_.HeroId -ceq $hero1 })[0]
    $originalClan=[string]$replacement.ClanId
    $replacement.ClanId="missing-clan-$RunToken"
    [IO.File]::WriteAllText($json,($session | ConvertTo-Json -Depth 64),[Text.UTF8Encoding]::new($false))
    Copy-Item $json (Join-Path $ArtifactDirectory 'save/session-replacement-input.json')
    $exe=Join-Path (Split-Path -Parent $data) 'BannerlordCoopServer.exe'
    $exeHash=(Get-FileHash $exe).Hash
    $stdin=Join-Path $ArtifactDirectory 'save/reload-stdin.txt'
    [IO.File]::Open($stdin,[IO.FileMode]::CreateNew).Dispose()
    # Reuse the prepared dedicated executable and its existing hidden launch arguments.
    $reload=Start-Process -FilePath $exe -ArgumentList @('--no-tui','--data-dir',('"'+$data+'"'),'--coop-test-run',$RunToken) `
        -WorkingDirectory (Split-Path -Parent $exe) -WindowStyle Hidden -RedirectStandardInput $stdin `
        -RedirectStandardOutput (Join-Path $ArtifactDirectory 'save/reload.stdout.log') `
        -RedirectStandardError (Join-Path $ArtifactDirectory 'save/reload.stderr.log') -PassThru
    Save-Json 'save/reload-process.json' @{ pid=$reload.Id; startedUtc=$reload.StartTime.ToUniversalTime().ToString('o'); runToken=$RunToken; serverData=$data; executableSha256=$exeHash }
    $deadline=[DateTime]::UtcNow.AddMilliseconds(600000)
    $serving=$false
    do {
        $engines=@(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" | Where-Object {
            $_.CommandLine -like '*TaleWorlds.Starter.DotNetCore.dll*' -and $_.CommandLine -like '*DedicatedServer.Windows*' -and
            $_.CommandLine -match ('(?i)(?:^|\s)' + [regex]::Escape($RunToken) + '(?:\s|$|\")')
        })
        if ($engines.Count -eq 1) {
            $reply=Invoke-LiveTestClientAction -RequestedAction Status -TargetProcessId ([int]$engines[0].ProcessId) -RequestTimeoutMilliseconds 30000
            $serving=$reply.ExitCode -eq 0 -and $reply.Response.ok -and $reply.Response.process.runToken -ceq $RunToken -and $reply.Response.result.readyForCampaignTests
            if ($serving) { $server=$reply.Response; break }
        }
        if ($reload.HasExited) { throw 'Saved authority exited before readiness.' }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $serving) { throw 'Saved authority did not become ready within cold endpoint budget.' }
    Save-Json 'save/reloaded-server-status.json' $reply
    $restored=Command $server 'coop.debug.crime.preflight' | ConvertFrom-Json
    Save-Json 'save/restored-player-registration.json' $restored
    $restoredPlayer=@($restored.players | Where-Object { $_.hero -ceq $hero1 })
    if ($restoredPlayer.Count -ne 1 -or $restoredPlayer[0].clan -cne $originalClan) { throw 'Production saved-player replacement did not restore the clan registration.' }
    foreach ($client in $clients) { Command $client 'coop.debug.connection.reconnect' | Out-Null }
    $ready=@(Wait-LiveTestReadiness -ExpectedRunToken $RunToken -ClientCount 2 -ExpectedClientPlatformIds @('testclient','testclient2') `
        -ReadyFor Campaign -RegisteredPlayerCount 2 -ConnectedPlayerCount 2 -ClientsOnly -AllowLegacyServerConnectionProbe `
        -EndpointStartupTimeoutMilliseconds 600000 -TimeoutMilliseconds 180000)
    Save-Json 'save/rejoined-readiness.json' $ready
    Assert-Crime 'saved-reload-replacement-rejoin' $threshold[$hero1].rating $threshold[$hero2].rating $true
    Capture 'final'
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
    try {
        Copy-Item -LiteralPath $raw -Destination (Join-Path $ArtifactDirectory 'raw-captures') -Recurse -ErrorAction Stop
        foreach ($file in @(Get-ChildItem -LiteralPath $raw -File)) {
            $copy=Join-Path (Join-Path $ArtifactDirectory 'raw-captures') $file.Name
            if ((Get-FileHash $file.FullName).Hash -cne (Get-FileHash $copy).Hash) { throw 'Raw capture retention hash differs.' }
        }
        Remove-Item -LiteralPath $raw -Recurse -ErrorAction Stop
    } catch { $restoreErrors += "Raw capture retention: $($_.Exception.Message)" }
    Save-Json 'scenario-result.json' @{
        outcome=$(if ($null -eq $failure -and $restoreErrors.Count -eq 0) {'passed'} else {'failed'})
        sourceHead=$ExpectedHead; runToken=$RunToken; firstFailure=$failure; restoreErrors=$restoreErrors; screenshots=$images
        claims=@('per-player isolation','production caravan coercion','owned-alley gain and ordinary decay','threshold war','actual paired save, authority reload, registered-player replacement and two-client rejoin')
        coverageLimit='Notification UI and native input wiring remain unverified. Screenshots document rendered endpoints only; numerical behavior requires retained server/client data.'
    }
}
if ($null -ne $failure -or $restoreErrors.Count -ne 0) { exit 1 }
