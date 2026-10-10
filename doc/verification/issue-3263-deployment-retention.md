# Deployment diagnostic retention fixture (#3263)

Prepared for PR #3451. **Live testing has not started.** This is a future runtime procedure; compile and headless tests do not establish native mission teardown.

## Source and build binding

The continued branch merged `origin/development` without rebasing:

| Identity | Commit | Tree |
| --- | --- | --- |
| Original PR head | `b38032546c3ac9d8761f26072816eb6d3f545b74` | `4e2304f5813e4772dbdc3ce684825e76240a77f5` |
| Authoritative base | `9963f59803dbab6054c8aaba8749865960816ba7` | `042ce9f6600bace996cfe64d5bebfa1082fc5f22` |
| Merge result | `8be863cbf4f2d8ab2abe1776a13817e37a73530c` | `bc50591035d68822a783489a5ed9115d4649e737` |

The only conflict was the independently equivalent explicit-worker-thread fix in `CallOriginalPolicyTests`. The development version was retained. The base still has the static `_loggedOverrides` `HashSet<Team>` and adds each foreign team to it. The negative control runs the static-root regression against the unmodified base assembly; the feature tests exercise the corrected assembly.

Before a later authorized runtime run, record `git rev-parse HEAD HEAD^{tree}`, clean status, the source archive SHA-256, Debug build log, and SHA-256/MVID of the exact `GameInterface.dll` supplied to the server and both clients. The command returns `sourceModuleVersionId`; require it to match that artifact. Capture process IDs and keep each client process alive across both rounds. A source, binary, configuration, or process change invalidates a mixed-round comparison.

The observer and command compile only in Debug. Release has no diagnostic target collections, observer command, or collection probe. The production override predicate is unchanged. Command name: `coop.debug.map_event.deployment_retention_state` (the underscore matches the current command catalog). It is registered through `ICoopCommand` and the existing Autofac assembly scan.

## Preconditions and real identities

Use a disposable campaign save with exactly two connected players, each with healthy troops, outside settlements and outside any battle, on land, and in distinct map factions. Danustica (`town_ES1`, Southern Empire) is a concrete staging location; remain outside its gates. Keep the initial save for restoring casualties or other campaign side effects after the fixture. The dedicated server has neither a player party nor a mission.

Run on the **server**, then on **both clients**:

```text
coop.debug.players.list
coop.debug.map_event.deployment_retention_state status
```

Use the server's two actual `ControllerId` and `Party` registry values; verify each `Party` is resolved and controlled on both clients. Label the displayed players A and B in the evidence, preserving their real names and IDs. They are runtime identities, not fixed `PlayerOne`/`PlayerTwo` strings. For each player, invoke `coop.debug.players.party_state` on the server with that exact `ControllerId`; require `connected=true`, `active=true`, and `mapEvent=none`. The command also returns `partyStringId` for inspection with `coop.debug.mobileparty.info` on either side. Confirm the saved prerequisites before proceeding; do not pick an arbitrary AI party if they fail.

At map idle, run on the server and each client:

```text
coop.debug.map_event.deployment_retention_state reset
coop.debug.map_event.deployment_retention_state status
```

Require successful command results; `schemaVersion=1`; matching MVID; the correct `role`; all four observed/alive counts and both override counters zero; `currentMissionActive=false`; `coopBattleActive=false`. Reset returns `resetPerformed=true`. An earlier surviving target makes reset fail with `observations_alive`; capture it and investigate instead of discarding observations. A missing Debug command or wrong binary is an unexercised fixture, never a pass.

## Two bounded rounds in the same processes

1. **Authoritative setup.** On the server invoke `coop.debug.map_event.start_player_field_battle` with A's literal `Party` registry value as the first argument and B's as the second. Copy the returned `MapEventId` into the evidence. Require success. This existing fixture validates ownership, connection, different factions, and map state before using the authoritative hostile-encounter service; it records the original war relationship for restoration. Run `coop.debug.players.party_state` with each recorded controller ID: both must identify the same active event. A rejected setup is unexercised; do not replay it after success or while restoration is pending.
2. **Native entry.** Both clients use the ordinary encounter **Attack** and deployment **Ready** UI. Campaign-changing setup and exit cheats remain server-side. Record `coop.debug.map_event.deployment_state` and `coop.debug.map_event.deployment_retention_state status` on both clients during deployment and after Ready. Require actual player/troop spawning and progression into battle, not a spectator or stuck deployment. Bound entry to 60 seconds after both clients are ready; failure is a deployment failure to diagnose, not permission to force the spawn gate.
3. **Positive diagnostic oracle.** At least one client must report `overrideCalls>0`, `emptyTeamOverrideCalls>0`, `totalObservedTeams>=1`, `totalObservedMissions>=1`, `aliveTeams>=1`, `aliveMissions>=1`, `currentMissionActive=true`, and `coopBattleActive=true`. This client exercised the actual empty foreign-team branch. Retain both clients' records and apply cleanup assertions to every client with observations. Do not assign the observer based on who launched the server or who was expected to own the battle. `lastOverrideSide` and `lastOverrideActiveAgents` explain the latest call; a later populated call may change the latter, so it need not remain zero. The cumulative empty-call counter supplies the earlier empty-team proof. Zero observations cannot pass.
4. **Deterministic exit.** On the server invoke `coop.debug.map_event.finish_player_encounter` with A's recorded controller ID, then with B's if B is still in that event. These requests use `PlayerLeaveBattleAttempted`. Wait for each client's actual return to the campaign map, at most 60 seconds. Record `coop.debug.map_event.encounter_state` on each client and `coop.debug.players.party_state` on the server for both IDs. Require no client mission/encounter/map event, both server player records at `mapEvent=none`, and retention status with both active flags false. A pending native mission or battle flag is a teardown failure; `collect` must refuse it with `battle_active`.
5. **Release oracle.** Capture ordinary `status` first. Then, only at the verified map-idle boundary, run on each observing client:

   ```text
   coop.debug.map_event.deployment_retention_state collect
   coop.debug.map_event.deployment_retention_state status
   ```

   Require successful results, `collectionRequested=true` on the first result and false on the second, **`aliveTeams=0` and `aliveMissions=0`**, both active flags false, and unchanged positive cumulative counts/counters. The explicit Debug GC is an observation probe after teardown, not the mechanism that releases references. Product code never invokes it. There is no soak or process-memory threshold.
6. **Restore the setup.** Once the server confirms both parties left the event, run `coop.debug.map_event.restore_player_field_battle` on the server. Require success. Its `PeaceRestored=false` is valid when the players were already at war; compare with the setup's `OriginalWarState`. It restores the fixture's war relationship, not casualties or all campaign effects. Restore the disposable save after the campaign work is finished if needed.
7. **Repeat immediately.** Do not reset observer counters or restart either client. Repeat steps 1–6 once. Capture each first-round cumulative count as the second-round baseline. The observing client must have a new positive empty-call delta and a larger `totalObservedMissions`; after exit, both live counts must again be zero. An old positive counter alone does not prove the second round exercised the branch. Check the same PID and MVID throughout.
8. **Reset proof.** After both successful rounds, run `reset` twice on each process, followed by `status`. Both resets must succeed and leave zero counts/counters and inactive flags. Reset does not collect and cannot discard live targets. No diagnostic state remains to contaminate the next fixture.

The two-round procedure covers authoritative forced exit. For normal victory and retreat coverage, repeat with normal player UI completion or Retreat instead of step 4, retain the same native-exit and collection assertions, and report those paths separately. Failed-start and exception paths have the same static-root absence invariant and focused synthetic guard/finalizer tests; do not label those tests as native exit-path passes. A runtime failure before any observed override is unexercised retention coverage, not proof of cleanup.

## Failure evidence

Preserve each command's success/error code and full JSON, server stdout, separate client logs, timestamps, PID, MapEventId, real player/party IDs, source identity, and artifact hashes. If entry stalls, include `deployment_state`, `encounter_state`, and both sides' party-state output. If an observed team or mission survives the post-exit collection probe, **full release failed**: do not reset or start another mission to hide it. Capture a managed dump and inspect roots for the surviving `Team`, `Mission`, and reachable `ObjectManager` before restarting. Compare stable map-idle counts after both rounds if a dump is captured. Weak-reference status does not enumerate every mission or registry in the process.

The specific diagnostic root must be absent. Roots owned by a mission Autofac scope (#3109) or `NetworkAgentRegistry` (#3113) are separate defects; name the observed root and keep full mission-release verification blocked rather than accepting nonzero counts. No claim about the entire reported multi-gigabyte private/native memory delta follows from this fixture.

## Non-live checks

Build `source/CoopTests.slnf` in Debug and Release in an isolated compile/test workspace with game reference assemblies. Run the focused `E2E.Tests.Services.Missions.CoopDeploymentPatchTests` class in both configurations, the Release `source/CoopUnitTests.slnf` suite, and the Release missions E2E subset. Debug tests exercise actual managed patch/command methods with synthetic graphs; they cannot prove native scene teardown. The static-root test must fail on the unchanged base assembly and pass on the corrected assembly. The `GameInterface` Debug compile against installed v1.4.8 references verifies the command's game API bindings without launching anything.

Do not build/deploy the `Coop` module, rotate a checkout, launch any game/server/client, or admit a live-test lane as part of these checks. Runtime confirmation is pending.
