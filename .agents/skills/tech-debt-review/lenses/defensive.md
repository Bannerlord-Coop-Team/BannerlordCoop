# Lens: defensive

Review the change for code that guards against failures instead of understanding them. Follow the Rules in `.agents/skills/tech-debt-review/SKILL.md` first; you get a brief (what the change must do, checkout path, diff) and the scan hits tagged `defensive:`.

## Check

**Exceptions are handled at a cohesive boundary.** The boundaries in this codebase:
- a message handler's entry;
- a `GameThread.RunSafe` closure, which already catches and logs, so a try/catch inside one is redundant;
- a Harmony patch, where an escaping exception lands in vanilla code;
- the `Poller` thread, where an uncaught exception silently stops networking.

For every catch the change adds, list every type of unexpected error that could be hidden by it. Below those boundaries, a try/catch is debt unless it actually recovers. Nested try/catch and log-and-rethrow at every layer are the same smell. An empty catch, or one that logs and carries on when the caller needed to know, hides a failure.

**No validation of always-valid inputs.** Null checks on constructor-injected dependencies (Autofac never injects null), on values the caller just created, or on state an invariant guarantees add noise and hide real bugs. Checks belong where bad data can actually arrive: network message contents, an id that may not resolve, or game state that can be gone by the time a message applies (a destroyed party, `Campaign.Current` during load). Trace the callers before deciding a value can't be null.

**Root cause, not symptom.** Especially for bug fixes: a null guard, try/catch, retry, delay, or early return that makes the failure go away without explaining it. Ask why the value was null or the event out of order, and whether the change fixes that cause. A `Thread.Sleep`/`Task.Delay` that papers over ordering is this smell.

## Return

Three lists, every item with `path:line`:

- **Candidate findings:** which check, the debt, its concrete cost, and the smallest fix.
- **Looks bad but is fine:** things you considered flagging and dropped, with the reason. If it's empty, you didn't look hard enough.
- **Open questions:** anything you're unsure is debt or intentional. Ask here instead of asserting a finding.

Write "Nothing material" for a check with no findings instead of padding it.
