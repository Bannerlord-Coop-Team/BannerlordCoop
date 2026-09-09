# Repo-local MCP setup (Windows, Pi and Codex)

## First-time setup

Run **`runmefirst.cmd`**. It keeps the existing game-path workflow: launch Bannerlord when prompted, confirm its installation, create the `mb2` junction and user `COOP_LOG` setting, then acknowledge the pause. Only after that succeeds, it publishes the standalone `tools/CoopMcpServer` project and creates `.mcp-local/profiles.json` if missing. Script paths are anchored to this checkout, even when called from another directory. A rejected path or setup error returns nonzero and prevents the next step.

The game running during path discovery is expected. **MCP setup never closes it or touches the live module.** It does not build/deploy the mod, launch games, change saves/game configs, install Pi/Codex, edit global agent settings, or grant project trust. Only the original game-path step changes the user `COOP_LOG` environment variable. Publishing requires the Windows .NET 10 SDK (`dotnet.exe` on PATH); package restore may need network access. Windows PowerShell is also required.

Existing developers with an `mb2` junction can refresh just MCP without the interactive game-path step:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\mcp\setup.ps1
```

If no junction exists, pass `-GameDirectory` with the absolute Bannerlord installation directory. This explicit local override creates no junction and changes no environment variables. `-DotnetPath` accepts an existing SDK executable if not on PATH. `-RuntimeDirectory` is an optional isolated publish/profile destination for checks; normal agent launchers always use this checkout's `.mcp-local`, not that override.

Local layout (all gitignored):

- `.mcp-local/server/`: framework-dependent Release MCP executable and dependencies, not a Release mod build.
- `.mcp-local/profiles.json`: absolute game executable and artifact paths, profile `local`, default modules and four client platform IDs. Review module selection before live testing.
- `.mcp-local/runs/`: artifacts created by later, explicitly authorized runs.

Setup republishes the tool but **preserves existing profile bytes**, including manual edits, even if the junction has since changed. To change installations, edit the local profile. Do not republish while an MCP session is connected; explicitly stop its runs and exit the client first. Setup fails clearly if publishing fails; rerun the helper after fixing the SDK/path error. No local settings or machine paths belong in commits.

## Pi

An existing Pi installation needs [pi-mcp-adapter](https://github.com/nicobailon/pi-mcp-adapter#install). If missing, the developer can opt into a **project-only** install from the repo root with `pi install -l npm:pi-mcp-adapter`, then restart Pi. This is not run by setup. Pi's own project trust/package approval remains the developer's decision. See [Pi packages](https://github.com/badlogic/pi-mono/blob/main/packages/coding-agent/docs/packages.md) and [settings](https://github.com/badlogic/pi-mono/blob/main/packages/coding-agent/docs/settings.md).

Start `pi` **at the repo root**. `.mcp.json` is discovered automatically; no `/mcp setup` import or global MCP entry is needed. From a subdirectory or another cwd, invoke this checkout's `tools\mcp\start-pi.cmd` by its relative/absolute path. The wrapper changes Pi's cwd to its own repo root, forwards quoted arguments and exit status, and does not search ancestors. It does not preserve the starting subdirectory as Pi's project context.

Plain `pi` in `source` does **not** discover the root `.mcp.json`. The adapter uses active-cwd `.mcp.json` and passes that cwd to stdio children; relative `-File tools/mcp/launch.ps1` resolves there, not beside an arbitrarily supplied config file. Passing only `--mcp-config` from a subdirectory does not fix the launcher path. Higher-precedence `.pi/mcp.json` overrides may disable/change this server; inspect `/mcp` if expected tools are missing.

The shared config requests `directTools: true`, `lifecycle: lazy-keep-alive` and a 360-second request timeout. On the first uncached session, connect once with `mcp({ connect: "bannerlord-coop" })` to initialize/list tools without launching a game. The adapter then hot-loads the seventeen direct tools. Its [README](https://github.com/nicobailon/pi-mcp-adapter#direct-tools) documents cache-first registration and the [lifecycle](https://github.com/nicobailon/pi-mcp-adapter#lifecycle-modes): lazy-keep-alive avoids idle shutdown after connecting. Use direct/proxy calls for 300-second waits, not `mcpScript` with its default 30-second script deadline.

## Codex

Start `codex` **at the repo root**, or invoke this checkout's `tools\mcp\start-codex.cmd` from a subdirectory. The wrapper changes cwd to this repo root and forwards arguments/status without ancestor discovery. Do not pass `--cd`/`-C` to change away from that root; the configured stdio `-File` argument is cwd-relative. Subdirectory **plain `codex` is not supported by this configuration**, even if it discovers the parent project config.

`.codex/config.toml` defines the same PowerShell launcher with 30-second startup and 360-second tool timeouts. Codex loads project configuration only after **the user trusts the project**. Neither setup nor the config auto-trusts it, changes sandbox/approval policy, or adds a global server. After deciding trust, use Codex `/mcp` to inspect the server/tools. Startup/initialize/tools-list alone do not launch a game.

Path references checked against official documentation:

- [Basic config](https://developers.openai.com/codex/config-basic): trusted project layers load from project root to active cwd, nearest layer wins.
- [Advanced config](https://developers.openai.com/codex/config-advanced): generally describes project relative paths as based at the containing `.codex` folder.
- [Config reference](https://developers.openai.com/codex/config-reference) and [MCP](https://developers.openai.com/codex/mcp): stdio command/args/cwd and timeout fields.

Do **not** infer that MCP `cwd = ".."` is safe from the general relative-path wording. Current upstream [MCP types](https://github.com/openai/codex/blob/main/codex-rs/config/src/mcp_types.rs) uses `LegacyAppPathString` for `cwd`; its [transparent string deserializer](https://github.com/openai/codex/blob/main/codex-rs/utils/path-uri/src/api_path_string.rs) does not use the [loader's](https://github.com/openai/codex/blob/main/codex-rs/config/src/loader/mod.rs) `AbsolutePathBuf` resolution guard. This config deliberately omits `cwd` and relies on explicit root startup instead of an unverified relative base. Actual Codex CLI integration still requires a machine with Codex installed; schema validation is not an end-to-end Codex test.

## Agent operation and cleanup

The launcher only starts the already-published MCP process with its local profile. It never auto-builds, installs, deploys or launches Bannerlord. Diagnostics go to stderr; stdout is JSON-RPC only. Missing setup fails with the `runmefirst.cmd`/helper instructions, not a fallback to another checkout.

After initialization, agents can directly use all seventeen tools without developer-managed file IPC. **Do not call `start_run` until a matching DEBUG mod deployment and a live test are separately authorized.** See the [server workflow](../../tools/CoopMcpServer/README.md#agent-workflow) and [UI automation limits](mcp-ui.md). One local MCP session/co-op host at a time; do not run Pi and Codex against the game ports concurrently.

Always call **`stop_run`**, confirm cleanup succeeded, and only then exit/reload/reconnect Pi or Codex. Lazy-keep-alive retains owned runs across idle, not across client exit or forced process termination. Restarted sessions cannot adopt old runs. Never blindly retry uncertain mutations; inspect state/logs first. Setup-only checks prove no live UI/autoconnect/runtime behavior.

## Checks without launching a game

```powershell
dotnet test tools\CoopMcpServer.Tests\CoopMcpServer.Tests.csproj -c Release
```

`McpSetupTests` copies scripts into temporary paths containing spaces. It stubs SDK publishing and the interactive game-path step, checks real temporary junction resolution, idempotence/profile preservation, failure propagation, wrappers' cwd/quoted arguments/status, and missing-setup stderr. It also uses the real official C# MCP SDK 1.4.1 through the shared launcher to initialize/list seventeen tools with **empty profiles**, then checks EOF/stdout cleanliness. It never invokes the real interactive `runmefirst` workflow or changes the user environment.

Static client config checks use an existing adapter installation (its TOML parser and Ajv) plus the downloaded [official Codex schema](https://developers.openai.com/codex/config-schema.json):

```powershell
node tools\mcp\validate-config.mjs "$env:USERPROFILE\.pi\agent\npm\node_modules\pi-mcp-adapter" "$env:TEMP\codex-config-schema.json"
```

The checker isolates the adapter parser from user overrides, reads configs without starting agents, validates positive/negative schema cases, and checks identical launch argv, timeouts and discovery semantics. It does not install dependencies. Full Pi direct-tool registration/idle retention, actual Codex startup/trust, real first-time interactive setup, and server/client UI/autoconnect tests are separate manual/runtime gates. No live deployment or game launch is part of these checks.
