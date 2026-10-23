# up29 owner disconnect/rejoin: BLOCKED, bench released

The runtime launched and a fresh Lady quest was accepted through real offer dialogue. The required disconnect/rejoin flow was **not run** because the native acceptance journal could not select the fresh active entry through the installed tooling. There is no cancellation, exact-once finalization, reward-stability-after-rejoin, or no-resurrection PASS claim.

Source `3d1140bbd01d588e18655734f96da083fbf86b43`, tree `e6c31dde1f707d2ff806abf32f449ef8cdf28f2d`. Source remains clean and unchanged. Local token `issue3781-20261023T185354`. Fixture manifest SHA256 `32a2fb3b62c1307f78c1842e9b5e848fd2c2c23c64fb967eb09f8097f2951bfe`, ignored kit revision `3781-rejoin-r1`. Runtime DLL locations and MVIDs are in runtime-identity.json and ready-discover.json. Codex gpt-6.1-sol, high, serviceTier=default.

## Observed

- Dedicated server 38312 (wrapper 48472), A/testclient 53824, B/testclient2 76232. Both campaign-ready; server roster confirms both connected before mutation.
- Read-only finalize_watch armed on server and both clients before acceptance. Exact issue_1227 baseline: each endpoint observed=0 received=0. No later count is claimed.
- Sora was present but failed free-lady placement. The retained initial setup batch then created an unaccepted Sora offer issue_1226 in Danustica. It was never accepted or used for the flow. Own command runner was corrected to stop on unsuccessful command results.
- Eligible Nus, registered Hero_CharacterObject_2704 / character CharacterObject_2704, was placed at neutral Vostrum town_ES2 with security 40. Town owner clan_empire_south_1, leader lord_1_14. Fresh issue_1227 accepted through inspected real conversation choice `Then I will enter the tournament and reach round 2 my lady.` No injected accept or ending.
- Server ownership=testclient, issue state=SolvingWithQuestSolution, attached issue_1227_quest. A startedLadyQuests=1, startedHere=True, ongoing=True, one log naming Nus. B startedLadyQuests=0 and no attached quest.
- Canonical server baselines after setup and at acceptance unchanged: A gold14308, renown1191.4495, influence100, Honor XP90, Honor level0, Nus relation0. B gold10000, renown547.8492, influence1.5, Honor XP uninitialized, Honor level0, Nus relation-14. Both local Honor XP0. Raw endpoint values are retained; client renown formatting differs slightly.
- Both native journals captured and visually reviewed. A Active Quests (1), B Active Quests (0). The shared `journal_select Lady` chooses the last matching entry across active+old lists, so A's selected detail is an OLD Sora success. It must not be captioned as Nus acceptance. B displays an existing old Sora entry. No journal history was deleted or altered.

## Terminal blocker

Command:
`bash /home/pwisorlowska/coop-live-orchestration/3781/up29/op.sh -Mode Click -Target A -Label select-active-Lady-A -Layer QuestScreen -Match "^Lady.s Knight Out$"`

Exact error: `No clickable ancestor`.

Evidence: `select-active-Lady-A-inspect-0.json` and `select-active-Lady-A-elements.json`. Freshly observed active-row title e36 has ancestors e33/e32/e31/e26 with no declared click action. Two full journal inspections previously returned `stale_reference: Widget hierarchy changed; inspect again.` The row page itself was successfully read. No unsupported click, foreground input, shared helper edit, or gap acceptance followed.

The installed disconnect.ps1 and reconnect.ps1 were NOT invoked. A never reached an intentionally disconnected main menu during the scenario; there is no offline server/B finalization or actual rejoin observation. Cleanup shutdown is not counted as the required flow. No synthetic cancel, timeout, direct completion, or forged message was used.

## Cleanup

local_down.sh ran immediately after the terminal blocker. `cleanup-issue3781-20261023T185354/cleanup.json`: completed2026-10-23T19:27:43.9132219Z, verifiedAbsent=true, remainingProcessIds=[]. local-down.log ends BENCH_LOCK_RELEASED3781. Independent owned-pids-absent.json confirms server38312, wrapper48472, A53824, B76232, detached64920, launcher70556 absent. bench-after-cleanup.txt says free, no processes. Launcher exit0, crashDialogs0; engine profile restored with exactOriginalRestored=true. Per-process stop diagnostics are retained in the cleanup receipt, whose final inventory confirms all absent.

No product/shared-helper/other-worktree edit, commit, new test, CI, GitHub action, extra agent/model call, HIVE SSH, or QuestLearnings change. Only own up29 operator/evidence files were written; the existing cycle handled authorized runtime rotation/build/deployment. No reacquisition or retry after cleanup. Up28's four flows were not repeated.

STATUS: DONE-bench
BLOCKED: fresh active native journal selection; owner disconnect/rejoin remains UNRUN.