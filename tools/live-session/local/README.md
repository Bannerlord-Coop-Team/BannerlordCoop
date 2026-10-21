# Local session lifecycle

The shared copies live in `C:\Users\Andrew\CodexWindows\coop-live-orchestration`.
Check `bench_lock.sh show` before installing changes, back up each shared file,
and never edit scripts during another owner's run.

Clients launch with deferred join. `wait_ready.sh` joins both clients and waits
for campaign readiness. For clean leave, use MCP `disconnect_client`, wait for
`readyToJoin`, then `reconnect_client` and wait for `readyForCampaignTests`.
The Local kit exposes the same operations through `live_test_client.ps1`
actions `Disconnect` and `Reconnect`, with the process id and expected run token.

For an abrupt client loss, terminate that client's process and run
`client_relaunch.sh /mnt/c/Users/Andrew/CodexWindows/coop-live-orchestration/4013/p5b testclient2`.
Then run `wait_ready.sh` with the same run token. Verify the player and hero ids
with `coop.debug.players.list` before and after. Abrupt replacement still needs
its own live proof; clean reconnect and save restart have passed.

Save on the server with `coop.debug.save.save_as p5save`. The name must be fresh;
success reports the byte counts of both the native `.sav` and co-op `.json`.
Keep incomplete files for diagnosis and use a fresh name after a failed write.
Restart through the full cycle, with the save name in `LOCAL_SERVER_SAVE` or as
the seventh positional argument. The completed proof used:

```bash
LOCAL_SERVER_SAVE=p5save bash cycle.sh \
  /home/pwisorlowska/worktrees/BannerlordCoop/fix-live-test-bridge-session-save \
  9a7ce75f5 4013 \
  /mnt/c/Users/Andrew/CodexWindows/coop-live-orchestration/4013/p5b \
  - rotate
bash wait_ready.sh issue4013-20261021T070013
```

Use the token printed by the cycle for a new run. Both clients must reach
campaign readiness; compare `coop.debug.players.list` and an explicit saved
state marker on the server and both clients. This proof preserved
`testclient2=Hero_Player`, `testclient=Hero_Player2863`, and
`coop.debug.hero.gold_state Hero_Player` returned `401305` on all three.
Always finish with `local_down.sh`, check `verifiedAbsent=true`, and release the bench.

## Dedicated server diagnostics

DEBUG dedicated live runs write the mod's Serilog events directly to a sibling
of the bridge-reported `logPath`, replacing `.log` with `.coop.log`. The launcher
log remains separate. This works even when native startup detaches stdout and
the dedicated engine does not load `CoopMod.SetupLogging`. Debug messages and
exceptions are included; the file is bounded at 30 MiB. Ordinary clients and
non-live runs do not acquire this additional sink.

## Remote

MCP lifecycle actions use the same DEBUG bridge. Remote scripts can issue the
corresponding `disconnect` and `reconnect` pipe operations and inspect readiness.
The Local cycle helper is not a Remote launcher. For a Remote restart, use
`start_run` with `save_name` after stopping and cleaning the old run, and use the
same client platform ids. Remote save/restart has not been live-proven here.
