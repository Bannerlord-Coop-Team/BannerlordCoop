# Lens: flow

Review the change by tracing what runs where: sync invariants, temporary code, and dead code. Follow the Rules in `.agents/skills/tech-debt-review/SKILL.md` first; you get a brief (what the change must do, checkout path, diff) and the scan hits tagged `flow:`.

## Check

**Sync invariants hold** (`AGENTS.md`, Architecture & Sync Invariants). Trace each new or changed path through patch, message, handler, and apply:
- no server game logic under `AllowedThread`, and no objects created where registration is silenced;
- registry lookups and `Campaign` reads inside the `GameThread` action, not on the poll thread before marshalling;
- no side-effect ordering that depends on handlers crossing threads;
- `GameThread.RunSafe` unless an exception must reach a blocking caller;
- no host-party edge cases: `MainHero`/`MainParty` is fine on client paths, debt when a server path can reach it.

**No temporary code without an exit.** Fallbacks, compatibility paths, flags, repairers, backfills, "for now" code, and TODO markers each need a linked issue and a removal condition.

**No dead code.** Members, messages, handlers, or config nothing uses; a message with no subscriber or a subscriber with no publisher; a patch on a method that never runs; leftovers of the approach the change replaced; `doc/` or `AGENTS.md` text naming a symbol the change renames or deletes.

## Return

Candidate findings, each with `path:line`, which check, the debt, its concrete cost, and the smallest fix. Return nothing for a check that's clean.
