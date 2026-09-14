#if DEBUG
using Common;
using HarmonyLib;
using System.Reflection;
using Common.Messaging;
using GameInterface;
using Missions.Agents.Packets;
using GameInterface.Services.MapEvents.Messages;
using TaleWorlds.Core;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects.Usables;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace Missions.Battles;

public interface ISiegeInteractionDebugBehavior
{
    object Observe(Guid? observedAgentId = null);
}

internal sealed class SiegeInteractionDebugBehavior : MissionBehavior, ISiegeInteractionDebugBehavior
{
    private const int UseGameKeyId = 13;
    private readonly IMessageBroker messageBroker;
    private readonly HashSet<string> requests = new HashSet<string>();
    private readonly List<object> inputSamples = new List<object>();
    private readonly Dictionary<int, object> receivedStates = new Dictionary<int, object>();
    private readonly List<object> useDispatchSamples = new List<object>();
    private StandingPoint useDispatchPoint;
    private string useDispatchRequestId;
    private int useDispatchMachineId;
    private int useDispatchCalls;
    private int useDispatchPressedCalls;
    private int useDispatchDropped;
    private int useDispatchSequence;
    private int useDispatchThreadId;
    private int stateSequence;
    private int inputGameKeyId = UseGameKeyId;
    private int? observedMachineId;
    private readonly Dictionary<int, int> localShots = new Dictionary<int, int>();
    private readonly Dictionary<int, int> receivedShots = new Dictionary<int, int>();
    private MissionMainAgentInteractionComponent interaction;
    private IFocusable eventFocus;
    private bool? eventInteractable;
    private string requestId;
    private string status = "unexercised";
    private bool pressInvoked;
    private bool externalInputArmed;
    private int inputVirtualKey;
    private bool edgeObserved;
    private bool edgeCleared;
    private int tick;
    private int pressTick;
    private bool removed;
    private Agent capturedAgent;
    private Vec3 capturedPosition;
    private Vec3 capturedLookDirection;
    private MissionScreen capturedScreen;
    private Camera capturedCamera;
    private Camera stagingCamera;
    private float capturedCameraBearing;
    private float capturedCameraElevation;
    private bool nativeCameraStaged;
    private object nativeAimTarget;
    private Guid? watchedAgentId;
    private object observerFrame;
    private EquipmentIndex previousMainHand;
    private ItemObject previousMainHandItem;
    private bool fixtureRestored;
    private string captureFailureReason;
    private Agent dismountAgent;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public SiegeInteractionDebugBehavior(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<NetworkSiegeInteractionDebugRequest>(Handle);
        messageBroker.Subscribe<NetworkSiegeMachineState>(ObserveReceivedState);
        messageBroker.Subscribe<SiegeWeaponFired>(ObserveLocalShot);
        messageBroker.Subscribe<NetworkSiegeWeaponFired>(ObserveReceivedShot);
    }

    public override void OnPreDisplayMissionTick(float dt)
    {
        tick++;
        UpdateDismount();
        BindObserver();
        if ((!pressInvoked && !externalInputArmed) || edgeCleared || inputSamples.Count >= 300) return;
        var screen = ScreenManager.TopScreen as MissionScreen;
        if (screen?.SceneLayer?.Input == null) return;
        var input = screen.SceneLayer.Input;
        bool pressed = input.IsGameKeyPressed(inputGameKeyId);
        bool down = input.IsGameKeyDown(inputGameKeyId);
        bool released = input.IsGameKeyReleased(inputGameKeyId);
        var registeredKey = inputGameKeyId >= 0 && inputGameKeyId < input._registeredGameKeys.Count
            ? input._registeredGameKeys[inputGameKeyId] : null;
        var keyboardKey = registeredKey?.KeyboardKey;
        RecordInputSample(pressed, down, released, new
        {
            contextType = input.GetType().FullName,
            contextId = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(input),
            layerType = screen.SceneLayer.GetType().FullName,
            layerId = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(screen.SceneLayer),
            isKeysAllowed = input.IsKeysAllowed,
            registeredGameKeyId = registeredKey?.Id,
            registeredCategory = registeredKey?.MainCategoryId,
            registeredKeyboardKey = keyboardKey?.InputKey.ToString(),
            registeredVirtualKey = keyboardKey == null ? (int?)null : Input.GetVirtualKeyCode(keyboardKey.InputKey),
            armedVirtualKey = inputVirtualKey,
            rawPressed = keyboardKey == null ? (bool?)null : Input.IsKeyPressed(keyboardKey.InputKey),
            rawDown = keyboardKey == null ? (bool?)null : Input.IsKeyDown(keyboardKey.InputKey),
            rawReleased = keyboardKey == null ? (bool?)null : Input.IsKeyReleased(keyboardKey.InputKey)
        });
    }

    internal void RecordInputSample(bool pressed, bool down, bool released, object nativeInput = null)
    {
        edgeObserved |= pressed || down;
        edgeCleared = edgeObserved && tick > pressTick && !pressed && !down;
        inputSamples.Add(new { tick, recordedUtc = DateTime.UtcNow.ToString("O"), pressed, down, released, nativeInput });
        if (edgeCleared) status = "input_edge_cleared";
        else if (inputSamples.Count == 300) status = "unexercised_input_lifecycle";
    }

    internal object ReadUseDispatch()
    {
        lock (useDispatchSamples)
            return new { handlerCalls = useDispatchCalls, pressedCalls = useDispatchPressedCalls,
                dropped = useDispatchDropped, samples = useDispatchSamples.ToArray() };
    }

    internal void AppendUseDispatch(object observation)
    {
        lock (useDispatchSamples)
        {
            if (useDispatchSamples.Count < 16) useDispatchSamples.Add(observation);
            else useDispatchDropped++;
        }
    }

    internal int ObserveUseDispatch(object instance, string method, object[] args, int call = 0,
        Exception failure = null, string expectedRequestId = null)
    {
        if (removed || Mission == null || useDispatchPoint == null || capturedAgent == null ||
            useDispatchRequestId != requestId ||
            (expectedRequestId != null && expectedRequestId != useDispatchRequestId) ||
            !ReferenceEquals(Mission, TaleWorlds.MountAndBlade.Mission.Current) ||
            !ReferenceEquals(capturedAgent, Mission.MainAgent) ||
            (instance is Agent actor ? !ReferenceEquals(actor, capturedAgent) : !ReferenceEquals(instance, interaction))) return 0;
        bool entry = call == 0;
        int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        try
        {
            if (threadId != useDispatchThreadId)
            {
                if (entry) call = System.Threading.Interlocked.Increment(ref useDispatchSequence);
                AppendUseDispatch(new { requestId = useDispatchRequestId, call, method,
                    phase = entry ? "entry" : "exit", tick, recordedUtc = DateTime.UtcNow.ToString("O"),
                    threadId, observationError = "non_game_thread", exception = failure?.GetType().FullName });
                return call;
            }
            var input = capturedScreen?.SceneLayer?.Input;
            bool pressed = input?.IsGameKeyPressed(UseGameKeyId) == true;
            if (entry && method == nameof(MissionMainAgentInteractionComponent.FocusStateCheckTick))
            {
                useDispatchCalls++;
                if (pressed) useDispatchPressedCalls++;
                if (useDispatchCalls != 1 && !pressed) return 0;
            }
            if (entry) call = System.Threading.Interlocked.Increment(ref useDispatchSequence);
            ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry);
            CoopAgentInfo info = null;
            CoopAgentInfo userInfo = null;
            registry?.TryGetAgentInfo(capturedAgent, out info);
            var user = useDispatchPoint.UserAgent;
            if (user != null) registry?.TryGetAgentInfo(user, out userInfo);
            var session = Mission.GetMissionBehavior<CoopBattleController>()?.Session;
            AppendUseDispatch(new
            {
                requestId = useDispatchRequestId, call, method, phase = entry ? "entry" : "exit",
                tick, recordedUtc = DateTime.UtcNow.ToString("O"), threadId, sessionId = session?.InstanceId,
                identityComplete = info?.AgentId != null && info.AgentId != Guid.Empty &&
                    ReferenceEquals(info.Agent, capturedAgent) && session != null,
                inputGameKeyId, inputVirtualKey,
                inputContextId = input == null ? (int?)null : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(input),
                controllerId = session?.OwnControllerId, actorId = info?.AgentId.ToString("N"),
                originalOwner = info?.OriginalOwner, currentAuthority = info?.CurrentAuthority,
                machineId = useDispatchMachineId, pointId = useDispatchPoint.Id.Id,
                pointUserId = userInfo?.AgentId.ToString("N"), pointHasUser = user != null,
                pointHasAIUser = useDispatchPoint.HasAIUser, pointUserIsActor = ReferenceEquals(user, capturedAgent),
                sameFocusAgent = ReferenceEquals(Agent.Main, capturedAgent),
                nativeClient = GameNetwork.IsClient, nativeClientOrReplay = GameNetwork.IsClientOrReplay,
                radialMenuActive = capturedScreen?.IsRadialMenuActive,
                itemInteractionEnabled = Mission.IsMainAgentItemInteractionEnabled,
                orderMenuOpen = Mission.IsOrderMenuOpen, ableToUseMachine = capturedAgent.IsAbleToUseMachine(),
                pressed, down = input?.IsGameKeyDown(UseGameKeyId), released = input?.IsGameKeyReleased(UseGameKeyId),
                focusedObject = Describe(interaction?.CurrentFocusedObject),
                interactableObject = Describe(interaction?._currentInteractableObject),
                argumentObject = Describe(args?.OfType<UsableMissionObject>().FirstOrDefault()),
                usingObject = capturedAgent.IsUsingGameObject, usedObject = Describe(capturedAgent.CurrentlyUsedGameObject),
                exception = failure?.GetType().FullName
            });
        }
        catch (Exception exception)
        {
            AppendUseDispatch(new { requestId = useDispatchRequestId, call, method,
                phase = entry ? "entry" : "exit", tick, recordedUtc = DateTime.UtcNow.ToString("O"),
                observationError = exception.GetType().FullName });
        }
        return call;
    }

    [HarmonyPatch]
    [HarmonyPatchCategory("CoopSiegeInteractionDebug")]
    internal static class UseDispatchObservationPatch
    {
        internal static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
        {
            AccessTools.Method(typeof(MissionMainAgentInteractionComponent), nameof(MissionMainAgentInteractionComponent.FocusStateCheckTick)),
            AccessTools.Method(typeof(Agent), nameof(Agent.HandleStartUsingAction), new[] { typeof(UsableMissionObject), typeof(int) }),
            AccessTools.Method(typeof(Agent), nameof(Agent.UseGameObject), new[] { typeof(UsableMissionObject), typeof(int) }),
            AccessTools.Method(typeof(Agent), nameof(Agent.StopUsingGameObjectAux), new[] { typeof(bool), typeof(Agent.StopUsingGameObjectFlags) })
        };

        [HarmonyPrefix]
        internal static void Prefix(object __instance, MethodBase __originalMethod, object[] __args,
            out (SiegeInteractionDebugBehavior Observer, int Call, string RequestId) __state)
        {
            __state = default;
            try
            {
                var observer = TaleWorlds.MountAndBlade.Mission.Current?.GetMissionBehavior<SiegeInteractionDebugBehavior>();
                int call = observer?.ObserveUseDispatch(__instance, __originalMethod.Name, __args) ?? 0;
                if (call != 0) __state = (observer, call, observer.useDispatchRequestId);
            }
            catch { } // Observation must not interrupt vanilla dispatch.
        }

        [HarmonyFinalizer]
        internal static void Finalizer(object __instance, MethodBase __originalMethod, object[] __args,
            (SiegeInteractionDebugBehavior Observer, int Call, string RequestId) __state, Exception __exception)
        {
            try
            {
                __state.Observer?.ObserveUseDispatch(__instance, __originalMethod.Name, __args, __state.Call, __exception, __state.RequestId);
            }
            catch { } // Keep the original result and exception unchanged.
        }
    }

    public override void OnRemoveBehavior()
    {
        removed = true;
        messageBroker.Unsubscribe<NetworkSiegeInteractionDebugRequest>(Handle);
        messageBroker.Unsubscribe<NetworkSiegeMachineState>(ObserveReceivedState);
        messageBroker.Unsubscribe<SiegeWeaponFired>(ObserveLocalShot);
        messageBroker.Unsubscribe<NetworkSiegeWeaponFired>(ObserveReceivedShot);
        UnbindObserver();
        ReleaseCamera();
    }

    private void BindObserver()
    {
        var next = Mission?.GetMissionBehavior<MissionMainAgentController>()?.InteractionComponent;
        if (ReferenceEquals(next, interaction)) return;
        UnbindObserver();
        interaction = next;
        if (interaction == null) return;
        interaction.OnFocusGained += OnFocusGained;
        interaction.OnFocusLost += OnFocusLost;
    }

    private void UnbindObserver()
    {
        if (interaction != null)
        {
            interaction.OnFocusGained -= OnFocusGained;
            interaction.OnFocusLost -= OnFocusLost;
        }
        interaction = null;
        eventFocus = null;
        eventInteractable = null;
    }

    private void OnFocusGained(Agent agent, IFocusable focus, bool isInteractable)
    {
        if (agent != Mission.MainAgent) return;
        eventFocus = focus;
        eventInteractable = isInteractable;
    }

    private void OnFocusLost(Agent agent, IFocusable focus)
    {
        if (agent != Mission.MainAgent || !ReferenceEquals(eventFocus, focus)) return;
        eventFocus = null;
        eventInteractable = null;
    }

    private void ObserveLocalShot(MessagePayload<SiegeWeaponFired> payload)
    {
        int id = payload.What.Weapon.Id.Id;
        GameThread.RunSafe(() => CountShot(localShots, id));
    }

    private void ObserveReceivedShot(MessagePayload<NetworkSiegeWeaponFired> payload)
    {
        GameThread.RunSafe(() => CountShot(receivedShots, payload.What.MachineId));
    }

    private void CountShot(Dictionary<int, int> counts, int id)
    {
        if (removed || Mission != TaleWorlds.MountAndBlade.Mission.Current) return;
        counts.TryGetValue(id, out int previous);
        counts[id] = previous + 1;
    }

    private void ObserveReceivedState(MessagePayload<NetworkSiegeMachineState> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (removed || Mission != TaleWorlds.MountAndBlade.Mission.Current) return;
            var message = payload.What;
            receivedStates[message.MachineId] = new
            {
                sequence = ++stateSequence, receiveTick = tick,
                message.HostEpoch, message.AuthorityRevision, message.SenderControllerId,
                message.GateState, message.LadderState, message.StoneAmmo, message.WeaponState
            };
        });
    }

    private void Handle(MessagePayload<NetworkSiegeInteractionDebugRequest> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() => ApplyRequest(payload.What));
    }

    internal bool CanAcceptInputAction(string action, bool keyPressed, bool keyDown)
    {
        return (!(pressInvoked || externalInputArmed) || edgeCleared || action == "restore") ||
            (externalInputArmed && action == "arm-stop" && !keyPressed && !keyDown);
    }

    private void ApplyRequest(NetworkSiegeInteractionDebugRequest request)
    {
        var session = Mission?.GetMissionBehavior<CoopBattleController>()?.Session;
        if (removed || Mission != TaleWorlds.MountAndBlade.Mission.Current || session == null ||
            session.InstanceId != request.MapEventId || session.OwnControllerId != request.ControllerId ||
            string.IsNullOrEmpty(request.RequestId) || requests.Contains(request.RequestId)) return;
        var screen = ScreenManager.TopScreen as MissionScreen;
        var input = screen?.SceneLayer?.Input;
        if ((pressInvoked || externalInputArmed) && !edgeCleared && request.Action != "restore" &&
            !CanAcceptInputAction(request.Action,
                input == null || input.IsGameKeyPressed(inputGameKeyId),
                input == null || input.IsGameKeyDown(inputGameKeyId))) return;
        if (requests.Count >= 256) return;
        requests.Add(request.RequestId);
        requestId = request.RequestId;
        if (status == "fixture_dismount_pending") status = "unexercised";
        BindObserver();
        var agent = Mission.MainAgent;
        if (request.Action == "dismount")
        {
            Dismount(agent);
            return;
        }
        if (request.Action == "capture")
        {
            Capture(screen, agent);
            return;
        }
        if (request.Action == "stage" || request.Action == "watch" || request.Action == "approach" || request.Action == "aim")
        {
            Stage(screen, agent, request.MachineId, request.StandingPointIndex, request.Action == "watch",
                request.Action == "approach" || request.Action == "aim", request.Action == "aim");
            return;
        }
        if (request.Action == "restore")
        {
            Restore(screen, agent);
            return;
        }
        bool externalInput = request.Action == "arm-use" || request.Action == "arm-stop";
        string action = externalInput ? request.Action.Substring(4) : request.Action;
        externalInput |= action == "fire" || action == "attack";
        if (action != "use" && action != "stop" && action != "fire" && action != "attack")
        {
            status = "unexercised_unknown_action";
            return;
        }
        inputGameKeyId = (action == "fire" || action == "attack") ? 9 : UseGameKeyId;
        pressInvoked = false;
        externalInputArmed = false;
        inputVirtualKey = 0;
        edgeObserved = false;
        edgeCleared = false;
        inputSamples.Clear();
        var focusedMachine = interaction?.CurrentFocusedMachine as MissionObject;
        var focusedObject = interaction?.CurrentFocusedObject as MissionObject;
        var machine = Mission.MissionObjects.OfType<UsableMachine>()
            .FirstOrDefault(candidate => candidate.Id.Id == request.MachineId);
        if (action == "stop" && request.MachineId == 0 && agent?.IsUsingGameObject == true)
            machine = Mission.MissionObjects.OfType<UsableMachine>().FirstOrDefault(candidate =>
                candidate.StandingPoints.Any(point => ReferenceEquals(point, agent.CurrentlyUsedGameObject)));
        observedMachineId = machine?.Id.Id ?? request.MachineId;
        bool usingTarget = agent?.IsUsingGameObject == true && machine != null &&
            machine.StandingPoints.Any(point => ReferenceEquals(point, agent.CurrentlyUsedGameObject));
        bool focusedTarget = (focusedMachine?.Id.Id == request.MachineId ||
            focusedObject?.Id.Id == request.MachineId) && interaction?._currentInteractableObject != null;
        bool actionReady = action == "fire"
            ? usingTarget && machine is RangedSiegeWeapon
            : action == "attack"
                ? capturedAgent == agent && agent != null && !agent.IsUsingGameObject
                : action == "stop" ? usingTarget : focusedTarget && agent != null && !agent.IsUsingGameObject;
        if (screen?.SceneLayer?.Input == null || agent == null || !agent.IsActive() || !actionReady)
        {
            status = "unexercised_no_focus";
            return;
        }
        if (screen.SceneLayer.Input.IsGameKeyDown(inputGameKeyId) ||
            screen.SceneLayer.Input.IsGameKeyPressed(inputGameKeyId))
        {
            status = "unexercised_input_busy";
            return;
        }
        var key = HotKeyManager.GetCategory(CombatHotKeyCategory.CategoryId)?.GetGameKey(inputGameKeyId);
        if (key?.KeyboardKey == null)
        {
            status = "unexercised_unbound_key";
            return;
        }
        inputVirtualKey = key.KeyboardKey.InputKey == InputKey.LeftMouseButton
            ? 1 : Input.GetVirtualKeyCode(key.KeyboardKey.InputKey);
        if (externalInput && (inputVirtualKey <= 0 || inputVirtualKey > 255))
        {
            status = "unexercised_unbound_key";
            return;
        }
        lock (useDispatchSamples)
        {
            useDispatchPoint = inputGameKeyId == UseGameKeyId && machine is Ballista ballista
                ? ballista.PilotStandingPoint : null;
            useDispatchRequestId = requestId;
            useDispatchThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            useDispatchMachineId = machine?.Id.Id ?? request.MachineId;
            useDispatchCalls = useDispatchPressedCalls = useDispatchDropped = 0;
            useDispatchSamples.Clear();
        }
        pressTick = tick;
        externalInputArmed = externalInput;
        pressInvoked = !externalInput;
        status = externalInput ? "external_input_armed" : "press_invoked_outcome_pending";
        if (!externalInput) Input.PressKey(key.KeyboardKey.InputKey);
    }

    private void Dismount(Agent agent)
    {
        dismountAgent = null;
        if (agent == null || !agent.IsActive() || agent.IsUsingGameObject)
        {
            status = "fixture_dismount_rejected";
            return;
        }
        if (agent.MountAgent == null)
        {
            status = "fixture_dismounted";
            return;
        }
        var controller = Mission?.GetMissionBehavior<MissionMainAgentController>();
        if (controller == null)
        {
            status = "fixture_dismount_rejected";
            return;
        }
        dismountAgent = agent;
        status = "fixture_dismount_pending";
        // Let the player controller retain braking until it can dismount.
        controller._autoDismountModeActive = true;
    }

    private void UpdateDismount()
    {
        if (dismountAgent == null) return;
        if (!dismountAgent.IsActive() || dismountAgent.IsUsingGameObject)
        {
            if (status == "fixture_dismount_pending") status = "fixture_dismount_rejected";
            dismountAgent = null;
        }
        else if (dismountAgent.MountAgent == null)
        {
            if (status == "fixture_dismount_pending") status = "fixture_dismounted";
            dismountAgent = null;
        }
    }

    private void Capture(MissionScreen screen, Agent agent)
    {
        captureFailureReason = null;
        if (RejectCapture(capturedAgent != null, "already_captured") ||
            RejectCapture(screen?.CombatCamera == null, "combat_camera_missing") ||
            RejectCapture(agent == null, "agent_missing") ||
            RejectCapture(!agent.IsActive(), "agent_inactive") ||
            RejectCapture(agent.IsUsingGameObject, "agent_using_object") ||
            RejectCapture(agent.MountAgent != null, "agent_mounted")) return;
        capturedAgent = agent;
        capturedPosition = agent.Position;
        capturedLookDirection = agent.LookDirection;
        capturedScreen = screen;
        capturedCamera = screen.CustomCamera;
        capturedCameraBearing = screen.CameraBearing;
        capturedCameraElevation = screen.CameraElevation;
        nativeCameraStaged = false;
        nativeAimTarget = null;
        fixtureRestored = false;
        previousMainHand = agent.GetPrimaryWieldedItemIndex();
        previousMainHandItem = previousMainHand == EquipmentIndex.None ? null : agent.Equipment[previousMainHand].Item;
        observerFrame = null;
        status = "fixture_captured";
    }

    internal bool RejectCapture(bool rejected, string reason)
    {
        if (!rejected) return false;
        captureFailureReason = reason;
        status = "fixture_capture_rejected";
        return true;
    }

    private void Stage(MissionScreen screen, Agent agent, int machineId, int pointIndex, bool watchOnly, bool nativeCamera = false, bool reaimOnly = false)
    {
        if (reaimOnly && (!nativeCameraStaged || observedMachineId != machineId))
        {
            status = "fixture_stage_rejected";
            return;
        }
        observedMachineId = machineId;
        nativeAimTarget = null;
        var machine = Mission.MissionObjects.OfType<UsableMachine>()
            .FirstOrDefault(candidate => candidate.Id.Id == machineId);
        if (capturedAgent != agent || agent == null || !agent.IsActive() || agent.IsUsingGameObject ||
            screen != capturedScreen || machine == null || pointIndex < 0 ||
            pointIndex >= machine.StandingPoints.Count ||
            !ReferenceEquals(screen.CustomCamera, stagingCamera ?? capturedCamera))
        {
            status = "fixture_stage_rejected";
            return;
        }
        var point = machine.StandingPoints[pointIndex];
        if (reaimOnly && (!(machine is Ballista ballista) || !ReferenceEquals(point, ballista.PilotStandingPoint)))
        {
            status = "fixture_stage_rejected";
            return;
        }
        var position = reaimOnly ? agent.Position : point.GetUserFrameForAgent(agent).Origin.GetGroundVec3();
        if (nativeCamera)
        {
            if (capturedCamera != null || agent.MountAgent != null)
            {
                status = "fixture_stage_rejected";
                return;
            }
            ReleaseCamera();
            var direction = point.GetUserFrameForAgent(agent).Rotation.f;
            if (machine is StonePile)
            {
                var targetCenter = (machine.GameEntity.GlobalBoxMin + machine.GameEntity.GlobalBoxMax) * 0.5f;
                var eyeHeight = (agent.Monster.StandingEyeHeight + 0.2f) * agent.AgentScale;
                direction = GetNativeStagingDirection(position, eyeHeight, targetCenter);
            }
            else if (machine is Ballista)
            {
                try
                {
                    var nativeTarget = GetStagingTarget(machine, position, false, true);
                    var eyeHeight = (agent.Monster.StandingEyeHeight + 0.2f) * agent.AgentScale;
                    direction = GetNativeStagingDirection(position, eyeHeight, nativeTarget);
                    if (!(direction.LengthSquared >= 0.5f))
                        throw new InvalidOperationException("Ballista target coincides with the player eye.");
                }
                catch (Exception exception)
                {
                    nativeAimTarget = new { target = nativeAimTarget, error = exception.Message };
                    status = "fixture_native_target_unavailable";
                    return;
                }
            }
            if (machine is ArrowBarrel)
            {
                var bowSlot = Enumerable.Range(0, (int)EquipmentIndex.NumAllWeaponSlots)
                    .Select(index => (EquipmentIndex)index)
                    .Where(slot => agent.Equipment[slot].Item?.PrimaryWeapon?.WeaponClass == WeaponClass.Bow)
                    .DefaultIfEmpty(EquipmentIndex.None).First();
                if (bowSlot == EquipmentIndex.None || agent.GetOffhandWieldedItemIndex() != EquipmentIndex.None)
                {
                    status = "fixture_bow_unavailable";
                    return;
                }
                agent.TryToWieldWeaponInSlot(bowSlot, Agent.WeaponWieldActionType.WithAnimationUninterruptible, false);
            }
            if (!reaimOnly) agent.TeleportToPosition(position);
            agent.LookDirection = direction;
            screen.CameraBearing = direction.RotationZ;
            screen.CameraElevation = direction.RotationX;
            nativeCameraStaged = true;
            status = "fixture_staged_native_focus_pending";
            return;
        }
        if (stagingCamera == null)
        {
            stagingCamera = Camera.CreateCamera();
            stagingCamera.FillParametersFrom(screen.CombatCamera);
        }
        if (!watchOnly) agent.TeleportToPosition(position);
        var target = GetStagingTarget(machine, point.GameEntity.GlobalPosition, watchOnly);
        var eye = watchOnly ? target + new Vec3(3f, 3f, 2f) : position + (Vec3.Up * 1.6f);
        observerFrame = null;
        if (watchOnly && watchedAgentId.HasValue)
        {
            if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry) ||
                !registry.TryGetAgentInfo(watchedAgentId.Value, out var info) || info?.Agent == null ||
                info.AgentId != watchedAgentId.Value || ReferenceEquals(info.Agent, agent) ||
                !ReferenceEquals(info.Agent.Mission, Mission) || !info.Agent.IsActive())
            {
                observerFrame = new { agentId = watchedAgentId.Value.ToString("N"), tick,
                    rejectionReason = "actor_identity_or_activity_unavailable", visualEntityAvailable = (bool?)null,
                    lookDirection = DescribePosition(null), horizontalLookLengthSquared = (float?)null };
                ReleaseCamera();
                status = "fixture_observer_actor_unavailable";
                return;
            }
            var actor = info.Agent;
            if (actor.AgentVisuals?.GetEntity() == null)
            {
                observerFrame = new { agentId = info.AgentId.ToString("N"), tick,
                    rejectionReason = "visual_entity_unavailable", visualEntityAvailable = false,
                    lookDirection = DescribePosition(null), horizontalLookLengthSquared = (float?)null };
                ReleaseCamera();
                status = "fixture_observer_actor_unavailable";
                return;
            }
            target = actor.Position + (Vec3.Up * actor.AgentScale);
            var lookDirection = actor.LookDirection;
            var behind = lookDirection;
            behind.z = 0f;
            if (behind.LengthSquared < 0.5f)
            {
                observerFrame = new { agentId = info.AgentId.ToString("N"), tick,
                    rejectionReason = "horizontal_look_too_short", visualEntityAvailable = true,
                    lookDirection = DescribePosition(lookDirection), horizontalLookLengthSquared = behind.LengthSquared };
                ReleaseCamera();
                status = "fixture_observer_actor_unavailable";
                return;
            }
            eye = target - (behind.NormalizedCopy() * 3f) + (Vec3.Up * 0.8f);
            observerFrame = new { agentId = info.AgentId.ToString("N"), info.OriginalOwner,
                actorPosition = DescribePosition(actor.Position), target = DescribePosition(target), eye = DescribePosition(eye) };
        }
        var up = Math.Abs(Vec3.DotProduct((target - eye).NormalizedCopy(), Vec3.Up)) > 0.99f
            ? new Vec3(0f, 1f, 0f) : Vec3.Up;
        stagingCamera.LookAt(eye, target, up);
        stagingCamera.SetFovVertical(65f * MathF.PI / 180f, TaleWorlds.Engine.Screen.AspectRatio, 0.065f, 12500f);
        screen.CustomCamera = stagingCamera;
        status = watchOnly ? "fixture_observer_camera_staged" : "fixture_staged_native_focus_pending";
    }

    internal static Vec3 GetNativeStagingDirection(Vec3 userPosition, float eyeHeight, Vec3 targetCenter)
    {
        return (targetCenter - (userPosition + (Vec3.Up * eyeHeight))).NormalizedCopy();
    }

    internal Vec3 GetStagingTarget(UsableMachine machine, Vec3 standingPointPosition, bool watchOnly, bool nativeCamera = false)
    {
        if (nativeCamera && !watchOnly && machine is Ballista ballista)
        {
            var body = ballista.ballistaBody;
            if (body == null || !body.GameEntity.IsValid)
                throw new InvalidOperationException("The current ballista has no resolved body.");
            var entity = GameEntity.CreateFromWeakEntity(body.GameEntity);
            var min = entity.GlobalBoxMin;
            var max = entity.GlobalBoxMax;
            var target = (min * 0.5f) + (max * 0.5f);
            nativeAimTarget = new
            {
                requestId, tick, recordedUtc = DateTime.UtcNow, machineId = machine.Id.Id, bodyId = body.Id.Id,
                bodyName = body.GameEntity.Name, bodyTag = ballista.BodyTag,
                min = DescribePosition(min), max = DescribePosition(max), target = DescribePosition(target),
                ancestors = DescribeAncestors(body.GameEntity)
            };
            if (new[] { min.x, min.y, min.z, max.x, max.y, max.z }
                    .Any(value => float.IsNaN(value) || float.IsInfinity(value)) ||
                min.x > max.x || min.y > max.y || min.z > max.z || (max - min).LengthSquared < 0.0001f)
                throw new InvalidOperationException("The resolved ballista body has invalid world bounds.");
            return target;
        }
        if (watchOnly || !(machine is CastleGate gate)) return standingPointPosition;
        // Gate standing-point origins can lie directly beneath the player's feet.
        var bounds = gate.ComputeGlobalPhysicsBoundingBoxMinMax();
        return (bounds.Item1 + bounds.Item2) * 0.5f;
    }

    private void Restore(MissionScreen screen, Agent agent)
    {
        if (capturedAgent == null || capturedAgent != agent || screen != capturedScreen ||
            !agent.IsActive() || agent.IsUsingGameObject ||
            !ReferenceEquals(screen.CustomCamera, stagingCamera ?? capturedCamera))
        {
            status = "fixture_restore_rejected";
            return;
        }
        if (externalInputArmed)
        {
            if (screen.SceneLayer.Input.IsGameKeyDown(inputGameKeyId) ||
                screen.SceneLayer.Input.IsGameKeyPressed(inputGameKeyId))
            {
                status = "fixture_restore_rejected_input_held";
                return;
            }
            externalInputArmed = false;
        }
        if (previousMainHand != EquipmentIndex.None && !ReferenceEquals(agent.Equipment[previousMainHand].Item, previousMainHandItem))
        {
            status = "fixture_restore_rejected_equipment_changed";
            return;
        }
        if (agent.GetPrimaryWieldedItemIndex() != previousMainHand)
        {
            if (previousMainHand == EquipmentIndex.None)
                agent.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.InstantAfterPickUp);
            else
                agent.TryToWieldWeaponInSlot(previousMainHand, Agent.WeaponWieldActionType.InstantAfterPickUp, false);
            if (agent.GetPrimaryWieldedItemIndex() != previousMainHand)
            {
                status = "fixture_restore_rejected_wield_state";
                return;
            }
        }
        agent.TeleportToPosition(capturedPosition);
        agent.LookDirection = capturedLookDirection;
        ReleaseCamera();
        if (nativeCameraStaged)
        {
            screen.CameraBearing = capturedCameraBearing;
            screen.CameraElevation = capturedCameraElevation;
            nativeCameraStaged = false;
        }
        fixtureRestored = (agent.Position - capturedPosition).LengthSquared < 0.01f &&
            ReferenceEquals(screen.CustomCamera, capturedCamera) &&
            agent.GetPrimaryWieldedItemIndex() == previousMainHand &&
            (previousMainHand == EquipmentIndex.None || ReferenceEquals(agent.Equipment[previousMainHand].Item, previousMainHandItem));
        status = fixtureRestored ? "fixture_restored" : "fixture_restore_mismatch";
        if (fixtureRestored) capturedAgent = null;
    }

    private void ReleaseCamera()
    {
        if (stagingCamera == null) return;
        if (capturedScreen != null && ReferenceEquals(capturedScreen.CustomCamera, stagingCamera))
            capturedScreen.CustomCamera = capturedCamera;
        stagingCamera.ReleaseCamera();
        stagingCamera = null;
    }

    public object Observe(Guid? observedAgentId = null)
    {
        var screen = ScreenManager.TopScreen as MissionScreen;
        var agent = Mission?.MainAgent;
        var session = Mission?.GetMissionBehavior<CoopBattleController>()?.Session;
        ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry);
        CoopAgentInfo mainInfo = null;
        if (agent != null) registry?.TryGetAgentInfo(agent, out mainInfo);
        var selectedAgentId = observedAgentId ?? mainInfo?.AgentId;
        watchedAgentId = observedAgentId;
        return new
        {
            success = !removed && Mission == TaleWorlds.MountAndBlade.Mission.Current,
            sessionId = session?.InstanceId,
            controllerId = session?.OwnControllerId,
            hostControllerId = session?.HostControllerId,
            hostEpoch = session?.HostEpoch,
            mainAgentId = mainInfo?.AgentId.ToString("N"),
            observedAgent = selectedAgentId.HasValue ? ReadObservedAgent(selectedAgentId.Value, registry) : null,
            mainAgentPosition = DescribePosition(agent?.Position),
            screenPresent = screen != null,
            inputContextPresent = screen?.SceneLayer?.Input != null,
            mainAgentActive = agent?.IsActive() == true,
            wieldedSlot = agent == null ? -1 : (int)agent.GetPrimaryWieldedItemIndex(),
            focusedObject = Describe(interaction?.CurrentFocusedObject),
            focusedMachine = Describe(interaction?.CurrentFocusedMachine),
            nativeInteractable = interaction?._currentInteractableObject != null,
            focusEventObject = Describe(eventFocus),
            focusEventInteractable = eventInteractable,
            usingObject = agent?.IsUsingGameObject == true,
            usedObject = Describe(agent?.CurrentlyUsedGameObject),
            requestId, status, pressInvoked, externalInputArmed, inputVirtualKey, edgeObserved, edgeCleared, inputGameKeyId, tick,
            fixtureActive = capturedAgent != null,
            fixtureRestored, captureFailureReason,
            nativeCameraStaged, nativeAimTarget, observerFrame,
            stagingCameraActive = stagingCamera != null && ReferenceEquals(screen?.CustomCamera, stagingCamera),
            focusDiagnostic = ReadFocusDiagnostic(screen, agent),
            inputSamples = inputSamples.ToArray(),
            useDispatch = ReadUseDispatch(),
            receivedStates, localShots, receivedShots,
            equipment = ReadEquipment(agent),
            observedMachineId,
            // Keep every input frame and the requested machine, even after focus is lost.
            machines = Mission?.MissionObjects.OfType<UsableMachine>()
                .Where(machine => !observedMachineId.HasValue || machine.Id.Id == observedMachineId.Value)
                .Select(machine => new
            {
                id = machine.Id.Id,
                type = machine.GetType().Name,
                machine.IsDeactivated,
                machine.IsDisabled,
                gateState = machine is CastleGate gate ? (int?)gate.State : null,
                stoneAmmo = machine is StonePile stones ? (int?)stones.AmmoCount : null,
                stoneItemId = machine is StonePile pile ? pile.GivenItemID : null,
                hitPoints = machine.DestructionComponent?.HitPoint,
                ladderState = machine is SiegeLadder ladder ? (int?)ladder.State : null,
                rangedState = machine is RangedSiegeWeapon weapon ? (int?)weapon.State : null,
                rangedStateName = (machine as RangedSiegeWeapon)?.State.ToString(),
                standingPoints = machine.StandingPoints.Select((point, index) =>
                {
                    try
                    {
                        float? distanceSquared = null;
                        float? heightDifference = null;
                        bool? reachable = null;
                        if (agent != null)
                        {
                            var userFrame = point.GetUserFrameForAgent(agent);
                            distanceSquared = userFrame.Origin.AsVec2.DistanceSquared(agent.Position.AsVec2);
                            float pointHeight = point.UseOwnPositionInsteadOfWorldPosition
                                ? point.GameEntity.GlobalPosition.z : userFrame.Origin.GetGroundVec3().z;
                            heightDifference = Math.Abs(pointHeight - agent.Position.z);
                            reachable = agent.CanReachAndUseObject(point, distanceSquared.Value);
                        }
                        return (object)new
                        {
                            index,
                            id = point.Id.Id,
                            point.IsDeactivated,
                            point.IsDisabledForPlayers,
                            disabledForMainAgent = agent == null ? (bool?)null : point.IsDisabledForAgent(agent),
                            occupied = point.UserAgent != null,
                            point.HasAIUser,
                            vacantForPlayer = !point.HasUser || point.HasAIUser,
                            ownedByMainAgent = agent != null && point.UserAgent == agent,
                            distanceSquared, heightDifference, reachable,
                            heightWithinReach = heightDifference.HasValue ? (bool?)(heightDifference.Value < 1.5f) : null,
                            x = point.GameEntity.GlobalPosition.X,
                            y = point.GameEntity.GlobalPosition.Y,
                            z = point.GameEntity.GlobalPosition.Z
                        };
                    }
                    catch (Exception exception)
                    {
                        return new { index, diagnosticError = exception.ToString() };
                    }
                }).ToArray()
            }).ToArray()
        };
    }

    internal object ReadObservedAgent(Guid agentId, INetworkAgentRegistry registry)
    {
        if (removed || Mission == null || !ReferenceEquals(Mission, TaleWorlds.MountAndBlade.Mission.Current) ||
            registry == null || !registry.TryGetAgentInfo(agentId, out var info) || info?.Agent == null ||
            info.AgentId != agentId || !ReferenceEquals(info.Agent.Mission, Mission) || !info.Agent.IsActive())
            return new { agentId = agentId.ToString("N"), available = false };

        var observed = info.Agent;
        return new
        {
            agentId = agentId.ToString("N"), available = true,
            originalOwner = info.OriginalOwner, currentAuthority = info.CurrentAuthority,
            tick, recordedUtc = DateTime.UtcNow.ToString("O"),
            usingObject = observed.IsUsingGameObject,
            usedObject = Describe(observed.CurrentlyUsedGameObject),
            actions = Enumerable.Range(0, 2).Select(channel =>
            {
                int index = observed.GetCurrentAction(channel).Index;
                return new
                {
                    channel, index, name = AgentActionData.GetActionNameWithCode(index),
                    type = observed.GetCurrentActionType(channel).ToString()
                };
            }).ToArray()
        };
    }

    internal static object DescribePosition(Vec3? position)
    {
        if (!position.HasValue) return null;
        var value = position.Value;
        return new { x = value.x, y = value.y, z = value.z };
    }

    internal object ReadFocusDiagnostic(MissionScreen screen, Agent agent)
    {
        if (removed) return null;
        if (screen == null || agent == null)
            return new { tick, requestId, unavailable = "camera_agent_or_scene_missing" };
        object gates = null;
        try
        {
            var controller = Mission?.GetMissionBehavior<MissionMainAgentController>();
            var focusAgent = Agent.Main;
            gates = new
            {
                controllerPresent = controller != null,
                controllerDisabled = controller?.IsDisabled,
                controllerActivated = controller?._activated,
                agentState = agent == null ? (int?)null : (int)agent.State,
                agentAIControlled = agent?.IsAIControlled,
                sameFocusAgent = ReferenceEquals(agent, focusAgent),
                focusAgentPresent = focusAgent != null,
                ghostMode = screen?.IsCheatGhostMode,
                photoMode = screen?.IsPhotoModeEnabled,
                missionMode = Mission == null ? (int?)null : (int)Mission.Mode,
                itemInteractionEnabled = Mission?.IsMainAgentItemInteractionEnabled,
                objectInteractionEnabled = Mission?.IsMainAgentObjectInteractionEnabled,
                focusMountable = interaction?._currentInteractableObject is Agent mount && mount.IsMount,
                orderMenuOpen = Mission?.IsOrderMenuOpen,
                key25Down = screen?.SceneLayer?.Input?.IsGameKeyDown(25),
                ableToUseMachine = focusAgent?.IsAbleToUseMachine()
            };
            if (screen?.CombatCamera == null || focusAgent == null || Mission?.Scene == null)
                return new { tick, requestId, gates, unavailable = "camera_agent_or_scene_missing" };
            var camera = screen.CombatCamera;
            var position = camera.Position;
            var direction = camera.Direction;
            var agentPosition = focusAgent.Position;
            float horizontalDistance = new Vec2(position.X, position.Y)
                .Distance(new Vec2(agentPosition.X, agentPosition.Y));
            var origin = position + (direction * horizontalDistance);
            float length = 10f;
            bool blockerHit = Mission.Scene.FocusRayCastForFixedPhysics(origin, origin + (direction * length),
                out float blockerDistance, out Vec3 blockerPoint, out WeakGameEntity blocker,
                0.01f, unchecked((BodyFlags)(-251707585)));
            if (blockerHit) length = blockerDistance;
            bool terrainHit = Mission.Scene.RayCastForClosestEntityOrTerrain(origin, origin + (direction * length),
                out float terrainDistance, out Vec3 terrainPoint, out WeakGameEntity terrain,
                0.01f, unchecked((BodyFlags)(-251707585)));
            if (terrainHit && terrainDistance < length) length = terrainDistance;
            bool focusHit = Mission.Scene.FocusRayCastForFixedPhysics(origin, origin + (direction * (length + 0.1f)),
                out float focusDistance, out Vec3 focusPoint, out WeakGameEntity hit,
                0.2f, (BodyFlags)79617);
            object fallbackProbes = null;
            if (observedMachineId.HasValue)
            {
                bool nearHit = Mission.Scene.RayCastForClosestEntityOrTerrain(origin, origin + (direction * (length + 0.1f)),
                    out float nearDistance, out Vec3 nearPoint, out WeakGameEntity nearEntity, 0.2f, (BodyFlags)79617);
                bool wideHit = Mission.Scene.RayCastForClosestEntityOrTerrain(origin + (direction * 0.4f), origin + (direction * (length + 0.1f)),
                    out float wideDistance, out Vec3 widePoint, out WeakGameEntity wideEntity, 0.6f, (BodyFlags)79617);
                fallbackProbes = new
                {
                    length = length + 0.1f,
                    nearHit, nearDistance, nearPoint = DescribePosition(nearHit ? nearPoint : (Vec3?)null),
                    nearAncestors = DescribeAncestors(nearEntity),
                    wideHit, wideDistance, widePoint = DescribePosition(wideHit ? widePoint : (Vec3?)null),
                    wideAncestors = DescribeAncestors(wideEntity)
                };
            }
            return new
            {
                tick, requestId, gates,
                agentPosition = new[] { agentPosition.X, agentPosition.Y, agentPosition.Z },
                cameraPosition = new[] { position.X, position.Y, position.Z },
                cameraDirection = new[] { direction.X, direction.Y, direction.Z },
                stagingPosition = ReferenceEquals(stagingCamera, null) ? null
                    : new[] { stagingCamera.Position.X, stagingCamera.Position.Y, stagingCamera.Position.Z },
                stagingDirection = ReferenceEquals(stagingCamera, null) ? null
                    : new[] { stagingCamera.Direction.X, stagingCamera.Direction.Y, stagingCamera.Direction.Z },
                rayOrigin = new[] { origin.X, origin.Y, origin.Z },
                // These read-only probes do not replace vanilla's agent and wider fallback ray selection.
                fallbackProbes,
                rayProbe = new
                {
                    length, blockerHit, blockerDistance,
                    blockerPoint = DescribePosition(blockerHit ? blockerPoint : (Vec3?)null),
                    blockerAncestors = DescribeAncestors(blocker),
                    terrainHit, terrainDistance, terrainPoint = DescribePosition(terrainHit ? terrainPoint : (Vec3?)null),
                    terrainAncestors = DescribeAncestors(terrain),
                    focusHit, focusDistance, focusPoint = DescribePosition(focusHit ? focusPoint : (Vec3?)null),
                    focusAncestors = DescribeAncestors(hit)
                }
            };
        }
        catch (Exception exception)
        {
            return new { tick, requestId, gates, diagnosticError = exception.ToString() };
        }
    }

    private static object[] DescribeAncestors(WeakGameEntity entity)
    {
        var ancestors = new List<object>();
        for (int depth = 0; entity.IsValid && depth < 16; depth++, entity = entity.Parent)
        {
            var focus = entity.GetFirstScriptWithInterfaceOfType<IFocusable>();
            var missionObject = entity.GetFirstScriptOfType<MissionObject>();
            var ancestor = new
            {
                depth, entityPointer = entity.Pointer.ToUInt64().ToString("X16"),
                id = missionObject?.Id.Id, type = missionObject?.GetType().Name,
                focus = Describe(focus), isFocusable = focus?.IsFocusable
            };
            ancestors.Add(depth == 0 ? new
            {
                ancestor.depth, ancestor.entityPointer, name = entity.Name, bodyFlags = (int)entity.BodyFlag,
                min = DescribePosition(entity.GlobalBoxMin), max = DescribePosition(entity.GlobalBoxMax),
                ancestor.id, ancestor.type, ancestor.focus, ancestor.isFocusable
            } : (object)ancestor);
        }
        return ancestors.ToArray();
    }

    private static object[] ReadEquipment(Agent agent)
    {
        if (agent == null) return Array.Empty<object>();
        var slots = new List<object>();
        for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot;
             index < EquipmentIndex.NumAllWeaponSlots; index++)
        {
            var weapon = agent.Equipment[index];
            slots.Add(new
            {
                slot = (int)index, itemId = weapon.Item?.StringId, amount = weapon.Amount,
                weaponClass = weapon.Item?.PrimaryWeapon?.WeaponClass.ToString()
            });
        }
        return slots.ToArray();
    }

    private static object Describe(IFocusable focus)
    {
        if (focus == null) return null;
        var missionObject = focus as MissionObject;
        return new { type = focus.GetType().Name, id = missionObject?.Id.Id };
    }
}
#endif
