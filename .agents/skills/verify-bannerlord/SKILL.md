---
name: verify-bannerlord
description: Inspect the Bannerlord feature map, select verification with the repository harness, and drive source-bound co-op recipes through the existing Windows MCP tools. Use for BannerlordCoop capability questions and reusable verification; game recipes require an authorized owned live run.
---

# Verify Bannerlord

Read [the map](../../../features/README.md) and the selected recipe. This skill works from
the BannerlordCoop repository root on native Windows. Python 3.10+ and the repository's
declared .NET SDK are needed for the local map/selector operations; pstack is not a dependency.

The map is granular discovery and acceptance planning, not an exhaustive compatibility claim.
Four game recipes are draft, source-inspected instructions awaiting live exercise. The selector
recipe was exercised on the source named in [the evidence](../../../features/evidence/authoring.md).

## Scope and selection

1. Bind the intended checkout, commit/tree, changed paths and deployed assembly identities.
   Preserve an existing workflow's owner, Local/Remote lane, source receipts and test plan.
2. Read [the granularity contract](../../../features/granularity.md) and select individual
   [behavior leaves](../../../features/behaviors/README.md), not just an action group.
   Query `python tools/feature_map.py find WrappedHandles --kind perk-effect` or
   `python tools/feature_map.py find trade.partial-stock --kind interpreted-case`.
   `check` validates declarations, source spans/hashes, parent IDs, links and generated pages;
   it does not test a game.
3. Use [VerificationHarness](../../../doc/automated-testing/verification-harness.md) for the
   actual required profile. Do not translate the feature inventory into another tier selector.
4. Keep leaf knowledge, runtime and support separate. Candidate requirements, definitions,
   interpreted complete-member cases and [structural paths](../../../features/source-paths.md)
   establish different facts. Structural paths are not behavior oracles. Read the complete
   caller/guard context before promoting one into a source case; never inherit another leaf's pass.
5. For perks, retain choice plus each primary/secondary effect separately, even when both
   roles match. Use [perk sheets](../../../features/perks.md) and the bound model references;
   trace role, primary/secondary argument, troop mask, caps and formula before predicting an
   effect. A located reference is not automatically assigned to either effect. Disabled paths
   and optional modules remain disabled/unknown despite definitions or debug catalog entries.

From the root, the existing selector accepts real Git identities:

```powershell
$verificationHead = git rev-parse HEAD
$verificationTree = git rev-parse 'HEAD^{tree}'
dotnet run --project source/VerificationHarness/VerificationHarness.csproj -c Release -- plan --head $verificationHead --tree $verificationTree doc/automated-testing/verification-harness.md
```

That is a planning example for the named documentation path. For a change, pass the actual
authoritative changed-path list with `--stdin` as described in the harness documentation.
Unknown paths require `full-live`; unavailable executors remain blocked. Neither map validation
nor this example substitutes for the returned cumulative checks.

## Launch and doctor

For the local selector, follow [selection](../../../features/recipes/selection.md).
It builds only the inspected harness/Common graph and launches no game.

For a game recipe, first read [the shared runtime procedure](../../../features/recipes/runtime.md).
Use the repository's direct `bannerlord-coop` MCP tools, whose inputs are defined by
[DebugTools](../../../tools/CoopMcpServer/DebugTools.cs). Discover a configured profile and save
through that driver. Do not invent a launch profile or create a shell/file IPC driver.
MCP setup/initialization does not authorize game launch or deployment. Follow
[setup](../../../doc/automated-testing/mcp-setup.md) and the current workflow's runtime ownership.

`preflight_run` is read-only. `start_run` launches already-deployed matching DEBUG assemblies,
never builds/deploys. `state: started` means launch only. Inspect `get_run` and wait for
`readyForCampaignTests`; clients first need `readyToJoin`, one explicit join and campaign readiness.
Use one server and two real rendered clients for cross-client proof. The host has no playable party.

If the direct driver, matching Debug build, source identity, save/isolation or owner is missing,
retain the exact blocker and continue independent map/selector work. Do not change another owner's
runtime, a selected lane or a save merely to exercise a recipe.

## Drive and evidence

Select one of the five recipes in the map. Resolve runtime-only identities from working read-only
lookup commands and the recipe's selection rule. Danustica is settlement `town_ES1`, town object
`town_comp_ES1`; those IDs are not interchangeable with hero or party IDs.

Confirm commands in `list_commands` after `commandRegistryReady`. Inspect structured `ok`,
`result.succeeded`, `result.errorCode`, output and independent state. A request acknowledgment
does not prove that its action applied. Setup fixtures do not replace a production action.
Perform authoritative state-changing debug setup on the server; genuine player requests such
as tournament join/choice run on their client. Clients inspect the resulting state read-only.

Record expected/actual outcomes, exact action, source/build/run identities, each peer's observations,
selected leaf IDs, side effects, negative/boundary cases and raw evidence paths. Separate
admission, state application, reward, persistence and reconnect observations. Use the existing
verification report contract when required;
do not invent another run schema. Inspect actual rendered screenshot contents for visual claims.

## Cleanup

Restore only the selected recipe's owned fixture and confirm its result. Always call `stop_run`
for a run created by this procedure before closing/reloading the MCP session. Confirm
`cleanupComplete: true` and `processTreeAlive: false` for all owned instances. If a caller delegated
an existing run, return it to that owner's prescribed boundary rather than stopping it unilaterally.
Preserve evidence after success or failure. Reconcile an uncertain mutation through observation;
never automatically repeat it. Cleanup failure remains an explicit blocker.
