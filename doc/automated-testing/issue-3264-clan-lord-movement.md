# Clan lord movement after a conversation (#3264)

Live testing has not started. This document describes a future Debug run; the
completed checks exercise source, compilation and tests without launching the game.

## What the non-live reproduction establishes

The original report describes clan lords waiting after an army and stopping again
after an NPC conversation. Its server log records `Reset stale join references`
followed by `to Hold` during join capture. Those records identify `Created_*`
parties, so they do not establish that the three named lords were reset.

Development `9963f59803dbab6054c8aaba8749865960816ba7`, tree
`042ce9f6600bace996cfe64d5bebfa1082fc5f22`, still calls `SetMoveModeHold` and
`SetNavigationModeHold` from `MobilePartyBehaviorSnapshot.TryCreateJoinState`.
Two regression tests against that base's product source fail with expected
`EngageParty`, actual `Hold`: `TryCreateJoinState_UnregisteredReferences_UsesPointSnapshotWithoutMutatingParty`
and `TryCreateJoinState_RegisteredNonLiveReferences_UsesPointSnapshotWithoutMutatingParty`.
The same tests pass with the PR's snapshot implementation. The test overlay
changes only tests; the base product source is unchanged in that comparison.

The fix normalizes unavailable references in the outgoing join snapshot without
changing authoritative party movement. It does not establish that every cause of
the reported conversation freeze has been resolved. A real conversation followed
by movement on the server and clients remains the required runtime oracle.

The installed managed evidence is Bannerlord **v1.4.8**,
`bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll`, SHA-256
`1f8e33e2ed73e6ec653d7629180afb70649ddc6e5bd1657a802a264efda1c3ae`.
`MobileParty.SetMoveModeHold` clears movement targets and changes the default and
short-term behaviors to Hold. `SetNavigationModeHold` also clears navigation.

## Source and save identity

Before a future run, record the tested commit, tree, build configuration, assembly
hashes and game version. Every participant must use that same built source. Keep
the exact build manifest with the command JSON and screenshots; a later source
change invalidates the run's source binding.

Use a disposable copy of the reporter's paired files:

| File | SHA-256 |
| --- | --- |
| `saveauto1.sav` | `2511556bc0fd4ab9eaeea933adf5255c3e16873f648a308afe5d9bc11b21ac02` |
| `saveauto1.json` | `db8d5506a8b47acf2b5a5595e79c6ddc69c246968b119cdd758e10c3e8b2d684` |

The saved player's party is
`MobileParty_Player_recovered_517c7a227d364777915062f6ab47967f`, clan `Clan_Player`.
The reporter's client log identifies **Valaria** (`lord_1_63_1`), **Zachanis**
(`lord_1_74`) and **Zena** (`lord_1_74_1`). Resolve the party from its real hero and
registry identity in the loaded campaign; do not invent a `Created_*` mapping.

Use one standalone dedicated server and two clients. The server has no player
party; the saved player joins as a client. Complete joining before fixture setup.
Keep the original paired files unchanged. Restoring staged party state does not
rewind elapsed campaign time, conversation history, encounters or unrelated world
events; reloading the disposable pair is the complete reset.

## Commands for the future run

Run setup on the server after the saved player has joined and is on the campaign
map outside an army, settlement, battle, captivity and navigation transition:

```text
coop.debug.mobileparty.clan_lord_fixture_setup MobileParty_Player_recovered_517c7a227d364777915062f6ab47967f
```

Setup returns the selected lord, clan and caravan names and registry IDs, a
fixture token, the staged destination, and complete executable observation
commands. Copy those commands verbatim to the indicated participant; the numeric
coordinates, times and `Created_*` IDs depend on the loaded save. Do not substitute
a different lord if one participant cannot resolve the selected one.

The observation command is `coop.debug.mobileparty.clan_lord_fixture_observe`.
The participating client and server use phases `before`, `during`, `released`
and `verify`; copy the full command printed for each phase. `released` prints a
new `verify` command bound to that participant's post-exit position and campaign
ticks. Keep each participant's returned command with its own evidence.

The passive second client uses the full `state` command printed by an observation.
Capture its output at release and after time advances. Compare the same lord's
coordinates and campaign ticks, requiring at least 0.1 map units displacement,
matching destination within 0.01 map units, and non-Hold behavior. It does not
claim to have opened a local conversation.

The selected player must use the normal map UI to interact with the named staged
caravan, enter dialogue, and leave peacefully. Keep the dialogue open long enough
to capture the `during` evidence on the server and that client. No conversation,
movement or time cheat is part of this action.

Setup gives the lord a fixed GoToPoint order and temporarily prevents new AI
decisions while leaving its AI enabled. This makes route displacement repeatable;
it does not verify that autonomous AI chooses its next order after the route ends.
The decision-prevention flag is server-local, so its client value is diagnostic
only. Restore checks and restores the captured flags and movement state.

Restore on the server after success or any failed assertion:

```text
coop.debug.mobileparty.clan_lord_fixture_restore
```

Retain the restore result and repeat the command to check its idempotent result.

## Required observations

The fixture must select a registered, eligible lord in the selected player's
clan, print the actual identities, stage movement and place a real caravan within
normal interaction range. Selection or setup failure is an unexercised test,
never a pass. The server performs setup and restoration with synchronization
patches active. Clients use observation commands and the normal conversation UI.

Capture all of these phases:

1. **Ready:** server and both clients resolve the same lord and caravan. The lord
   has coherent Point/GoToPoint navigation and a destination different from its
   current position.
2. **During:** the selected client is visibly talking to the selected caravan;
   its observation reports an active conversation and encounter. The server
   confirms the exact player owns the exact caravan engagement.
3. **Released:** leave through the normal conversation UI. The server engagement
   and the client's conversation/encounter have ended. Capture fresh position and
   campaign-time baselines on every participant now.
4. **Moving:** advance ordinary campaign time, then verify at least 0.1 map units
   displacement from each participant's **released** baseline, increasing
   campaign ticks, non-Hold behavior and matching destination within 0.01 map
   units. Sample while at least 0.5 map units remain before the destination;
   arrival is outside this movement oracle. Movement before or during the
   conversation cannot satisfy this check.
5. **Repeat:** restore, set up again and repeat the conversation. Confirm the
   fixture did not leave duplicate state or change the selected identities
   silently.

Do not use an instantaneous campaign-clock jump as evidence of movement. Do not
force a movement command after release before taking the measurements, because
that would hide the reported freeze.

## Failure evidence and cleanup

Retain setup, phase, observation, verification and restore JSON, including failed
commands. Preserve the source/build manifest, server output, both client logs and
screenshots of conversation and post-release map state. Copy logs before any
restart because client log files are recreated at startup.

Classify missing eligible actors, incomplete joins, stopped campaign time,
unreachable staging positions and missing conversation evidence as unexercised.
A witnessed release followed by Hold, waiting, no displacement or inconsistent
targets is a product failure requiring diagnosis. Inspect party registry IDs,
hero/clan IDs, AI flags, army/settlement/map-event membership, time mode, target
coordinates and the first relevant log error.

Always attempt fixture restoration after a failed assertion. A failed restore
must preserve its captured state for retry and report the unresolved party. Do
not declare cleanup successful until restoration is verified. If world changes
make restoration impossible, retain evidence and reload the disposable save pair.
