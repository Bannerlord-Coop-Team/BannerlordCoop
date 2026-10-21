# up25: influence completion and late-join journal

Source: 2bc64211ba1125939d93d47c41024be5c0e309b5, tree bcf44dda641ddd431e91c70629bfc3d314785f1e. Run token issue3781-20261021T215749. Dedicated engine75564, launcher122764; A/testclient40448, B/testclient2 49048. Native Windows Local, private desktop, no foreground input.

Both clients reached campaign-ready. Baseline archives already contained older Lady entries. Zerosica (Hero_lord_1_2) had no entry in B's baseline. B disconnected to InitialState/campaignLoaded=false before offer creation and remained offline through completion.

Setup only: Zerosica in Danustica/town_ES1, security40; lord_setup gave A the town and influence100 before baselines. Created unaccepted issue_1068, difficulty0.1, lordCost18. A clicked the actual influence dialogue choice. Server and A showed SolvingWithLordSolution, one log and influence100. After leaving dialogue, server move testclient town_ES1 drove the native settlement-entry/menu callback that failed in up24. Server and A then both cleared the issue and retained two issue logs, including the terminal influence text. There were zero started Lady quests on all three endpoints.

Measured on server and A: gold69340 unchanged; renown1103.6409 to1111.6409 (client rounding1103.641 to1111.641); influence100 to83; relation8 to17; Honor level0 and local XP0 unchanged. These are actual live totals, not the fixture's 82/+7. Installed IssueBase charges18, Lady consequence adds7 renown and base5 relation, then IssuesCampaignBehavior.OnIssueUpdated applies perk/model-dependent effects, including Oratory renown/influence. The player's selected perks and exact bonus attribution were not independently queried. No exact message count is claimed live. No further change occurred when B joined.

A's new Zerosica journal entry is ended=True with two logs. The corresponding server entry is ended=False with two logs, so it is present in the raw save archive but excluded from completed history. B reconnected after completion, downloaded/loaded the campaign and reached readyForCampaignTests=true. B's new Zerosica raw entry is likewise ended=False, absent from Old Quests; B has no live issue or active Lady quest. B gold18970, renown542.8492, influence0, relation0 and local HonorXP0 remained unchanged. All endpoints agree on replicated A values after B's join.

The own ignored native journal_open kit action pushed the real QuestsState, selecting A's actual completed log and leaving B's normal journal selection when no ended Zerosica entry exists. Screenshots visually inspected: A Old Quests30, Zerosica completed journal; B Old Quests29, no added Zerosica completion. Older Lady entries are visible and are not labelled as this scenario's entry.

Captions:
- InfluenceSolution owner: A's native journal shows Zerosica's completed influence solution and both messages, Old Quests30.
- InfluenceSolution late-join non-owner: B joins after completion; native journal has Old Quests29 and zero active quests. The new Zerosica completion is absent; existing historical Lady entries remain. Giver-specific archive reads establish absence.

Cleanup completed2026-10-21T22:23:56.8580336Z, verifiedAbsent=true, no remaining processes; BENCH_LOCK_RELEASED3781 and subsequent lock show free. Bench released before publication/audits. No source files changed. Pending-lord joins, tournament OnQuestCompleted archive filtering and owner completed-history persistence across reload remain excluded, as disclosed in the reviews.