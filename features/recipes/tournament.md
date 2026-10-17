# Danustica tournament lobby and choices

Status: draft, source-inspected only; live exercise blocked on the prerequisites recorded in
[runtime](runtime.md) and [authoring evidence](../evidence/authoring.md).
Sources: [TournamentDebugCommand](../../source/GameInterface/Services/Tournaments/Commands/TournamentDebugCommand.cs),
[tournament services](../../source/GameInterface/Services/Tournaments) and the installed
`TournamentCampaignBehavior` registration.

Follow the shared runtime procedure. Use the isolated campaign, Danustica (`town_ES1` /
`town_comp_ES1`), a server and two campaign-ready clients. Confirm every command below in each
intended instance's `list_commands`. No save-specific ID is typed into these Danustica commands.

Setup: server `execute_command`, name `coop.debug.tournaments.danustica_fixture_begin`,
`arguments:[]`. It captures the existing town tournament, refuses an open co-op session, and
tracks the tournament it creates. Require success and retain `danustica_fixture_state` from
server/client1/client2 before player input. Do not begin a second fixture after uncertain success.

Drive the real controller request path with `execute_command`:

1. Client1 `coop.debug.tournaments.danustica_request_join`, `[]`.
   Observe `coop.debug.tournaments.danustica_observe`, `[]` on all peers until one shared session
   is in `Preparation`. A request receipt alone does not satisfy this assertion.
2. Client2 runs the same join. Retain the shared session/revision and both participant IDs.
   The two IDs must be distinct; participant membership must agree after authoritative application.
3. From the observation, select the client whose local player is the actual controller. That
   client runs `coop.debug.tournaments.danustica_request_start`, `[]` once.
   Require a current `AwaitingChoices` phase and nonempty current match ID before choosing.
4. Client1 runs `coop.debug.tournaments.danustica_request_choice`, `["Join"]`;
   client2 runs the same command with `["Watch"]`. Retain every response and fresh observation.
   Each command sends its locally observed session/revision; stale rejection is evidence to
   inspect, not a reason to blindly replay.

Oracle: one server session, matching session/match/phase/revision after updates settle, distinct
correct players, one current controller, and recorded choices matching the accepted actions.
If a mission starts, verify client1's participation and client2's spectator context through the
reported mission state and actual rendered screenshots. Do not claim match combat, victory or
reward correctness from only a choice/request acknowledgment.

For native UI entry coverage, independently inspect actual UI layers/elements and drive the
currently visible control using fresh `ui_inspect` snapshot/element IDs. This draft has not
validated the visible entry path or guessed widget IDs. Controller debug requests prove only
that request path even if the same service is used by the UI.

Safe negative slice on an otherwise owned run: request choice `["None"]`; source validation
rejects it before submission. Require structured command failure and unchanged session state.
Only perform it when the command is confirmed present and the observation window is stable.

Cleanup: both clients run `coop.debug.tournaments.danustica_request_leave`, `[]` where applicable.
Observe session completion/removal. Server runs `coop.debug.tournaments.danustica_fixture_restore`,
`[]`; it refuses an open session. If normal cleanup fails, inspect first. The server-only
`danustica_fixture_abort` is eligible only for a session owned by this fixture and a tournament
the fixture created; retain the result, then restore. Do not abort a pre-existing tournament.
Finally stop the owned run, confirm process-tree cleanup and readable artifacts.

Excluded: all tournament round permutations, betting, prize/renown/XP, authority migration,
disconnects, concurrent unrelated towns, whole-branch live acceptance and the entire battle system.
