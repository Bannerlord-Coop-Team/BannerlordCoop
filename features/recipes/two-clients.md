# Staged two-client admission

Status: draft, source-inspected only on the implementation named in [provenance](../provenance.md).
Blocked during authoring: the direct `bannerlord-coop` tools were absent; no matching deployed
Debug build, isolated save or owned run was admitted. No live launch was attempted.

Behavior: two players join the same authoritative campaign through explicit staged admission,
receive distinct player identities and observe the same world. This is not proof of every
connection/password/module/reconnect branch listed in the map.

Follow [runtime](runtime.md) for ownership, source/build checks, launch, doctor and cleanup.
Use one server, `client1` and `client2`. Require readiness and command registry for all three,
record loaded campaign identity and assemblies, and resolve Danustica through the real lookup:

| Tool | Instance | Name / arguments | Purpose |
| --- | --- | --- | --- |
| `execute_command` | server, client1, client2 | `coop.debug.town.list_towns`, `[]` | locate Danustica's town object `town_comp_ES1` |
| `execute_command` | server, client1, client2 | `coop.debug.town.info`, `["town_comp_ES1"]` | independent common world observation |
| `execute_command` | server, client1, client2 | `coop.debug.players.list`, `[]` | actual controller/hero/party/clan identities and resolution/control status |
| `get_run` | owned run | returned `run_id` | inspect exact run/instance/campaign/registry state |
| `capture_screenshot` | client1, client2 | returned `run_id`, `timeout_seconds:60` | inspect each rendered campaign |

The production actions are `join_client` on each ready client, not the read-only commands.
Before the second join, retain the first client's state; after joining, retain all peers' state.
Resolve distinct player hero/party identities through `coop.debug.players.list`, matching each
client's reported local controller ID to the registry rows. This command reports actual hero,
party and clan IDs, their object resolution and player-control membership. On the isolated
two-client campaign expect two registered players, host excluded, and both heroes/parties
resolvable and controlled. Record pre-existing saved/disconnected registrations separately
rather than silently replacing that expectation with a registry count. Unique process IDs
alone do not prove distinct player parties.

Oracle: both client joins succeed once; both reach `readyForCampaignTests`; the same actual
campaign identity is loaded; the server's object registry is ready; each player controls its
own resolvable hero/party; each screenshot shows the intended campaign; Danustica's ownership,
governor and relevant roster IDs/counts agree after settled authoritative updates. Compare
read-only values while the server's normal time policy gives a stable observation window,
otherwise retain timestamped observations and reconcile expected intervening authoritative ticks.
Do not pause/mutate another owner's world to obtain matching samples.

Fail on mismatched identity, duplicate party, not-ready client, failed lookup, disconnected
peer or an error indicating the join/action never completed. A `started` response alone fails
the readiness oracle. A startup popup outcome is recorded separately from campaign readiness.

Retain join/readiness responses, registry/player observations, common state, logs and both
screenshots in the returned run artifacts. Stop the owned run with confirmed cleanup and
re-read retained files. Missing player-inspection proof remains a blocker.

Excluded: real reconnect, wrong password, module mismatch, transport interruption, save corruption,
joining during battle, interaction between players, production shutdown saves and full-branch
acceptance. Those need their own actions and expected/actual evidence; this pass cannot cover them.
