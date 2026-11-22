---
name: review
description: Review a GitHub pull request and post the result to the PR as a request-changes review with direct, human-sounding inline comments. For the local working diff use /code-review instead.
---

# PR Review

Review the pull request given as `$ARGUMENTS` (PR number or URL; if omitted, the PR for the current branch).

## Gather

1. `gh pr view <pr> --json title,body,author,baseRefName,headRefName,state,additions,deletions,changedFiles,labels` for context.
2. `gh pr diff <pr>` for the unified diff (write it to a temp file if the output gets truncated).
3. `git fetch origin pull/<pr>/head` and check out the head commit into a separate worktree so surrounding code can be read directly. Don't review in the main working directory, because it's a rotating test bench that usually holds unrelated changes (see `AGENTS.md`).
4. Check `gh pr checks <pr>` and existing reviews/comments before writing anything.

## Analyze

Review the PR to determine whether the implementation is **correct, simple, readable, and appropriately decoupled**. The goal is not merely to find bugs, but to verify that the resulting code is the clearest and simplest correct solution for the project.

### First Pass: Correctness

Review correctness, project conventions, threading and ordering, performance, test coverage, security, and behavioral compatibility.

Do not review from the diff alone. Read the surrounding implementation and verify every load-bearing assumption before raising a finding, including:

* Who calls the changed code.
* What thread or execution context it runs on.
* What ordering guarantees exist.
* What referenced helpers actually do.
* What invariants the surrounding code relies on.
* Whether tests exercise the behavior being changed.

Do not speculate. Drop any finding that does not survive verification.

Prefer identifying actual behavioral problems over hypothetical edge cases that cannot occur within the project's constraints. Co-op assumes cooperative, trusted clients, so frame findings on their honest failure path (double-click, race, stale local state), not as anti-cheat or forged-packet exploits. Decompile before asserting anything about vanilla behavior (`.agents/skills/decompile`).

### Second Pass: Simplicity and Maintainability

Assuming the implementation is correct, evaluate whether it is the **simplest, human readable, and reasonable implementation**.

Favor:

* Clear, direct control flow.
* High readability at the call site.
* Small, focused responsibilities.
* Low coupling between unrelated components.
* Existing project abstractions when they genuinely simplify the solution.
* Explicit behavior over clever or surprising behavior.
* Fewer concepts, layers, helpers, states, and dependencies when they provide no meaningful benefit.
* Code that is easy to modify or remove later.

Challenge unnecessary:

* Abstractions.
* Indirection.
* Wrapper methods.
* Configuration.
* State.
* Dependencies.
* Generic solutions for problems that are currently specific.
* Duplication removal that makes the resulting code harder to understand.
* Defensive logic for states the system cannot actually enter.

Do not recommend abstraction merely because code could theoretically be reused. Introduce abstractions only when they make the current implementation materially easier to understand, safer, or less coupled.

When comparing multiple valid approaches, prefer the one with the smallest conceptual surface area while preserving correctness and project conventions.

### Decision

* **Request changes** if the implementation is incorrect.
* **Request changes** if the implementation is correct but unnecessarily complex, difficult to read, or introduces avoidable coupling.
* **Request changes** if a materially simpler and equally correct solution exists.
* **Approve** when the implementation is correct, readable, appropriately tested, low-coupling, and reasonably as simple as the problem allows.

Do not request changes for personal stylistic preference when the existing implementation is already clear, conventional, and simple.

## Post the review (always)

The deliverable is a GitHub review on the PR, not a chat summary. Post it as **request changes** whenever the review contains anything actionable (only a genuinely clean review gets an approve instead).

Anchor each finding as an inline comment on the exact changed line. Build a JSON payload and POST it:

```
gh api repos/<owner>/<repo>/pulls/<pr>/reviews --method POST --input review.json
```

with `"event": "REQUEST_CHANGES"`, a `body`, and `comments: [{path, line, side: "RIGHT", body}]`. Line anchors must be lines present in the diff, resolved on the PR head, not diff-offset estimates.

If the call fails 403 with "Resource not accessible by personal access token", the env token is a limited fine-grained PAT, so retry with the keyring token: `env -u GH_TOKEN -u GITHUB_TOKEN gh api ...`.

After posting, reply in chat with just the review URL and a one-line recap of the blocking items.

## Comment style (direct and human)

Follow the maintainer voice in `AGENTS.md` ("Review Comments & PR Prose"): short, lowercase, casual, no em dashes, no first person, no jargon filler. On top of that:

- Lead the body with what's right about the PR in a sentence or two, then say plainly why changes are requested. No "Verdict:", no scorecards, no emoji, no section headers in inline comments.
- Each inline comment states the problem, why it matters, and what to do, in that order, in plain sentences. Include a short code block when a concrete fix is clearer than prose.
- Cut hedging ("might possibly", "consider perhaps") and AI tells ("verified that...", "Great work on..."). Nits get one line prefixed "Nit:".
- Reference project history the way a teammate would ("Poller-thread throws have bitten this before"), not like an auditor citing evidence.
