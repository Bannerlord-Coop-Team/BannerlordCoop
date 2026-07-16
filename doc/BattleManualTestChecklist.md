# Battle Manual Test Checklist

**Companion to:** [BattleRequirements.md](BattleRequirements.md) (v1.0 baseline) and
[BattleRequirementsTestPlan.md](BattleRequirementsTestPlan.md)

Requirements whose substance is engine-level (a live `Mission` / `BattleEndLogic`, real spawning, AI/formation
control, or in-engine command grant) cannot be verified in the headless E2E/integration harness. They are
verified by live 2-client play using the steps below. Each row is one pass/fail check.

Setup for every scenario unless noted: host + at least one joining client, both in the same campaign, driven
into a shared battle mission (field battle unless the scenario says siege).

## BR-050 — Player Retreat (retreat permission gate)

Coop adds no retreat condition of its own — the requirement is that native retreat gating is not broken. The
coop contribution is two Harmony reshapes: the retreat-confirmation inquiry still shows for a
`NeedsPlayerConfirmation` exit result (`DisableRetreatConfirmationPausePatch`), and a siege defender's
`SurrenderSiege` exit is downgraded to `NeedsPlayerConfirmation` while a coop battle is active
(`SiegeBattleExitPatch`). These scenarios confirm the native condition gate itself, which the unit tests cannot.

| BR | Scenario | Steps | Expected |
|---|---|---|---|
| BR-050 | Retreat NOT offered when native conditions are unmet | In a live coop field battle, attempt to retreat/leave the mission while the game's retreat conditions are not satisfied (e.g. immediately at battle start, or while surrounded by the enemy). | The retreat option is not available / the retreat is refused; no retreat-confirmation pop-up appears. The player stays in the battle. |
| BR-050 | Retreat offered when native conditions ARE met | Continue the same battle until the native retreat conditions are satisfied (safe distance / disengaged), then attempt to retreat. | The native retreat-confirmation inquiry appears; confirming it removes the player's surviving troops (BR-051) and the player leaves the battle instance (BR-054). The battle persists for any remaining players. |
| BR-050 | Retreat inquiry still pauses/prompts (not auto-confirmed) | Trigger a `NeedsPlayerConfirmation` retreat and observe the prompt. | Exactly one retreat inquiry is shown and it waits for the player's choice (the coop patch preserves the confirmation, it does not silently auto-retreat or skip the prompt). |
| BR-050 | Siege defender leave is a personal retreat, not a garrison surrender | In an active coop siege with multiple players on the defending side, have one defender player leave the mission. | The leave is treated as a personal retreat (ordinary confirmation prompt), NOT a whole-garrison `SurrenderSiege`. The leaver's surviving troops are adopted via host migration (BR-031) and the siege continues for the remaining players. |

## BR-025 — Deployment Time Limit (native auto-finish on expiry)

The timer gate and its commit wiring are unit-tested headlessly (`BattleDeploymentTimerTests`,
`BattleDeploymentTimeLimitWiringTests`); what only a live mission can verify is the expiry actually invoking
the native finish — `DeploymentHandler.FinishDeployment()`, the same method the deployment UI's Start Battle
button funnels into — so the un-pause, hero handoff, deployment-view teardown, reveal (BR-023), and NPC
release (BR-024) all run as if the button had been clicked.

| BR | Scenario | Steps | Expected |
|---|---|---|---|
| BR-025 | Deployment auto-finishes when the limit expires | Set `BattleDeploymentConfig.DeploymentTimeLimitSeconds` to a short value (e.g. 20). Host + client enter a shared field battle; BOTH players idle on the Order-of-Battle screen without clicking Start Battle. | About 20s after each player's loading screen ends (per-player clocks — a slower loader expires later), that player's deployment finishes by itself: the OoB screen closes, the player gets their hero at the deployed position, and their troops stand at their current placements. The first expiry releases the NPC AI (BR-024) and each player's own troops become visible to the other on that player's expiry (BR-023). |
| BR-025 | Manual finish inside the limit disarms the timer | Same setup; one player clicks Start Battle well before the limit while the other idles past it. | The clicking player's battle starts immediately on the click and nothing re-fires at the limit mark; the idle player is auto-finished at their own expiry. Exactly one deployment-finish per player (no duplicate reveal, no re-announce burst in the log). |
| BR-025 | Zero disables the limit | Set `BattleDeploymentConfig.DeploymentTimeLimitSeconds = 0` and idle in deployment far longer than the old limit. | Deployment never auto-finishes; the player commits only via Start Battle. |

## Residuals for later waves (noted here, not yet scripted)

These are live-mission behaviors adjacent to this wave's requirements; capture them as their own manual
scenarios when those requirements are scheduled.

| BR | Residual to verify live | Why it is manual-only |
|---|---|---|
| BR-012 | The mission host actually drives every player-unowned NPC party's agents (movement, formation, and AI decisions) in the running mission. | Runtime NPC AI control quality is engine-level; the headless harness verifies only the ownership/assignment routing, not the agents maneuvering. |
| BR-033 | After a disconnect + reconnect mid-battle, the returning player's surviving troops actually MOVE under their control again on every client: the returner's orders/AI drive them, the (former holder) host's copies follow the returner's replicated movement instead of freezing or fighting the interpolator, and the returner's hero is playable (camera + input) when alive. | The reclaim's registry/authority/Controller flip is E2E-tested (`BattleReconnectControlTests`), but movement is IPacket (not routable in the harness) and interpolator-vs-AI interaction is engine-level — only live 2-client play shows the handed-back agents walking. |
| BR-020 | A joining player receives in-engine COMMAND of its own party's troops (Order-of-Battle over `PlayerTeam` + `MainAgent` control), beyond the reserve being delivered. | The command grant is native (OoB + agent control) and needs a live mission; the reserve-delivery half is E2E-testable, the control half is not. |
| BR-024 | NPC party troops are released — begin moving under AI/formation control — when the FIRST participating player finishes deployment, regardless of whether that player is the mission host; troops of players still deploying stay hidden. | Actual spawn/visibility/formation release is observable only inside a live `Mission`. |
| BR-073 | Reinforcement waves actually field in a live large battle: in a ~2000-troop 2-client field battle (BattleSize 600), each client's casualties open the wave gate and later engine pulls spawn `isReinforcement: true` waves until the reserve drains — the battle no longer stalls at the initial allotment (live battle 406's 600-of-2000 freeze). Also confirm the non-fielding client's view: the foreign side keeps fighting at strength (its quota is advanced on its owner, replicated troops appear as puppets) and no side is declared depleted early. | The origin→supplier quota feedback and the native wave gate are E2E-tested against the real `MissionBattleSideSpawnContext`/`ComputeWaveBatch`, but the live pull runs on the engine's global 3s reinforcement tick inside a real `Mission` with both clients' suppliers and the puppet replication live — only 2-client play shows the waves walking onto the field. |
