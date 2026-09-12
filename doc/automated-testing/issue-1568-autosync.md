# Issue 1568 AutoSync fixture

Status: prepared and checked without starting a game. Live execution is pending.

The implementation in [PR #1803](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/pull/1803)
merged as `931f494a90ad975362d211a6ffd07aecea584d69`. A static comparison of the
pre-fix tree `fe659628797eef9c4cbc62d6405de52270ed0714` and current base
`9963f59803dbab6054c8aaba8749865960816ba7` finds both registrations for each of the
four pairs below before the fix, and exactly the property registration on the base.
The original defect is absent in the current registrations. Runtime packet counts
and setter side effects still need this check. This continuation adds only DEBUG
fixture commands and validation; it does not repeat the merged fix.

## Source and setup

Record `git rev-parse HEAD HEAD^{tree}`, clean status, configuration, installed game
version, game DLL hashes, and hashes of the built DLLs in the run evidence. All
three processes must use those same Debug outputs. A different tree or game build
requires fresh verification. Do not infer a runtime pass from compilation or E2E.

When a live run is separately authorized, use one dedicated server and exactly two
fully joined clients, with no joining/leaving peers during a measurement. Start
from a disposable copy of a campaign save, retain the original save, and keep all
players on the campaign map. The server has no player party. Run all mutations
below on the server; client commands are read-only.

1. Record `coop.debug.get_time_mode` on the server. Run
   `coop.debug.set_time_mode Pause` and confirm `coop.debug.get_time_mode` reports
   `Pause` on every machine. Keep it paused through all observations.
2. On every machine run `coop.debug.town.list_towns`, then
   `coop.debug.town.info town_comp_ES1`. This is Danustica, Southern Empire,
   settlement `town_ES1`, town object `town_comp_ES1`. Record initial Security and
   Loyalty and require matching initial values.
3. On every machine run `coop.debug.hero.list Rhagaea`, then
   `coop.debug.hero.info lord_1_14`. `lord_1_14` is Rhagaea's installed campaign id;
   use the actual registered id from `hero.list` if this save differs. If she is
   unavailable, use `coop.debug.hero.list` to select one living existing hero and
   record its printed id/name. Require matching `_health` before changing it.
4. Run `coop.debug.autosync.attachment_candidates` on the server. It selects two
   registered eligible parties in StringId order and prints complete commands
   with their actual ids, so no guessed party ids are needed. Record their names
   from `coop.debug.mobileparty.list`. Copy the printed `attachment_state` command
   to the server and both clients. Require `childDetached=true`,
   `attachedToParent=false`, `parentChildCount=0`, `parentAttachmentCount=0`,
   `childAttachmentCount=0`, `parentDetached=true`, both `*IdleOnLand=true`, and
   `timeMode="Pause"` on every machine. Keep these same ids for the whole check.
   A missing pair or failed precondition leaves attachment unexercised.

## Packet and convergence oracle

The server logs `Packet profile` every 10 wall-clock seconds while packets are
being sent. `MessagePacket` entries count logical sends per recipient before
aggregation; aggregate entries account separately for framing. This is a send
oracle, not proof of delivery. The client state checks supply the apply oracle.

Before each row, retain two consecutive complete profile windows with none of
the eight target names below. Require two stable connected clients and drained
reliable queues. Then perform the single server action, await its profile output
and two subsequent quiet windows, retaining every window in between. Sum each
target count across that whole interval, so a flush boundary cannot lose a send.
Do not start another row until both client observations pass. Do not use a fixed
sleep as proof that clients applied the update; require the observations within
30 seconds and fail on timeout. If profiles stop, peers change, target traffic
appears during the baseline, or campaign time advances, the row is inconclusive.

| Server action | Retained message, exactly 2 sends | Removed message, exactly 0 sends |
| --- | --- | --- |
| `coop.debug.hero.set_hitpoints lord_1_14 37` | `Hero_HitPoints_SetNetworkMessage` | `Hero__health_SetNetworkMessage` |
| `coop.debug.town.set_security town_comp_ES1 41` | `Town_Security_SetNetworkMessage` | `Town__security_SetNetworkMessage` |
| `coop.debug.town.set_loyalty town_comp_ES1 43` | `Town_Loyalty_SetNetworkMessage` | `Town__loyalty_SetNetworkMessage` |
| Printed `attachment ... attach` command | `MobileParty_AttachedTo_SetNetworkMessage` | `MobileParty__attachedTo_SetNetworkMessage` |
| Printed `attachment ... detach` command | `MobileParty_AttachedTo_SetNetworkMessage` | `MobileParty__attachedTo_SetNetworkMessage` |

Log names have the `MessagePacket:` prefix. For the scalar rows, choose a value
different from the captured initial value; use 38, 42, or 44 respectively if the
first value already matches. Require the authoritative command to report the
requested resulting value. On both clients run the matching `hero.info` or
`town.info` command and require that same result (`_health` for the hero). Do not
use a town setter on a client even though its existing command accepts both roles.

After attach, the printed `attachment_state` command on all machines must show
`attachedToParent=true`, `childDetached=false`, `parentChildCount=1`,
`parentAttachmentCount=1`, `childAttachmentCount=0`, `parentDetached=true`, and
both `*IdleOnLand=true`. After detach, require the initial graph again. The parent
backlink matters: matching only `child.AttachedTo` would miss lost setter effects.

Repeat each printed attachment mutation once after its successful row, with a
fresh quiet baseline. The repeated command must leave the graph unchanged and
send zero target messages. The fixture avoids calling an unchanged setter.
Only the first attach and first detach are counted as the two changing rows.

## Restoration and failure diagnostics

Detach the selected child on the server and verify the detached graph on both
clients before cleanup. Reload the original disposable save on all participants
through the authorized runtime owner, then restore the recorded time mode with
`coop.debug.set_time_mode` and verify it. Reload is the full restoration boundary
for hero health events, town changes, and secondary attachment state; do not save
the modified campaign over the original. If detach fails, preserve evidence and
reload instead of clearing fields or suppressing the failure.

Keep command outputs, profile timestamps/counts, server console output, both
client logs, and any AutoSync/registry errors. A standalone server writes to its
console; `Coop_server.log` belongs to the in-game server mode. Client logs are
recreated on startup, so copy them before reload. Capture the first exception or
`Failed to get` diagnostic and the target registry ids. A count other than 2 on a
changing row, any removed-name message, stale client value, duplicate/stale parent
backlink, or apply exception fails the row. A rejected setup is unexercised, not a
pass. Profile counts alone cannot attribute background traffic to an object;
that is why a quiet baseline and paused world are mandatory.

The fixture refuses inactive parties, sea/navigation transitions, armies,
settlements, battles, siege camps, and other attachment graphs. Installed Native
v1.4.8 `TaleWorlds.CampaignSystem.dll` SHA-256
`1f8e33e2ed73e6ec653d7629180afb70649ddc6e5bd1657a802a264efda1c3ae`
was used to inspect `AttachedTo` and `SetAttachedToInternal`; the setter changes
the parent graph and can copy or clear battle, siege, settlement, navigation,
and visual state. The constrained fixture avoids those active contexts.

## Non-live validation

Build `source/CoopTests.slnf` in Release with `-p:PostBuildEvent=`. Run the broader
`source/CoopUnitTests.slnf` suite and focused E2E `HeroSyncTests`,
`HeroCreationTests`, `TownSyncTests`, and `MobilePartyPropertyTests`. Build Debug
`source/GameInterface.Tests/GameInterface.Tests.csproj` and run
`AutoSyncAttachmentFixtureTests` plus command/DI registration checks. The focused
guards cover client rejection and running-campaign rejection before world access.
Existing `ServerCreateBareHero_PreservesDefaultHealthOnClients` covers the mirror
constructor default; existing property tests cover the retained sync paths.

The Linux CI game assemblies may differ from the installed Windows game. Record
both identities and keep that distinction in the handoff. These compile, unit,
and synthetic E2E checks do not start live testing or certify the runtime rows.
