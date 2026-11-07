# Battle benchmark (#4483)

A repeatable in-game measurement for battle performance changes. Every battle performance PR reports before and after
numbers from it, and a change whose target metric stays within run-to-run noise gets closed instead of merged.

## Measuring a window

Run on every process that should report, normally the dedicated server and both clients, right after the battle is
staged:

```text
coop.debug.benchmark.start field-A-01 120 C:\bench\run01 field
```

Arguments: a run id (letters, digits, dots, dashes, underscores), the window in seconds (1 to 900), an optional absolute
output folder (default `CoopBenchmark` next to the game executable) and an optional label. The process writes
`<folder>/<run id>/<role>-<controller id>-<pid>.json` when the window ends. The role is `server`, `battle-host` or
`client`.

- `coop.debug.benchmark.status` reports whether a window is recording and the last report path.
- `coop.debug.benchmark.stop` ends the window early and writes the report.
- `coop.debug.benchmark.camera [height] [distance]` (client) fixes the mission camera beside the battle: level with
  the midpoint between the two sides' average positions, 80 m to the side and 50 m up by default, looking at the
  midpoint. Run it after the charge order so render cost is comparable between runs.

These commands are in every build, Release included. The scenario staging below uses DEBUG-only fixture commands.

When no window is recording the instrumentation does nothing measurable: the timed call sites in
`CoopMissionController` and the traffic counters in `LiteNetP2PClient` and `MessagePacketHandler` only read a flag, and
`GameThread.Update` only checks its `FrameObserver` for null.

## What a report holds

| Section | Contents |
| --- | --- |
| `Frames` | Frame count, FPS, average, p50, p95, p99 and max frame time, measured between `GameThread` drains (one per application tick). |
| `Sections` | Calls, total ms, ms per second, average and max per call for `MovementSend` (`AgentMovementHandler.PollMovement`), `MovementApply` (the interpolator tick that moves received puppets every frame), `ActionPoll` (`AgentActionHandler.PollActionsAfterNativeTick`) and `GameThreadDrain`. Per-packet `ApplyMovement` runs inside the drain. |
| `Garbage` | Gen0, gen1 and gen2 collections, allocated bytes and bytes per second over the window. |
| `Cpu` | Process CPU seconds and percent of one core. |
| `Memory` | Working set at start and end, peak working set since the process started, private and managed heap at the end. |
| `Traffic`, `Routes` | Per peer and in total, sent and received: movement datagrams and bytes, reliable messages (logical sends, before batching) and bytes, reliable packets (what went to LiteNetLib: a batch or a reliable packet) and bytes, each also per second. Sent traffic also has `ReliableDatagrams` (what LiteNetLib puts on the wire: one per packet up to its single-packet size, 1,020 B at the 1,024 B MTU neither connection raises, otherwise `ceil(bytes / (size - 6))` fragments), `OversizedReliablePackets` (packets that fragmented), and reliable bytes per second split into `Direct` (mission peer) and `Relayed` (the serialized relay packet on the campaign connection). Received reliable packets arrive reassembled, so they have no datagram count. |
| `ReceivedMessages` | Every message received, by type, and the total per second. On the dedicated server this is the battle traffic it receives. |
| `MissionStart`, `MissionEnd` | In mission, battle host, active agents, agents this process owns, scene. |
| `Process`, `Window` | Role, controller id, build configuration, version (with commit), runtime, window start, end and length. |

## Comparing runs

`tools/BattleBenchmark` reads run sets: a folder with one subfolder per run, each holding that run's reports.

```text
dotnet run --project tools/BattleBenchmark -- compare <baseline-runs> <candidate-runs> [--all] [--filter <regex>] [--out compare.md]
dotnet run --project tools/BattleBenchmark -- summary <runs> [--all] [--filter <regex>]
```

`compare` prints one Markdown table per process role: each metric's baseline and candidate mean ± standard deviation
[min, max], the delta and the delta in percent. The verdict is `separated` only when every candidate run lies above,
or every one below, every baseline run; otherwise the change is `within noise`. With fewer than 3 runs on either side
it says `too few runs`. Without `--all` it shows the headline metrics only. `summary` prints one set's mean and spread,
for posting baseline numbers.

## Scenarios

Both use a dedicated server and two clients (`testclient` hosts the battle, `testclient2` joins), started from the same
save every run. The local runner that stages them, builds both sides and alternates A/B runs is documented with it
(`coop-live-orchestration/perf-bench/README.md` on the bench machine); any harness that can send console commands can
run the same steps.

Field battle, about 1,000 agents (`coop.debug.mobile_party.list` gives the bandit id for the save):

```text
server:  coop.debug.set_time_mode Pause force-live-test
server:  coop.debug.map_event.bandit_attack_fixture_prepare testclient <bandit id>
server:  coop.debug.mobile_party.large_battle_roster_begin <bandit id> testclient 450
server:  coop.debug.map_event.late_join_mode_fixture testclient testclient2
server:  coop.debug.map_event.late_join_mode_join, then late_join_mode_enter once testclient is in the mission
clients: coop.debug.map_event.late_join_mode_begin_field_battle
clients: coop.debug.battle.charge_owned_formations
clients: coop.debug.benchmark.camera
all:     coop.debug.benchmark.start <run id> 120 <folder> field   (10 s after the charge)
```

The host owns the bandit's and its own troops, about 95% of the agents, like the #3252 runs. Dying is off for the
whole battle, so the load stays steady through the window.

Siege of Danustica (`town_ES1`, garrison id from `coop.debug.town.info Danustica`): the garrison gets 450
`imperial_archer` and testclient 450 `imperial_recruit` through `large_battle_roster_begin <garrison id> testclient 450
imperial_archer imperial_recruit`; `coop.debug.siege.start town_ES1 <lord party id>` starts an AI siege, `join_players
town_ES1 2 declare-missing-wars` adds both players to the attackers, `prepare_ladders_only town_ES1` and `assault
town_ES1` start the assault, and each client runs `join_active_assault town_ES1` and `coop.debug.map_event.start_attack_mission`.

## Release and DEBUG

The live-test bench builds `DebugAutoConnect`, which compiles the mod's projects as `Debug`. Release numbers move the
same way for these reasons:

- `Common`, `GameInterface`, `Missions` and `Coop.Core` build with `Optimize=false` in Release too, so the battle code
  is the same IL and gets the same unoptimized JIT in both configurations. Only `Coop` (the module entry point) is
  optimized in Release.
- What differs is the `#if DEBUG` code and the log level. On the measured paths the DEBUG code is diagnostics that are
  off unless a debug command turns them on: `MissionActionDiagnostics` (`performanceEnabled` and `currentPoll` checks),
  the synthetic receive pressure in `AgentMovementHandler`, and `SiegeInteractionDebugBehavior` with its observation
  patches, which add a lookup per arrow shot and per siege-engine use in sieges. These are fixed costs that don't
  depend on the change being measured, so they shift both sides of an A/B by the same amount.
- DEBUG logs at Debug level. The runner keeps each run's client logs; the number of `DBG` lines inside the window is in
  the baseline post on #4483.

## Memory

A dedicated server and two DEBUG clients in a 1,015-agent field battle (the host owning about 962), sampled every 5 s:

| | Working set | Private |
| --- | --- | --- |
| battle host client | 4.4 GB | 7.2 GB |
| other client | 4.3 GB | 7.0 GB |
| dedicated server | 1.1 GB | 1.4 GB |

Together they took about 16 GB of available memory, so start a run with at least 20 GB available.
