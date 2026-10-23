# Lady's Knight Out

Local up28 on `b57cac112` passed village crime (owner Honor -50), TournamentFailure, a real co-op TournamentSuccess, and B joining during A's pending influence solution, with paired journals showing no new B completion or rewards. The supervisor accepted reuse on `3d1140bbd` for the conflict-only merge and quest-category attributes. Local up29/retry1 on `3d1140bbd` passed owner disconnect cancellation and same-process rejoin: one server cancellation, one B finalize, offline A received zero and reloaded with no active quest. Evidence and cleanup receipts are linked in PR #3949.

The review correction now migrates influence handling to the shared lord coordinator and removes the unused native tournament fallback. A fresh combined Local run of influence and co-op tournaments is required on that correction's final head; the runs above remain historical. Use the lane assigned by the supervisor, a dedicated server and two clients.

## Installed source

The port was checked against Native and SandBox v1.4.8. `TaleWorlds.CampaignSystem.dll` has SHA-256 `1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE` (file version 1.0.0.0). Full local decompilations of `LadysKnightOutIssueBehavior`, `IssueBase` and `QuestBase` are retained with the validation artifacts.

`LadysKnightOutIssueBehavior` offers a tournament quest and a lord solution. `IsThereAlternativeSolution` is false. Neither solution creates a quest party, transfers troops or companions, or offers persuasion, a bribe or a counter-offer. The issue constructor takes only the giver; the quest constructor takes difficulty and fixed 750 gold. There is no Lady-specific captivity, siege, target-capture or ownership-change quest callback. The inherited settlement-ownership callback removes obsolete dialogue lines.

Natural generation requires a female noncombatant lord who is not her clan's leader, has negative Mercy, nonpositive Softspoken and Curt traits, and is in a town with security at most 50. An existing offer stays valid while the giver is a noncombatant in a town with security at most 80. Acceptance refuses relation below -10 and war with the giver's town faction. The framework supplies the per-player similar-commitments checks.

## Preparation and observations

Use Danustica (`town_ES1`, Southern Empire) for a worked test location. Run `coop.debug.hero.list Zerosica` to resolve Zerosica's current registered ID; her vanilla character ID is `lord_1_2`, but registered IDs belong to the loaded save. `coop.debug.issues.list_types` must include `LadysKnightOutIssue`. `coop.debug.issues.give` creates an unaccepted offer and requires a female noncombatant lord currently in a town; follow its argument help with the resolved registered ID. Do not substitute a completed synthetic quest for dialogue acceptance.

Record both players' gold, clan renown, influence, relation with the giver and Honor XP/level before each independent outcome. Record each issue's giver, issue ID, owner controller, difficulty, local quest/journal, and the authoritative tournament session, slot, match, round and revisions. The server must have an unstarted quest object; only the accepting client has a started personal quest. The other client marks the offer taken and has no quest object or personal log.

The expected relation amounts below are vanilla base amounts; record any native perk or relation-model adjustments separately. Retain relevant journal/dialogue/tournament screenshots from both clients and structured state from the server and both clients.

## Live flows

1. Open the actual offer dialogue in a settlement, decline, reopen and accept. Repeat through map quick talk with a giver in a town. Declining leaves the offer available. Accepting creates the owner's start log; the observer gets no personal quest. Refuse stale requests after the giver leaves town, security exceeds 80, relation falls below -10 or war begins. Also exercise the exact boundary values: security 80 and relation -10 remain eligible.

2. Let both players accept separate Lady offers concurrently. Each sees only their own quest. Try a second offer of the same type for one player: the framework refuses it, while the other player's first offer remains valid. Try simultaneous acceptance of one offer: only one owner remains.

3. Fight real co-op tournament matches. Difficulty at most 0.2 requires round 2; above 0.2 through 0.5 requires round 3; above 0.5 through 0.7 requires round 4; above 0.7 requires the championship. For each difficulty band, exercise elimination one round below its goal and at its qualifying round, where reachable. A championship-only quest fails on final-round elimination and succeeds on a championship. Intermediate wins keep the quest active. A completed match before acceptance, watching, skipping, another player's win, and an NPC replacing a departed competitor must not finish this player's quest. A later valid tournament can still finish it.

4. Tournament success pays the owner 750 gold, adds 1 clan renown and base +5 giver relation, appends the success log and ends the quest on every machine. Failure changes base giver relation by -5, appends the failure log and ends the quest. The other player's money, renown, relation and journal remain unchanged. Replaying the same tournament update must not pay or penalize twice.

5. Let the 30-day quest expire without a qualifying tournament. The server applies base -5 giver relation, the owner sees the timeout log, and every copy ends. The owner client must not apply the timeout penalty a second time.

6. Kill the giver, or move her out of a settlement and raise the normal settlement-departure event. Both cancel the quest with their own log and no tournament reward. Declare ordinary war or change kingdom so owner and giver become enemies: cancellation. Owner-caused hostility fails the owner's quest; hostility initiated by another player in the same faction cancels the owner's quest without blaming that player. The framework cancels a direct quest when its owner disconnects or the server reloads.

7. On the server, drive the owner's real raid, forced-supplies or forced-volunteers action against a village of the giver's clan. The quest fails with its crime log, base -5 relation and -50 personal Honor XP. Other players' actions must not trigger these personal penalties; their Honor stores stay unchanged. Observe the final trait level calculated by the native development model. The shared minor/major coercion fallback is not a separate normal Lady flow: `QuestManager.QuestExistInClan` finds the giver only in her own clan, which the preceding Lady-specific branch already handles, and a lady lord is not a village notable. Lady leaves that shared helper unpatched, because patching it compiles it before the hero sync patches and its giver power change would stop reaching clients. A compatibility E2E case invokes the helper directly and verifies the giver's power on every client.

8. Offer the lord solution only when the giver's town belongs to the requesting player's clan. Verify the influence refusal, accept through dialogue, then return to the map menu. The server charges `15 + ceil(30 * difficulty)` influence, gives 7 clan renown and base +5 giver relation, and the owner's issue journal receives the start and completion logs. No tournament reward is paid. The observer sees no personal history or reward. Repeated completion requests do not charge twice. Disconnect before completion: the framework dismisses the issue without charging influence. If the giver dies before completion, the server cancels the issue and every client ends its copy.

## Automated coverage

`LadysKnightOutTests` uses the real issue and `QuestScenario`, including `QuestAcceptedConsequences`/`StartQuest` on the owning client. It covers creation identity, native acceptance, owner-only journals, tournament thresholds and rewards, unrelated competitors, absent tournament proof, timeout, giver death during the quest and during a pending lord solution, concurrent owners, framework duplicate refusal, stale security refusal, personal Honor and lord influence.

`LadysKnightOutTournamentProgressTests` covers authoritative match revisions, all elimination rounds, intermediate progress, spectators, replacement NPCs, already-completed matches and session cleanup. These are automated functional checks, not proof of the live flows above.
