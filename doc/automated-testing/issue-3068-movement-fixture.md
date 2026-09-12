# Issue #3068: dedicated-server field-battle movement

This is a prepared procedure, not a record of a live run. **Live testing has not
started.** Compile checks, simulated peers, fake agents, and packet assertions
are synthetic evidence. They cannot establish that Bannerlord renders moving
units correctly. Execute the runtime procedure only under a later, explicit
live-test authorization.

Issue: [#3068](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/3068).
Implementation: [#3308](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/pull/3308).
The reported failure is asymmetric: a late joiner sees the first player's and
enemy units frozen while the first player sees the joiner moving. Reports include
Steam, direct-connect, and arena/tournament cases. This fixture covers the
field-battle case; a passing field battle does not close the arena oracle.

## Source and evidence binding

Before the later run, record these values from the reviewed implementation
checkout and attach the resulting text to the run evidence:

```sh
git rev-parse HEAD HEAD^{tree}
git status --porcelain=v1
git show -s --format='%H %P %s' HEAD
```

Require a clean tracked tree. Retain the review and non-live test records with
their own exact commit/tree and the merged base commit; do not reuse an older
PR's green result after changing source. Record the build configuration, source
archive SHA-256, game version, baseline save SHA-256, and each process's loaded
`Coop`, `Missions`, `GameInterface`, and `Common` assembly path/version/MVID. Check
the loaded modules, not just DLLs found on disk. All three processes must use the
same reviewed build. A dedicated-server launcher from another revision is a
separate identity to record. Missing identity evidence makes the run inconclusive.

Retain separate directories for `synthetic`, `direct-field`, and `steam-field`.
Each runtime directory needs timestamped server console output, both client
logs, raw command output including its success envelope and `LIVE_TEST_JSON`,
and simultaneous recordings of both client views. Preserve logs before any
relaunch: `Coop_client.log` is recreated at startup. The standalone server's
console is its log source; do not assume an in-game `Coop_server.log` belongs to it.
Use distinct client log captures if both run on one machine.

## Preconditions and roles

Use an isolated, disposable campaign save with autosave disabled. Preserve an
untouched copy; final reset is to reload that exact copy. Both players must
already have healthy parties outside settlements and no active map event. The
save must contain a healthy hostile bandit party and enough friendly troops to
observe at least one player and one non-player human per side of the peer link.
Record the starting party roster counts. The reported army sizes were about
30 units per player and 6–8 looters; match those counts when preparing the saved
baseline, but do not change rosters between transport runs.

The dedicated server S owns campaign setup, membership, host election, and
cleanup. It has no party, native mission, or agent to drive. A and B are playing
clients; A enters first and is the elected **battle host**, which is different
from S. Native player input and `drive_owned_agents` run only on the client that
currently owns those agents. The deployment/dying helpers below are a necessary
mission-native staging exception: only clients have `Mission.Current`. They keep
this movement fixture alive and do not substitute for server-owned campaign
setup or host election. Other client commands observe state or enable diagnostic
counters. Disconnect and rejoin through the normal session/connection UI.

First run on S, A, and B:

```text
coop.debug.players.list
```

Bind A and B to the actual `ControllerId`, hero, and party IDs in this output.
The worked commands below use `PlayerOne` for A and `PlayerTwo` for B, as in the
existing fixture's source examples. They are **not asserted to be launcher
defaults**: copy the IDs from the lookup output into every command if they differ.
Never identify a controller by player display name alone. Map-event IDs and
agent GUIDs are save/run-specific; obtain them from fixture and snapshot output,
not from a previous run. No hardcoded town is needed: the fixture uses the
players' current campaign positions and a real hostile bandit party.

Run direct-connect and Steam as separate cases from the same pristine save.
For Steam, use distinct authenticated Steam accounts and retain evidence of the
actual mission Steam bridge/transport route. Neither a Steam-enabled launcher
nor `steamIdentityMatched=true` proves that packets used Steam: the latter is
also true for a direct peer whose expected Steam ID is zero. Match both clients'
`[ReceivePath]` mapped transitions by instance/controller, require a nonzero
matching expected/tracked Steam identity, and retain the Steam bridge's route
establishment and traffic evidence. If that evidence is unavailable, label
transport provenance unproven; do not count it as the required Steam case.

## A enters, then B joins the existing field battle

1. On S, record the initial restoration state:

   ```text
   coop.debug.map_event.late_join_mode_fixture_state PlayerOne PlayerTwo
   ```

   Require `fixtureActive=false`, both map-event IDs null, both mission flags
   false, and `restored=true`. This command's JSON `success` means **restored**;
   it is expected to be false while a fixture is active. Check its individual
   fields rather than treating active-fixture `success=false` as a command error.

2. On S, once:

   ```text
   coop.debug.map_event.late_join_mode_fixture PlayerOne PlayerTwo
   ```

   Save the returned `mapEvent`, `eventType`, opponent name/StringId, and player
   IDs. Require `eventType=FieldBattle`. The command chooses the nearest eligible
   healthy bandit party hostile to both players, creates a real field map event,
   and sends A's request through the normal server battle-start handler. It does
   not fabricate a dedicated-server player. Repeated setup is rejected while
   active; inspect and clean up instead of issuing another setup. With the same
   paused baseline save and player positions, require the same chosen opponent
   in both transport cases. If no opponent qualifies, report a setup failure and
   prepare a suitable disposable baseline; do not silently choose another scenario.

3. Allow at most 90 seconds for A to load. On A, read:

   ```text
   coop.debug.movement.controller_agents PlayerOne
   ```

   Require its `missionInstanceId` to equal the returned map event,
   `localControllerId=PlayerOne`, `hostControllerId=PlayerOne`, `localIsHost=true`,
   and `hostEpoch=1`. On S, require `instanceId` equal to the returned map event,
   `hostControllerId=PlayerOne`, `hostEpoch=1`, `firstInMission=true`, and
   `joiningInMission=false` in `late_join_mode_fixture_state`; retain the matching
   `[BattleHost] Elected host ... at epoch ...` log.
   Keep A in deployment until B is ready, so ordinary battle deaths cannot end
   the scenario before both observations exist.

4. On S, issue these separately, checking each result before continuing:

   ```text
   coop.debug.map_event.late_join_mode_join
   coop.debug.map_event.late_join_mode_enter
   ```

   Require `Late join accepted` for the original map event, attacker side, and
   Mission mode, then `Late joiner mission requested`. These invoke the real
   join and mission-start handlers. Within 90 seconds S must report both mission
   flags true and both map-event IDs equal the original event. On each client,
   once its local deployment is ready, run:

   ```text
   coop.debug.map_event.late_join_mode_begin_field_battle
   ```

   Require `Local deployment finished; the field battle is active and the local
   player is protected.` This finishes native deployment, disables dying in that
   local mission, and protects its main agent. If deployment already finished
   normally, use `coop.debug.map_event.late_join_mode_disable_dying` on that
   client and require its protected-player success. A must remain host at epoch
   1; B must have `localIsHost=false` and the same instance. If deployment or a
   main agent never becomes ready, stop with setup evidence. Do not replace the
   step with a command that edits only membership. Death protection is specific
   to this disposable movement fixture; it makes no damage/casualty claim.

5. On A and B, capture the following read-only snapshots, labeling the process
   and UTC time for each invocation:

   ```text
   coop.debug.movement.peer_state PlayerOne
   coop.debug.movement.peer_state PlayerTwo
   coop.debug.movement.controller_agents PlayerOne
   coop.debug.movement.controller_agents PlayerTwo
   coop.debug.movement.state
   ```

   For `peer_state`, evaluate only the **other** controller: B on A, A on B.
   There is no requirement for a mapped self-peer. Within 15 seconds require
   `routeExists`, `credentialAnnounced`, `credentialMatched`,
   `steamIdentityMatched`, and `mapped` true in the other controller's JSON.
   Outer command success only means the observation executed; its JSON carries
   the route assertion. Match `missionInstanceId`, current host, and epoch on
   both client agent snapshots and the authoritative server log.

`controller_agents` groups by **CurrentAuthority**, not OriginalOwner. Each
record includes its GUID, OriginalOwner, CurrentAuthority, MovementScopeId,
MovementId, AuthorityRevision, human/team classification, isLocalMainAgent,
position, and velocity. `sampledAtUtc` timestamps each snapshot; synchronize
client clocks before comparing samples. Identify each player GUID from that
player's local `isLocalMainAgent=true` record and find the same GUID on the peer.
Preserve the GUID plus movement scope/id pair for comparisons across migration.
`expectedLocalActionEpoch` describes the expected sender stamp for the process
on which the command ran, even if the command queried the other controller.
It is a prediction from local authority state, **not captured wire evidence**.

## Action-packet observations

After the reciprocal routes are mapped, enable diagnostics once on A and B:

```text
coop.debug.battle.action_performance start
```

Keep B's recording active from the initial epoch-1 baseline through A's return.
Do not run `start` again on B: it clears the recorded rows and counters. Read
without resetting with:

```text
coop.debug.battle.action_performance snapshot
```

The `ACTION_PERFORMANCE` JSON contains `actionTraffic.authorityObservations`,
grouped by the actual packet's `controllerId` and `battleHostEpoch`. Each row
has `sentPackets`, `sentUpdates`, `sentSerializedBytes`, `receivedPackets`,
`receivedUpdates`, and `receivedSerializedBytes`. The group capacity is 64;
require `evictedAuthorityObservations=0` when asserting that the old epoch-1
baseline is still represented. Sent rows count packets serialized for send;
received rows count decoded packets before receive handling. A sent row alone
does not prove delivery, and a received row does not prove authorization, action
application, or rendering. Require the paired receive row and native oracle.

During the normal attack in the initial motion probe, require A's `(PlayerOne,1)`
row to increase `sentPackets` and `sentUpdates`, and B's matching row to increase
`receivedPackets` and `receivedUpdates`. Save these snapshots before disconnect.
After the migration/return, require B's `(PlayerTwo,2)` sent row and A's matching
received row, and returning A's `(PlayerOne,0)` sent row with B's matching
received row. On B retain both `(PlayerOne,1)` and `(PlayerOne,0)` rows in the
same recording. If reconnect replaces A's process or disables its diagnostics,
start a new recording on returned A before the repeat action probe; label it as
a new sender-side capture. B's continuous receiver capture must not be reset.
A predicted `expectedLocalActionEpoch` is insufficient for these assertions.

## Native movement oracle in both directions

Record both windows throughout. Do not inject packets, teleport agents, force
movement rates, or simulate receiver pressure in this case. Commands that drive
agents use their native movement input/AI target and the normal movement sender.
Use an open stretch of the field with no wall or dense melee obstructing the
selected troops. Failure to obtain a moving authoritative sample is inconclusive,
not proof that the peer transport is broken or fixed.

1. Obtain baseline `controller_agents` snapshots for both controllers on both
   clients. On A run:

   ```text
   coop.debug.battle.drive_owned_agents 3
   ```

   Save the returned `agentIds` and full `drive` snapshot. The command accepts
   integer durations 3–30 seconds and selects only active locally authoritative
   agents whose OriginalOwner is also local. It does not force enemy AI to move.
   Require at least one player-owned human in the returned set. At elapsed 1,
   2, and 3 seconds collect `controller_agents PlayerOne` on A and B; continue
   recording for five seconds after completion. On A read:

   ```text
   coop.debug.battle.owned_agent_drive_state
   ```

   Within five seconds of the requested duration require `active=false`,
   `agentCount=0`, `failedAgents=0`, `invalidatedAgents=0`, and `restoredAgents`
   equal to the original driven count. A missing `drive` is not a completed probe.

2. Repeat the same sequence on B, using `controller_agents PlayerTwo` on both
   clients. Cancel any still-active drive before starting the other:

   ```text
   coop.debug.battle.cancel_owned_agent_drive
   ```

   Cancellation is idempotent. After a completed drive, repeat cancel and verify
   it does not change the other client's authority or restart movement. Then
   each player, one at a time, runs forward, turns, and performs three normal
   melee attacks while the other records the remote agent. Read the action
   counter snapshots after those attacks; do not rely on idle packets alone.

3. For each direction, compare at least one driven player and one driven AI
   human by the **same GUID**, using measurements no more than 250 ms apart.
   Require the authoritative sample to travel at least 1 metre in the three
   seconds. The receiver must also travel at least 1 metre, show no continuous
   two-second freeze while the owner moves, and finish within 2 metres of the
   owner's corresponding position after allowing one second for convergence.
   Record every sample; do not compare a fast-moving owner's current position
   against a receiver sample taken seconds earlier. Unexpected teleports or
   missing shared GUIDs fail the movement/replication assertion. Insufficient
   authoritative displacement makes the probe inconclusive and requires a new
   recorded unobstructed attempt, not a looser threshold.

4. Observe at least one active human on `teamSide=Defender` in the current
   battle host's controller snapshot and its matching GUID on the other client.
   Initially this is `controller_agents PlayerOne` on both A and B; after the
   migration it is `controller_agents PlayerTwo` on both. Let normal enemy AI
   approach the players; capture at one-second intervals for up to 30 seconds.
   Once it moves at least 1 metre on the current host, require the same remote
   displacement/freeze/convergence limits.
   If all defenders stand still, resolve the staging condition through normal
   play and repeat; without a moving enemy sample the enemy-unit oracle remains
   incomplete. Retain ordinary running, turning, and attack animations on both
   recordings as well as numeric coordinates.

## Real host migration and former-host return

Run this while the same battle is still active and B remains connected and
mission-ready. Do not finish the battle, reload the save, start another map event,
or remove A only from the server's membership registry.

1. Preserve the epoch-1 baseline on **both** clients: A is host, B is ordinary,
   both hold A's player/AI GUIDs. Preserve an actual A attack animation observed
   by B before departure. Expected local action stamps are A=1, B=0. Movement
   packets have no host-epoch field; `BattleHostEpoch` belongs to action packets.
   Movement packets carry the actual sender separately from stable identity
   scope, plus per-agent AuthorityRevisions; those revisions must match the
   receiver registry when applied on the game thread.

2. On B start `coop.debug.battle.drive_owned_agents 30` and retain its captured
   epoch-1 drive snapshot. Within five seconds, disconnect A through its normal
   session/connection UI. Record the actual disconnect and server departure.
   Leave B in the mission. Within 15 seconds require S's
   `[BattleHost] Host ... left battle ...; promoted ... at epoch ...` to identify
   PlayerOne, PlayerTwo, the original map event, and epoch 2. Require the same
   host and epoch in S's `late_join_mode_fixture_state` when both player objects
   remain resolvable. On B require
   `hostControllerId=PlayerTwo`, `hostEpoch=2`, `localIsHost=true`, and
   `expectedLocalActionEpoch=2`.

3. Within five seconds after B observes epoch 2, its old drive must be inactive.
   An epoch-only change stops the drive and restores movement state for an
   otherwise identical registered agent with unchanged CurrentAuthority and
   AuthorityRevision. `failedAgents` must stay zero. A snapshot whose authority,
   revision, native agent identity, or mission actually changed must instead be
   discarded; it must not restore stale state into the successor's agent. Retain
   `restoredAgents`, `invalidatedAgents`, and the before/after snapshots to explain
   which path occurred. Begin a fresh three-second B drive and verify ordinary
   input/AI behavior still resumes after it. A null post-reconnect drive on A
   does not by itself prove the departed process restored or invalidated anything.

4. Reconnect A normally using its original authenticated player identity while
   B stays in this battle. Use the normal campaign encounter/join and Attack
   flow to return A to the **same recorded map event**. The earlier
   `late_join_mode_join`/`enter` commands target B and must not be repurposed for
   A's return. Allow 90 seconds. If the game cannot return A to that battle,
   preserve the result as a reconnect failure; creating a replacement battle
   would not exercise this transition. On returned A run
   `coop.debug.map_event.late_join_mode_disable_dying` once its main agent exists,
   requiring protected-player success; if it reenters deployment, use the
   begin-field-battle helper first.

5. Repeat peer-state and agent snapshots on both clients. Require reciprocal
   mapped routes with current credentials; the same original map event; B still
   host at epoch 2; A ordinary with `expectedLocalActionEpoch=0`. This is the
   former-host **1-to-0 action domain** case, not a host-epoch reset. For every
   surviving GUID whose CurrentAuthority changes, require a nondecreasing
   AuthorityRevision and agreement between peers. Never reset AuthorityRevision
   to zero or treat action epoch zero as an older positive host generation.
   Track original GUIDs through B's adoption and A's return; a freshly spawned
   replacement GUID must be recorded as a replacement, not counted as that
   surviving agent's authority-return evidence.

6. Repeat the bidirectional player/AI and enemy movement oracle. A performs
   normal running, turning, and an attack; B must see the resulting motion and
   animation, and vice versa. B's receiver stays alive across A's epoch-1
   baseline and epoch-0 return, so do not clear its action state to make the test
   pass. Read the action-performance snapshots and require the actual
   sent/received epoch rows above. Exact stale-action rejection remains a
   synthetic assertion unless an actual stale packet is captured in the runtime
   evidence. The predicted snapshot field cannot prove received packet contents.

## Reset and failure evidence

Before campaign cleanup, cancel each connected client's drive and capture its
state. On both clients collect the final packet counters and freeze recording:

```text
coop.debug.battle.action_performance snapshot
coop.debug.battle.action_performance stop
```

`stop` freezes the counters; retain the output before either process exits. On S:

```text
coop.debug.map_event.late_join_mode_exit_missions
coop.debug.map_event.late_join_mode_fixture_state PlayerOne PlayerTwo
```

Wait at most 30 seconds for both clients to return to campaign and both mission
flags to become false. Only then run:

```text
coop.debug.map_event.late_join_mode_cleanup
coop.debug.map_event.late_join_mode_fixture_state PlayerOne PlayerTwo
```

Require `fixtureActive=false`, null map-event IDs, both mission flags false, and
`restored=true`. Do not use the combined `late_join_mode_restore` as evidence
that asynchronous mission exits already completed. A repeated cleanup reports
no active fixture; verify restoration state instead of treating that response
as a new failure. The helper restores recorded party movement, not a full
campaign snapshot: casualties, time, rosters, and other gameplay may have changed.
Exit without saving and reload the pristine save before repeating any transport
case. If exit/cleanup times out, preserve evidence and end the authorized run;
do not erase logs or create another fixture over an unresolved one.

At the first failure retain the previous 30 seconds and following 10 seconds of
both recordings/logs, command envelopes/JSON, exact GUIDs/scopes/revisions,
instance and host assignment, route transitions, and server membership. Capture
`coop.debug.movement.state` on both clients, including active/moving/local agent
counts, sender and apply cost, receiver queue, deferred age, rate/cap and reason.
Separate setup, route admission, spawn/identity, authority, and actual native
movement failures. Preserve pending-peer buffer/credential warnings and receive
path counters. A command's JSON `success`, a mapped socket, or increasing packet
counts cannot substitute for the paired native-motion oracle.

## Non-live verification contract

Run these checks only through the task's compile/unit/static workflow. This
section does not authorize launching any game or server process.

- Compile DEBUG commands and Release product source with deployment disabled
  (`ModName=` and `PostBuildEvent=`; clearing only the post-build event does not
  disable the repository's separate deployment target).
  The implementation review must include `Side` declarations against the current
  command interface and BOM/CRLF preservation for changed C# files.
- Exercise actual socket/peer admission using deterministic in-process tests:
  direct and Steam credential paths, announcement-before/after-connect,
  reliable payload arriving before mapping, replacement peer, disconnect,
  credential rotation, stale instance traffic, and bounded pending payloads.
- Drive lifecycle tests must cover natural completion, repeated cancel, original
  AI target modes/input restoration, unchanged speed-limit mode, epoch-only
  cancellation, authority transfer, revision change/ABA, registry replacement,
  mission replacement, per-agent exceptions, and no stale restoration.
- Capture outgoing action packets in a synthetic authority transition: retain
  A's epoch-1 baseline on B, promote B to epoch 2, return A as current ordinary
  authority emitting epoch 0, and verify B accepts the new action/equipment
  baseline while rejecting stale former-host positive epochs. Include pending
  action/equipment state; clearing that receiver state would avoid the bug.
  Independently verify CurrentAuthority, stable movement identities, and
  monotonically increasing AuthorityRevision through transfer and return. Hold
  an old movement packet across A-to-B-to-A authority changes, then verify its
  old revision is rejected even though the controller ID matches again. Cover
  rider and mount packets and a packet queued before a transfer but applied
  afterward. Include a non-host disconnect observed by a third client, plus a
  capped spawn buffer retained across two host migrations with different rider
  and mount revisions. Verify current-revision movement still applies after the
  rejection and after the buffered agents finally spawn. Include catch-up for
  a rider using another connected player's horse, preserving the horse's own
  authority and revision through rider departure and accepting the horse's
  current-authority movement after dismount. Cover graceful horse-owner leave,
  disconnect, and host migration while spawn is buffered. Hold the original
  catch-up across departure and re-entry, then deliver it before and after the
  actual refreshed authority snapshot; both orders must converge on the newer
  authority without changing the agent's stable movement identity.
- Run the appropriate broader non-live mission/network suite on the same final
  tree. Record exact commands, result counts, failures/skips, and artifact hashes
  externally with the source identities above. Do not insert unexecuted pass
  counts into this runbook.

The later native pass requires direct and proven-Steam late entry, movement in
both directions including enemies, real host migration/return in the same
battle, paired visual and numeric observations, and confirmed reset. Until those
records exist, the field-battle runtime oracle is **pending**.
