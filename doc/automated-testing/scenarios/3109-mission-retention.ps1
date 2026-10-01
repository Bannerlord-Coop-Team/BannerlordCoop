[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ArtifactDirectory,
    [Parameter(Mandatory)] [string] $RawCaptureRoot,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_-]{1,64}$')] [string] $RunToken,
    [Parameter(Mandatory)] [ValidatePattern('^[a-f0-9]{40}$')] [string] $ExpectedHead,
    [Parameter(Mandatory)] [ValidatePattern('^[a-f0-9]{40}$')] [string] $ExpectedTree,
    [switch] $SelfCheck
)

$ErrorActionPreference = 'Stop'
if ($env:CODEX_HIDDEN_WINDOWS_CHILD_MARKER) {
    [System.IO.File]::Open($env:CODEX_HIDDEN_WINDOWS_CHILD_MARKER, [System.IO.FileMode]::CreateNew).Dispose()
}
Add-Type -AssemblyName System.Drawing
. '\\wsl.localhost\Ubuntu\home\pwisorlowska\.codex\skills\issue-to-pr\scripts\live_test_client.ps1'

$script:startedUtc = [DateTime]::UtcNow.ToString('o')
$script:firstFailure = $null
$script:images = @()
$script:client1 = $null
$script:client2 = $null
$script:server = $null
$script:fixtureActive = $false
$script:restored = $false
$script:bodyPassed = $false
$script:baselines = @{}
$script:completed = @{client1=0;client2=0}
$script:originalPositions = @{}
$script:tournamentActive = $false
$script:rawCaptureCreated = $false

function Save-Json([string] $Name, [object] $Value) {
    $path = Join-Path $ArtifactDirectory $Name
    if (Test-Path -LiteralPath $path) { throw "artifact already exists: $path" }
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    [IO.File]::WriteAllText($path, (($Value | ConvertTo-Json -Depth 64) + "`n"), [Text.UTF8Encoding]::new($false))
}

function Fail-Once([string] $Stage, [object] $ErrorValue) {
    if ($null -ne $script:firstFailure) { return }
    $script:firstFailure = [ordered]@{ stage = $Stage; message = [string]$ErrorValue; utc = [DateTime]::UtcNow.ToString('o') }
    try { Save-Json 'first-failure.json' $script:firstFailure } catch {}
}

function Command([string] $Name, [object] $Endpoint, [string] $FullName, [string[]] $Arguments = @(), [switch] $Structured, [string] $ExpectedOutputPattern = '') {
    $reply = Invoke-LiveTestClientAction -RequestedAction Command -TargetProcessId ([int]$Endpoint.process.pid) `
        -RequestedCommandName $FullName -RequestedArgumentsJson (ConvertTo-Json -InputObject @($Arguments) -Compress) `
        -RequestTimeoutMilliseconds 30000
    Save-Json "$Name.json" $reply.Response
    if ($reply.ExitCode -ne 0 -or -not [bool]$reply.Response.ok -or -not [bool]$reply.Response.result.found -or
        ($null -ne $reply.Response.result.PSObject.Properties['succeeded'] -and
            $reply.Response.result.succeeded -ceq $false)) {
        throw "$Name failed: $($reply.Response.error | ConvertTo-Json -Compress) $($reply.Response.result.output)"
    }
    if ($Structured -and (-not [bool]$reply.Response.result.hasStructuredResult -or $null -eq $reply.Response.result.structuredResult)) {
        throw "$Name has no structured result"
    }
    if ($ExpectedOutputPattern -and [string]$reply.Response.result.output -notmatch $ExpectedOutputPattern) {
        throw "$Name returned unexpected output: $($reply.Response.result.output)"
    }
    return $reply.Response.result
}

function Wait-Fixture([string] $Name, [scriptblock] $Predicate, [int] $Seconds = 120) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    $polls = @()
    do {
        $reply = Invoke-LiveTestClientAction -RequestedAction Command -TargetProcessId ([int]$script:server.process.pid) `
            -RequestedCommandName 'coop.debug.map_event.late_join_mode_fixture_state' `
            -RequestedArgumentsJson '["testclient","testclient2"]' -RequestTimeoutMilliseconds 30000
        $polls += $reply.Response
        if ($reply.ExitCode -eq 0 -and [bool]$reply.Response.ok -and [bool]$reply.Response.result.found) {
            $lines = @(([string]$reply.Response.result.output) -split '\r?\n' |
                Where-Object { $_.StartsWith('LIVE_TEST_JSON=', [StringComparison]::Ordinal) })
            if ($lines.Count -eq 1) {
                $state = $lines[0].Substring('LIVE_TEST_JSON='.Length) | ConvertFrom-Json -ErrorAction Stop
                if ([bool](& $Predicate $state)) {
                    Save-Json "$Name-polls.json" $polls
                    return $state
                }
            }
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    Save-Json "$Name-polls.json" $polls
    throw "$Name did not converge"
}

function Catalog([string] $Name, [object] $Endpoint, [string[]] $Required) {
    $reply = Invoke-LiveTestClientAction -RequestedAction CommandCatalog -TargetProcessId ([int]$Endpoint.process.pid) -RequestTimeoutMilliseconds 30000
    Save-Json "$Name-catalog.json" $reply.Response
    if ($reply.ExitCode -ne 0 -or -not [bool]$reply.Response.ok -or
        @($Required | Where-Object { $_ -notin @($reply.Response.result.commands) }).Count -ne 0) { throw "$Name command catalog incomplete" }
}

function Wait-Deployment([string] $Name, [object] $Endpoint, [int] $Seconds = 120) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    $polls = @()
    do {
        $reply = Invoke-LiveTestClientAction -RequestedAction Command -TargetProcessId ([int]$Endpoint.process.pid) `
            -RequestedCommandName 'coop.debug.map_event.deployment_state' -RequestedArgumentsJson '[]' -RequestTimeoutMilliseconds 30000
        $polls += $reply.Response
        if ($reply.ExitCode -eq 0 -and [bool]$reply.Response.ok -and
            [string]$reply.Response.result.output -match 'teamSetupOver=True.*handler=True') {
            Save-Json "$Name-polls.json" $polls
            return
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    Save-Json "$Name-polls.json" $polls
    throw "$Name did not become ready"
}

function Wait-Campaign([string] $Name, [object] $Endpoint, [int] $Seconds = 120) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    $polls = @()
    $stable = 0
    do {
        $reply = Invoke-LiveTestClientAction -RequestedAction Status -TargetProcessId ([int]$Endpoint.process.pid) -RequestTimeoutMilliseconds 30000
        $polls += $reply.Response
        if ($reply.ExitCode -eq 0 -and [bool]$reply.Response.ok -and
            [bool]$reply.Response.result.readyForCampaignTests -and -not [bool]$reply.Response.result.missionActive -and
            $null -ne $reply.Response.result.PSObject.Properties['gameThreadQueueDepth'] -and
            [int]$reply.Response.result.gameThreadQueueDepth -eq 0) {
            $stable++
        } else { $stable=0 }
        if($stable -ge 3) {
            Save-Json "$Name-polls.json" $polls
            return
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    Save-Json "$Name-polls.json" $polls
    throw "$Name did not return to campaign"
}

function Capture([string] $Stage) {
    if ($null -eq $script:client1 -or $null -eq $script:client2) { throw 'both client endpoints are required for screenshots' }
    $captures = @(
        [pscustomobject]@{ ProcessId = [int]$script:client1.process.pid; Path = (Join-Path $RawCaptureRoot "$Stage-client1.bmp"); role = 'client1' },
        [pscustomobject]@{ ProcessId = [int]$script:client2.process.pid; Path = (Join-Path $RawCaptureRoot "$Stage-client2.bmp"); role = 'client2' }
    )
    $batch = Invoke-LiveTestScreenshotBatch -Captures $captures -TimeoutMilliseconds 60000
    Save-Json "screenshots/$Stage-batch.json" $batch
    foreach ($capture in $captures) {
        $entry = @($batch | Where-Object { [int]$_.processId -eq $capture.ProcessId })[0]
        if ($null -eq $entry -or -not [bool]$entry.response.ok -or -not [bool]$entry.response.result.captureComplete -or
            [string]$entry.response.result.captureId -cne [string]$entry.response.result.completionCaptureId) {
            throw "incomplete screenshot: $Stage/$($capture.role)"
        }
        $relative = "screenshots/$Stage-$($capture.role).png"
        $png = Join-Path $ArtifactDirectory $relative
        $converted = Convert-LiveTestScreenshotToPng -BmpPath ([string]$entry.path) -PngPath $png
        $bitmap = [System.Drawing.Bitmap]::new($png)
        try {
            $rendered = @((50, 250, 640, 1000, 1230) | Where-Object {
                $pixel = $bitmap.GetPixel($_, 50)
                $pixel.R -gt 8 -or $pixel.G -gt 8 -or $pixel.B -gt 8
            }).Count -gt 0
            if (-not $rendered) { throw "screenshot still shows a loading screen: $Stage/$($capture.role)" }
        } finally { $bitmap.Dispose() }
        $script:images += [ordered]@{
            path = $relative; sha256 = $converted.sha256; role = $capture.role; checkpoint = $Stage
            processId = [int]$capture.ProcessId; captureId = [string]$entry.response.result.captureId
            completionObservedUtc = [string]$entry.response.result.completionObservedUtc
            width = $converted.width; height = $converted.height
        }
    }
}

function Json-Output([object]$Result) {
    if ($Result.hasStructuredResult) { return $Result.structuredResult }
    return ([string]$Result.output | ConvertFrom-Json -ErrorAction Stop)
}
function Assert-Sample([object]$Baseline, [object]$Sample, [int]$Count) {
    foreach ($field in @('observationSession','processId','processStartedUtc')) {
        if ([string]$Baseline.retention.$field -cne [string]$Sample.retention.$field) { throw "retention identity changed: $field" }
    }
    if (($Baseline.shared | ConvertTo-Json -Compress) -cne ($Sample.shared | ConvertTo-Json -Compress)) { throw 'shared services changed' }
    if (-not $Sample.retention.diagnosticFullCollection -or [long]$Sample.retention.managedHeapBytes -le 0 -or
        [int]$Sample.retention.observedMissionCount -ne $Count) { throw 'missing comparable full collection or completed count' }
    foreach ($cycle in $Sample.retention.cycles) {
        if ($cycle.controllerAlive -or @($cycle.types | Where-Object { $_.alive -ne 0 }).Count -ne 0) { throw "ended graph retained: cycle $($cycle.cycle)" }
    }
}
function Sample([string]$Name, [string]$Role, [object]$Endpoint) {
    Wait-Campaign "$Name-idle" $Endpoint
    $sample = Json-Output (Command "$Name-collect" $Endpoint 'coop.debug.mission.retention_collect')
    Save-Json "$Name-data.json" $sample
    if (-not $script:baselines.ContainsKey($Role)) { $script:baselines[$Role]=$sample }
    Assert-Sample $script:baselines[$Role] $sample $script:completed[$Role]
    return $sample
}
function Track-Mission([string]$Name, [object]$Endpoint, [string]$Type, [int]$Count) {
    $deadline=[DateTime]::UtcNow.AddSeconds(120); $polls=@()
    do {
        $reply=Invoke-LiveTestClientAction -RequestedAction Status -TargetProcessId ([int]$Endpoint.process.pid) -RequestTimeoutMilliseconds 30000
        $polls += $reply.Response
        if ($reply.ExitCode -eq 0 -and $reply.Response.ok -and $reply.Response.result.readyForMissionTests) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    Save-Json "$Name-native-readiness.json" $polls
    if (-not $reply.Response.result.readyForMissionTests) { throw "$Name native mission not ready" }
    $data=Json-Output (Command "$Name-track" $Endpoint 'coop.debug.mission.retention_track')
    Save-Json "$Name-observations.json" $data
    if ([int]$data.observedMissionCount -ne $Count) { throw 'expected one new real mission controller' }
    $cycle=@($data.cycles)[-1]
    if ($cycle.missionType -cne $Type -or -not $cycle.controllerAlive) { throw "wrong real mission controller: $($cycle.missionType)" }
    $types=@($cycle.types.type)
    if (@($types | Where-Object { $_ -match 'CoopMissionComponent$' }).Count -ne 1 -or
        @($types | Where-Object { $_ -match 'Handler' }).Count -eq 0 -or
        @($types | Where-Object { $_ -match 'Session' }).Count -eq 0 -or $types.Count -lt 3) { throw 'representative mission graph not observed' }
}
function Observe([string]$Name, [object]$Endpoint, [string]$Pattern) {
    $deadline=[DateTime]::UtcNow.AddSeconds(120);$index=0
    do {
        $result=Command "$Name-$index" $Endpoint 'coop.debug.tournaments.danustica_observe'
        if ([string]$result.output -match $Pattern) { return $result }
        $index++;Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "$Name tournament observation did not converge"
}
function Position([string]$Name, [string]$PartyId) {
    $result=Command $Name $script:server 'coop.debug.mobile_party.position' @($PartyId)
    $values=@{}
    foreach($field in ([string]$result.output -split '\|')) {
        if($field -match '^([^=]+)=(.*)$') { $values[$Matches[1]]=$Matches[2] }
    }
    return $values
}
if ($SelfCheck) {
    $baseline=@{retention=@{observationSession='s';processId=1;processStartedUtc='t'};shared=@{broker=1}}
    $sample=@{retention=@{observationSession='s';processId=1;processStartedUtc='t';diagnosticFullCollection=$true;managedHeapBytes=100;observedMissionCount=1;cycles=@(@{controllerAlive=$false;types=@(@{alive=0})})};shared=@{broker=1}}
    Assert-Sample $baseline $sample 1
    $sample.retention.cycles[0].types[0].alive=1
    $rejected=$false;try{Assert-Sample $baseline $sample 1}catch{$rejected=$true}
    if(-not $rejected){throw 'retained graph accepted'}
    $sample.retention.cycles[0].types[0].alive=0;$sample.retention.processId=2
    $rejected=$false;try{Assert-Sample $baseline $sample 1}catch{$rejected=$true}
    if(-not $rejected){throw 'replacement process accepted'}
    'sample self-check passed, retained graph and changed process rejected';return
}
try {
    if ($RawCaptureRoot -notmatch '^[A-Za-z]:\\' -or $RawCaptureRoot -notmatch [regex]::Escape($RunToken) -or (Test-Path -LiteralPath $RawCaptureRoot)) { throw 'fresh Windows-local token capture root required' }
    New-Item -ItemType Directory -Path $RawCaptureRoot -ErrorAction Stop | Out-Null
    $script:rawCaptureCreated=$true
    $endpoints=@(Wait-LiveTestReadiness -ExpectedRunToken $RunToken -ClientCount 2 -ExpectedClientPlatformIds @('testclient','testclient2') -ReadyFor Campaign -AllowLegacyServerConnectionProbe -EndpointStartupTimeoutMilliseconds 600000 -TimeoutMilliseconds 180000)
    Save-Json 'readiness.json' $endpoints
    $script:server=@($endpoints | Where-Object { $_.process.role -eq 'server' })[0]
    $script:client1=@($endpoints | Where-Object { $_.process.role -eq 'client' -and $_.process.platformId -eq 'testclient' })[0]
    $script:client2=@($endpoints | Where-Object { $_.process.role -eq 'client' -and $_.process.platformId -eq 'testclient2' })[0]
    $clients=@(@{role='client1';controller='testclient';endpoint=$script:client1},@{role='client2';controller='testclient2';endpoint=$script:client2})
    foreach($endpoint in @($script:client1,$script:client2)) {
        if($null -eq $endpoint -or [string]$endpoint.result.buildVersion -notmatch [regex]::Escape($ExpectedHead)){throw 'exact client source missing'}
    }
    if($null -eq $script:server -or $script:server.process.role -cne 'server') { throw 'standalone server endpoint required' }
    Catalog 'server' $script:server @('coop.debug.players.party_state','coop.debug.mobile_party.position','coop.debug.mobile_party.restore_position','coop.debug.mobile_party.move_to_settlement','coop.debug.map_event.leave_settlement','coop.debug.map_event.late_join_mode_fixture','coop.debug.map_event.late_join_mode_fixture_state','coop.debug.map_event.late_join_mode_join','coop.debug.map_event.late_join_mode_enter','coop.debug.map_event.late_join_mode_exit_missions','coop.debug.map_event.late_join_mode_cleanup','coop.debug.tournaments.danustica_fixture_begin','coop.debug.tournaments.danustica_fixture_abort','coop.debug.tournaments.danustica_fixture_restore','coop.debug.tournaments.danustica_observe')
    foreach($c in $clients){
        Catalog $c.role $c.endpoint @('coop.debug.mission.retention_track','coop.debug.mission.retention_collect','coop.debug.location.enter','coop.debug.location.leave','coop.debug.map_event.finish_current_encounter','coop.debug.map_event.encounter_state','coop.debug.map_event.deployment_state','coop.debug.tournaments.danustica_request_join','coop.debug.tournaments.danustica_request_start','coop.debug.tournaments.danustica_request_leave','coop.debug.tournaments.danustica_observe')
        $party=Json-Output (Command "$($c.role)-party" $script:server 'coop.debug.players.party_state' @($c.controller))
        if($party.controllerId -cne $c.controller -or -not $party.connected -or -not $party.active -or $party.mapEvent -cne 'none'){throw 'connected idle player required'}
        $c.partyId=[string]$party.partyId
        $pos=Position "$($c.role)-original-position" $c.partyId
        if($pos.settlement -cne 'none' -or $pos.moveMode -cne 'Hold'){throw 'restorable map Hold state required before mutation'}
        $script:originalPositions[$c.controller]=$pos
        $null=Sample "$($c.role)-baseline" $c.role $c.endpoint
    }
    Capture 'baseline-campaign'
    for($round=0;$round -lt 4;$round++){
        $prefix="battle-$round"
        $null=Command "$prefix-create" $script:server 'coop.debug.map_event.late_join_mode_fixture' @('testclient','testclient2')
        $script:fixtureActive=$true
        $null=Wait-Fixture "$prefix-first" {param($s)$s.firstInMission}
        $null=Command "$prefix-join" $script:server 'coop.debug.map_event.late_join_mode_join'
        $null=Command "$prefix-enter" $script:server 'coop.debug.map_event.late_join_mode_enter'
        $null=Wait-Fixture "$prefix-both" {param($s)$s.firstInMission -and $s.joiningInMission -and $s.firstMapEventId -ceq $s.joiningMapEventId}
        foreach($c in $clients){Wait-Deployment "$prefix-$($c.role)" $c.endpoint;Track-Mission "$prefix-$($c.role)" $c.endpoint 'Missions.Battles.CoopBattleController' ($script:completed[$c.role]+1)}
        $null=Command "$prefix-exit" $script:server 'coop.debug.map_event.late_join_mode_exit_missions'
        foreach($c in $clients){Wait-Campaign "$prefix-$($c.role)-exit" $c.endpoint}
        $null=Command "$prefix-cleanup" $script:server 'coop.debug.map_event.late_join_mode_cleanup';$script:fixtureActive=$false
        $null=Wait-Fixture "$prefix-restored" {param($s)$s.restored}
        foreach($c in $clients){$script:completed[$c.role]++;$null=Sample "$prefix-$($c.role)-postgc" $c.role $c.endpoint}
    }
    foreach($c in $clients){$null=Command "$($c.role)-move-danustica" $script:server 'coop.debug.mobile_party.move_to_settlement' @($c.partyId,'town_ES1','true')}
    foreach($c in $clients){
        $deadline=[DateTime]::UtcNow.AddSeconds(120);$index=0
        do{$pos=Position "$($c.role)-danustica-$index" $c.partyId;if($pos.settlement -ceq 'town_ES1'){break};$index++;Start-Sleep -Milliseconds 500}while([DateTime]::UtcNow -lt $deadline)
        if($pos.settlement -cne 'town_ES1'){throw 'authoritative Danustica arrival missing'}
    }
    for($round=0;$round -lt 4;$round++){
        foreach($c in $clients){
            $prefix="location-$round-$($c.role)"
            $entry=Command "$prefix-enter" $c.endpoint 'coop.debug.location.enter' @('Location_town_ES1_tavern')
            if([string]$entry.output -match 'Started a local encounter'){$null=Command "$prefix-open" $c.endpoint 'coop.debug.location.enter' @('Location_town_ES1_tavern')}
            Track-Mission $prefix $c.endpoint 'Missions.Taverns.CoopLocationsController' ($script:completed[$c.role]+1)
            $null=Command "$prefix-leave" $c.endpoint 'coop.debug.location.leave'
            Wait-Campaign "$prefix-exit" $c.endpoint
            $null=Command "$prefix-finish-encounter" $c.endpoint 'coop.debug.map_event.finish_current_encounter'
            $script:completed[$c.role]++;$null=Sample "$prefix-postgc" $c.role $c.endpoint
        }
    }
    foreach($c in $clients){$null=Command "$($c.role)-tournament-arrival" $script:server 'coop.debug.mobile_party.move_to_settlement' @($c.partyId,'town_ES1','true')}
    foreach($c in $clients){
        $deadline=[DateTime]::UtcNow.AddSeconds(120);$index=0
        do{$pos=Position "$($c.role)-tournament-arrival-$index" $c.partyId;if($pos.settlement -ceq 'town_ES1'){break};$index++;Start-Sleep -Milliseconds 500}while([DateTime]::UtcNow -lt $deadline)
        if($pos.settlement -cne 'town_ES1'){throw 'authoritative tournament arrival missing'}
    }
    for($round=0;$round -lt 4;$round++){
        $prefix="tournament-$round"
        $preflight=Command "$prefix-native-preflight" $script:server 'coop.debug.tournaments.danustica_observe'
        if([string]$preflight.output -notmatch 'nativeType=none\|' -or [string]$preflight.output -notmatch '(?m)^session=none'){throw 'Danustica needs no preexisting tournament/session'}
        $begin=Command "$prefix-begin" $script:server 'coop.debug.tournaments.danustica_fixture_begin';$script:tournamentActive=$true
        if([string]$begin.output -notmatch 'created=True'){throw 'fixture-owned tournament required for abort/restore'}
        foreach($c in $clients){$null=Command "$prefix-$($c.role)-join" $c.endpoint 'coop.debug.tournaments.danustica_request_join';$null=Observe "$prefix-$($c.role)-joined" $c.endpoint 'phase=Preparation'}
        $null=Observe "$prefix-both-joined" $script:server 'humans=2\|'
        $null=Command "$prefix-start" $script:client1 'coop.debug.tournaments.danustica_request_start'
        foreach($c in $clients){
            $null=Observe "$prefix-$($c.role)-native" $c.endpoint 'nativeBracketReady=True'
            Track-Mission "$prefix-$($c.role)" $c.endpoint 'Missions.Tournaments.CoopTournamentController' ($script:completed[$c.role]+1)
        }
        foreach($c in $clients){Track-Mission "$prefix-$($c.role)-before-exit" $c.endpoint 'Missions.Tournaments.CoopTournamentController' ($script:completed[$c.role]+1)}
        foreach($c in $clients){$null=Command "$prefix-$($c.role)-leave" $c.endpoint 'coop.debug.tournaments.danustica_request_leave';Wait-Campaign "$prefix-$($c.role)-exit" $c.endpoint}
        $state=Command "$prefix-observe-session" $script:server 'coop.debug.tournaments.danustica_observe'
        if([string]$state.output -notmatch '(?m)^session=none'){$null=Command "$prefix-abort" $script:server 'coop.debug.tournaments.danustica_fixture_abort'}
        $null=Command "$prefix-restore" $script:server 'coop.debug.tournaments.danustica_fixture_restore';$script:tournamentActive=$false
        foreach($c in $clients){$null=Observe "$prefix-$($c.role)-removed" $c.endpoint '(?m)^session=none';$script:completed[$c.role]++;$null=Sample "$prefix-$($c.role)-postgc" $c.role $c.endpoint}
    }
    Capture 'decisive-retention-collected'
    Save-Json 'completed-native-cycles.json' @{perClient=$script:completed;warmupPerKind=1;measuredPerKind=3;kinds=@('battle','location','tournament');proof='native mission readiness, attached controllers, acknowledged exits and idle convergence';preFixLiveBaselineAvailable=$false}
    $script:bodyPassed=$true
} catch {
    Fail-Once 'retention-scenario' $_.Exception.Message
    foreach ($record in @(Get-LiveTestEndpointRecords -ExpectedRunToken $RunToken)) {
        try {
            $status = Invoke-LiveTestClientAction -RequestedAction Status -TargetProcessId ([int]$record.pid) -RequestTimeoutMilliseconds 3000
            Save-Json "failure-status-$($record.pid).json" $status.Response
        } catch { Save-Json "failure-status-$($record.pid)-error.json" @{error=$_.Exception.Message} }
    }
    if($null -ne $script:client1 -and $null -ne $script:client2){try{Capture 'decisive-first-failure'}catch{Save-Json 'failure-screenshot-error.json' @{error=$_.Exception.Message}}}
} finally {
    try{
        foreach($c in $clients){
            $status=Invoke-LiveTestClientAction -RequestedAction Status -TargetProcessId ([int]$c.endpoint.process.pid) -RequestTimeoutMilliseconds 30000
            Save-Json "cleanup-$($c.role)-status.json" $status.Response
            if($status.ExitCode -ne 0 -or -not $status.Response.ok){throw 'cleanup status failed'}
            if($status.Response.result.missionActive -and -not $script:fixtureActive -and -not $script:tournamentActive){$null=Command "cleanup-$($c.role)-native-exit" $c.endpoint 'coop.debug.location.leave';Wait-Campaign "cleanup-$($c.role)-native-idle" $c.endpoint}
        }
        if($script:fixtureActive){$null=Command 'cleanup-battle-exit' $script:server 'coop.debug.map_event.late_join_mode_exit_missions';foreach($c in $clients){Wait-Campaign "cleanup-$($c.role)-battle" $c.endpoint};$null=Command 'cleanup-battle-fixture' $script:server 'coop.debug.map_event.late_join_mode_cleanup';$script:fixtureActive=$false}
        if($script:tournamentActive){
            foreach($c in $clients){$null=Command "cleanup-$($c.role)-tournament-leave" $c.endpoint 'coop.debug.tournaments.danustica_request_leave';Wait-Campaign "cleanup-$($c.role)-tournament-idle" $c.endpoint}
            $state=Command 'cleanup-tournament-state' $script:server 'coop.debug.tournaments.danustica_observe'
            if([string]$state.output -notmatch '(?m)^session=none'){$null=Command 'cleanup-tournament-abort' $script:server 'coop.debug.tournaments.danustica_fixture_abort'}
            $null=Command 'cleanup-tournament-restore' $script:server 'coop.debug.tournaments.danustica_fixture_restore';$script:tournamentActive=$false
        }
        foreach($c in $clients){
            if(-not $script:originalPositions.ContainsKey($c.controller)){continue}
            $encounter=Command "cleanup-$($c.role)-encounter" $c.endpoint 'coop.debug.map_event.encounter_state'
            if([string]$encounter.output -match '(?m)^PlayerEncounter.Current: PRESENT'){
                $null=Command "cleanup-$($c.role)-finish-encounter" $c.endpoint 'coop.debug.map_event.finish_current_encounter'
                Wait-Campaign "cleanup-$($c.role)-finished-encounter" $c.endpoint
                $after=Command "cleanup-$($c.role)-encounter-after" $c.endpoint 'coop.debug.map_event.encounter_state'
                if([string]$after.output -notmatch '(?m)^PlayerEncounter.Current: <null>'){throw 'local encounter remains'}
            }elseif([string]$encounter.output -notmatch '(?m)^PlayerEncounter.Current: <null>'){throw 'encounter presence observation missing'}
            $pos=Position "cleanup-$($c.role)-position" $c.partyId
            if($pos.settlement -cne 'none'){$null=Command "cleanup-$($c.role)-leave-settlement" $script:server 'coop.debug.map_event.leave_settlement' @($c.controller)}
            $original=$script:originalPositions[$c.controller]
            $null=Command "cleanup-$($c.role)-restore-position" $script:server 'coop.debug.mobile_party.restore_position' @($c.partyId,$original.x,$original.y,$original.isOnLand)
            Wait-Campaign "cleanup-$($c.role)-idle" $c.endpoint
            $restored=Position "cleanup-$($c.role)-restored" $c.partyId
            foreach($field in @('x','y','isOnLand','moveMode','settlement')){if($restored[$field] -cne $original[$field]){throw "party restoration mismatch: $field"}}
        }
        if($script:originalPositions.Count -eq 2){Capture 'final-restored-campaign';$script:restored=$true}
    }catch{Fail-Once 'fixture-cleanup' $_.Exception.Message}
    # Readiness can fail before endpoint assignment; retain token-bound native logs before shutdown.
    try {
        . '\\wsl.localhost\Ubuntu\home\pwisorlowska\.codex\skills\issue-to-pr\scripts\remote_live_runtime.ps1'
        $state = @{runToken=$RunToken;startedUtc=$script:startedUtc}
        $pidPath = '\\wsl.localhost\Ubuntu\home\pwisorlowska\.codex\runtime\issue-to-pr\local-dedicated-server\live-server.pid'
        if (Test-Path -LiteralPath $pidPath) {
            $pidState = Get-Content -LiteralPath $pidPath -Raw | ConvertFrom-Json
            if ($pidState.runToken -ceq $RunToken) { $state.serverData = $pidState.serverData; $state.startedUtc = $pidState.startedUtc }
        }
        if ($null -ne $script:firstFailure) {
            . '\\wsl.localhost\Ubuntu\home\pwisorlowska\.codex\skills\issue-to-pr\scripts\remote_live_process.ps1'
            # Bound dump collection separately so a stalled client cannot strand cleanup.
            $stateBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($state | ConvertTo-Json -Compress)))
            $destinationBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((Join-Path $ArtifactDirectory 'startup-diagnostics')))
            $command = @"
`$ErrorActionPreference = 'Stop'
. '\\wsl.localhost\Ubuntu\home\pwisorlowska\.codex\skills\issue-to-pr\scripts\remote_live_runtime.ps1'
`$state = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('$stateBase64')) | ConvertFrom-Json
`$destination = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('$destinationBase64'))
`$result = Copy-RemoteLiveRuntimeDiagnostics -State `$state -DestinationRoot `$destination -CaptureStartupMiniDump
`$result | ConvertTo-Json -Depth 32
if (`$result.errorCount -ne 0) { exit 1 }
"@
            $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
            try {
                $dumpResult = Invoke-RemoteLiveProcess -FileName (Join-Path $PSHOME 'powershell.exe') `
                    -Arguments "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand $encoded" -TimeoutSeconds 60 `
                    -StandardOutputPath (Join-Path $ArtifactDirectory 'startup-diagnostics.stdout.log') `
                    -StandardErrorPath (Join-Path $ArtifactDirectory 'startup-diagnostics.stderr.log') `
                    -TimeoutDescription 'token-bound startup stack collection'
                Save-Json 'startup-diagnostics-process.json' $dumpResult
            } catch { Save-Json 'startup-diagnostics-error.json' @{error=$_.Exception.Message} }
        }
        $diagnostics = Copy-RemoteLiveRuntimeDiagnostics -State $state -DestinationRoot (Join-Path $ArtifactDirectory 'runtime-diagnostics')
        if ($diagnostics.errorCount -ne 0) { throw 'runtime diagnostic collection incomplete' }
    } catch { Fail-Once 'runtime-diagnostics' $_.Exception.Message }
    foreach ($endpoint in @($script:client1,$script:client2,$script:server)) {
        if ($null -ne $endpoint -and [string]$endpoint.result.logPath) {
            try { Copy-Item -LiteralPath $endpoint.result.logPath -Destination (Join-Path $ArtifactDirectory ('runtime-' + $endpoint.process.pid + '.log')) -ErrorAction Stop } catch { Fail-Once 'log-retention' $_.Exception.Message }
        }
        if ($null -eq $endpoint) { continue }
        try {
            $reply=Invoke-LiveTestClientAction -RequestedAction Shutdown -TargetProcessId ([int]$endpoint.process.pid) -RequestTimeoutMilliseconds 30000
            Save-Json ('shutdown-' + $endpoint.process.pid + '.json') $reply.Response
            if ($reply.ExitCode -ne 0) { throw 'shutdown failed' }
        } catch { Fail-Once 'shutdown' $_.Exception.Message }
    }
    $retainedRaw=$false
    if ($script:rawCaptureCreated) {
        try {
            $destination=Join-Path $ArtifactDirectory 'raw-screenshots'
            Copy-Item -LiteralPath $RawCaptureRoot -Destination $destination -Recurse -ErrorAction Stop
            foreach ($raw in @(Get-ChildItem -LiteralPath $RawCaptureRoot -File)) {
                if ((Get-FileHash $raw.FullName).Hash -cne (Get-FileHash (Join-Path $destination $raw.Name)).Hash) { throw 'raw retention mismatch' }
            }
            $retainedRaw=$true
            Remove-Item -LiteralPath $RawCaptureRoot -Recurse -Force
        } catch { Fail-Once 'raw-retention' $_.Exception.Message }
    }
    $requiredPhases=@('baseline-campaign','decisive-retention-collected','final-restored-campaign')
    $missing=@($requiredPhases | ForEach-Object { $phase=$_; @('client1','client2') | Where-Object { $role=$_; @($script:images | Where-Object { $_.checkpoint -ceq $phase -and $_.role -ceq $role }).Count -ne 1 } })
    if ($missing.Count -gt 0 -and $script:bodyPassed) { Fail-Once 'planned-captures' 'missing planned client capture' }
    $accepted=$script:bodyPassed -and $script:restored -and $retainedRaw -and $null -eq $script:firstFailure
    Save-Json 'actions-summary.json' @{accepted=$accepted; bodyPassed=$script:bodyPassed; restored=$script:restored; firstFailure=$script:firstFailure; images=$script:images}
    $dataFiles=@(Get-ChildItem -LiteralPath $ArtifactDirectory -File -Recurse -Filter '*.json' | ForEach-Object {
        @{path=$_.FullName.Substring($ArtifactDirectory.TrimEnd('\').Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    })
    Save-Json 'result-manifest.json' @{schemaVersion=2;runToken=$RunToken;source=@{head=$ExpectedHead;tree=$ExpectedTree};expectedHead=$ExpectedHead;expectedTree=$ExpectedTree;accepted=$accepted;status=$(if($accepted){'passed'}else{'failed'});verdict=$(if($accepted){'passed'}else{'failed'});files=$script:images;dataFiles=$dataFiles;firstFailure=$script:firstFailure;dataPointers=@{completed='completed-native-cycles.json#/perClient';client1='tournament-3-client1-postgc-data.json#/retention';client2='tournament-3-client2-postgc-data.json#/retention'}}
    if (-not $accepted) { exit 1 }
}
