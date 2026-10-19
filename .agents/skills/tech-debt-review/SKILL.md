---
name: tech-debt-review
description: Review a BannerlordCoop PR, or a local new implementation or bug fix, for the tech debt it adds before it merges. Use when asked for a tech debt review of a PR number, a branch, or uncommitted changes.
---

# Tech debt review

Find the debt a change **adds or extends**, through four lenses, then verify and rank what they find.

## Rules (every lens follows these)

- **Scope.** Debt that already existed is out of scope unless the change copies it, builds on it, or makes it harder to remove. Intentional, tracked debt (a linked issue plus a removal condition) is fine; report it only when the tracking is missing.
- **Concrete cost.** Every finding names what it costs: a likely bug, a harder future change, a flaky test, or code that misleads the next reader. Drop anything that can't.
- **Evidence.** Cite `path:line` on the PR head or working tree, not diff offsets. Decompile before asserting anything about vanilla behavior (`.agents/skills/decompile`).
- **Out of bounds.** No anti-cheat or malicious-client concerns, no contorting code for CodeFactor, no code changes, no GitHub posts unless asked.

## 1. Get the change and its purpose

- **PR:** `gh pr diff <N>` plus the PR description and linked issue. Check out the head (`git fetch origin pull/<N>/head`) into a worktree so lenses can read whole files.
- **Local:** `git diff "$(git merge-base origin/development HEAD)"`, which includes uncommitted changes. Run it in the checkout that holds the change; the main working directory is a rotating test bench (see `AGENTS.md`) and usually holds unrelated edits.

Write a short brief: what the change must do, the checkout path, and how to get the diff. Every lens gets it.

## 2. Run the scan

```bash
git diff "$(git merge-base origin/development HEAD)" | .agents/skills/tech-debt-review/scripts/scan.sh
gh pr diff <N> | .agents/skills/tech-debt-review/scripts/scan.sh
```

It greps the added lines and prints `path:line [lens: rule] text`. Hand each lens the hits tagged with its name. Hits are leads, not findings.

## 3. Run the lenses

| Lens | File | Covers |
| --- | --- | --- |
| fit | `lenses/fit.md` | existing frameworks used, nothing new invented, no unneeded complexity |
| defensive | `lenses/defensive.md` | exception boundaries, validating always-valid inputs, symptom fixes |
| flow | `lenses/flow.md` | sync invariants, temporary code, dead code |
| tests | `lenses/tests.md` | tests worth their upkeep, missing tests |

- **Small change** (under about 10 files and 300 changed lines): run all four lenses yourself, one after another as separate passes.
- **Larger change:** give each lens to its own subagent, in parallel if your agent supports it, with the brief, its lens file, and its scan hits. Without subagent support, run them as separate passes.

Each lens returns candidate findings; it doesn't rank or filter across lenses.

## 4. Verify

Re-check every candidate against the code yourself; lenses over-report. Drop findings that are wrong, pre-existing, or have no concrete cost. Merge duplicates found by more than one lens.

## 5. Report

List findings in chat, ranked by cost. For each give `path:line`, the debt, what it costs, and the smallest fix. Split them into **fix before merge** and **follow-up issue**; for a follow-up, check `gh issue list --search` for an existing issue first.

When asked to post on a PR, write one point per inline comment in the maintainer voice from `AGENTS.md` (Review Comments & PR Prose).
