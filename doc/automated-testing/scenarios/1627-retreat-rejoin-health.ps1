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
Add-Type -AssemblyName System.Drawing
. '\\wsl.localhost\Ubuntu\home\pwisorlowska\.codex\skills\issue-to-pr\scripts\live_test_client.ps1'

$script:firstFailure = $null
$script:images = @()
$script:client1 = $null
$script:client2 = $null
$script:server = $null
$script:fixtureActive = $false
$script:cleanupSetup = $null
$script:restored = $false
$script:bodyPassed = $false
$script:mapEventId = $null
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
    do {
        $reply = Invoke-LiveTestClientAction -RequestedAction Status -TargetProcessId ([int]$Endpoint.process.pid) -RequestTimeoutMilliseconds 30000
        $polls += $reply.Response
        if ($reply.ExitCode -eq 0 -and [bool]$reply.Response.ok -and
            [bool]$reply.Response.result.readyForCampaignTests -and -not [bool]$reply.Response.result.missionActive) {
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

function Read-State([string]$Name, [object]$Endpoint, [string]$CommandName, [string[]]$CommandArguments = @()) {
    $result = Command $Name $Endpoint $CommandName $CommandArguments
    $lines = @(([string]$result.output) -split '\r?\n' | Where-Object { $_.StartsWith('LIVE_TEST_JSON=') })
    if ($lines.Count -ne 1) { throw "$Name requires one actual JSON result" }
    return ($lines[0].Substring(15) | ConvertFrom-Json)
}
function Health([string]$Name, [object]$Endpoint) {
    return Read-State $Name $Endpoint 'coop.debug.battle.health_state'
}
function Reserve([string]$Name) {
    return Read-State $Name $script:server 'coop.debug.map_event.health_reserve_state' @($script:mapEventId)
}
function Request([string]$Name, [string]$Controller, [string]$Operation, [string[]]$Extra = @()) {
    $null = Command $Name $script:server 'coop.debug.map_event.health_fixture_request' (@($Controller,$script:mapEventId,$Operation) + $Extra)
}
function Wait-Health([string]$Name, [object]$Endpoint, [scriptblock]$Predicate) {
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    $index = 0
    do {
        $state = Health "$Name-$index" $Endpoint
        if (& $Predicate $state) { return $state }
        $index++
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "$Name did not converge"
}
function Deploy([string]$Name, [object]$Endpoint, [string]$Controller) {
    Wait-Deployment "$Name-ready" $Endpoint
    Request "$Name-request" $Controller 'deploy'
    return Wait-Health "$Name-active" $Endpoint { param($state) @($state.agents | Where-Object { $_.active -and $_.authority -ceq $Controller }).Count -ge 5 }
}
function Distribution([object[]]$Agents, [string]$Party) {
    return @($Agents | Where-Object { $_.active -and $_.partyId -ceq $Party } |
        ForEach-Object { '{0}|{1}|{2}' -f $_.characterId, $_.hero, ([double]$_.health).ToString('R',[Globalization.CultureInfo]::InvariantCulture) } | Sort-Object) -join "`n"
}
function Reserve-Character([object[]]$Entries, [string]$StringId) {
    if ([string]::IsNullOrWhiteSpace($StringId)) { throw 'nonempty character StringId required' }
    $registryId = 'CharacterObject_' + $StringId
    $matches = @($Entries | Where-Object { $_.CharacterId -ceq $registryId })
    if ($matches.Count -eq 0) { throw "no reserve character matches $StringId ($registryId)" }
    return $matches
}
function Owned-ReserveParty([object]$Health, [object]$Reserve, [string]$HeroStringId, [string]$Controller) {
    $heroes = @($Health.agents | Where-Object { $_.active -and $_.hero -and $_.characterId -ceq $HeroStringId -and $_.authority -ceq $Controller })
    if ($heroes.Count -ne 1 -or [string]::IsNullOrWhiteSpace($heroes[0].partyId)) { throw 'exactly one current owned matching hero/party required' }
    $parties = @($Reserve.parties | Where-Object { $_.partyId -ceq $heroes[0].partyId })
    if ($parties.Count -ne 1) { throw 'current owned MapEventParty reserve missing or ambiguous' }
    return $parties[0]
}
function Capture-FinalRestoration([object]$Setup) {
    Wait-Campaign 'final-first-campaign' $script:client1
    Wait-Campaign 'final-second-campaign' $script:client2
    try { Restore-Check $Setup 'final'; $script:restored=$true }
    catch { Save-Json 'final-restoration-error.json' @{error=$_.Exception.Message}; Fail-Once 'final-restoration' $_.Exception.Message }
    Capture 'final-restored-campaign'
}
function Roster([string]$Name, [string]$First, [string]$Second) {
    return Command $Name $script:server 'coop.debug.mobile_party.exact_battle_roster_status' @($First,$Second)
}
function Roster-Healthy([object]$Read, [string]$Party) {
    $lines = @(([string]$Read.output) -split '\r?\n' | Where-Object { $_.StartsWith("party=$Party|", [StringComparison]::Ordinal) })
    if ($lines.Count -ne 1 -or $lines[0] -notmatch '\|healthy=(\d+)\|') { throw "missing exact healthy roster: $Party" }
    return [int]$Matches[1]
}
function Roster-Fingerprint([object[]]$Entries) {
    $ordered = [Collections.Generic.List[object]]::new()
    foreach ($entry in $Entries) { $ordered.Add($entry) }
    $ordered.Sort([Comparison[object]]{ param($a,$b) [StringComparer]::Ordinal.Compare($a.characterId,$b.characterId) })
    $content = ''
    foreach ($entry in $ordered) { $content += "$($entry.characterId)|$($entry.number)|$($entry.wounded)|$($entry.xp)`r`n" }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($content)))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
}
function Restore-Check([object]$Setup, [string]$Phase) {
    foreach ($snapshot in $Setup.originalRosters) {
        $read = Roster ($Phase + '-restored-roster-' + $snapshot.partyId) $snapshot.partyId 'testclient'
        $line = @(([string]$read.output) -split '\r?\n' | Where-Object { $_.StartsWith("party=$($snapshot.partyId)|") })[0]
        if (-not $line) { throw "missing restored party $($snapshot.partyId)" }
        $expected = Roster-Fingerprint @($snapshot.entries)
        if ($line -notmatch ('fingerprint=' + $expected + '$')) { throw "original counts/wounds/XP mismatch for $($snapshot.partyId)" }
        if ($line -match '\|leader=([^|]+)\|leaderHitPoints=(\d+)\|') {
            $heroId = $Matches[1]; $hp = [int]$Matches[2]
            $original = @($Setup.originalHeroHealth | Where-Object heroId -CEQ $heroId)
            if ($original.Count -ne 1 -or $hp -ne $original[0].health) { throw "restored leader health mismatch: $heroId" }
        }
    }
}
if ($SelfCheck) {
    $rosterEntries = @([pscustomobject]@{characterId='aserai_faris';number=18;wounded=1;xp=2440}, [pscustomobject]@{characterId='Player2863';number=1;wounded=0;xp=0})
    if ((Roster-Fingerprint $rosterEntries) -cne '79a1a6b5379b9e46ec9fafbe0e43098df2abfc5ae6d945e569684e951325b62a') { throw 'ordinal restored roster fingerprint mismatch' }
    $rosterEntries[0].xp++
    if ((Roster-Fingerprint $rosterEntries) -ceq '79a1a6b5379b9e46ec9fafbe0e43098df2abfc5ae6d945e569684e951325b62a') { throw 'changed roster XP accepted' }
    $a = [pscustomobject]@{ active=$true; partyId='p'; characterId='troop'; hero=$false; health=20.5 }
    $b = [pscustomobject]@{ active=$true; partyId='p'; characterId='troop'; hero=$false; health=21.5 }
    if ((Distribution @($a) 'p') -ceq (Distribution @($b) 'p')) { throw 'health mismatch accepted' }
    if ((Distribution @($a,$b) 'p') -cne (Distribution @($b,$a) 'p')) { throw 'agent ordering affected agreement' }
    $read = @{output="party=p|total=1201|wounded=0|healthy=1201|leader=h|`nparty=q|total=1200|wounded=0|healthy=1200|leader=k|"}
    if ((Roster-Healthy $read 'p') -ne 1201) { throw 'party healthy count incorrect' }
    $rejected=$false
    try { Roster-Healthy $read 'missing' | Out-Null } catch { $rejected=$true }
    if (-not $rejected) { throw 'missing party accepted' }
    $entries = @([pscustomobject]@{CharacterId='CharacterObject_imperial_recruit';Health=$null}, [pscustomobject]@{CharacterId='CharacterObject_Created_42';Health=37})
    if (@(Reserve-Character $entries 'imperial_recruit').Count -ne 1 -or (Reserve-Character $entries 'Created_42').Health -ne 37) { throw 'StringId/registry binding failed' }
    $rejected=$false
    try { Reserve-Character $entries 'missing' | Out-Null } catch { $rejected=$true }
    if (-not $rejected) { throw 'empty reserve match accepted' }
    $freshState = @{agents=@(@{active=$true;hero=$true;characterId='Created_42';authority='testclient';partyId='MapEventParty_Created_fresh'})}
    $freshLedger = @{parties=@(@{partyId='MapEventParty_Created_old';entries=@()}, @{partyId='MapEventParty_Created_fresh';entries=$entries})}
    $resolved = Owned-ReserveParty $freshState $freshLedger 'Created_42' 'testclient'
    if ($resolved.partyId -cne 'MapEventParty_Created_fresh' -or @(Reserve-Character $resolved.entries 'imperial_recruit').Count -ne 1) { throw 'fresh party was not resolved from current health' }
    $rejected=$false
    try { Owned-ReserveParty $freshState @{parties=@($freshLedger.parties[0])} 'Created_42' 'testclient' | Out-Null } catch { $rejected=$true }
    if (-not $rejected) { throw 'destroyed old party identity accepted' }
    $rejected=$false
    try { Owned-ReserveParty @{agents=@($freshState.agents[0],$freshState.agents[0])} $freshLedger 'Created_42' 'testclient' | Out-Null } catch { $rejected=$true }
    if (-not $rejected) { throw 'ambiguous owned hero identity accepted' }
    # Exercise final capture after a failed body without launching games.
    function Wait-Campaign { }
    function Restore-Check([object]$Setup) { if ($Setup.id -cne 'original') { throw 'wrong cleanup snapshot' } }
    function Capture([string]$Stage) { $script:selfCheckCapture=$Stage }
    $script:firstFailure=@{stage='original-failure';message='health mismatch'}
    $script:bodyPassed=$false
    Capture-FinalRestoration @{id='original'}
    if ($script:selfCheckCapture -cne 'final-restored-campaign' -or -not $script:restored -or $script:firstFailure.stage -cne 'original-failure' -or $script:bodyPassed) { throw 'failed-body cleanup capture changed verdict or first failure' }
    $script:selfCheckCapture=$null; $script:restored=$false
    function Save-Json { }
    Capture-FinalRestoration @{id='wrong'}
    if ($script:selfCheckCapture -cne 'final-restored-campaign' -or $script:restored -or $script:firstFailure.stage -cne 'original-failure') { throw 'restoration failure suppressed capture or replaced first failure' }
    'scenario self-check passed: health/order, party count, StringId/registry binding, missing-match rejection, distinct fresh party/stale rejection, ambiguous hero rejection, failed-body final capture and first-failure preservation'
    return
}
try {
    if ($RawCaptureRoot -notmatch '^[A-Za-z]:\\' -or $RawCaptureRoot -notmatch [regex]::Escape($RunToken) -or (Test-Path -LiteralPath $RawCaptureRoot)) { throw 'fresh Windows-local token capture root required' }
    New-Item -ItemType Directory -Path $RawCaptureRoot -ErrorAction Stop | Out-Null
    $script:rawCaptureCreated = $true
    $endpoints = @(Wait-LiveTestReadiness -ExpectedRunToken $RunToken -ClientCount 2 -ExpectedClientPlatformIds @('testclient','testclient2') -ReadyFor Campaign -EndpointStartupTimeoutMilliseconds 600000 -TimeoutMilliseconds 180000)
    Save-Json 'readiness.json' $endpoints
    $script:server = @($endpoints | Where-Object { $_.process.role -eq 'server' })[0]
    $script:client1 = @($endpoints | Where-Object { $_.process.role -eq 'client' -and $_.process.platformId -eq 'testclient' })[0]
    $script:client2 = @($endpoints | Where-Object { $_.process.role -eq 'client' -and $_.process.platformId -eq 'testclient2' })[0]
    foreach ($endpoint in @($script:server,$script:client1,$script:client2)) {
        if ($null -eq $endpoint -or [string]$endpoint.result.buildVersion -notmatch [regex]::Escape($ExpectedHead)) { throw 'exact source topology missing' }
    }
    Catalog 'server' $script:server @('coop.debug.map_event.battle_reward_fixture_prepare','coop.debug.map_event.late_join_mode_fixture','coop.debug.map_event.late_join_mode_fixture_state','coop.debug.map_event.late_join_mode_join','coop.debug.map_event.late_join_mode_enter','coop.debug.map_event.health_fixture_request','coop.debug.map_event.health_reserve_state','coop.debug.map_event.late_join_mode_exit_missions','coop.debug.map_event.late_join_mode_cleanup','coop.debug.mobile_party.exact_battle_roster_status')
    foreach ($item in @(@{name='client1'; endpoint=$script:client1},@{name='client2'; endpoint=$script:client2})) { Catalog $item.name $item.endpoint @('coop.debug.battle.health_state','coop.debug.map_event.deployment_state','coop.debug.map_event.enter_current_battle') }
    Capture 'baseline-campaign'
    $preflight = Read-State 'fixture-preflight' $script:server 'coop.debug.map_event.late_join_mode_fixture_state' @('testclient','testclient2')
    if (-not $preflight.restored) {
        $null = Command 'saved-idle-encounter-preparation' $script:server 'coop.debug.map_event.battle_reward_fixture_prepare' @('testclient','testclient2')
    }
    $setup = Read-State 'fixture-setup' $script:server 'coop.debug.map_event.late_join_mode_fixture' @('testclient','testclient2','health')
    $script:fixtureActive = $true
    $script:cleanupSetup = $setup
    $script:mapEventId = [string]$setup.mapEventId
    $null = Wait-Fixture 'first-mission' { param($s) $s.firstInMission }
    $null = Command 'second-join' $script:server 'coop.debug.map_event.late_join_mode_join'
    $null = Command 'second-enter' $script:server 'coop.debug.map_event.late_join_mode_enter'
    $null = Wait-Fixture 'both-missions' { param($s) $s.firstInMission -and $s.joiningInMission -and $s.firstMapEventId -ceq $script:mapEventId -and $s.joiningMapEventId -ceq $script:mapEventId }
    $owner = Deploy 'first-deploy' $script:client1 'testclient'
    $observer = Deploy 'second-deploy' $script:client2 'testclient2'
    Capture 'baseline-mission'
    $hero = @($owner.agents | Where-Object { $_.active -and $_.hero -and $_.authority -ceq 'testclient' })[0]
    $regular = @($owner.agents | Where-Object { $_.active -and -not $_.hero -and $_.authority -ceq 'testclient' -and $_.partyId -ceq $hero.partyId })
    if ($null -eq $hero -or $regular.Count -lt 4) { throw 'owned hero plus four regular controls required' }
    $partyId = [string]$hero.partyId
    $baselineReserve = Reserve 'baseline-reserve'
    $party = @($baselineReserve.parties | Where-Object partyId -CEQ $partyId)[0]
    if ($null -eq $party -or $party.entries.Count -le $party.supplied) { throw 'actual unsupplied reserve tail required' }
    $unspawned = $party.entries[[int]$party.supplied]
    $bindings = @(@($hero) + $regular | Select-Object -ExpandProperty characterId -Unique | ForEach-Object {
        $matched = @(Reserve-Character $party.entries $_)
        @{characterStringId=$_; characterRegistryId=$matched[0].CharacterId}
    })
    Save-Json 'character-bindings.json' @{bindings=$bindings; source='AutoRegistryBase.RegisterExistingObject and AutoRegistryHandler.Handle_InstanceCreated'; verification='exact nonempty baseline reserve match'}
    Save-Json 'subjects.json' @{ hero=$hero; injured=$regular[0]; routed=$regular[1]; casualty=$regular[2]; healthy=$regular[3]; unspawned=$unspawned; partyId=$partyId }
    $baselineRoster = Roster 'baseline-rosters' 'testclient' 'testclient2'
    $firstRosterLine = @(([string]$baselineRoster.output) -split '\r?\n' | Where-Object { $_.StartsWith('party=') })[0]
    if ($firstRosterLine -notmatch '^party=([^|]+)\|') { throw 'first controller roster identity missing' }
    $partyStringId = $Matches[1]
    $baselineHealthy = Roster-Healthy $baselineRoster $partyStringId
    if ($null -ne $unspawned.Health) { throw 'baseline unsupplied descriptor must have default health' }
    foreach ($agent in @($hero,$regular[0],$regular[1])) { Request ('damage-' + $agent.agentId) 'testclient' 'damage' @([string]$agent.agentId,'20') }
    $damaged = Wait-Health 'injuries' $script:client1 { param($s) @(@($hero,$regular[0],$regular[1]) | Where-Object { $a=$_; @($s.agents | Where-Object { $_.agentId -ceq $a.agentId -and $_.health -gt 0 -and $_.health -lt $a.health }).Count -eq 1 }).Count -eq 3 }
    $routed = @($damaged.agents | Where-Object agentId -CEQ $regular[1].agentId)[0]
    Request 'rout-request' 'testclient' 'rout' @([string]$regular[1].agentId)
    $null = Wait-Health 'rout-removal' $script:client1 { param($s) @($s.agents | Where-Object { $_.active -and $_.agentId -ceq $regular[1].agentId }).Count -eq 0 }
    Request 'casualty-request' 'testclient' 'damage' @([string]$regular[2].agentId,'10000')
    $beforeRetreat = Wait-Health 'casualty-removal' $script:client1 { param($s) @($s.agents | Where-Object { $_.active -and $_.agentId -ceq $regular[2].agentId }).Count -eq 0 }
    $casualtyRoster = Roster 'casualty-rosters' 'testclient' 'testclient2'
    if ((Roster-Healthy $casualtyRoster $partyStringId) -ne ($baselineHealthy - 1)) { throw 'expected one authoritative healthy casualty reduction' }
    $expected = @($damaged.agents | Where-Object { $_.agentId -in @($hero.agentId,$regular[0].agentId) }) + @($routed)
    Save-Json 'expected-survivors.json' $expected
    for ($round=1; $round -le 2; $round++) {
        $current = Health "pre-retreat-$round" $script:client1
        foreach ($survivor in @($expected | Where-Object { $_.agentId -cne $routed.agentId })) {
            if (@($current.agents | Where-Object { $_.active -and $_.characterId -ceq $survivor.characterId -and $_.partyId -ceq $partyId -and $_.health -eq $survivor.health }).Count -eq 0) { throw 'additional combat made injury comparison ambiguous' }
        }
        Request "retreat-$round" 'testclient' 'retreat'
        Wait-Campaign "campaign-$round" $script:client1
        $reserve = Reserve "retreat-reserve-$round"
        $p = @($reserve.parties | Where-Object partyId -CEQ $partyId)[0]
        Save-Json "retreat-round-$round-state.json" @{ reserve=$reserve; ownerReturned=$true }
        $null = Command "reenter-$round" $script:client1 'coop.debug.map_event.enter_current_battle'
        $state = Deploy "reentry-deploy-$round" $script:client1 'testclient'
        $reserve = Reserve "reentry-reserve-$round"
        $p = @($reserve.parties | Where-Object partyId -CEQ $partyId)[0]
        foreach ($survivor in $expected) {
            $count = @($expected | Where-Object { $_.characterId -ceq $survivor.characterId -and $_.health -eq $survivor.health }).Count
            if (@(Reserve-Character $p.entries $survivor.characterId | Where-Object { $_.Health -eq $survivor.health }).Count -lt $count) { throw 'surviving injury missing from rebuilt reserve' }
            if (@($state.agents | Where-Object { $_.active -and $_.partyId -ceq $partyId -and $_.characterId -ceq $survivor.characterId -and $_.health -eq $survivor.health }).Count -lt $count) { throw 'surviving injury missing from real respawned agents' }
        }
        if (@(Reserve-Character $p.entries $regular[2].characterId).Count -ne 1199) { throw 'casualty included in rebuilt regular reserve' }
        if (@($p.entries | Select-Object -Skip $p.supplied | Where-Object { $null -eq $_.Health }).Count -eq 0) { throw 'unspawned healthy reserve default was lost' }
        if (@($state.agents | Where-Object { $_.active -and $_.partyId -ceq $partyId -and -not $_.hero -and $_.health -eq $regular[3].health }).Count -eq 0) { throw 'healthy control lost' }
        $other = Wait-Health "observer-round-$round" $script:client2 { param($s) (Distribution $s.agents $partyId) -ceq (Distribution $state.agents $partyId) }
        Save-Json "round-$round-proof.json" @{ owner=$state; observer=$other; reserve=$reserve; expected=$expected }
    }
    Capture 'decisive-health-preserved'
    $null = Command 'exit-missions' $script:server 'coop.debug.map_event.late_join_mode_exit_missions'
    Wait-Campaign 'first-final-campaign' $script:client1
    Wait-Campaign 'second-final-campaign' $script:client2
    $null = Command 'fixture-cleanup' $script:server 'coop.debug.map_event.late_join_mode_cleanup'
    $script:fixtureActive = $false
    $old = Reserve 'finalized-old-ledger'
    if ($old.registered -or @($old.parties).Count -ne 0) { throw 'finalized ledger retained old health' }
    Restore-Check $setup 'old'
    $fresh = Read-State 'fresh-fixture' $script:server 'coop.debug.map_event.late_join_mode_fixture' @('testclient','testclient2','health')
    $script:fixtureActive = $true
    $script:cleanupSetup = $fresh
    if (($setup.originalHeroHealth | Sort-Object heroId | ConvertTo-Json -Compress) -cne ($fresh.originalHeroHealth | Sort-Object heroId | ConvertTo-Json -Compress)) { throw 'original roster hero health was not restored' }
    $script:mapEventId = [string]$fresh.mapEventId
    $null = Wait-Fixture 'fresh-first-mission' { param($s) $s.firstInMission }
    $freshHealth = Deploy 'fresh-deploy' $script:client1 'testclient'
    $freshReserve = Reserve 'fresh-reserve'
    $freshParty = Owned-ReserveParty $freshHealth $freshReserve $hero.characterId 'testclient'
    Save-Json 'fresh-party-binding.json' @{originalMapEventPartyId=$partyId; freshMapEventPartyId=$freshParty.partyId; heroStringId=$hero.characterId; controller='testclient'; health=$freshHealth; reserve=$freshReserve}
    $freshTroops = @(Reserve-Character $freshParty.entries $regular[0].characterId)
    Save-Json 'fresh-regular-defaults.json' $freshTroops
    if (@($freshTroops | Where-Object { $null -ne $_.Health }).Count -ne 0) { throw 'new battle inherited old regular health' }
    $script:bodyPassed = $true
} catch {
    Fail-Once 'health-scenario' $_.Exception.Message
    if ($null -ne $script:client1 -and $null -ne $script:client2) { try { Capture 'decisive-first-failure' } catch { Save-Json 'failure-screenshot-error.json' @{error=$_.Exception.Message} } }
} finally {
    if ($script:fixtureActive) {
        try {
            $null = Command 'cleanup-exit' $script:server 'coop.debug.map_event.late_join_mode_exit_missions'
            Wait-Campaign 'cleanup-first-campaign' $script:client1
            Wait-Campaign 'cleanup-second-campaign' $script:client2
            $null = Command 'cleanup-fixture' $script:server 'coop.debug.map_event.late_join_mode_cleanup'
            $script:fixtureActive = $false
        } catch { Save-Json 'fixture-cleanup-error.json' @{error=$_.Exception.Message}; Fail-Once 'fixture-cleanup' $_.Exception.Message }
    }
    if (-not $script:fixtureActive -and $null -ne $script:cleanupSetup) {
        try { Capture-FinalRestoration $script:cleanupSetup } catch { Save-Json 'final-capture-error.json' @{error=$_.Exception.Message}; Fail-Once 'final-capture' $_.Exception.Message }
    }
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
    $requiredPhases=@('baseline-campaign','baseline-mission','decisive-health-preserved','final-restored-campaign')
    $missing=@($requiredPhases | ForEach-Object { $phase=$_; @('client1','client2') | Where-Object { $role=$_; @($script:images | Where-Object { $_.checkpoint -ceq $phase -and $_.role -ceq $role }).Count -ne 1 } })
    if ($missing.Count -gt 0 -and $script:bodyPassed) { Fail-Once 'planned-captures' 'missing planned client capture' }
    $accepted=$script:bodyPassed -and $script:restored -and $retainedRaw -and $null -eq $script:firstFailure
    Save-Json 'actions-summary.json' @{accepted=$accepted; bodyPassed=$script:bodyPassed; restored=$script:restored; firstFailure=$script:firstFailure; images=$script:images}
    Save-Json 'result-manifest.json' @{schemaVersion=2;runToken=$RunToken;expectedHead=$ExpectedHead;expectedTree=$ExpectedTree;accepted=$accepted;verdict=$(if($accepted){'passed'}else{'failed'});files=$script:images;firstFailure=$script:firstFailure}
    if (-not $accepted) { exit 1 }
}
