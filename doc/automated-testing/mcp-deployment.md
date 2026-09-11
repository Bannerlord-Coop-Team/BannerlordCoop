# Explicit MCP mod deployment (Windows)

`deploy_mod` is separate from launch and requires separate operator permission. `start_run`, `preflight_run`, setup and MCP initialization **never build or deploy**. No live deployment or game launch is needed to run the fake tests below.

## Opt in a trusted local profile

Deployment is disabled when the profile's `deployment` property is omitted or null. To enable it, add this object to an existing launch profile in your **local** profiles file, adjusting the paths to installed tools and durable storage:

```json
"deployment": {
  "durableRoot": "C:\\CoopMcpServer\\Deployments",
  "msbuildPath": "C:\\Program Files\\Microsoft Visual Studio\\18\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe",
  "buildSpaceBytes": 4294967296,
  "diskReserveBytes": 1073741824
}
```

The server does not discover/install MSBuild or edit MCP settings. The root must be absolute, outside Windows/user Temp and separate from the source checkout and game installation. Deployment paths must not traverse junctions/symlinks; configure the real installation path, not its `mb2` alias. The checkout's read-only build-reference `mb2` junction remains supported. Do not put this durable root on disposable storage or delete it to resolve a failure.

Use one MCP session/host per machine as before. All deployment-enabled profiles/sessions for the same installation must use the same durable root. The shared root's exclusive file lease serializes deployment and `start_run`/`start_client` across those sessions. Within a server, the existing run lifecycle gate covers all profiles, including deployment-disabled profiles. An owned run must be stopped with confirmed cleanup before deployment, even if its root process exited.

The MCP account needs permission to execute the configured toolchain, write build outputs/package caches, read the installation, write the affected module files, and retain durable evidence. No privilege escalation, app shutdown, disk cleanup or pagefile adjustment is performed.

## Tool schema

```json
{
  "solution_path": "C:\\work\\BannerlordCoop\\source\\Coop.sln",
  "profile": "local",
  "configuration": "Debug"
}
```

`solution_path` and `profile` are required. Optional `configuration` defaults to `Release`; only the exact strings `Release` and `Debug` are accepted. Use `Debug` explicitly for live testing. There are no command, argument, target directory or script parameters. Configure the client's tool deadline for up to 20 minutes per build project plus backup/deployment time; the normal 360-second live-tool timeout may be too short. Caller cancellation stops the owned build tree or rolls back an apply; if the response is lost, inspect the durable manifest before retrying.

**Accepted build-output link risk:** existing `bin`/`obj` junctions or symlinks are not rejected before MSBuild runs. A build can write through them into the module before transaction backups exist; later output collection may reject the link, but `failed_before_apply` does not undo those build writes. The operator accepts this risk, not a rollback guarantee for linked build outputs.

Only the expected `source/Coop.sln` checkout layout and projects are accepted. The destination is derived from the configured `bin/Win64_Shipping_Client/Bannerlord.exe`; an existing `Modules/Native/SubModule.xml` and `Modules/Coop` are required. This updates an existing installation, not a full installer.

**Trusted-checkout boundary:** MSBuild executes repository project/import/task code and may restore packages. Path/layout checks and fixed arguments are not a sandbox. Only pass a checkout whose build code and dependencies the operator trusts. Global `ModName`, `PreBuildEvent` and `PostBuildEvent` are empty across referenced projects; `ModsRoot`, `ModDir`, `ModBinDir` and `ModPrefabDir` point into the deployment artifact's disabled-deploy scratch area. Neither `Deploy.targets` nor `deploy.ps1` is intentionally invoked for installation. Do not run another build or edit the checkout during deployment.

## Exact write set

The service rebuilds `Coop.csproj` and its reference graph in the selected configuration with Windows MSBuild, package restore enabled and node reuse disabled. If `source/Missions.Naval` exists, its solution entry/project and output are required. It is rebuilt last without rebuilding already-built shared references. No naval optin is created and no naval XML declaration is inferred.

The deployed assembly set is `Coop`, `Common`, `Coop.Core`, `GameInterface`, `Missions`, `Coop.Steam`, `Coop.CrashReporter.exe`, and `Missions.Naval` when present. SDK projects must use the supported `bin/Release/netstandard2.0` (or `bin/Debug/netstandard2.0` when selected) layout; other layouts fail rather than guessing. Shared project outputs must match their copies in the selected `Coop/bin/Release` or `Coop/bin/Debug` directory by length, hash and MVID. A fixed runtime dependency filename allowlist in `DeploymentPlan` selects only existing top-level DLLs from that selected directory; unrecognized non-game DLLs fail rather than silently producing a partial package. No recursive bin traversal, game DLLs, publicizer directories, Steamworks.NET (game-owned), PDBs or whole-engine trees are shipped. When dependency/layout changes require new files, update the allowlist deliberately and test it; this is not a generic packager.

`UIMovies/**/*.xml` files are mapped to `GUI/Prefabs`. The total set is capped at 2048 files. Stale/unlisted files are **not** deleted. Static launch scripts, `ModuleData`, DedicatedServer content, save/session data, runtime configs and optin files are outside the write/backup set. This is why a backup does not copy a massive existing DedicatedServer tree.

`SubModule.xml` stays byte-for-byte untouched by default. A valid existing XML declaring module `Coop` and `Coop.dll` is required. To replace it deliberately, configure an absolute `deployment.subModuleXml` pointing to a complete, already-rendered XML file. It must declare the same module/DLL and contain no template tokens. Its exact bytes are staged, the existing XML's original presence/attributes/bytes are backed up, and it participates in rollback like any other target. Omit this setting to preserve all existing declarations. No optin-edit API is provided.

## Evidence, disk budget and failure states

Each attempt creates `durableRoot/<deploymentId>/`, containing:

- `manifest.json`: selected `configuration`, absolute source/target/staged/backup paths, original existence and file attributes, original and replacement length/SHA-256/MVID evidence, attempted writes, created directories, state and unresolved restorations. MVID is empty for non-assemblies; new targets have `original: null`.
- `Coop-command.json`, `Coop-stdout.log`, `Coop-stderr.log` (and equivalent `Missions.Naval-*` files when applicable).
- `staged/`: only the exact replacement set, hash-verified after copying.
- `backup/`: a **fresh** verified copy of every existing target, not a reused snapshot or recursive module backup. Originally absent targets have no backup and are recorded for removal on rollback.

The response includes the artifact directory and the same structured report. Input/idle/initial disk/lease failures can be MCP errors before an attempt directory is created. Build and subsequent failures return a report with `build_cleanup_required`, `failed_before_apply`, `rolled_back`, or `rollback_failed`; success is `deployed`. A pre-crash manifest may show `preparing`, `backup_verified` or `applying`, none of which proves completion.

Before creating artifacts/building, disk checks require the configured build-space estimate plus reserve on checkout, durable, Temp and package-cache volumes. Before staging/backups/apply they require original bytes plus twice replacement bytes plus reserve on both destination and durable volumes, conservatively allowing shared-volume needs. Defaults are 4 GiB build space and 1 GiB reserve; build space is bounded to 1..100 GiB and reserve to 256 MiB..100 GiB. These are checks, not disk reservations; another application can still exhaust space. Partial build evidence is retained on failure, not automatically cleaned.

Existing unsupported special file attributes (compressed/encrypted/sparse etc.) fail before mutation rather than promising an inexact restoration. Read-only/hidden/system/archive and supported ordinary attributes are recorded and restored exactly. Before each write, the target's original existence, attributes and hash are rechecked. For Debug deployments, built Coop bridge metadata must pass the same protocol/capability requirements as launch preflight. Release deployments retain artifact/hash/MVID checks but do not require DEBUG bridge metadata. **A successful Release deployment is not live-test-launch compatible:** the bridge is compiled out, so `start_run` still refuses it. Deploy Debug explicitly before an authorized live test. Existing memory/bridge/loaded-MVID launch and join gates remain unchanged.

The read-only process check refuses any observed `Bannerlord*`, `DedicatedServer` or `ServerConsole` process and reports names/PIDs. It runs before build and again before/during apply. No process is killed/adopted by that check. Ordinary external launchers do not participate in the MCP lock; keep them closed throughout. If an external process appears during apply, rollback is attempted; locked files may require manual recovery. The tool cannot promise exclusion against arbitrary concurrent filesystem writers or a machine crash.

## Recovery

Builds are created suspended and assigned to a kill-on-close Windows Job Object before resuming, with no descendant breakaway. Caller cancellation and the 20-minute per-project deadline cover root exit and both output drains. Cleanup has a separate five-second deadline and verifies both root termination and an empty job, even after the root already exited. Final buffered-log flushing/disposal is included in that deadline; retries await the same finalization operations. A nonzero root exit immediately enters cleanup rather than waiting for descendants to close inherited output pipes.

`build_cleanup_required` means cleanup could not be confirmed. The MCP host retains the build handles, unfinished log operations and exclusive deployment lease, and blocks further starts/deployments. Keep that host open: retrying a start or deployment first retries bounded cleanup and proceeds only after confirmation. Do not delete `deployment.lock` or restart the host to bypass this state. The original manifest remains failure evidence even after a later retry recovers. Forced host termination follows the crash-recovery guidance below.

Before any game mutation, a flushed `durableRoot/recovery-required.json` points to the complete backup manifest. It blocks future configured starts/deployments until verified completion or successful rollback removes it. Never delete this marker merely to unblock a tool.

On ordinary exceptions/cancellation, rollback runs without the canceled token, restoring verified backup bytes and attributes in reverse order, deleting newly created targets, and removing only recorded empty new directories. Each unresolved target/directory is returned explicitly and retained in the manifest. If even evidence updates fail, the response reports that failure and the pre-apply marker remains available.

After a crash, forced MCP termination, or `rollback_failed`:

1. Stop all games/builds and inspect the marker's `artifactDirectory`, `manifest.json`, `error` and `unresolvedRestoration`. Retain the whole attempt directory.
2. For each attempted target that originally existed, verify its backup length/SHA-256/MVID against `original`, restore only that target, and restore `originalAttributes`. Do not recursively restore the entire module.
3. For attempted targets with `originallyExisted: false`, remove only the recorded new file. Remove recorded created directories only if empty. Treat an interrupted `applying` manifest as potentially written; write-ahead flags can be true before the copy began.
4. Verify restored bytes/attributes and original absence against the manifest. Resolve every failure, including special locks or newly appeared files, before removing the recovery marker manually. There is no automatic recovery/adoption API or unsafe retry override.

Manifest writes use a flushed pending file followed by rename. Keep `manifest.json.pending` too if storage failure interrupted evidence persistence. Build outputs in the source checkout/package caches are not rolled back. ACLs, alternate data streams, timestamps, whole-install snapshots and power-loss-proof storage guarantees are outside this file-byte/attribute transaction.

## Checks without Bannerlord

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tools\CoopMcpServer.Tests\CoopMcpServer.Tests.csproj --filter 'FullyQualifiedName~ModBuildServiceTests|FullyQualifiedName~WindowsJobBuildProcessRunnerTests|FullyQualifiedName~ModDeploymentServiceTests|FullyQualifiedName~DeploymentOutputMatchesSchema|FullyQualifiedName~DeploymentConfigurationMatchesInputAndOutputSchema|FullyQualifiedName~DeploymentRequiresStoppedOwnedRun|FullyQualifiedName~McpStdioTests|FullyQualifiedName~LaunchPreflightTests'
& 'C:\Program Files\dotnet\dotnet.exe' build tools\CoopMcpServer\CoopMcpServer.csproj -c Release
```

Tests use fake MSBuild/process/disk probes, temporary fixture installations outside OS Temp, real hashes/MVID readers and official MCP stdio schemas. They cover exact XML/default optin preservation, fresh backups, original attributes/absence, partial apply/cancellation rollback, corrupt backup refusal, unresolved rollback gating, disk/build/active-process refusal, owned-run/concurrent start/deploy locking, optional naval output, mixed-project/unknown-dependency refusal, fixed disabled-deploy arguments, Release default/explicit Debug propagation through build/output/manifest/schema, invalid configuration rejection, and Debug-only bridge validation. A harmless executable fixture proves root-exits-first inherited stdout/stderr cleanup on cancellation, timeout and nonzero exit; injected cleanup failure and delayed log finalization verify retained leases and start/deploy blocking until recovery. They never build/deploy the actual mod or launch Bannerlord. A real trusted checkout/toolchain deployment and subsequent game validation require separate authorization.
