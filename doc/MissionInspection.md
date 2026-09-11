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

## Water-only single-client naval view investigation

The supplied run `0387669a93a4406bacb8e8d6c0cbffd5` remained **before deployment**. Parent confirmed no `complete-deployment` action was sent. Its scalar baseline reports Deployment, `deploymentAttempted=false`, input disabled, an owned main agent/captain at approximately `(250.779,235.888,1.193)`, and stepped ship support. The screenshot shows water without visible hull/agent/native HUD. These are different observations; neither establishes swimming or a camera defect.

Installed method bodies explain an important prerequisite:

- `MissionScreen.GetSpectatingData` excludes the main agent as the normal follow target in Deployment and chooses free camera in that mode absent other overrides. `UpdateCamera` disables `MissionMainAgentController` during Deployment.
- `MissionScreen.SetCameraFrameToMapView` initially uses deployment strategy-camera entities or spawn-path placement, otherwise the scene/boundary center and ground height. It does not initially target this lab's arbitrary ship spawn.
- `NavalMissionScreen` inherits these camera paths. Its overrides concern initialization, character/camera-toggle eligibility and a cheat, not a replacement deployment camera update.
- The lab deliberately enters Deployment with AI ticking disabled. `CompleteNativeDeployment` performs deployment callbacks and crew placement before enabling AI and changing the mode to Battle. The accepted tactics/power-provider correction remains unchanged; it has not been live postdeployment-validated by this diagnostic increment.

**Proposed next investigation, not an implemented gameplay fix:** in a separately reviewed parent-owned run, capture these four slices and a screenshot before any action. Compare CombatCamera origin/distances and current mode with the actual main-agent position and native naval inspect. If separately authorized, capture the same observations after the existing deployment action, checking its receipt and callback counts first. A reproducible predeployment-only framing gap could justify a later reviewed deployment-camera initialization near the fixture; no force-camera workaround, auto-deployment or gameplay change is included here. Hull rendering and postdeployment native HUD/input remain unverified.

Headless tests exercise actual command/DI registration, no-mission/finalized/ending guards, paging, argument/output caps, finite scalar serialization and managed nonmutation. They do not certify live native camera, visibility, contact or renderer lifetime behavior. Native runtime verification and deployment approval remain separate gates.
