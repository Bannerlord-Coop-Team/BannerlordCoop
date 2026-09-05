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

1. `start_run`: `{"profile":"local","client_count":2}`. `state: "started"` means processes were launched, **not** that the server is ready or clients are connected. A partially failed launch is cleaned up and reported with its run/artifact identity.
2. `wait_for_state` on `server`, `state: "readyForCampaignTests"`, `timeout_seconds: 300`. Check `reached`. A timeout does not end the run; use `get_run` for `activeState`, `coopState`, queue depth, registry readiness, loaded assembly identities, and errors. `processAlive` alone is never readiness.
3. For each client, `wait_for_state` with `state: "readyToJoin"`, `timeout_seconds: 300`, then call `join_client` once. Joining is explicit so clients do not race server initialization. Check the response's `ok` and `result.started`.
4. Wait for each client `readyForCampaignTests`. The server's `connectedPlayerCount` and `connectedControllerIds` provide additional join diagnostics. A join acknowledgment is not a completed connection.
5. Call `list_commands` for each instance after `commandRegistryReady`. It includes **all registered framework commands**, such as `coop.unstuck`, not only debug commands. Legacy `coop.debug.*` commands remain supported. Unregistered `coop.*` names and arbitrary vanilla console commands are not exposed. Catalogs before session initialization can contain only legacy commands.
6. Execute commands with a string-array `arguments`, without shell quoting. For a read-only Danustica inspection, use `name: "coop.debug.location.list_characters"`, `arguments: ["Location_town_ES1_lordshall"]` on a client after checking its catalog. `coop.debug.town.list_towns` with `[]` lists towns, including Danustica (`town_ES1`, town object `town_comp_ES1`, Southern Empire). Run authoritative state-changing cheats on `server` only; inspect the replicated result on clients. Existing command role and authority checks still apply.
7. `read_logs`: use `max_bytes: 16384`, initially omit `cursor`. Pass the returned opaque `cursor` on subsequent reads of the same instance. Output is limited to 4..65536 UTF-8 bytes per call and preserves split UTF-8 characters. `hasMore` means the current read limit left more data to read. An incomplete UTF-8 tail returns `hasMore: false` without consuming it; keep the cursor and poll again for later appends. `reset: true` restarts from the beginning when replacement, truncation or observed compaction invalidates the cursor. The reader checks creation time, a bounded prefix including the sink's compaction marker, and the cursor boundary. A concurrent rewrite can return an error; retry this **read** with the same cursor. This is not a lossless log subscription: the game sink can already have removed old middle entries before polling.
8. Optional `screenshot`, then `screenshot_status` with the returned `captureId` as `capture_id`. Wait for `result.complete`; the initial request is not a finished image. Unique BMP paths are allocated inside the run's artifact directory. MCP returns the path, not inline image data.
9. Always `stop_run` before closing the MCP client. It requests bridge shutdown, allows bounded grace, then kills only still-alive process handles created by this session. No process-name scans, PID adoption, process-tree kills, or cleanup of unrelated games. Cleanup failure is reported as `cleanup_failed`; inspect errors and retry `stop_run`. Starting another run is blocked until cleanup completes.

`wait_for_state` accepts `controlReady`, `readyToJoin`, `commandRegistryReady`, `readyForCampaignTests`, `readyForMissionTests`, and `exited`, with a 1..300-second budget. `get_run` probes instances in parallel with a three-second status budget per instance. Command calls have a 35-second pipe budget; calls on one instance are serialized. Waiting for another command's instance gate can add latency to other tools, while `wait_for_state` includes gate waiting in its deadline. Configure the MCP client's tool timeout above the requested wait budget.

### Failures and artifacts

- Each endpoint registration and reply must match the owned PID, UTC process start time, role, platform ID and run token. Reply request IDs must match too. A stale registration or mismatched bridge version/identity is not adopted. Both protocol peers must include the new `processStartedUtc` envelope field; older DEBUG deployments will not work with this tool.
- Inspect `ok`, `error`, `error.outcomeUncertain`, and command `result.output`. Framework output can report an argument/role failure even when the bridge successfully dispatched the command. Structured `LIVE_TEST_JSON` output is preserved in the bridge result.
- **Never blindly retry a mutation with `outcomeUncertain: true`.** A game-thread timeout, lost reply, or cancellation after sending can leave the operation queued or applied. There is no automatic mutation retry, including joins and screenshots. Inspect state/logs before deciding what to do next.
- Each run retains `run.json`, per-request response JSON, requested screenshots and copies of endpoint-reported logs at stop. The actual path comes from a validated status response, not guessed `Coop_client.log` names. If the process dies before any successful status, no log path is available and no guessed log is copied. Passing a live-file cursor after stop can reset because the archive path changed.
- Completed runs remain accessible in the same MCP session. Restarted MCP sessions do not adopt old runs or processes; inspect retained files directly. Normal stdin EOF/host shutdown attempts owned-process cleanup, but force-killing the MCP process or a short client shutdown timeout can interrupt it. Always explicitly stop runs first. A machine crash cannot guarantee artifact archival or orphan cleanup. Preserve the recorded run manifest for manual inspection in that case.
- V1 uses the game's existing auto-start and connection configuration. It does not select saves, change ports/passwords, bypass startup errors, run builds/deployments, or coordinate background joins.

## Checks without launching Bannerlord

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tools\CoopMcpServer.Tests\CoopMcpServer.Tests.csproj
```

The suite uses fake launchers/processes, temporary log files and local test pipes. The stdio tests start **only this MCP executable**, initialize an official SDK client, list all ten tools, check an unknown-run error, and verify stdin EOF exits cleanly. Source-linked dispatcher tests use game-thread/vanilla-registry fakes; the existing `source/GameInterface.Tests/Services/LiveTesting/LiveTestCommandDispatcherTests.cs` covers the real referenced game assembly seam too.

For a compile-only game check, clearing `PostBuildEvent` alone is insufficient: current `Deploy.targets` also runs after Build. Set the **global** `ModName` property empty as well, which disables its deployment condition:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' source\Coop\Coop.csproj -t:Build -p:Configuration=Debug -p:Platform=AnyCPU -p:PostBuildEvent= -p:ModName= -p:NuGetAudit=false
```

A real server/two-client run, command replication and screenshot completion still require a separately authorized live validation using the workflow above.
