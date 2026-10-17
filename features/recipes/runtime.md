# Shared native Windows runtime procedure

This is common launch/doctor/evidence/cleanup guidance, not a sixth feature recipe.
The direct `bannerlord-coop` MCP tool inputs are defined in
[DebugTools.cs](../../tools/CoopMcpServer/DebugTools.cs), with the staged workflow in
[the server README](../../tools/CoopMcpServer/README.md#agent-workflow).
Use the existing workflow/owner's runtime admission when it owns the environment.

Required before a game recipe: a separately authorized live test, no conflicting game/port
owner, an isolated test campaign, a configured profile, and a matching deployed DEBUG mod
bound to the intended checkout. The bridge is absent in Release. Follow
[setup](../../doc/automated-testing/mcp-setup.md) to expose the direct driver when missing;
initialization alone does not launch a game. Deploy only when separately authorized through
the existing [deployment procedure](../../doc/automated-testing/mcp-deployment.md).

The example profile `local` below is usable only if that profile actually exists in the
operator's local configuration. The tools have no profile enumeration API: inspect the selected
repository launcher's configured profiles without editing it or copying machine paths into recipes.
Do not treat `local` as a guaranteed installed profile. The save name must likewise be discovered.

Tool sequence and JSON arguments, with values obtained from prior responses:

1. `preflight_run` with `{"profile":"local","client_count":0}`. Check commit headroom,
   DEBUG bridge protocol/capabilities and no unresolved deployment/ownership conflict.
2. `list_saves` with `{"profile":"local"}`. Follow returned `nextOffset` until complete.
   Select the agreed disposable campaign basename. If the isolated catalog actually contains
   `MP`, `start_run` can use `{"profile":"local","client_count":0,"save_name":"MP"}`.
   Otherwise use the selected actual basename; no missing-save fallback. Ordinary autosaves
   are not disabled. Do not use a valuable ongoing campaign as an implicit fixture.
3. Retain returned `runId` as `run_id` in every subsequent call. Record the returned artifact
   directory. `start_run` being `started` only confirms launch.
4. `wait_for_state` with the returned `run_id`, `instance:"server"`,
   `state:"readyForCampaignTests"`, `timeout_seconds:300`. Require `reached`.
5. `start_client` with `run_id` and `client_index:1`. Wait for `client1` with
   `state:"readyToJoin"`, then `join_client` with `run_id`, `instance:"client1"` once.
   Require response success and wait for `readyForCampaignTests`.
6. Repeat the staged procedure for `client_index:2` / `client2`. Do not replay an existing,
   exited, failed or uncertain client slot. Reconcile with `get_run` first.
7. `get_run` with `run_id`: record process/role/platform identities, loaded assembly
   MVIDs/source build binding, campaign identity, readiness, registry state and errors for all peers.
   The server has no playable party. A requested save acknowledgment does not confirm a filename loaded.
8. `list_commands` with `run_id` and each `instance`, after `commandRegistryReady`.
   Require the chosen recipe's commands to be present on the intended sides.

To drive a command use `execute_command` with `run_id`, `instance`, exact `name` and
`arguments` as a string array. Example: `instance:"client1"`,
`name:"coop.debug.town.list_towns"`, `arguments:[]`. This lookup is read-only.
State-changing debug setup originates on `server`; intentional player requests originate on
their owning client. An `ICoopCommand` side of `Both` is not permission to stage mutations on clients.

Require `ok`, command `result.succeeded`, absence of a command error, and the independent
world/UI oracle. Legacy text output does not provide an inferred success flag. Preserve
`LIVE_TEST_JSON` when returned. `outcomeUncertain:true`, transport cancellation or timeout after
send can mean the action applied; inspect `get_run`, state and logs before any further mutation.

Capture `read_logs` per instance with `max_bytes:16384`, retaining returned cursors and all
additional data indicated by `hasMore`. Log rotation/reset is observable, not a proof of no errors.
For visual assertions use `capture_screenshot` with `run_id`, intended client `instance`,
`timeout_seconds:60`; inspect the actual returned PNG and artifact metadata. Use `ui_layers`
and `ui_inspect` to obtain current snapshot/element IDs before a native `ui_action`. Do not
invent widget selectors, reuse stale snapshot IDs, or treat a screenshot receipt as visual acceptance.

Cleanup on success or failure: restore only an owned recipe fixture while the run is healthy,
retain all raw responses/screenshots/logs and expected/actual results, then `stop_run` with `run_id`.
Require `cleanupComplete:true` and `processTreeAlive:false` for every owned instance, including
descendants after a root exited. Preserve the run's `run.json`, per-request JSON, copied endpoint
logs and completed images in its returned artifact directory; re-read them after stop. A cleanup
failure must remain explicit. Never stop an unrelated run, delete saves, change source/lane/helper
pins or close/reconnect the MCP session to bypass cleanup.
