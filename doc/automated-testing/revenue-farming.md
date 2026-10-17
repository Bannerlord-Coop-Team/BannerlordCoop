# Revenue Farming co-op verification

Live testing is pending. This recipe is for the assigned live lane after the draft handoff.

The quest uses `Issues/Framework` for creation, acceptance, per-player eligibility,
ownership and finalization. Collection has its own server-side hourly progression and
menu-state replay because vanilla collection rolls random incidents and gives gold
before the quest ends. The server keeps an unstarted quest, and only the accepting
client starts its quest and registers its listeners. There is no alternative or lord
solution. Trait XP follows the framework's current policy: quest trait XP is skipped,
so the campaign's shared XP store cannot reward a different player.

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
   from the giver; declare war or change faction; trigger a battle interruption
   and the generic coercion path; replace the owning character. Verify the
   correct progress reduction or cancellation, final journal and encounter cleanup.
7. Disconnect during collection and while a demand is open. The framework
   dismisses the departing player's quest. Reconnect and accept a fresh offer;
   the old collection session must not claim the new menus or retain a payment
   prompt. A second connected player's quest must remain active. Restarting
   the server also dismisses active quests under the current framework policy.

Retain the server and both clients' before/after state and screenshots for each
exercised flow. Unrun flows remain pending; automated E2E results are not live proof.
