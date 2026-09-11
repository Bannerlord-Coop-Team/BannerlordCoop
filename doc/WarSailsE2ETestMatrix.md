# Naval lab headless integration matrix

`NavalMissionTestEnvironment` extends the existing `MissionTestEnvironment`. It uses production DI-resolved lab coordinators/controllers, membership/election handlers, registries, measurements and campaign guards. The existing campaign and mission routers serialize messages; both run naval receives as poll-thread work and recipient game-thread queues are pumped explicitly. Land fixtures retain their historical default delivery mode.

Only the optional native adapter/loader and native save driver are replaced. Each client has its own `MockMission`, agent shells, frame storage and adapter call log. Readiness is the real controller's `AfterStart`, invoked explicitly instead of a native scene callback. There is no fixture campaign MapEvent, server party or alternate naval protocol. Test players provide connection identity only.

| Area | Automated checks |
| --- | --- |
| Stage 1 identity/readiness | Two serialized starts, distinct client mirrors, ten combatants/two ships, immutable registry owner/revision/movement identity, delayed second ready receipt prevents release, readiness order elects host, duplicate create/start does not reopen |
| Stage 1 controls | Helm goes to elected host even for the other owner's ship; walk/turn/jump/crew go only to original owner; retries do not apply twice; misrouting, changed authority and mismatched native agent reject |
| Stage 2 frame transport | Host-only emission/application isolation, independent frame copies, poll-thread receive before game-thread apply, stale incarnation/epoch/sequence rejection, blocked mesh and different mission isolation, sequence gaps |
| Stage 2 observations | Probe receipt on both clients; duplicate does not restart; receive/apply/next-callback observation separation; failed apply cannot become successful observation; superseded samples, 64-entry bound and eight-entry pagination via injected serialized frame traffic |
| Failure/cleanup | Native open failure rolls back controller/mesh and holds peer; stop cancels both probes and is idempotent; epoch-two host departure holds without adopting crew |
| Non-host departure regression | Epoch remains one, but remaining host disables adapter authority and stops frames across later ticks; pending observation cancels; already received frame rejects if membership changes before apply |
| Campaign isolation | Real casualty/result handlers set the blocker before campaign accounting; campaign tick sends hold to both clients; installed Game.Save patch reports failure without touching a strict native driver |
| Configuration | Debug guard asserts production handlers and naval scenario classes are present; Release guard asserts their expected absence. A passing Release guard is NOT naval scenario coverage; run naval scenarios with `-c Debug` |

The non-host departure regression exposed a production defect: cancelling controls did not gate authority/frame publication or queued frame application on original-owner readiness. The authorized fix is confined to `NavalLabController.OnMissionTick` and `ReceiveFrames`. It does not implement recovery or change land behavior.

## Run

Use the installed Windows toolchain from the repository root. No Docker, deployment, junction changes or live game is required.

```powershell
dotnet build source/E2E.Tests/E2E.Tests.csproj -c Debug --no-restore /p:ModName= /p:PostBuildEvent= /nr:false /m:1
dotnet test source/E2E.Tests/E2E.Tests.csproj -c Debug --no-build --no-restore /p:ModName= /p:PostBuildEvent= /nr:false /m:1 --filter "FullyQualifiedName~NavalLab"
```

The project automatically uses `e2e.runsettings` (Harmony JIT call-counting setting). Restore packages first when needed. This run needed a supervisor-approved restore-only `-p:NuGetAudit=false` for existing Scriban advisories; that is not vulnerability remediation.

## Not proved

These tests are not Stage 1/2 exit acceptance or a native L1 pass. They do not prove water/rigid-body simulation, collidable follower decks, support-local locomotion, real player-hero input, native AI orders, one-second helm deadman timing, 30-second host sample cadence, bridge retirement, admission/withdrawal, snapshots, recovery, direct/forced-relay transport or native teardown. The bounded-history case injects frame messages; it is not a 30-second real-time run. Existing naval unit tests cover elapsed-window bookkeeping separately. Save coverage is the installed disk-save denial patch, not native save/reload or session-sidecar persistence.

Remaining physical and unfinished transaction gates stay in `WarSailsBattleFoundation.md`. Parent owns native runs and independent candidate review.

## Explicit factory-authority diagnostic

`NavalLabFactoryAuthorityProbeTests` adds 18 E2E cases using the same real DI/election/serialized queues. Empty scene readiness is separate from complete hydration; both election orders, delayed hydration receipts, pre-hydration queued frame rejection, factory exceptions/incomplete actors, departure during factory and frame queues, remote-owner pulses versus host helm, authority mismatch, paired observations/stale frames, callback fault, bounded deadlines and isolation/stop are covered. Fourteen Coop.Tests native-boundary cases cover complete-return prerequisites, retained host/disabled follower, no enable calls, partial-body refusal, original initialization exception identity, conservative pre-completion callback rejection, active/force counters and unchanged old-mode observers. Native engine calls are substituted, not a physics model. Existing old-mode suites must also pass.

See [FactoryAuthorityProbe](WarSailsFactoryAuthorityProbe.md) for the real-process gate. Zero observed callback counters are not proof of no native integration in the unobserved prefab interval, collidable follower support or a parallel barrier. No native multiplayer UI input or physics acceptance is claimed.

## Explicit single-client mode

`NavalLabSingleClientTests` uses the same environment with `numClients=1`, serialized membership/election and registries. Thirteen cases cover one-owner readiness, one-hull/five-agent start, no server adapter/follower frames, exactly-once deployment request, scripted-control refusal on server and recipient, pre/post-deployment stop, native-boundary open/deployment failure, authority/actor mismatch and campaign-write hold. The optional adapter remains mocked; these tests do not prove native completion or physics. Seven new Coop.Tests cases cover manifest separation, real Mission callback dispatch, native input guard refusals, terminal hold and installed Harmony binding. The callback test has an empty agent list, not a native ship/crew mission.

Coverage: 144 naval unit cases, 41 naval E2E cases (28 existing plus 13 single-client), and 62 normal battle neighbors. Runtime protocol and limitations are at the top of WarSailsBattleFoundation.md. Two-client native follower acceptance remains separately blocked, not replaced by a one-client motion result.

Six shutdown regressions bind the actual single-mode pre-removal hook, invalidate the cached hull WeakGameEntity at the simulated engine-removal boundary, then run the real controller OnLeaving/Dispose and adapter finalization path. Later holds never disable the retired identity again. Foreign mission, both old modes and original removal exception identity, including a failed hold, are covered. The native removal body is substituted at the engine boundary; this is not proof of real native retirement.
