# Naval lab runbook

Operational handoff for a fresh testing agent. Read this before building, deploying or launching. This is a disposable DEBUG experiment, not production naval support.

## 1. What the lab is

Use **TwoClientNative** for current ship-control work:

- One in-game dedicated-authority server (`/server`) and two playing clients. The server has no playable party or mission agent.
- Two fixture ships, five synthetic actors per ship: a main player/captain and four AI rowers. These are not the participants' campaign heroes or fleets.
- Each participant controls only its original ship. Cross-ship helm work was cancelled and is not in the current source.
- The elected playing client simulates both ships. The other client applies host hull frames with a 50 ms interpolation window and `SetGlobalFrame(..., false)`, then updates attached navigation and occupied station targets. Taking a helm does not transfer physics authority.
- Damage and campaign/capture effects are disabled. Native crew, water and ship physics remain active on the simulator. Do not enable combat to test controls.
- Current source automatically seats each original main agent at its own helm after deployment/readiness, using native spawn assignment once. A later tick must confirm actual use; a dispatch receipt is insufficient.
- Confirmed occupied rowers and pilots withhold their on-foot movement sends. Equipment, actions and ship inputs remain separate.
- Latest source adds server-routed remote helm take/release, actual native station use on the remote `None` replica, and actor-specific movement exclusion. A DEBUG movement revision and per-fixture movement scope reject stale pre-seat/old-fixture packets. This increment has compiled but has **not passed native acceptance**.

**Lifetime: 120 seconds after fixture release.** The user briefly requested five minutes, then explicitly reverted to two. Do not lengthen the lifetime, input deadmen or observation deadlines without approval. Setup/confirmation also has shorter deadlines. Terminal state, departure or epoch change is not recoverable in-process: stop and exit all owned games. Never create a second fixture in the same processes.

### Current evidence, not assumptions

As of the remote-helm source handoff:

- Two-client campaign startup, fixture deployment and scalar inspection have worked.
- Lerp plus rower-target refresh looked substantially better to the user. That is not quantitative continuous-contact acceptance.
- Automatic local helm use was observed on both clients with matching main/user/pilot identities.
- A single captured sample showed pilot movement withholding active; it was not a complete counter-delta test.
- Steering, propulsion, sail presentation, release/resumption and the latest remote helm replication still need acceptance.
- Source was intentionally implemented without additional tests or independent review at the user's request. Do not claim either passed. An earlier interpolation E2E run had an unresolved `SamplesWaitForEndpoint_AndSupersededTargetsNeverClaimAnAppliedEndpoint` failure.

## 2. Ownership and interrupted-agent recovery come first

There must be **one runtime owner**. The implementation agent writes source; the lab agent owns deployment, game processes, evidence and restoration. A new source-ready message supersedes older candidate instructions. Check queued pause/cancel messages before every build, deployment and launch. Never launch a previously approved candidate after a newer pause.

When replacing an agent that died:

1. Read its last handoff, run artifacts, deployment script and backup records. Do not assume death stopped its game or MCP processes.
2. Obtain a read-only process inventory with PID, parent PID, creation time and command line. Identify the `/cooptestrun` token and artifact directory. A PID alone is not ownership proof.
3. Check for `installed.json`, `staged-snapshot.json`, `restoration-targets.json`, `restored.json` and canonical/config/save inventories in its backup directory. Absence of a completion marker is **not** proof that nothing changed.
4. Do not launch over an unresolved run or incomplete deployment. Adopt the old run only through a supported owned-run mechanism; a new MCP process must not be assumed able to `get_run` or `stop_run` another MCP session's in-memory run.
5. If adoption is unavailable, report exact ownership evidence to the lead and obtain recovery direction. Only terminate verified old lab-owned process trees, never unrelated games, Pi/Node sessions, Steam, browsers or build workers.
6. Restore only after game processes are gone. Compare current files with both the saved canonical inventory and any staged inventory. If staging stopped halfway, inspect script execution order and actual hashes before constructing a targeted recovery. Never rerun `install` over an existing backup or blindly use an old `restore` script that expects files which were never written.
7. Record the final full installation differences, config/save differences and process scan. Only then declare the machine available for a new run.

The dated, machine-specific companion handoff is deliberately outside git. Ask the lead for its location. This runbook contains no canonical backup that is safe to reuse for future deployment.

## 3. Source and candidate handoff

Read repository `AGENTS.md`. Use the isolated naval implementation checkout and the exact source revision/diff handed over by the lead, not the rotating main checkout.

At this document's creation the pushed checkpoint is `8898966945761cb3c1884247ad8eb648334b56fe` on `feature/warsails-battle-foundation`. Autohelm, pilot movement suppression and remote occupancy are additional uncommitted changes. **Checking out the pushed commit alone omits these features.** The source tree and actual built file inventory must both be identified in the run report.

Before building:

- Confirm no source writer is active. Record `git status`, HEAD and the approved task diff. Preserve unrelated changes.
- Record whether authority is manual-only or allows synthetic control commands. Do not interpret permission to launch as permission to run a large test suite.
- Do not run tests, independent review, create a freeze, commit or push unless the task authorizes them. Minimal compilation is needed for a new source candidate.
- Use Windows MSBuild with `/p:ModName=` to prevent `Deploy.targets` from deploying into the live installation. Clearing `PostBuildEvent` alone is insufficient; use both where appropriate. Prefer `/m:1 /nr:false` and avoid persistent build workers.

Typical compile-only commands from the implementation checkout (resolve the installed MSBuild executable first):

```text
MSBuild.exe source\Coop\Coop.csproj /t:Build /p:Configuration=Debug /p:Platform=AnyCPU /p:ModName= /p:PostBuildEvent= /m:1 /nr:false
MSBuild.exe source\Missions.Naval\Missions.Naval.csproj /t:Build /p:Configuration=Debug /p:ModName= /p:PostBuildEvent= /m:1 /nr:false
```

Inspect exit codes and errors, not just the last log line. Do not rebuild dependencies against mismatched outputs or quietly substitute a prior candidate after failure.

Make an immutable per-run candidate copy of exactly these managed deployment files:

```text
Coop.dll
Common.dll
GameInterface.dll
Missions.dll
Coop.Core.dll
Missions.Naval.dll
```

The first five normally come from the coherent `Coop` Debug output; the adapter comes from `Missions.Naval/bin/Debug/netstandard2.0`. Verify the actual current build layout. Never copy publicized TaleWorlds/NavalDLC DLLs or whole build directories into the game. Record full source paths, SHA256, byte length and MVID for all six. Preserve the approved opt-in marker as a seventh input. The latest DEBUG wire additions require a matching complete build on server and both clients.

## 4. MCP access for a fresh agent

Use the **isolated `bannerlord-naval-lab`** MCP registration, not the unrelated/shared `bannerlord-coop` server. The lab-compatible publication has 18 tools, including `ui_layers` and layer-scoped `ui_inspect`.

The runtime owner needs its own publication/config/profile/artifact directory and actual tool access. A child agent does not automatically inherit an interactive parent's live MCP connection. Before delegating testing, verify that the chosen agent exposes the MCP proxy and loads the required extension. Read the installed pi-subagents/MCP documentation for this harness rather than inventing launch parameters.

Supported startup is a per-session `--mcp-config`, an approved isolated-cwd configuration, or a child-only extension exporting the installed adapter's `createMcpAdapter({ config })` factory. The factory's explicit config snapshot is not merged with ambient/project MCP files. For a testing subagent, load only that prepared extension and allowlist `mcp` and `mcpScript` plus its required inspection/artifact tools; run it as a background child. No interactive reload is needed for that supported factory path. Verify publication hashes and actual 18-tool discovery before trusting it.

Without that prepared child extension, use the normal registration process. If registration requires an interactive reload, request it for the **testing session**, not the lead's session. Do not reuse another session's `resume.ps1`, rewrite shared `.pi/mcp.json`, repeatedly kill keepalive servers, or silently replace MCP with an SDK/file-IPC/CLI workaround. A missing runner/extension/tool is an infrastructure blocker to report.

Configuration shape, with absolute paths supplied by the runtime owner:

```json
{
  "mcpServers": {
    "bannerlord-naval-lab": {
      "command": "ABSOLUTE_PRIVATE_PUBLICATION/CoopMcpServer.exe",
      "args": ["--config", "ABSOLUTE_PRIVATE_DIRECTORY/profiles.json"],
      "cwd": "ABSOLUTE_PRIVATE_DIRECTORY",
      "lifecycle": "lazy-keep-alive",
      "requestTimeoutMs": 360000,
      "directTools": false
    }
  }
}
```

Profile `naval` uses modules, in order, `Native`, `SandBoxCore`, `SandBox`, `StoryMode`, `NavalDLC`, `Coop`; server platform ID `testserver`; client platform IDs `testclient1`, `testclient2` (additional slots are optional). Use the actual installed `Bannerlord.exe` and a private runs directory. These platform IDs are configuration, not permission to assume the elected host or runtime agent IDs.

Inspect the registered tool schemas before calling them. Typical proxy call:

```javascript
mcp({ server: "bannerlord-naval-lab", tool: "start_run",
      args: { profile: "naval", client_count: 2 } })
```

Use the exact discovered name if the adapter prefixes it. Calls return transport envelopes as well as game outcomes. Preserve raw responses; handle `structuredContent.result` or parsed content text as appropriate. Do not treat an omitted parsed field as proof that a mutation did not execute. `join_client` may expose `started` directly rather than under `result`.

## 5. Fresh deployment

1. Confirm no competing lab owner, running user game or unresolved restore obligation.
2. Take a **fresh** canonical inventory and backup of the live Coop module, including file hashes, lengths and Windows attributes. Back up `SubModule.xml`, the prior opt-in state, redirected Documents `Configs` and `Game Saves`, plus logs that startup will overwrite.
3. Verify the backup against the inventory. Recheck that canonical files did not change during backup.
4. Stage only the six approved DLLs and opt-in. Record each changed path and its original existence/attributes. The marker is `naval-lab.optin` alongside the mod binaries; obtain its exact approved bytes from the handoff, do not invent a new capability token.
5. Confirm that the profile module ID matches `SubModule.xml`. Some canonical installations have directory `Coop` but module ID `CoopNightly`. A lab-only reversible swap to ID `Coop` and incompatible ID `CoopNightly` has been used. Never modify this without backing up exact bytes/attributes; restore it afterward.
6. Save a staged inventory and an explicit restoration-owed record before launching.
7. Call `preflight_run` for `naval`, `client_count: 2`. The gate measures **commit headroom**, not free physical RAM, and verifies the DEBUG bridge. Do not lower budgets or change the pagefile. A successful preflight is not a reservation.

The approved marker plus actual `/cooptestrun` token enables a fresh disposable DLC campaign. Do not supply an old MP save, write the marker into shared profiles, allow disk-save fallback or disable the campaign isolation guards. In-memory campaign transfer is intentional; disk saves/autosaves are denied in this mode.

## 6. Launch, join and clear the campaign inquiry

For speed, `start_run(profile: "naval", client_count: 2)` launches all three processes back-to-back after aggregate preflight. It does **not** mean they are connected.

1. Save the returned run ID and artifact directory immediately.
2. Await server `readyForCampaignTests` and both clients `readyToJoin`; these waits can run concurrently. Bounded timeout or early exit means stop and investigate, not repeat launch.
3. `join_client` once for each ready client, then await each `readyForCampaignTests`. Do not blindly retry a join with an uncertain outcome.
4. Read current status and verify loaded six assembly identities, command registry readiness and the same run-scoped naval capability. Process-alive is not endpoint-ready; endpoint-ready is not campaign/mission-ready.
5. On each client clear **Call of the Oceans** before fixture creation. Use `ui_layers`, select the live `QueryManager` layer, then complete layer-scoped `ui_inspect`. Find the actually visible/enabled exact Continue button and activate it with `ui_action` using that snapshot/element. Reinspect: the inquiry layer should be inactive, with no roots/input mask. Capture and inspect the rendered result when needed.

Never reuse snapshot IDs, guess element references, click through an incomplete/truncated tree, or use generic OS clicks/keyboard synthesis. If the title is different, stop: **Troubled Waters** is not equivalent and can mutate campaign state. Do not rerun character creation. A native shader-cache/mod-change notice before the bridge may require the user to acknowledge it; do not loop blind clicks.

Save PIDs as separate fields with process creation times. Write `client1 PID: 20320`, not `client120320`.

## 7. Create and deploy the current fixture

Run authoritative game commands on `server`; clients are for inspections and real user keyboard input. Use `execute_command` with a string-array `arguments`, not a quoted console line.

First run `coop.debug.players.list` with `[]` and verify the actual connected IDs. With the standard profile the following is a concrete worked example. **Generate fresh UUIDs for every new operation; the literals below illustrate one run only.** Store the UUID before dispatch and use that same UUID for receipt lookup.

```text
server coop.debug.naval_lab.create
  ["61b3a19c-0179-4d55-99ee-b7456198ad10", "testclient1", "testclient2", "two-client-native"]

server coop.debug.naval_lab.receipt
  ["61b3a19c-0179-4d55-99ee-b7456198ad10"]
```

Wait for both clients `readyForMissionTests`. Read `coop.debug.naval_lab.inspect []` on server and both clients. Require both original owners ready, epoch 1, no failure, two initialized hulls and ten finite/error-free actors on each client. Slot 0 belongs to the first create controller and slot 1 to the second; **host election can choose either client**.

Before release/deployment, be ready to notify the human promptly; don't spend the 120-second window writing reports. Complete deployment once:

```text
server coop.debug.naval_lab.action
  ["cf2ad430-4cda-4aaf-8e95-2675cf38b5ef", "complete-deployment", "0", "0", "false"]
```

This single action targets both clients. Read its receipt rather than sending a second deployment. The coordinator handles fixture release, station exchanges and native-controls readiness; do not invent a separate console `release` action.

### Minimum gate before announcing ready

Use `inspect`, `helm-status` and `control-status` on both clients; save the complete raw responses.

- Deployment and after-deployment callback counts are 1; Battle/input setup exists; both owners and epoch remain valid.
- The four-oar manifests are committed/observed. Both hulls and all ten agents remain valid; no blocker/failure/terminal flag.
- Automatic seating was observed on a later tick, not just dispatched. Own `point.UserAgent`, main agent's used object and machine pilot match; own native view selects its own helm.
- With the **remote-occupancy candidate**, both slots' revisions must be applied, later-observed and confirmed on both clients. Remote pilot/user/used-object match the correct captain while its native controller stays `None`. This requirement was not present in older candidates.
- Host bodies are active; follower active fixed/parallel/force and precompletion-unattributed counters remain zero. Increasing inactive `fixedTicks` alone proves nothing.
- Window focus is separate from helm readiness. A background window can be correctly seated with `inputPermitted=false`.

Announce immediately: exact window/PID, whether it is host/follower, **focus the window and steer, no F required**, and the two-minute limit. Do not require the user to race through manual helm acquisition when autohelm succeeded. `F` releases it; automatic seating does not repeat after release.

## 8. Controls and bounded experiments

Native default bindings: W/S rowing input, A/D steering, Z sail changes, F release/use. Respect remapped bindings and actual UI. Native focus, modal and readiness gates remain. Do not force window focus or claim a console action tested the keyboard.

Current synthetic actions use original ship slot 0 or 1. Run only when authorized and never overlap them with manual input. Verify actual occupied/confirmed helm and permitted input first.

```text
server coop.debug.naval_lab.action
  ["acfaeb21-893f-4080-b96f-87a549c00a62", "sail-full", "0", "0", "false"]

server coop.debug.naval_lab.action
  ["f7805c4c-0ff9-4db4-ae9c-3b17e04a0e99", "native-axes-pulse", "0", "0.5", "true"]

server coop.debug.naval_lab.action
  ["84b8a3f6-e678-4bf4-a86c-8a2f7d5b39ab", "native-release-helm", "0", "0", "false"]
```

The pulse is at most one second; rudder argument is lateral axis, `row=true` requests forward rowing. Normal completion zeros axes while retaining sail choice; safety loss can stop input/raise sails. Other sail kinds are `sail-raised` and `sail-square-raised`. Normal `native-take-helm` remains aim/focus gated; auto setup is a distinct path. Don't substitute old `helm`, `probe` or held-mode `take-helm` actions in TwoClientNative.

Read-only commands:

| Command | Arguments | Purpose |
|---|---|---|
| `coop.debug.naval_lab.inspect` | `[]` | Session, hulls, actors, native counters and lifecycle |
| `coop.debug.naval_lab.helm-status` | `[]` | Local/remote occupancy, revisions, observations and identities |
| `coop.debug.naval_lab.control-status` | `[]` | Input gates, routing, spatial and movement-counter observations |
| `coop.debug.naval_lab.sail-status` | `[]` | Owner request, host-observed sail target and follower presentation |
| `coop.debug.naval_lab.receipt` | operation UUID | Server operation outcome after uncertain dispatch |
| `coop.debug.naval_lab.samples` | `["0"]`, then greatest returned sequence | Existing bounded probe records, if a permitted probe window exists |
| `coop.debug.mission.summary` / `.camera` | `[]` | Main agent, mission and stored camera/follow state |
| `coop.debug.mission.agents` / `.views` | discover current command args | Bounded pages of agents/support and views |

Some command descriptions predate remote replication; inspect the actual returned candidate schema. Avoid enormous all-widget or unbounded log dumps.

### Evidence required for a useful result

Record at least a before/after pair for the active case, without claiming asynchronous reads are a synchronized physics cut:

1. **Control routing:** non-host owner's intended input reaches host for the same incarnation/epoch/slot; host applied input changes and returns to neutral. A receipt alone is not actuator or propulsion evidence.
2. **Movement:** actual host hull position/orientation/velocity response and corresponding follower poses. Compare with idle drift; motion alone is not proof of rowing thrust.
3. **Pilot and crew:** native occupancy, support/local/visual position and animations remain plausible as the hull moves. Increasing captured/withheld with unchanged sent counts while eligible demonstrates suppression treatment, not contact correctness.
4. **Sails:** host actuator target versus owner's displayed state. Target state is not proof of cloth animation completion or thrust. Screenshot the visible icon for UI acceptance.
5. **Release:** exact native identities clear locally and remotely, revision advances, movement resumes at the released revision without an old pose snapping back. A callback or dispatched receipt is insufficient.
6. **Ownership:** follower never starts ship simulation; remote replicas remain `None`; local HUD still addresses the local ship.

Do not declare stages 1–2 complete from this fixture. Actual campaign heroes, boarding, admissions, withdrawal/evacuation and host recovery remain outside current acceptance.

### In-process station drift recording

Current source adds client-only `coop.debug.naval_lab.drift-start [operation_uuid, "30"]` and `coop.debug.naval_lab.drift-status []`. This is diagnostic recording, not a gameplay mutation. Use a fresh shared recording UUID on the two clients after all helm and station confirmations; their start times are still asynchronous. A duplicate UUID with the same duration returns the existing recording without resetting it. Only one recording is allowed per fixture, with duration 1-60 seconds.

The recorder samples all ten actors at no more than 20 Hz on the game thread, retaining at most 1202 values per actor for each aggregate. It reports absolute horizontal/geometric vertical offsets, 3D drift from the first valid offset, visual-root drift, max/nearest-rank p95, first/last/worst rows, hull-origin displacement, coverage/errors and timing gaps. `drift-status` reads retained scalars and does not query native actors or refresh station caches. Export it before teardown.

The station reference is reconstructed from its entity global frame and custom local frame. It deliberately does **not** call `GetUserFrameForAgent` or `WorldFrame`, which refresh the cache under investigation. It accepts only the locked/translating native station branch. Geometric Z is not validated navmesh-ground height, so vertical offsets are not a penetration measurement. A constant initial animation/root offset can cancel in baseline-relative drift: inspect absolute offsets too.

`completeCoverage` is coverage only, not a pass. Require a full 30-second span, ten valid rows on both clients, no gaps/errors/occupancy changes that undermine interpretation, and meaningful measured hull motion. Report the provisional 0.20 m baseline-drift budget separately from uncalibrated absolute/animation offsets. A stationary or incomplete run is inconclusive, not a moving-deck success. No inspection may continually refresh station caches while this recording is running; ordinary gameplay target refresh remains enabled. Use the candidate-specific private protocol for the actual experiment.

## 9. Stop and restore every run

On expiry, fault, readiness failure, crash, user cancellation or final evidence capture:

```text
server coop.debug.naval_lab.action
  ["e478a3d9-460d-4429-b724-df298ed408d5", "stop", "0", "0", "false"]
server coop.debug.naval_lab.receipt
  ["e478a3d9-460d-4429-b724-df298ed408d5"]
```

Use one stop operation and bounded receipt check. Missing receipt does not justify waiting indefinitely or creating a fresh fixture. Call owned `stop_run`; verify all server/client/watchdog descendants exited. A receipt proves an apply result, not native teardown correctness. Process exit is mandatory even when stop fails.

- Archive run artifacts and current per-instance logs before another startup overwrites them. Count actual logger `ERR` headers, not only a guessed `[ERR]` pattern. Keep crashes/dumps local; inspect for sensitive data before any publication.
- Restore the **current run's** backed-up six DLLs, original opt-in existence/content, exact original XML and attributes. Refuse to overwrite unexplained external changes. Never restore from the previous run's backup.
- Compare the entire canonical inventory. Preserve and report Configs/Game Saves deltas; don't delete user saves or blindly replace changed configs.
- Some runs create hundreds of AutoSyncExport additions. Identify only new run-created files against the before inventory, archive them and verify hashes before removal. Never remove preexisting exports.
- Record final hash/length/attribute differences, config/save differences and a fresh process scan. Report any unresolved item as restoration owed; do not announce a clean machine until verified.
- Leave unrelated MCP runtimes, profiles and Pi settings untouched. Do not change system memory settings or kill arbitrary processes to make another run fit.

## 10. Fresh testing-subagent task template

Supply the bracketed operational values as real handoff data before delegation, not as invented IDs or paths:

> Read `AGENTS.md` and `doc/NavalLabRunbook.md`, plus the lead's dated local recovery handoff. You are the sole lab runtime owner, not a source writer. First resolve any existing process/deployment obligations. Use your own supported isolated `bannerlord-naval-lab` MCP session; report tool/registration blockers rather than substituting external IPC. The approved candidate is [source HEAD plus diff and actual binary manifest]; the experiment is [manual controls or explicitly authorized synthetic cases]. No tests/review/source edits/commits unless separately authorized. Build only if requested, with deployment targets disabled. Back up current canonical state, verify the six binaries/opt-in and loaded identities, launch server plus two clients, join, dismiss only the verified inquiry, create TwoClientNative and complete deployment once. Announce ready immediately after actual autohelm and remote-occupancy confirmation. Preserve the 120-second limit and all input/fault gates. Never overlap synthetic controls with user input. Stop and restore all owned resources on every exit. Return raw artifact paths, exact observations, limitations and explicit cleanup status. Check new pause/cancel instructions before each mutation; never continue an older launch approval after a newer pause.

Lead responsibilities: provide a coherent source-ready candidate, ensure no concurrent source writer during builds, answer escalations, and consume the runtime report before deciding the next source change. A child failure is not evidence that its processes or deployment were cleaned up.

## Source map

- `source/Missions/Battles/NavalLabCommands.cs`: exact command arguments and side restrictions.
- `NavalLabCoordinator.cs`, `.Native.cs`, `.HelmOccupancy.cs`: routing, deployment/station/occupancy confirmations.
- `NavalLabController.cs`, `.Native.cs`, `.FactoryProbe.cs`, `.HullInterpolation.cs`: lifetime, authority, frame application and movement gates.
- `source/Missions.Naval/NavalLabBehavior.NativeHelm.cs`, `.HelmOccupancy.cs`, `.TwoClient.cs`: native auto seating, remote use and target refresh.
- `source/Missions/Agents/Handlers/AgentMovementHandler.NavalStation.cs` and `source/Missions/Agents/Packets/AgentData.cs`: suppression and DEBUG revision fence.
- `doc/ShipControlsAndInteractions.md`: installed vanilla prerequisites; historical lab-status sections are not current acceptance.
- `doc/WarSailsTwoClientNative.md`, `doc/WarSailsBattleFoundation.md`, `doc/WarSailsE2ETestMatrix.md`: design and managed-harness context, not proof that native controls passed.
