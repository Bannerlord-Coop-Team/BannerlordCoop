[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]{1,64}$')][string]$RunToken,
    [Parameter(Mandatory=$true)][string]$ArtifactDirectory,
    [Parameter(Mandatory=$true)][string]$LiveTestClientScript,
    [Parameter(Mandatory=$true)][string]$RuntimeScript,
    [Parameter(Mandatory=$true)][string]$ExpectedHead,
    [Parameter(Mandatory=$true)][string]$ExpectedTree,
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
$selected = @{}
$server = $null
$fixtureActive = $false
$images = @()
$failure = $null
$restoreErrors = @()
$ordinal = 0
$raw = Join-Path ([IO.Path]::GetDirectoryName($ArtifactDirectory)) "captures-$RunToken"
if ($raw -notmatch '^[A-Za-z]:\\' -or (Test-Path -LiteralPath $raw)) { throw 'Capture directory must be fresh Windows-local storage.' }
New-Item -ItemType Directory -Path $raw | Out-Null
function Command($Endpoint, [string]$Name, [string[]]$Arguments) {
    $script:ordinal++
    $reply = Invoke-LiveTestClientAction -RequestedAction Command -TargetProcessId ([int]$Endpoint.process.pid) `
        -RequestedCommandName $Name -RequestedArgumentsJson (ConvertTo-Json -InputObject @($Arguments) -Compress) -RequestTimeoutMilliseconds 30000
    Save-Json ("commands/{0:D3}-{1}.json" -f $script:ordinal,$Endpoint.process.pid) $reply
    $succeeded = $reply.Response.result.PSObject.Properties['succeeded']
    if ($reply.ExitCode -ne 0 -or -not $reply.Response.ok -or -not $reply.Response.result.found -or
        ($null -ne $succeeded -and $succeeded.Value -ne $true)) { throw "Command $Name failed: $($reply.Response.result.output)" }
    return $reply.Response.result
}
function State($Endpoint, [string]$Name, [string[]]$Arguments) {
    $result = Command $Endpoint $Name $Arguments
    if (-not $result.hasStructuredResult -or $null -eq $result.structuredResult) { throw "$Name did not return in-game data." }
    return $result.structuredResult
}
function Wait-State($Endpoint, [string]$Name, [string[]]$Arguments, [scriptblock]$Predicate) {
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    do {
        try {
            $state = State $Endpoint $Name $Arguments
            if (& $Predicate $state) { return $state }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out waiting for $Name. $last"
}
function Assert-Captain($State, [string]$CharacterId) {
    if ($State.patchCount -ne 1 -or -not $State.ordinaryTroopFound -or $State.ordinaryTroopHasHero) {
        throw 'Expected one installed tooltip patch and an ordinary troop without a hero.'
    }
    $captains = @($State.captains | Where-Object { $_.characterId -ceq $CharacterId })
    if ($captains.Count -ne 1) { throw 'The selected captain is absent or duplicated.' }
    $captain = $captains[0]
    if (-not $captain.linkedHero -or $captain.characterId -ceq $captain.heroId -or [string]::IsNullOrWhiteSpace($captain.name)) {
        throw 'A real named captain with the mismatched-id hero link is required.'
    }
    foreach ($id in @('OneHanded','TwoHanded','Polearm','Bow','Crossbow','Throwing','Riding','Athletics','Tactics','Leadership')) {
        $skill = @($captain.skills | Where-Object { $_.id -ceq $id })
        if ($skill.Count -ne 1) { throw "Missing actual hero skill: $id" }
        $row = @($captain.rows | Where-Object { $_.DefinitionLabel -ceq $skill[0].name -and $_.OnlyShowWhenNotExtended })
        if ($row.Count -ne 1 -or [string]$row[0].ValueLabel -cne [string]$skill[0].characterValue) {
            throw "Production tooltip skill differs: $id"
        }
    }
    foreach ($label in @('Infantry Influence','Ranged Influence','Cavalry Influence','Horse Archer Influence')) {
        $row = @($captain.rows | Where-Object { $_.DefinitionLabel -ceq $label -and $_.OnlyShowWhenNotExtended })
        if ($row.Count -ne 1 -or [string]$row[0].ValueLabel -notmatch '^-?[0-9]+$') { throw "Missing production influence: $label" }
    }
    if (@($captain.rows | Where-Object { $_.DefinitionLabel -ceq $captain.name -or $_.ValueLabel -ceq $captain.name }).Count -eq 0) {
        throw 'The production tooltip does not identify the captain.'
    }
    if (@($captain.rows | Where-Object { $_.OnlyShowWhenExtended }).Count -eq 0 -or
        @($captain.rows | Where-Object { $_.DefinitionLabel -match 'Alt' -or $_.ValueLabel -match 'Alt' }).Count -eq 0) {
        throw 'Actual extended perk properties and the Alt prompt are required.'
    }
    return $captain
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
    $initial = State $server 'coop.debug.map_event.late_join_mode_fixture_state' @('testclient','testclient2')
    if ($initial.fixtureActive -or -not $initial.restored) { throw 'The existing battle fixture is not restored.' }
    # The existing authoritative fixture selects a real hostile bandit and preserves party movement.
    $fixtureActive = $true
    $null = Command $server 'coop.debug.map_event.late_join_mode_fixture' @('testclient','testclient2')
    $null = Wait-State $server 'coop.debug.map_event.late_join_mode_fixture_state' @('testclient','testclient2') { param($s) $s.firstInMission }
    $null = Command $server 'coop.debug.map_event.late_join_mode_join' @()
    $null = Command $server 'coop.debug.map_event.late_join_mode_enter' @()
    $null = Wait-State $server 'coop.debug.map_event.late_join_mode_fixture_state' @('testclient','testclient2') { param($s) $s.firstInMission -and $s.joiningInMission }
    $rosters = @($clients | ForEach-Object {
        Wait-State $_ 'coop.debug.battle.captain_tooltip' @('observe') { param($s) @($s.captains).Count -gt 0 }
    })
    Save-Json 'captain-rosters.json' $rosters
    $candidate = @($rosters[0].captains | Where-Object {
        $_.characterId -cne $_.heroId -and $_.linkedHero -and $_.characterId -cin @($rosters[1].captains.characterId)
    } | Sort-Object { @($_.perks).Count } -Descending | Select-Object -First 1)
    if ($candidate.Count -ne 1) { throw 'Both real deployment rosters need the same mismatched-id captain.' }
    $id = [string]$candidate[0].characterId
    foreach ($client in $clients) {
        $selected[$client.process.platformId] = $id
        $before = State $client 'coop.debug.battle.captain_tooltip' @('hide',$id)
        $null = Assert-Captain $before $id
        Save-Json "captain-before-$($client.process.platformId).json" $before
    }
    Capture 'captain-before'
    $observed = @()
    foreach ($client in $clients) {
        $after = State $client 'coop.debug.battle.captain_tooltip' @('show',$id)
        $captain = Assert-Captain $after $id
        Save-Json "captain-after-$($client.process.platformId).json" $after
        $observed += $captain
    }
    if ($observed[0].heroId -cne $observed[1].heroId -or $observed[0].name -cne $observed[1].name -or
        ($observed[0].skills | ConvertTo-Json -Depth 8 -Compress) -cne ($observed[1].skills | ConvertTo-Json -Depth 8 -Compress) -or
        ($observed[0].rows | ConvertTo-Json -Depth 8 -Compress) -cne ($observed[1].rows | ConvertTo-Json -Depth 8 -Compress)) {
        throw 'Both clients must agree on the actual named captain, hero skills and production rows.'
    }
    Capture 'captain-after'
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
    foreach ($client in $clients) {
        if ($selected.ContainsKey($client.process.platformId)) {
            try { $null = Command $client 'coop.debug.battle.captain_tooltip' @('hide',$selected[$client.process.platformId]) }
            catch { $restoreErrors += $_.Exception.Message }
        }
    }
    if ($selected.Count -eq 2 -and $restoreErrors.Count -eq 0) {
        try { Capture 'captain-closed' } catch { $restoreErrors += $_.Exception.Message }
    }
    if ($fixtureActive -and $null -ne $server) {
        try {
            $null = Command $server 'coop.debug.map_event.late_join_mode_exit_missions' @()
            $null = Wait-State $server 'coop.debug.map_event.late_join_mode_fixture_state' @('testclient','testclient2') { param($s) -not $s.firstInMission -and -not $s.joiningInMission }
            $null = Command $server 'coop.debug.map_event.late_join_mode_cleanup' @()
            $restored = State $server 'coop.debug.map_event.late_join_mode_fixture_state' @('testclient','testclient2')
            Save-Json 'restored-fixture-state.json' $restored
            if ($restored.fixtureActive -or -not $restored.restored) { throw 'The authoritative battle fixture did not restore.' }
            $null = Wait-LiveTestReadiness -ExpectedRunToken $RunToken -ClientCount 2 -ExpectedClientPlatformIds @('testclient','testclient2') `
                -ReadyFor Campaign -ClientsOnly -AllowLegacyServerConnectionProbe -TimeoutMilliseconds 180000
            Capture 'restored'
        } catch { $restoreErrors += $_.Exception.Message }
    }
    try {
        Copy-Item -LiteralPath $raw -Destination (Join-Path $ArtifactDirectory 'raw-captures') -Recurse -ErrorAction Stop
        foreach ($file in @(Get-ChildItem -LiteralPath $raw -File)) {
            $copy = Join-Path (Join-Path $ArtifactDirectory 'raw-captures') $file.Name
            if ((Get-FileHash $file.FullName).Hash -cne (Get-FileHash $copy).Hash) { throw 'Raw capture retention hash differs.' }
        }
        Remove-Item -LiteralPath $raw -Recurse -ErrorAction Stop
    } catch { $restoreErrors += "Raw capture retention: $($_.Exception.Message)" }
    Save-Json 'scenario-result.json' @{
        outcome=$(if ($null -eq $failure -and $restoreErrors.Count -eq 0) {'passed'} else {'failed'})
        sourceHead=$ExpectedHead; sourceTree=$ExpectedTree; runToken=$RunToken; firstFailure=$failure; restoreErrors=$restoreErrors
        screenshots=$images; claims=@('same named campaign captain on both clients','linked hero despite mismatched ids','ten actual skill rows','four formation influence categories','production tooltip callback and extended perk data')
        coverageLimit='Native Alt-key and hover input wiring are excluded and unverified. Parent must inspect tooltip images before visual acceptance.'
    }
}
if ($null -ne $failure -or $restoreErrors.Count -ne 0) { exit 1 }
