# Lens: fit

Review the change for debt from not fitting the codebase. Follow the Rules in `.agents/skills/tech-debt-review/SKILL.md` first; you get a brief (what the change must do, checkout path, diff) and the scan hits tagged `fit:`.

## Check

**Existing frameworks are used.** For each job the change does, find how the codebase already does it: start from the framework table in `doc/CodeStyle.md`, then grep for an existing helper, service, message, or registry of the same shape. A second mechanism for something the codebase already does is debt even if both work.

**Nothing new is invented.** No new framework, paradigm, or naming convention (a novel suffix, a `SomeFeatureSentinel` concept, a homegrown event or registry system) without a reason written in the PR description or a code comment. The reviewer can't see the prompt that asked for it, so an unexplained invention is a finding.

**No complexity beyond what the change needs.**
- Pass-through layers or forwarding wrappers that make simple behavior hard to trace.
- A second source of truth for state that already has an owner.
- A wider public surface or more configuration than callers use.
- An ordinary change that had to touch many layers.
- A file pushed well past ~800 lines that now holds several responsibilities.
- Long comment blocks narrating vanilla behavior instead of a one-line why.

The repo's interface-per-class plus Autofac convention is not over-abstraction; don't flag it.

## Scan hits

- `reflection`: debt only if the member's assembly is on the `<Publicize Include>` list in the `.csproj`.
- `static helper`: Harmony patch classes are expected to be static; debt for new shared logic that should be an injected transient.
- `legacy command`: new console commands should be `ICoopCommand`s.

## Return

Candidate findings, each with `path:line`, which check, the debt, its concrete cost, and the smallest fix. Return nothing for a check that's clean.
