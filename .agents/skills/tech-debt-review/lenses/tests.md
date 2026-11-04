# Lens: tests

Review whether the change's tests add more value than they cost to maintain, and whether tests are missing. Follow the Rules in `.agents/skills/tech-debt-review/SKILL.md` first; you get a brief (what the change must do, checkout path, diff) and the scan hits tagged `tests:`.

## Check

Read each new or changed test next to the code it covers.

**High value:**
- a bug fix comes with a test that fails without the fix;
- serialization round-trips, including null and empty values;
- state transitions;
- sync behavior across instances (E2E).

**Upkeep without value:**
- tests that restate the implementation (mock every call, then verify every call);
- tests of getters, constructors, or framework behavior;
- coverage that duplicates another test of the same path;
- heavy setup for a trivial assertion;
- coupling to private details, so a refactor breaks the test without a behavior change;
- waits on timing;
- `Skip` without a linked issue.

**Missing tests.** Testable logic (serialization, state transitions, service behavior) with no test, and a new GameInterface dependency without a `RegisterMock` in `Coop.Tests/TestComponentBase.cs`. Logic that needs the game runtime should come with manual verification steps in the PR instead.

Before suggesting a test:
- name the specific failure or regression it would catch;
- check whether existing tests already cover the path, including `Coop.IntegrationTests` and `E2E.Tests`;
- weigh its upkeep against that benefit, and skip trivial getters, setters, and constructors.

## Return

Three lists, every item with `path:line`:

- **Candidate findings:** which check, the debt, its concrete cost, and the smallest fix.
- **Looks bad but is fine:** things you considered flagging and dropped, with the reason. If it's empty, you didn't look hard enough.
- **Open questions:** anything you're unsure is debt or intentional. Ask here instead of asserting a finding.

Write "Nothing material" for a check with no findings instead of padding it.
