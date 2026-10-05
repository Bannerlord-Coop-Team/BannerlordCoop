# Normal issue gate versus debug catalog

Status: draft, source-inspected only. Static inventory is retained; normal runtime/dialogue
observations have not been exercised. The direct game driver and a source-bound owned run
were unavailable during authoring.

Behavior: normal issue availability follows the explicit source allowlist. A factory or debug
catalog entry does not enable normal quest gameplay. The current 43 installed registered issue
behaviors include one allowlisted behavior, `GangLeaderNeedsToOffloadStolenGoodsIssueBehavior`;
the other 42 are excluded. `QuestsState` is independently blocked on both sides.

Sources: [allowlist and normal dialogue gate](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs),
[debug grant catalog](../../source/GameInterface/Services/Issues/Commands/IssueGiveCatalog.cs),
[debug commands](../../source/GameInterface/Services/Issues/Commands/IssuesDebugCommand.cs),
[journal gate](../../source/GameInterface/Services/UI/Patches/GameUIDisable.cs),
and [installed issue registrations](../baseline/installed.json).

Use [the focused branch map](../quest-branches.md) and [source cases](../source-cases.csv)
to distinguish this normal type-gate recipe from the 31 interpreted stolen-goods cases.
Other registered issue types remain disabled; registrations do not supply terminal-result oracles.

Read-only static discovery from the root:

```powershell
python tools/feature_map.py find Issues --kind vanilla-issue
python tools/feature_map.py find coop.debug.issues --kind command
python tools/feature_map.py check
```

For the real runtime slice, follow [runtime](runtime.md), without altering the source allowlist
or debug-granting a disabled issue. On server/client1/client2, confirm and execute
`coop.debug.issues.list_types`, `[]`, and `coop.debug.hero.issues`, `[]`.
The second command lists actual giver/issue IDs and can fail because no hero currently has
an issue; that is a recorded setup blocker, not a game-support failure or an invented giver.

Select an actual accessible notable with an existing issue from the returned IDs; inspect the
giver's current issue type through the available registered hero inspection and real dialogue.
Prefer Danustica when it contains an eligible giver. Do not hard-code generated notable IDs,
substitute an arbitrary hero or overwrite an existing quest. Keep the selected actual IDs in
the run evidence. If the runtime cannot identify an issue type independently, stop this slice
with that precise missing observation.

Drive the ordinary client conversation/issue option with the real game UI, obtaining fresh
native layer/snapshot/element IDs via `ui_inspect` before interaction. Inspect both normal
task and alternative-task entry gating where an existing eligible issue supplies them. For
the disabled journal slice, attempt the ordinary client journal input and observe that
`QuestsState` does not open. Do not infer behavior from the debug list output alone.

Oracle: no excluded behavior is admitted through normal issue dialogue; an existing
allowlisted issue is not rejected solely by the type gate; ordinary eligibility may still
reject acceptance. The debug catalog can list wired or unwired types without changing this
normal gate. Log any allowlist-application exception: the patch itself reports that a failure
can leave all behaviors unpatched, so a source list cannot override contrary runtime evidence.
The vanilla quest journal remains blocked by the current source patch.

Evidence: actual issue type/ID, normal dialogue entry observations, current source gate,
catalog output, client screenshots and relevant logs. A missing eligible issue is blocked;
debug granting/completing a quest would prove only those debug operations, not this production
admission oracle. Setup and mutations are not required for this recipe.

Cleanup: close only the owned conversation/UI, stop the owned run and confirm artifacts remain
readable. No issue is granted, completed or deleted by this procedure.

Excluded: personal quest acceptance, alternate troop/companion solution, each success/failure/
betrayal/timeout branch, reward ownership, journal synchronization, save/reload and every other
quest's support. These need source-derived branch matrices and two-client evidence of real actions.
