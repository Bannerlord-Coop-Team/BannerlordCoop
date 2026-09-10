# CoopMcpServer (V1, Windows)

A standalone .NET 10 stdio MCP server using the official C# `ModelContextProtocol` SDK, pinned to 1.4.1. No Bannerlord DLLs are loaded into the MCP process. The shared V1 wire protocol is source-linked from `source/Common/LiveTesting/LiveTestProtocol.cs`.

V1 starts **an in-game `/server` process**, plus configurable deferred `/client` processes. The host does not play: anyone playing, including the host operator, uses a client. `IGameProcessLauncher` is the replacement seam for a future dedicated-server launcher; no dedicated-server implementation is included.

## Install

1. Install the Windows .NET 10 SDK (or runtime for published framework-dependent output).
2. Prepare a Bannerlord installation with a **matching DEBUG build** of Coop, Common, GameInterface and the other mod assemblies. The bridge is compiled out in Release. These tools do not build or deploy the mod, edit game configuration, or install game dependencies.
3. From the repository root, publish only the MCP executable:

   ```powershell
   & 'C:\Program Files\dotnet\dotnet.exe' publish tools\CoopMcpServer\CoopMcpServer.csproj -c Release -o C:\CoopMcpServer\Server
   Copy-Item tools\CoopMcpServer\profiles.example.json C:\CoopMcpServer\profiles.json
   ```

4. Edit `C:\CoopMcpServer\profiles.json` to select the existing `Bannerlord.exe` and modules for your installation. The working directory is the executable's directory. The example uses the standard Steam install location. `artifactDirectory` must be absolute. Selected platform IDs must be distinct (including the server), and contain only ASCII letters/digits, `-` or `_`. Add client IDs to allow more clients, up to 16. Zero clients is supported. Profiles are trusted local operator configuration; tools accept a profile name, never an executable, shell command or arbitrary launch arguments.
5. Add this to an MCP client's configuration:

   ```json
   {
     "mcpServers": {
       "bannerlord-coop": {
         "command": "C:\\CoopMcpServer\\Server\\CoopMcpServer.exe",
         "args": ["--config", "C:\\CoopMcpServer\\profiles.json"]
       }
     }
   }
   ```

Use one MCP server session and one co-op host at a time on the local game ports. The transport is stdio plus local Windows named pipes, not HTTP. The launcher explicitly opts each DEBUG game process in with `/autoconnect /cooptestrun` and a fresh run token. Clients additionally receive `/cooptestmanualjoin`. Ordinary game launches are unaffected. Use only trusted local MCP clients, since registered co-op commands can change the save/world. No new command-role bypass or `AllowedThread` scope is introduced.

## Agent workflow

Tool parameters below are JSON objects. After `start_run`, use the returned `runId` as `run_id` on subsequent calls. Instance names are `server`, `client1`, `client2`, etc.; never target by a discovered PID.

1. `preflight_run`: `{"profile":"local","client_count":0}` checks commit headroom and DEBUG bridge compatibility without launching. Then `start_run`: `{"profile":"local","client_count":0}` starts only the server. `state: "started"` means processes were launched, **not** that the server is ready or clients are connected. A partially failed launch is cleaned up and reported with its run/artifact identity.
2. `wait_for_state` on `server`, `state: "readyForCampaignTests"`, `timeout_seconds: 300`. Check `reached`. A timeout does not end the run; use `get_run` for `activeState`, `coopState`, queue depth, registry readiness, loaded assembly identities, and errors. `processAlive` alone is never readiness.
3. Add a client only when needed: `start_client` with `client_index: 1` (then 2, etc., within configured IDs and the cap of 16). Repeating an index reports `existing_running`, `existing_exited`, `launch_failed`, or `outcome_unknown`, never relaunches it. A preflight rejection does not reserve a slot. For each launched client, `wait_for_state` with `state: "readyToJoin"`, `timeout_seconds: 300`, then call `join_client` once. Joining is explicit so clients do not race server initialization. Check the response's `ok` and `result.started`.
4. Wait for each client `readyForCampaignTests`. The server's `connectedPlayerCount` and `connectedControllerIds` provide additional join diagnostics. A join acknowledgment is not a completed connection.
5. Call `list_commands` for each instance after `commandRegistryReady`. It includes **all registered framework commands**, such as `coop.unstuck`, not only debug commands. Legacy `coop.debug.*` commands remain supported. Unregistered `coop.*` names and arbitrary vanilla console commands are not exposed. Catalogs before session initialization can contain only legacy commands.
6. Execute commands with a string-array `arguments`, without shell quoting. For a read-only Danustica inspection, use `name: "coop.debug.location.list_characters"`, `arguments: ["Location_town_ES1_lordshall"]` on a client after checking its catalog. `coop.debug.town.list_towns` with `[]` lists towns, including Danustica (`town_ES1`, town object `town_comp_ES1`, Southern Empire). Run authoritative state-changing cheats on `server` only; inspect the replicated result on clients. Existing command role and authority checks still apply.
7. `read_logs`: use `max_bytes: 16384`, initially omit `cursor`. Pass the returned opaque `cursor` on subsequent reads of the same instance. Output is limited to 4..65536 UTF-8 bytes per call and preserves split UTF-8 characters. `hasMore` means the current read limit left more data to read. An incomplete UTF-8 tail returns `hasMore: false` without consuming it; keep the cursor and poll again for later appends. `reset: true` restarts from the beginning when replacement, truncation or observed compaction invalidates the cursor. The reader checks creation time, a bounded prefix including the sink's compaction marker, and the cursor boundary. A concurrent rewrite can return an error; retry this **read** with the same cursor. This is not a lossless log subscription: the game sink can already have removed old middle entries before polling.
8. Optional `options_menu` on a campaign-ready client: `action: "open"`, `tab: "VoiceTab"` opens the co-op options screen and selects Voice. Omit tab to open the first available tab. `inspect` reports available tab ids and the selected tab; `select` changes only the tab, and `close` discards pending settings without applying. Only its own topmost options screen is controlled. Servers, arbitrary UI clicks, Apply, and opening from missions are not supported. Then `capture_screenshot` with `timeout_seconds: 60` returns completed PNG image content and artifact metadata. It observes advancing engine frames and positive renderer FPS on an unchanged screen/state, then awaits fresh, stable BMP evidence across frames and checks the file hash before encoding. This is pixel/render evidence, not proof that the intended screen is visually correct, so inspect the image. Limits: 64 MiB BMP, 8192 per dimension, 16 million pixels, 8 MiB PNG. Legacy `screenshot` and `screenshot_status` remain asynchronous BMP APIs.
9. Always `stop_run` before closing the MCP client. It requests bridge shutdown, allows bounded grace, then terminates the owned Windows Job Object if needed. Each root is created suspended, assigned before resuming, and descendants cannot break away. Cleanup is complete only when the root and job descendants are gone, including watchdogs retained after root exit. No process-name scans, PID adoption, or cleanup of unrelated games. `processTreeAlive: false` and `cleanupComplete: true` confirm owned-tree cleanup; a null tree observation is unknown, never confirmation. Cleanup failure is reported as `cleanup_failed`; inspect errors and retry `stop_run`. Starting another run is blocked until cleanup completes.

`ui_inspect` and `ui_action` also support bounded, snapshot-scoped Gauntlet menu inspection and ordinary button/toggle/text/dropdown/slider/scroll input, including before joining a campaign. See [MCP Gauntlet menu automation](../../doc/automated-testing/mcp-ui.md) for limits, stale-reference handling and `/autoconnect` endpoint syntax. Never retry an uncertain mutation or use Apply during reversible menu checks.

`wait_for_state` accepts `controlReady`, `readyToJoin`, `commandRegistryReady`, `readyForCampaignTests`, `readyForMissionTests`, and `exited`, with a 1..300-second budget. `get_run` probes instances in parallel with a three-second status budget per instance. Command calls have a 35-second pipe budget; calls on one instance are serialized. Waiting for another command's instance gate can add latency to other tools, while `wait_for_state` includes gate waiting in its deadline. Configure the MCP client's tool timeout above the requested wait budget.

### Preflight and staged launch

`start_run` still accepts 0..16 initial clients for legacy callers. Prefer starting with zero and adding clients only when needed. Initial preflight accounts for all newly requested processes; staged preflight accounts for one additional process because current system commit already includes existing instances. Joins recheck reserve and the deployed/loaded bridge MVID. The DEBUG bridge must advertise protocol `1` and capability revision `staged-ui-capture-v1`; incompatible or replaced builds fail before an expensive launch/join. Build/deploy separately, with permission.

Windows `GetPerformanceInfo` supplies committed bytes and the commit limit, not free physical RAM. Each profile may set `estimatedCommitBytesPerProcess` (default 8589934592, 8 GiB) and `commitReserveBytes` (default 2147483648, 2 GiB). The estimate is conservative relative to observed roughly 6 GiB game-process peaks, not a universal requirement or memory allocation. Results expose counters, estimate, reserve and required headroom. Tune from measured workloads; other applications can still consume commit after preflight. Unknown/invalid counters fail clearly. No pagefile/configuration edits or application shutdowns are performed.

### Choose a startup save

Call `list_saves` with `{"profile":"local"}`. It discovers top-level `.sav` files in Bannerlord's Windows Documents save directory, respecting Windows Documents redirection. Pages hold 64 entries, with a 512-entry catalog cap; pass `nextOffset` until unchanged and inspect `truncated`. The listing contains basename, byte length, modification time and presence of the co-op `.json` session sidecar. No save content is parsed, copied, deleted or changed by these tools. A missing sidecar can mean saved player registrations will not be restored; it does not prevent loading a campaign.

If the catalog includes `MP`, use `start_run` with `{"profile":"local","client_count":0,"save_name":"MP"}`. Use the exact returned basename, without `.sav`; ordinary names containing spaces are supported as a single Windows argument. Paths, traversal and extensions are rejected. A missing, empty or unreadable selected save fails before launch, without default-save fallback. Omit `save_name` to preserve normal startup. The server alone receives `/coopsave`; clients still join normally. This is startup selection, not hotload, fixture management or a save-writing API. Normal game behavior, including autosaves, is not disabled.

`requestedSave` in the run and `requestedSaveName` in bridge status identify the requested selection. `campaignLoaded`, `readyForCampaignTests` and `loadedCampaignId` report actual campaign readiness/identity; `loadedSaveNameConfirmed: false` explicitly avoids claiming the filename was confirmed from a mere startup acknowledgment. Save compatibility/corruption and world contents still require inspecting the loaded campaign and logs.

### Operation diagnostics

`wait_for_state` separates `outcome` (`reached`, `process_exited`, `deadline_expired`) from `lastError`; an actual wait deadline does not erase the last bridge rejection. Transport errors distinguish endpoint registration failure, response identity mismatch, connect deadline, request deadline, caller cancellation and other transport failures. Bridge codes/messages/uncertainty remain unchanged. An artifact-write failure retains `result.bridgeResponse` rather than replacing its diagnostic evidence. UI stale/hidden/clipped/focus/context rejections remain the bridge's own errors, not guessed timeouts. Text-only feature commands with combined messages still cannot expose an exact reason unless that feature supplies structured diagnostics.

### Failures and artifacts

- Each endpoint registration and reply must match the owned PID, UTC process start time, role, platform ID and run token. Reply request IDs must match too. A stale registration or mismatched bridge version/identity is not adopted. Both protocol peers must include the new `processStartedUtc` envelope field; older DEBUG deployments will not work with this tool.
- Inspect `ok`, `error`, `error.outcomeUncertain`, and command `result.output`. Framework `result.succeeded` and `result.errorCode` preserve actual command failures, including argument and side rejections. Legacy text-only commands have no inferred success or rejection reason. Structured `LIVE_TEST_JSON` output is preserved in the bridge result.
- **Never blindly retry a mutation with `outcomeUncertain: true`.** A game-thread timeout, lost reply, or cancellation after sending can leave the operation queued or applied. There is no automatic mutation retry, including joins and screenshots. Inspect state/logs before deciding what to do next.
- Each run retains `run.json`, per-request response JSON, requested screenshots and copies of endpoint-reported logs at stop. The actual path comes from a validated status response, not guessed `Coop_client.log` names. If the process dies before any successful status, no log path is available and no guessed log is copied. Passing a live-file cursor after stop can reset because the archive path changed.
- Completed runs remain accessible in the same MCP session. Restarted MCP sessions do not adopt old runs or processes; inspect retained files directly. Normal stdin EOF/host shutdown attempts owned-process cleanup, but force-killing the MCP process or a short client shutdown timeout can interrupt it. Always explicitly stop runs first. A machine crash cannot guarantee artifact archival or orphan cleanup. Preserve the recorded run manifest for manual inspection in that case.
- V1 uses the game's existing auto-start and connection configuration. It does not change ports/passwords, bypass startup errors, run builds/deployments, hotload campaigns, or coordinate background joins.

## Checks without launching Bannerlord

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tools\CoopMcpServer.Tests\CoopMcpServer.Tests.csproj
```

The suite uses fake game launchers/processes, temporary files and local test pipes. Harmless PowerShell child/grandchild fixtures prove owned-job cleanup after root exit and unrelated-process survival. Official SDK stdio tests list all seventeen tools, check schemas/errors and EOF, and use a separate fake-bridge test host to verify real PNG image content and metadata. No Bannerlord process is launched. Source-linked dispatcher tests use game-thread/vanilla-registry fakes; the existing `source/GameInterface.Tests/Services/LiveTesting/LiveTestCommandDispatcherTests.cs` covers the real referenced game assembly seam too.

For a compile-only game check, clearing `PostBuildEvent` alone is insufficient: current `Deploy.targets` also runs after Build. Audit evaluated properties/imports first. Set **global** `ModName`, `PreBuildEvent`, and `PostBuildEvent` empty and all deployment destinations to absolute scratch paths:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' source\Coop\Coop.csproj -t:Build -p:Configuration=Debug -p:Platform=AnyCPU -p:PreBuildEvent= -p:PostBuildEvent= -p:ModName= -p:ModsRoot=C:\CoopMcpChecks\Modules -p:ModDir=C:\CoopMcpChecks\Modules\Coop -p:ModBinDir=C:\CoopMcpChecks\Modules\Coop\bin -p:ModPrefabDir=C:\CoopMcpChecks\Modules\Coop\prefabs -p:NuGetAudit=false
```

A real selected-save server/staged-client run, generic UI behavior, command replication, rendered screenshot completion and game-watchdog cleanup still require separately authorized live validation using the workflow above.
