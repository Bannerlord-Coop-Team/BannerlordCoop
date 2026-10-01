# Retreat and rejoin health

Run through the existing Local private-desktop carrier with two real clients,
`testclient` and `testclient2`, on the adopted Debug source. Prepare that exact
source through the committed-source rotate path inside the Local lease before
build/cache validation. Use the existing named-pipe command and screenshot
actions, never desktop input. This checks numerical health persistence;
screenshots document the real mission and campaign state, not numerical health.

The executable scenario is [1627-retreat-rejoin-health.ps1](1627-retreat-rejoin-health.ps1).
Its [lease workload](1627-retreat-rejoin-health.sh) reuses the shared driver,
committed-source rotate and integration launcher. Invoke it once through the
existing Local lease producer after primary review and source adoption. Bind
`ISSUE1627_ATTEMPT_ROOT` to a fresh retained directory and `ISSUE1627_RUN_TOKEN`
to a fresh token; the producer supplies the adopted source and lease identity.
The canonical pipeline records shared helper hashes, which are checked before
rotation. The workload freezes and validates its exact source archive before
changing the bench.

The scenario's `-SelfCheck` option exercises its comparison predicates without
launching or mutating games. Parser and self-check receipts are retained in the
workflow artifacts; these checks do not establish live coverage.

## Runtime preflight and setup

1. Require the exact token-bound server and both client endpoints, current source
   build identity and campaign readiness. Cold endpoint startup gets 600000 ms.
   Validate all declared command names through their endpoint command catalogs.
2. Read `coop.debug.map_event.late_join_mode_fixture_state testclient testclient2` and player state before
   setup. If the loaded save has the shared idle encounter, the existing server
   `coop.debug.map_event.battle_reward_fixture_prepare testclient testclient2`
   checks and closes only that unresolved saved event. Other preflight failures
   end the attempt; do not finalize an arbitrary battle.
3. On the server run `coop.debug.map_event.late_join_mode_fixture testclient testclient2 health`,
   retain its returned map-event id and original roster/hero-health receipt. The
   optional Debug `health` mode snapshots all three parties' exact roster counts,
   wounds and experience and every roster hero's health. It keeps heroes and
   stages 1,200 healthy culture-basic regular troops for each player and 1,200
   ordinary bandits for the opponent, exceeding the current maximum battle size
   of 1,000 without changing battle configuration, hero health or mortality.
   Those synthetic roster writes are setup only. Then run
   `coop.debug.map_event.late_join_mode_join`. Read the existing fixture state to
   require both parties in that same unresolved event. Enter the second client
   through `coop.debug.map_event.late_join_mode_enter`.
4. On the server request `deploy` through
   `coop.debug.map_event.health_fixture_request`, with the selected controller
   and exact returned map-event id. It calls `DeploymentHandler.FinishDeployment`
   without changing health or mortality. Wait for committed deployment and
   actual active agents on both clients. Use this same route on every re-entry;
   the unrelated hit-sound fixture's `begin` heals and protects the main hero.
5. Capture baseline PNGs on both clients at the first renderable state using
   `Invoke-LiveTestScreenshotBatch`. Use a fresh Windows-local capture directory;
   retain raw captures, PNGs and the existing guarded capture receipts.

## Actions and independent comparisons

Read `coop.debug.battle.health_state` on each client and
`coop.debug.map_event.health_reserve_state` on the server. All return
`LIVE_TEST_JSON`. Resolve agent ids from these actual responses, not fixed ids.
The agent diagnostic uses character `StringId`; reserve `CharacterId` uses the
registry's `CharacterObject_` prefix. Retain the binding after requiring an
actual matching baseline reserve entry. Use that same binding for survivor,
casualty and fresh-battle comparisons. A missing fresh troop match is a failure,
not evidence of default health.

Select an active owned hero, at least three
owned regular troops, a healthy control troop, and an unspawned ledger entry.
Require each tested player's actual ledger `entries.Length` to exceed `supplied`
after deployment, and select the unspawned descriptor from that unsupplied tail.
Provisioned counts alone do not prove that a troop stayed unspawned.
Fail the runtime preflight if the mission cannot supply them; do not silently drop
coverage. Retain character names, party ids, descriptor seeds and agent ids.

From the server invoke `coop.debug.map_event.health_fixture_request` with the
selected controller, exact returned map-event id and operation:

- `damage` takes the selected registered agent id and a positive integer blow
  amount. Damage the hero and regular troop, then read their actual health.
  The requested amount is not an expected health value; native damage settings
  can change the result. Require positive health below the captured baseline.
- Damage a second regular troop, then request `rout` with its registered id.
  Wait for actual native removal, retaining its last positive health and the
  server reserve/roster observations. Requesting a rout alone is not a pass.
- Apply a fatal `damage` blow to a third regular troop. Require a real casualty
  transition and authoritative roster reduction before proceeding. An active
  agent with zero health does not satisfy this check.
- `retreat` takes no agent id or amount. Request it for the withdrawing owner.
  Wait for campaign return and departure/reserve cleanup while the other client
  remains in the unresolved battle. Retain observations on all three endpoints.

Re-enter on the withdrawing client through `coop.debug.map_event.enter_current_battle`
with no arguments. It calls the
production blocking mission-start coordinator. Finish deployment without a
health reset. Compare the new owner and observer health distributions grouped
by actual party and character id, since ordinary troop descriptor seeds can
change on rebuild. Require the damaged hero and ordinary troop health to equal
their last surviving values, the routed survivor's health to remain present,
the casualty count to remain excluded, the healthy control to remain healthy,
and the unspawned reserve to retain its prior health/default state. Fail on
additional combat that makes the numerical comparison ambiguous.

Repeat retreat and re-entry once more without staging damage or resetting
health. Require the same distribution and observer agreement again. Capture
decisive pass PNGs on both clients and retain the exact numerical excerpts.

## Finalization, evidence and cleanup

Use `coop.debug.map_event.late_join_mode_exit_missions`, wait for both campaign
returns, then `coop.debug.map_event.late_join_mode_cleanup` to finalize the test
battle and restore its owned movement state. Query `coop.debug.map_event.health_reserve_state` with the retained old id even
when the map event has been unregistered. Require `registered=false` and an empty
`parties` array; a failed lookup is not evidence of an empty ledger. Cleanup also
restores the exact original three rosters and captured hero health. Compare the
restored values with the setup receipt.
Create a new battle with the same fixture and inspect fresh reserves: no prior
ordinary troop partial-health values may be carried forward. Hero campaign
health is separate and must not be used to claim ordinary snapshot cleanup.
Close that fixture through the same existing exit/cleanup commands. Capture
final/restored PNGs on both clients.

On the first failure, capture both renderable clients before cleanup and retain
the first failure plus any cleanup errors. After successful cleanup and campaign
return, retain final/restored captures even if the scenario body failed. Use the
original or fresh setup receipt belonging to the last created fixture for
restoration checks; keep the first failure and failed verdict. Capture the
reached final state even if a restoration comparison itself fails.
The existing carrier owns process
cleanup once. Retention must succeed before deleting only this attempt's raw
capture directory. Every image receipt must retain role, checkpoint, pid,
completion time, relative path and SHA-256. The parent independently accepts
the retained numerical proof and classifies any visual claims. UI retreat
button wiring is outside this functional no-focus check and remains unverified.
