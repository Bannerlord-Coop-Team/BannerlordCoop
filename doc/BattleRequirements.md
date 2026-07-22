# Battle Requirements

**Version:** 1.2 (2026-07-21)
**Status:** Baseline (v1.2 adds Section 12 / BR-110 — the engine's 2000-agent render limit; v1.1 amended BR-017 — abandonment destroys the instance only; the map event persists for player-discretion resolution — with matching scope updates to BR-003 and BR-054)

## Scope

These requirements define the **base battle system** for Bannerlord Coop: the creation, hosting,
authority, synchronization, and resolution rules common to every battle backed by a playable
mission. All battle types — field battles, sieges, sally-outs, hideouts, naval, and any future
mission-backed map event — are built on this system. Battle-type-specific requirements (siege
engines, naval boarding, etc.) are layered in separate documents and shall not contradict this one.

## Definitions

| Term | Meaning |
|---|---|
| **Campaign server** | The authoritative server for persistent campaign state. Arbitrates host elections, detects disconnections, and commits final battle results. |
| **Map event** | The campaign-side record of a battle between parties. |
| **Battle instance** | The mission-scoped record of one playable battle: its unique identifier (BR-104), its map event, and its participant list. Created/identified by the server; hosted by player clients. |
| **Mission mesh** | The peer-to-peer network of the players currently inside a battle instance. |
| **Mission host** | The player holding the live-mission authority delegated by the server (BR-011). |
| **Mission-ready** | A client has finished loading the battle mission and can spawn its granted troops. |
| **Assignment** | The persistent record of which party a troop belongs to and which player is entitled to control it. |
| **Control** | Which client currently drives an agent. Control may temporarily differ from assignment (e.g., BR-031). |
| **Deployment finish** | A player committing their own deployment ("Start Battle"). |
| **Activation** | The moment the battle goes live: NPC troops are released (BR-024). |
| **Player simulation** | A player-selected automatic resolution of their own encounter ("Send Troops" or equivalent). |

---

## 1. Battle Creation and Participation

### BR-001 — Battle Resolution Mode

A map event shall be resolved using either:

1. a playable battle mission; or
2. automatic battle simulation.

The two resolution modes shall be mutually exclusive after battle resolution has begun.

### BR-002 — Battle Instance Creation

When a player selects the playable battle option, the server shall create — or identify, if one
already exists — the battle instance record for the corresponding map event: its unique mission
identifier (BR-104) and its association with the map event.

The battle mission itself runs on the participating players' clients (the mission mesh); the server
does not run the mission.

### BR-003 — Player Simulation Restriction

Player simulation ("Send Troops" or an equivalent player-selected automatic resolution) shall not
be available for a map event after a playable battle mission has been created for it.

A playable battle mission shall not be created for a map event after player simulation of that map
event has begun.

This requirement restricts **player simulation only**; it does not restrict map-side simulation of
AI-vs-AI map events before a player joins.

The mutual exclusion is scoped to the battle instance: if the battle instance is destroyed while
its map event is still unresolved (BR-017, BR-054), both resolution options become available
again.

### BR-004 — Player Eligibility

Only players whose parties are valid participants in the corresponding map event shall be permitted
to join the battle mission.

### BR-005 — Late Joining Player

An eligible player shall be permitted to join an active battle mission at any time before the
battle has ended — that is, before completion conditions have been met and result finalization has
begun (BR-075).

### BR-006 — Late Joining NPC

NPC parties shall only be able to join the battle during the first 1 game day after the
corresponding map event is created, measured in campaign time.

Campaign time may continue to advance or be paused while the battle mission is running; the join
window elapses only as campaign time advances.

### BR-007 — Armies

Armies shall be able to join the battle with the same join restrictions as mobile parties.

---

## 2. Mission Hosting and Authority

### BR-010 — Initial Mission Host

The first player to become **mission-ready** — to finish loading the battle mission, as observed
by the campaign server — shall become the mission host.

The campaign server shall arbitrate the election (BR-101) and shall issue the unowned (NPC) troop
reserves to the elected host together with the election, so that the host fields the NPC troops
as soon as it is able to spawn them.

Host migration (BR-014, BR-015) shall apply from the election onward — including while other
players are still loading or deploying — so a host that becomes unavailable before activation
does not strand the battle.

### BR-011 — Host Authority

The mission host shall be authoritative for the battle mission state delegated to the mission host
by the server.

The campaign server shall remain authoritative for persistent campaign state and final battle
results.

### BR-012 — NPC Party Control

The mission host shall control all participating NPC parties and NPC-controlled battle agents that
have not been assigned to another player.

### BR-013 — Host Connection Order

The campaign server shall maintain, for each battle instance, an ordered list of players based on
the sequence in which they became mission-ready in the mission instance, as observed by the
server. A player still on the loading screen has not yet joined for the purposes of this
ordering, so the first entry in the list is the initially elected host (BR-010).

### BR-014 — Host Migration

When the mission host disconnects, leaves, or becomes unavailable, hosting authority shall migrate
to the earliest remaining player in the mission connection order (BR-013) still connected to the
mission mesh.

Any player still connected to the mission mesh is eligible to host, regardless of the state of
their agents or troops (a player whose troops are all dead may still host).

### BR-015 — Repeated Host Migration

Host migration shall continue through the mission connection order until a valid host is found or
no players remain in the mission instance. If no players remain, BR-017 applies.

### BR-016 — Host Migration Continuity

Host migration shall not restart the battle mission, respawn defeated troops, or reset battle
progress.

### BR-017 — Abandoned Battle

If no players remain in an active battle mission — through disconnection, retreat, or leaving —
the server shall destroy the battle instance.

The map event itself shall persist, reflecting the last synchronized battle state, and shall
remain available for resolution at the players' discretion: a re-engaging player may start a new
playable battle mission (BR-002, BR-054), or players may choose player simulation — BR-003's
mutual exclusion resets when the battle instance is destroyed.

---

## 3. Player and Troop Assignment

### BR-020 — Initial Troop Assignment

When a player joins a battle mission, the player shall receive control of the troops assigned from
their participating party.

### BR-021 — NPC Assignment

Troops belonging to an NPC party shall remain under host control unless explicitly assigned to a
participating player.

### BR-022 — Player Party Assignment

A player shall not control troops belonging to another player's party unless control has been
transferred because of disconnection, retreat, delegation, or another explicitly supported
mechanic.

### BR-023 — Deployment Visibility

A player's controlled troops shall not be visible to other players until that player has finished
deployment.

(NPC troops are visible — held frozen — during deployment; their release is governed by BR-024.)

### BR-024 — Releasing NPC Troops

NPC party troops shall be released — begin moving under AI and formation control — when the
**first** participating player finishes deployment, regardless of whether that player is the
mission host.

Troops belonging to players who have not yet finished deployment are unaffected by the release:
they remain hidden per BR-023 until their own player's deployment finish.

### BR-025 — Deployment Time Limit

Each player's deployment phase shall be limited by a deployment time limit, beginning when that
player becomes mission-ready.

When a player's limit expires, that player's deployment shall be finished automatically with
their troops at their current positions — making them visible (BR-023) and counting as a
deployment finish for activation (BR-024).

The duration of the time limit is a game-configuration value.

---

## 4. Disconnection and Reconnection During Battle

### BR-030 — Disconnected Player Detection

A player shall be considered disconnected from an active battle mission when the player's
connection to the **campaign server** is unexpectedly lost.

The campaign server shall detect the disconnection and is responsible for notifying the battle
mission's remaining participants (including the mission host).

Loss of peer-to-peer connectivity within the mission mesh alone, while the player's server
connection remains, does not constitute disconnection from the mission.

### BR-031 — Temporary Host Control

When a player disconnects from an active battle mission, the mission host shall temporarily assume
control of the disconnected player's surviving assigned troops.

### BR-032 — Reconnection Eligibility

A disconnected player shall be permitted to reconnect to the battle mission while:

1. the mission remains active;
2. the player remains a valid participant in the map event; and
3. the player has not permanently retreated, surrendered, or otherwise left the battle.

### BR-033 — Restored Troop Control

After reconnection and synchronization, the player shall resume control of their previously
assigned surviving troops.

### BR-034 — Invalid Former Troops

Troops that were killed, wounded, routed, captured, retreated, or otherwise removed while the
player was disconnected shall not be restored when the player reconnects.

### BR-035 — Reconnection During Host Migration

A reconnecting player shall not interrupt an active host migration.

The reconnecting player may become host only according to the defined host-selection rules.

---

## 5. Disconnection Outside the Battle Mission

> **Status: Deferred (post-MVP).** This section is not required for the MVP. The requirements are
> retained for a later milestone.

### BR-040 — Disconnection Outside Mission *(deferred)*

When a player disconnects while their party is participating in a map event but the player is not
inside its battle mission, the player's party shall be removed from the map event.

### BR-041 — Army Member Removal *(deferred)*

If the disconnected player's party belongs to an army, the party shall be removed from both the
map event and the participating army as required by campaign rules.

### BR-042 — Map Event Recalculation *(deferred)*

After a disconnected party is removed, the server shall recalculate the map event's participating
parties, troop counts, encounter eligibility, and battle state.

### BR-043 — Empty Battle Side *(deferred)*

If removing the disconnected party leaves one side with no remaining participating parties or
troops, the server shall resolve or cancel the map event according to the applicable
battle-resolution rules.

---

## 6. Retreat and Withdrawal

### BR-050 — Player Retreat

A player shall be permitted to request retreat only when the game's retreat conditions are
satisfied.

### BR-051 — Party Troop Withdrawal

When a player successfully retreats, all surviving troops assigned to that player's party shall be
removed from the battle mission.

### BR-052 — Retreat Casualties

Troops killed, wounded, captured, routed, or otherwise lost before retreat shall not be restored
merely because the party retreated.

### BR-053 — Partial Army Retreat

When a player retreats while participating as a member of an army, only the retreating player's
party and assigned troops shall be removed unless the army commander initiates an army-wide
retreat.

### BR-054 — Retreat and Re-engagement

A player who retreats shall be removed from the battle instance's mission mesh.

If no players remain in the battle instance after the retreat, the battle instance shall be
destroyed (BR-017; the map event persists in its last synchronized state).

If other players remain, the battle instance shall persist, and a retreated player who re-engages
the same map event shall re-enter that persisting battle instance, with their troops entering as
reinforcements, subject to join eligibility (BR-004, BR-005).

If the battle instance was destroyed, a subsequent re-engagement shall create a new battle
instance per BR-002.

---

## 7. Surrender

### BR-060 — Surrender Request

A participating player shall be permitted to surrender their party when surrender is valid under
campaign and battle rules.

### BR-061 — Surrender Consequences

The final battle result shall record surrendered heroes and troops as prisoners or otherwise apply
the campaign's surrender consequences.

### BR-062 — Entire Side Surrender

If every remaining party on one side has surrendered, the battle shall end immediately.

### BR-063 — Surrender Before Mission Entry

If one side surrenders before the battle mission begins, the battle shall be resolved without
requiring the opposing side to enter or finish loading the mission.

---

## 8. Battle Completion

### BR-070 — Defeat Condition

A battle side shall be considered defeated when it has no remaining agents capable of continuing
combat and no eligible reinforcements remain, regardless of whether its losses resulted from
casualties, capture, rout, retreat, or withdrawal.

The campaign consequences applied to retreated or withdrawn troops are governed by the retreat
rules (Section 6) and the battle results (Section 9), not by this completion rule.

### BR-071 — Victory Condition

The battle shall declare the opposing side victorious when one side has been defeated.

### BR-072 — Asymmetric Player Actions

The battle shall resolve correctly when different players choose different valid encounter
actions.

For example, if one side attacks and the opposing side surrenders, the battle shall resolve as a
surrender without requiring combat.

### BR-073 — Unspawned Reinforcements

A battle shall not end merely because no opposing agents are currently spawned if the opposing
side has eligible reinforcements remaining.

### BR-074 — Simultaneous Elimination

If both sides lose all combat-capable agents during the same resolution interval, the server shall
determine the result using a deterministic rule.

### BR-075 — Authoritative Completion

Only the authoritative server shall finalize the battle result.

A mission host may report that completion conditions have been met, but shall not independently
commit persistent campaign results.

### BR-076 — Single Finalization

A battle result shall be finalized exactly once.

Repeated completion messages, host migration, disconnects, or reconnects shall not apply
casualties, loot, prisoners, experience, or relationship changes more than once.

---

## 9. Battle Results

### BR-080 — Result Contents

The final battle result shall include, where applicable:

1. winners and losers;
2. killed, wounded, routed, retreated, and surviving troops;
3. captured and escaped heroes;
4. prisoners;
5. loot;
6. troop and hero experience;
7. renown, influence, morale, and relationship changes;
8. party and army state changes; and
9. map-event completion state.

### BR-081 — Per-Party Results

Casualties, prisoners, loot, and other results shall be attributed to the correct participating
party.

### BR-082 — Player Result Synchronization

All participating players shall receive the authoritative final battle result.

### BR-083 — Campaign Application

The campaign server shall apply the finalized battle result to the campaign state before players
resume normal campaign actions involving the affected parties.

---

## 10. Mission Cleanup

### BR-090 — Map Event Cleanup

The completed map event shall be removed or transitioned to its completed state after its result
has been successfully applied.

### BR-091 — Idempotent Cleanup

Mission and map-event cleanup operations shall be safe to repeat without duplicating rewards,
deleting unrelated parties, or corrupting campaign state.

---

## 11. Synchronization and Reliability

### BR-100 — Mission State Synchronization

The authoritative system shall synchronize at least the following mission state:

1. participating players and parties;
2. current host;
3. troop and agent assignments;
4. spawned and unspawned reinforcements;
5. agent health and status;
6. casualties;
7. battle-completion state.

### BR-101 — Server-Arbitrated Authority Changes

Host elections and host migrations shall be arbitrated and announced by the campaign server. The
server's announcement is the single source of truth for the current host, so that all clients
agree.

A client may compute a provisional host locally (e.g., to avoid a round-trip during mission
setup), but the server's authoritative assignment shall reconcile any disagreement.

### BR-102 — Host Epoch and Stale Host Rejection

Each host assignment — the initial election and every migration — shall carry a **host epoch**: a
monotonically increasing generation number scoped to the battle instance and issued by the server.

Messages exercising host authority shall include the sender's host epoch. Receivers shall reject
host-authority messages bearing a stale epoch, so that messages from a former host are rejected
after authority has migrated to another player.

### BR-103 — Duplicate Message Handling

Battle commands and result messages shall include sufficient identifiers to detect and reject
duplicate or stale requests.

### BR-104 — Mission Identity

Every battle mission shall have a unique identifier associated with its map event, assigned by the
server when the battle instance record is created (BR-002).

The identifier shall persist for the life of the battle instance — including across host
migrations and across player retreats while other players remain (BR-054).

Messages from a previous or unrelated battle mission shall not affect the current mission.

### BR-105 — Party Identity

All battle participants and results shall use stable party, hero, troop, player, and agent
identifiers.

---

## 12. Engine Constraints

### BR-110 — Maximum Concurrent Agents

The Bannerlord engine can only render a maximum of 2000 agents.

The number of concurrently active agents in a battle mission on any client — locally spawned
troops, puppet agents replicated from other players, mid-battle reinforcements, and mounts alike —
shall therefore never exceed 2000.

A spawn system shall not spawn an agent when doing so would exceed the limit (a mounted troop and
its horse are two agents). Withheld troops are deferred, not lost: they remain eligible unspawned
reinforcements (BR-070, BR-073) and shall spawn as active agents are removed and capacity becomes
available.
