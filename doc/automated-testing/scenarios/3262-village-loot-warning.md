# Issue 3262: village raid loot warning

This fixture is prepared for non-live review. Live testing has not started. Unit and compile checks cannot establish the UI result below.

Use the existing issue 3262 PR branch, merged with the current `development`, and build Debug. The fixture commands and hook are excluded from Release. Before a later authorized runtime run, record `git rev-parse HEAD`, `git rev-parse HEAD^{tree}`, the baseline save SHA-256, build configuration, game version, and the deployed assembly hashes. Require both clients/server to report the same `buildVersion`, `sourceCommit` and `assemblyMvid` from the state command. An unknown source commit requires the external build manifest to bind those binaries to the recorded head/tree. Record the preparation output's fixture token alongside every case's evidence.

## Disposable baseline and setup

Use one dedicated authoritative server and one connected client. The server has no player party. Save a baseline with the client on land, holding outside any settlement, army, siege or map event. The client must have a healthy living leader, exactly one copy of that leader in the party, and no companions or hero prisoners. Its faction must differ from Polisia's faction. Polisia must be normal, not under siege or involved in a map event; no other mobile parties may be inside it. Its bound town must not be under siege.

The fixed location is **Polisia**, settlement `village_ES1_2`, village component `village_comp_ES1_2`, bound to **Danustica**, settlement `town_ES1`, town `town_comp_ES1`, in the Southern Empire at campaign start. Ownership can change in a save; preparation checks the actual factions.

For each case, load a **fresh copy of the baseline save**. Never save fixture changes over the baseline. The fixture permanently changes war state, troop rosters, position, village hit points, and subsequent battle outcomes. It has no restore command. Restore the prior raid-intervention setting after the campaign (it is session configuration, not part of the copied save). Reload the entire baseline after a failed preparation, defeat, completed case, disconnect, or unexpected event. A repeat prepare only succeeds before the raid starts while the same target still has the staged idle state; it does not add troops again. A different target or a partial mutation is rejected until reload.

On the server, run:

```text
coop.debug.players.list
coop.debug.mapevent.allow_raid_ai_intervention on
coop.debug.mapevent.allow_raid_ai_intervention status
coop.debug.mapevent.raid_loot_warning_prepare only-connected disposable-baseline
coop.debug.mapevent.raid_loot_warning_state only-connected
```

`only-connected` is an actual supported selector and requires exactly one registered player; preparation also requires that player to be connected. For a session with more registrations, copy the exact `ControllerId` from `coop.debug.players.list` as the first argument to both commands. Do not invent controller or party ids. The server checks all prerequisites before staging. It declares war if necessary, preserves the player's leader, replaces other troops with exactly 60 of that leader's culture basic troop, replaces Polisia's settlement party troops with exactly eight of its culture basic troop, clears nonhero prisoners and village items, sets village hit points to one, and places the player at Polisia's land gate in hold mode. It does not enter the settlement, start a raid, start a simulation, or manipulate a client screen.

On the client, read:

```text
coop.debug.mapevent.raid_loot_warning_state only-connected
```

Within 30 seconds, both sides must report the same controller/party ids, Polisia ids, position, 61 player members with zero wounded (leader plus 60 basic troops), eight healthy village members, village state `Normal`, hit points `1`, and `atWar=true`, `allowRaidAiIntervention=true`. Disabled raid intervention would skip the intended village-resistance path; preparation rejects it. Preserve the actual troop ids returned by `playerMembers` and `villageMembers`. If they differ, stop and capture both state outputs and logs; do not compensate with client cheats. `fixtureToken` and `fixturePhase` are authoritative server fields; the client's `not-prepared-on-this-side` label is expected because observing never creates fixture state. Correlate client evidence through controller/party ids and `mapEventId` with the server's `capturedMapEventId`.

## Native UI actions and required observations

1. On the client, click Polisia on the world map. Enter normally, select **Take a hostile action**, then **Raid the village**. Factions are already at war, so an additional declaration-of-war warning is not expected. No command may invoke the client action callbacks.
2. Read state on both sides. The client `mapEventId` must equal the server `capturedMapEventId`. Server `fixturePhase` must become `captured`, event type must be `RaidEventComponent`, and event settlement must be `village_ES1_2`. If capture is absent or the wrong event/party appears, stop; that is an unexercised case.
3. Choose **Send troops** on the native encounter menu. Let the simulation run, using its native fast-forward control if desired. Poll the read-only state after transitions, at most once per second. Allow 120 seconds for an attacker victory/result screen. Defeat, a mission opening in this case, no simulation, or timeout is a failed setup; reload the baseline instead of forging a result.
4. Click native **Done** on the simulation result, then native **Done** on the troop/prisoner loot screen without transferring anything. Record `simulationResultVisible` and `partyLootActive` as those states occur. Allow 30 seconds for each UI transition.
5. At the native inventory loot screen, record client `inventoryActive=true`, `topScreen=GauntletInventoryScreen`, `otherItemCount>0`, and `otherGrainCount=1`. On the server, require `fixturePhase=seeded`, `seedApplied=true`, `rawLootGrainCount=1`, and the matching captured event id. The hook normalizes the authoritative loot roster to one grain once, only for this raid's winning player party, before the ordinary results message is packed. It does not send a separate loot message or seed unrelated raids.
6. Apply the case action below using native buttons. Record video or screenshots of the warning text and every response click. `inquiryActive` reports any native inquiry, so it must be paired with the visible leaving-loot warning, not treated as proof of a specific dialog by itself.
7. Within 30 seconds after accepting or taking all loot, inventory and the leaving-loot warning must close. If the normal village result menu is shown, use its native **Leave**. Within a further 30 seconds, require client `topScreen=MapScreen`, `inventoryActive=false`, `partyLootActive=false`, `inquiryActive=false`, `encounterState=null` and `partyCurrentSettlementId=null`. Require server `capturedMapEventFinalized=true`, `capturedMapEventRegistered=false`, `capturedMapEventStillAttached=false`, and no current event id on both sides. Observe for another five seconds: no warning or loot screen may reappear. The fixture does not advance menus or release encounters for the test.

## Case matrix

Reload the original copied baseline before every row, repeat preparation, and record the new token.

| Case | Battle path | Native loot action | Required result |
| --- | --- | --- | --- |
| A: original report | Send troops | Transfer nothing, click Done, then Yes on the leaving-loot warning | Exactly one warning presentation; Yes dismisses it and normal departure completes |
| B: cancel then accept | Send troops | Transfer nothing, Done, No; verify inventory remains and grain is still one; Done, Yes | No returns to inventory; the second intentional Done shows one warning; Yes dismisses it without another unsolicited warning |
| C: take all | Send troops | Use native Take all, verify other item count and grain count are zero, Done | No leaving-loot warning; normal departure completes |
| D: fought battle regression | Native Attack / fight the village battle | Win by normal gameplay, then transfer nothing, Done, Yes | Same seeded loot and dismissal assertions after the native mission path; no command finishes or changes the battle |

Case D requires a recorded actual mission and attacker victory, not a synthetic battle outcome. Deterministic staging fixes troop types/counts, location and loot; combat results and timing remain native. A defeat is a setup failure requiring reload and must not be reported as product success. An unrelated field battle or a second raid during a campaign with a used fixture must never receive another seed.

## Failure diagnostics

Preserve server and client command output immediately before and after the failed transition, fixture token, controller and party ids, captured/current map event ids, roster state, source identity, timestamps, and screenshots/video of the visible UI. Copy the client log before any relaunch because startup replaces it; save the dedicated server console output. Search those copies for `Issue 3262 fixture`, `Failed to get`, `NetworkCommitMapEventResults`, `MapEvent`, `PlayerEncounter`, and exceptions. Also record `allowRaidAiIntervention`: a changed setting or additional party intervention invalidates the bounded fixture and needs its own diagnosis.

Distinguish failures precisely:

- Missing capture, seed, grain, simulation or loot transition means the oracle was not exercised.
- A leaving-loot warning that remains or reappears after Yes reproduces issue 3262.
- Inventory closes but encounter/event cleanup does not converge is a separate lifecycle failure with the same captured event evidence.
- Readiness state mismatch is a replication/setup failure; retain logs and reload the baseline after diagnosis.

Do not infer a pass from unit tests, command success, an empty inventory that never had the seed, a missing inquiry before any native Done, or an automatically closed screen without the native warning click. This document supplies future live-test steps only; it authorizes no launch, deployment, rotation or live lane.
