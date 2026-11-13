# Revenue Farming co-op verification

Live testing is pending. This recipe is for the assigned live lane after the draft handoff.

The quest uses `Issues/Framework` for creation, acceptance, per-player eligibility,
ownership and finalization. Collection has its own server-side hourly progression and
menu-state replay because vanilla collection rolls random incidents and gives gold
before the quest ends. The server keeps an unstarted quest, and only the accepting
client starts its quest and registers its listeners. There is no alternative or lord
solution. Trait XP uses the shared per-player store from #3918, under the accepting player's scope.
Record the owner's server-side XP before and after success, betrayal and timeout;
verify the outcome's XP change and that the other player's XP stays unchanged.
Client legacy Campaign.PlayerTraitDeveloper XP is not authoritative for this check.

Source inspected: Native v1.4.8, `TaleWorlds.CampaignSystem.dll` SHA-256
`1f8e33e2ed73e6ec653d7629180afb70649ddc6e5bd1657a802a264efda1c3ae`.
The relevant vanilla members are `RevenueFarmingIssue.GenerateIssueQuest`,
`RevenueFarmingIssueQuest.HourlyTick`, `ProgressRevenueCollectionForVillage`,
`OnBeforeTimedOut`, `QuestCompletedWithSuccess`, `QuestCompletedWithBetray`,
`RevenueFarmingIssueBehavior.AddVillageEvents`, and the quest's registered callbacks.

Use two clients and a dedicated server. Keep state changes on the server and use
both clients to inspect their own journals, gold, relations, inventory and troops.
Use Danustica (`town_ES1`, town object `town_comp_ES1`) as the worked settlement;
resolve its current owning clan leader from the loaded save before creating an
offer. `coop.debug.issues.list_types` must include `RevenueFarmingIssue`.
`coop.debug.issues.give` can create the issue for the resolved lord; acceptance must
still use the real dialogue. The command selects a town owned by that lord's clan,
so verify the offered villages before travelling.

Required live flows:

1. Offer, decline and accept from settlement and map conversations. Check vanilla
   relation, war and troop restrictions. Accepting must give only that client a
   quest, and the server must record its owner and an issue ID. A second player
   can hold another copy; the same player's second copy is refused by the framework.
2. Enter an offered village, collect one hour, leave and resume. The server and
   owner must agree on collected gold, partial progress, menu and journal; the
   other player's own journal, gold, troops and inventory must stay unchanged.
   Collection stops when the owner leaves, disconnects, is captured or enters battle.
3. Exercise all three choices in each incident: `offer_goods_and_troops`,
   `brawl_breaks_out`, `landlord_asks_for_money`, `village_is_under_quarantine`,
   `refuse_to_pay_what_they_owe`, and `relief_for_the_poorest`. Include disabled
   options for insufficient men or gold, and both random consequences where
   present. Check goods, payments, troop wounds/deaths, notable relations,
   partial-progress concessions and village completion. These incident delegates
   change rosters directly; they do not start a combat mission.
4. Complete every village, then pay the giver, pay a steward in an owned town or
   castle, and betray the giver on separate quests. Verify one payment, the
   giver relation, personal crime rating for betrayal, final journal and menu
   cleanup. A repeated choice or a stale incident must not pay or wound twice.
5. Let the deadline expire with and without enough gold. Only the owner gets the
   demand. Exercise send and keep, repeated hourly ticks while awaiting a reply,
   and loss of the payment money before replying. Verify timeout relation and
   journal, prompt removal and no effect on the other player's quest.
6. Raid one village and then all offered villages; transfer the target town away
   from the giver; declare war or move the giver's clan into a kingdom at war
   with the owner, with a kingdom change at peace as the control; trigger a battle interruption
   and the generic coercion path; replace the owning character. Verify the
   correct progress reduction or cancellation, final journal and encounter cleanup.
   Desertion only stops collection, it never cancels the quest. Field battle and
   captivity also stop collection without cancellation; verify collection resumes
   after returning to the village or being released.
7. Disconnect during collection and while a demand is open. The framework
   dismisses the departing player's quest. Reconnect and accept a fresh offer;
   the old collection session must not claim the new menus or retain a payment
   prompt. A second connected player's quest must remain active. Restarting
   the server also dismisses active quests under the current framework policy.

## Save and restart, after lane assignment

These flows are required and have no live result on the merged head. Rejoining a
running server does not test serialization. Keep the source head and build fixed
through the restart and retain both the native `.sav` and co-op `.json` sidecar.

The existing MCP launcher supports a graphical `/server` process plus two player
clients and an exact `save_name`. This gives a candidate route without #4013's
new disconnect/reconnect actions. The supervisor must assign a lane supporting
that topology before it is launched; it must not silently replace a standalone
dedicated-server run.

1. While the owner is collecting, capture the server and both clients' identities,
   ownership, gold, troops, items, relations, crime, server per-player Honor XP,
   active journal and old quests. Pause time. Execute the catalog command
   `coop.debug.save.save_as` on the server with argument `rf_collect` (use a fresh
   unused name on subsequent attempts). Require successful save completion in
   the server log, `coop.debug.save.state` reporting `isSaving=False`, and a
   nonempty native save plus its matching readable sidecar. A queued-save reply
   alone does not prove the save succeeded.
2. Preserve this pre-disconnect save. Execute `coop.server.shutdown` with
   arguments `0 rf_teardown` on the server, using a different fresh save name.
   This existing coordinator closes admission, disconnects clients, then saves.
   Wait for `coop.server.shutdown status` to report completion and confirm both
   clients left. Its later save must not replace `rf_collect`: disconnecting
   dismisses quests and would otherwise hide the load-time dismissal check.
   Call `stop_run` and require complete cleanup before starting another run.
3. Find `rf_collect` with `list_saves`, then call
   `start_run(profile="local", client_count=2, save_name="rf_collect")` in the
   assigned graphical lane. Wait for server campaign readiness, each client's
   join readiness, call `join_client` for each, then require campaign readiness
   on both. Use the original platform identities. Inspect the loaded campaign
   and saved values; a requested-save acknowledgment alone is insufficient.
4. Require no Revenue Farming quest, ownership, tracked village, payment popup
   or orphan journal on any endpoint. Saved player resources must match the
   pre-disconnect snapshot. Offer and accept again through real dialogue with
   Tais (`Hero_lord_3_5`, Hubyar `town_A5`) after resolving his eligibility in the
   loaded save. Exactly one new quest starts; the old session cannot claim it.
5. Repeat with fresh names `rf_demand` for a pending deadline popup and `rf_ended`
   after a real payment or timeout ending. The pending popup must not return or
   apply another payment/penalty. The completed result and exactly one owner's
   old-quest row must survive; the second player must never gain that row.
   Compare any village left raided against its saved state before restoring it.

For the standalone dedicated-server lane, verify save production before spending
a scenario on it. #4013 remains open and its native-save/disconnect changes are
not assumed present. If `save_as` cannot produce the pair there, the missing
capability is a server-only bridge operation that writes a named native save
through the existing `ISaveInterface.SaveCurrentGameToFile` and the matching
co-op session, reports both completion results and paths, and refuses an existing
name. The lane also needs to retain that pair across teardown/preparation and
select it on restart, rather than copying the default seed save. No session-store
round trip, dismissed-before-save snapshot, or synthetic completion substitutes
for these live restart checks. Report the exact unsupported operation and retain
the receipts if this route is unavailable.

Retain the server and both clients' before/after state and screenshots for each
exercised flow. Unrun flows remain pending; automated E2E results are not live proof.
