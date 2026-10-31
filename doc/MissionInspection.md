# Read-only mission inspection (DEBUG)

These `ICoopCommand` commands are registered by `MissionModule` on both sides. They do not require NavalDLC. The dedicated server normally returns `unavailable:no_mission`; inspect a participating client for native mission state.

| Command | Result |
| --- | --- |
| `coop.debug.mission.summary` | Scene, mission state/mode, deployment flag, agent/behavior counts, main agent. |
| `coop.debug.mission.camera` | Active top MissionScreen's **CombatCamera** scalar frame, screen flags, main-agent-controller disabled flag, last-followed agent, focused agent and world-space distances. |
| `coop.debug.mission.agents 0 8` | First eight entries in `AllAgents`: index, state, controller, movement mode, position, health, stepped entity name/presence and cached-visuals visibility flag. |
| `coop.debug.mission.views 0 8` | First eight mission behaviors: concrete type, view/camera-mode-logic classification, view finalization and whether its screen matches the top screen. Includes non-view behaviors to expose missing collaborators. |

Paging arguments are optional: offset defaults to 0, limit to 8. Offset range is 0..100000, limit 1..16. Use the returned `nextOffset` for the next page, for example `coop.debug.mission.views 8 8`. A null cursor is the end. Pages use collection order, not persistent identities, and can change between calls. Agent indices are local to that mission, not network ids.

The existing live-test transport invokes the same full command name with argument arrays, for example `command="coop.debug.mission.agents", arguments=["0","8"]`. Output is `LIVE_TEST_JSON=` followed by JSON. No MCP extension or transport changes are needed. Check command success and then each returned `status`: successful command dispatch is not proof that any native field was available. Invalid arguments return `invalid_arguments`; responses beyond 24576 JSON characters return `output_limit` without partial JSON. Strings are capped at 160 characters.

## Observation limits

- Reads run on the game thread, rejecting finalized/ending missions and unbuilt, foreign or zero-pointer agents before agent native reads. Inactive agents return only state/index. No camera, control, input, visibility, deployment, physics or gameplay setters are called.
- Never serialize an engine frame/vector. Frame `origin`, `rotationS`, `rotationF`, `rotationU` are scalar DTOs. Nonfinite components become null with explicit status, not `NaN`/infinity strings. These are the native frame axes, not assumed Euler angles.
- CombatCamera is **not claimed to be the currently scene-bound camera**. Installed SceneView has no read-only camera getter. Selected observer mode is unavailable: `MissionScreen.GetSpectatingData` performs input-dependent selection and can invoke custom collaborators. It is not called. LastFollowedAgent is the stored identity, not a recomputed observer target.
- Non-agent focus identity is unavailable; only its concrete type is reported. No arbitrary property/reflection traversal is exposed.
- Visibility is the flag from already-cached valid agent visuals, not camera-frustum visibility or proof of rendered pixels. The `AgentVisuals` getter is deliberately avoided because it hydrates its weak-reference cache. Missing cached visuals are explicitly unavailable. View `IsReady` is virtual and is not invoked; inventory reports only managed lifetime/binding observations.
- Native getters are sequential observations, not atomic snapshots or synchronization barriers. A stepped entity is not proof of stable contact; movement mode is not an inferred swimming diagnosis. An unavailable value is not a negative observation.

Headless tests exercise actual command/DI registration, no-mission/finalized/ending guards, paging, argument/output caps, finite scalar serialization and managed nonmutation. They do not certify live native camera, visibility, contact or renderer lifetime behavior. Native runtime verification and deployment approval remain separate gates.
