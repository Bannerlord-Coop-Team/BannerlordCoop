# Issue 3295: siege defense and army recovery

This is a prepared DEBUG fixture, not live-test evidence. No live testing was
started during its implementation. A future operator must have separate runtime
authorization. Use a dedicated server and two clients on the same recorded
commit/tree. The server has no player party.

Issue: https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/3295
Continued PR: https://github.com/Bannerlord-Coop-Team/BannerlordCoop/pull/3335

## Source and seed

Record the source commit, tree, configuration, game version, deployed assembly
hashes, and seed-save hash alongside the raw command results. Source changes
invalidate the run. The non-live comparison base is development
`9963f59803dbab6054c8aaba8749865960816ba7` (tree
`042ce9f6600bace996cfe64d5bebfa1082fc5f22`). It was merged into the existing branch,
not rebased. The pre-merge head/tree were
`7725c1a56bf0a84c7bb6739d56bae1868e55a055` /
`21453112bf3eaf209cc5725a381312bd84477ab5`; the merge head/tree were
`7241ad521ec4e37ab77f7667eaf42456ba7024ac` /
`6d932aa1204e3118eb34fd43b5ded4dfc0d048f8`.

Use a disposable copy of a campaign with **Danustica**, settlement `town_ES1`,
town `town_comp_ES1`, in the **Southern Empire** (`empire_s`). Before staging,
record its owner using `coop.debug.town.list_towns`, and the designated client's
controller/party/clan IDs using `coop.debug.players.list` on both peers. Use the
returned controller ID throughout; never guess it from a display name.

The designated player must lead an active land party outside any army,
settlement, siege, or battle. The player's clan and Danustica must belong to the
same kingdom. If the seed needs membership preparation, first record
`coop.debug.clan.membership` for the returned clan ID, then run server command
`coop.debug.kingdom.force_player_join_kingdom` with the returned controller ID
and `empire_s`. Its second argument accepts `none` to restore an originally
kingdomless clan. Preserve the original seed before this preparation.

The fixture selects two clean registered allied lord parties and one clean
registered hostile lord party, captures their exact IDs and movement, and refuses
to proceed if any participant is unavailable or has changed before staging.
Keep those emitted IDs as the worked party identities for this save. Do not use
an unrelated army or bypass a failed preflight to make the scenario run.

On the server, use `coop.debug.set_time_mode Pause force-live-test`, then verify
`coop.debug.get_time_mode` on each peer. Keep time paused for staging and all
state comparisons. Do not start a mission or autoresolve the assault.

## Commands and saved inputs

All six fixture commands have prefix `coop.debug.siege`. Pass JSON as one
argument using the command registry's argument array. For console input, use its
normal JSON/string quoting. Save the object after `LIVE_TEST_JSON=` verbatim,
without the preceding label.

| Command | Side | Arguments and retained result |
| --- | --- | --- |
| `capture_defense_army_fixture` | Server | Controller ID from `players.list`, `town_ES1`. Save result as **capture**. |
| `stage_defense_army_fixture` | Server | **capture**. Save successful result as **staged**. |
| `defense_army_fixture_state` | Either | Same controller ID, `town_ES1`. Read-only diagnostics. |
| `defense_army_state` | Either | Same controller ID, `town_ES1`, requested state, **staged**. |
| `restore_defense_army_fixture` | Server | **capture**. Retain result, including any failure. |
| `verify_defense_army_fixture` | Server | **capture**. Releases the fixture only after restoration checks pass. |

Run `defense_army_state` with `baseline` on the server and both clients before
clicking anything. Require a successful command and `success: true` in JSON.
Each side must resolve the same army, leader, two followers, siege, and map event
from **staged**. All three army parties must still be outside the event. Counts
without matching IDs are insufficient.

## Assertion A: correct siege-defense side

On the designated client, click **Danustica** and capture the fully visible menu
before selecting a choice. For the outside relief route, select **Assault the
siege camp.** (`join_siege_event` / `attack_besiegers`). If the normal encounter
menu instead presents **Help Danustica**, record that exact option and menu ID.
Do not invoke the old `open_defender_encounter` or `invoke_defender_join`
shortcuts; they were removed from this fixture.

Stop at the encounter menu without choosing to fight or send troops. Run
`defense_army_state` with `joined` and **staged** on the server and both clients.
Require the same three army members to belong to the staged map event's canonical
**Defender** side. None may appear on the attacker side. Save a screenshot of the
selected route and of the resulting player/allied army listing, together with
the JSON. A locally displayed side alone is not a pass.

The outside relief join normally changes the battle type from `Siege` to
`SiegeOutside`. This is accepted only with the same recorded event and correct
defender membership; continuing to require `IsSiegeAssault` would reject the
correct relief route.

The issue does not specify which siege-help option was used. **Break in to help
the defenders** (`join_siege_event_break_in`) is a different route. For coverage
of that route, repeat from the original disposable seed, capture and stage
again, select that option, record `break_in_menu`, and select **Go ahead with
that.** Then select **Continue** in `break_in_debrief_menu`. If the continuing
assault presents `join_encounter`, select its defender-help option before applying
the same defender assertions. Record this result separately. If the option is disabled, record its
reason and leave this route unverified; do not substitute an outside relief
join or manually attach parties. Breaking in may sacrifice troops, so reload
the untouched seed after its cleanup rather than treating movement restoration
as complete campaign rollback.

## Assertion B: unstuck preserves the army

This assertion has its own result and must not be inferred from Assertion A.
Retain the successful `joined` evidence and **staged** identity. On the designated
client, run the real player command **`coop.unstuck`** once and save its result.
The request must travel to the dedicated server. Wait for its normal reply and
client menu cleanup before inspecting state.

Run `defense_army_state` with `unstuck` and **staged** on the server and both
clients. Require all of the following:

- The original army ID, player leader ID, two follower IDs, and attachment
  relationships still match **staged**.
- Every fixture army party has left its map event, settlement, and besieger
  camp; the designated client's encounter/menu has closed.
- No replacement army or loss of a member is accepted as preservation.

Run `coop.unstuck` a second time and repeat the same read-only assertion. It must
leave the preserved army intact. The ordinary follower recovery path is covered
separately by non-live regression tests; this fixture concerns a player-led army.

## Cleanup, repeat, and diagnostics

Always attempt server `restore_defense_army_fixture` with **capture**, including
after an assertion fails. It finalizes only the fixture event, breaks only its
siege, disbands only its created army, and restores captured movement. Save its
result, then run `defense_army_state restored` with **staged** on all peers and
server `verify_defense_army_fixture` with **capture**. Require every captured
party and settlement to be restored, and the fixture's army, siege, and map event
IDs to be absent from each registry, before starting another capture. If staging
failed before producing **staged**, retain **capture** and use server restore
and verify; do not invent a staged identity for client checks.

A failed cleanup keeps its token and evidence for retry. Do not clear its state
or operate on a replacement army. If a participant was destroyed, the campaign
advanced, casualties occurred, the process restarted, or verification cannot
finish, preserve the logs and reload the untouched disposable seed. This
in-memory fixture does not restore campaign history, battle rewards, losses, or
the entire save. Restore any seed preparation and original time mode, or use the
seed reload as the complete reset. Do not save the staged campaign over it.

For a failure, retain **capture**, **staged**, each peer's fixture state, both
assertion results, controller IDs, exact menu screenshots, source identities,
and server/client logs. Diagnose `Failed to get id`, missing membership, wrong
canonical side, unexpected army destruction/removal, and failed finalization
against the matching event/party IDs. A disabled menu, missing participant, or
command failure is a failed setup/route, not a gameplay pass. Do not retry after
a restart using a token from the previous process.

Non-live tests validate source behavior and command contracts. They do not prove
the native menu, rendered side, dedicated-server gameplay, or restoration in a
running campaign. Those runtime confirmations remain pending.
