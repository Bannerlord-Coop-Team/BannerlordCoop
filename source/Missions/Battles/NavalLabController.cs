#if DEBUG
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Missions.Agents.Handlers;
using Missions.Messages;
using Missions.Services.Network;
using System;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace Missions.Battles;

public interface INavalLabController : IDisposable
{
    void Start(NavalLabManifest manifest, INavalMissionAdapter adapter, Action onEnd);
    void AbortStart();
    string Apply(NetworkNavalLabAction action);
    object TerminalStatus();
}

public sealed partial class NavalLabController : CoopMissionController, INavalLabController
{
    private readonly INetwork relay;
    private readonly IMissionContext context;
    private readonly BattleSession session;
    private readonly IBattleHostRegistry hosts;
    private NavalLabManifest manifest;
    private INavalMissionAdapter adapter;
    private Action onEnd;
    private volatile bool released;
    private bool networkStarted;
    private bool entered;
    private bool disposed;
    private bool startAborted;
    private bool faultReported;
    private float sendTime;
    private long callback;
    private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    public NavalLabController(IBattleNetwork network, INetwork relay, IMessageBroker broker,
        IObjectManager objects, ICoopMissionComponent component, IControllerIdProvider own,
        IBattleHostRegistry hosts, IMissionContext context)
        : base(network, broker, objects, component, MovementCadenceProfile.Battle)
    {
        this.relay = relay;
        this.context = context;
        this.hosts = hosts;
        session = new BattleSession(own, hosts);
        broker.Subscribe<NetworkNavalLabShipSample>(OnShipSample);
        broker.Subscribe<NetworkNavalLabStations>(OnStations);
        broker.Subscribe<NetworkNavalLabHelmOccupancy>(OnHelmOccupancy);
        broker.Subscribe<NetworkNavalLabRopeFinal>(OnFinalRopes);
    }

    public void Start(NavalLabManifest manifest, INavalMissionAdapter adapter, Action onEnd)
    {
        this.manifest = manifest;
        this.adapter = adapter;
        this.onEnd = onEnd;
        session.TryBegin(manifest.InstanceId);
        coopMissionComponent.AgentMovementHandler.ConfigureNavalLab();
        networkStarted = true;
        network.Start();
        network.ConnectToInstance(manifest.InstanceId);
        entered = true;
        relay.SendAll(new NetworkMissionEntered(session.OwnControllerId, manifest.InstanceId));
        deadline = Now + 30;
        adapter.Open(manifest, this, session.OwnControllerId);
        ConfigureNativeAdapter();
    }

    public override void AfterStart()
    {
        if (sceneReady || terminal) return;
        base.AfterStart();
        sceneReady = true;
        messageBroker.Publish(this, new BattleMissionReady(manifest.InstanceId));
        relay.SendAll(new NetworkNavalLabReceipt(manifest.IncarnationId, manifest.IncarnationId, "scene_ready"));
    }

    private void RegisterFixtureAgents()
    {
        coopMissionComponent.AgentRegistry.Clear();
        for (int i = 0; i < adapter.Agents.Length; i++)
        {
            string owner = manifest.Controllers[i / NavalLabManifest.CrewPerShip];
            if (!coopMissionComponent.AgentRegistry.TryRegisterAgent(
                owner, owner, manifest.InstanceId + ":" + owner, manifest.Combatants[i], (ushort)(i + 1), adapter.Agents[i], 1))
                throw new InvalidOperationException("Cannot register a synthetic combatant.");
        }
    }

    // Server-relayed fixture traffic for this incarnation; the coordinator only relays it on the server.
    private void OnShipSample(MessagePayload<NetworkNavalLabShipSample> payload) => GameThread.RunSafe(() =>
    {
        if (IsCurrent(payload.What.IncarnationId)) ReceiveShipSample(payload.What);
    }, context: nameof(OnShipSample));

    private void OnStations(MessagePayload<NetworkNavalLabStations> payload) => GameThread.RunSafe(() =>
    {
        var stations = payload.What;
        if (IsCurrent(stations.IncarnationId) && (stations.Phase == "commit" || stations.Phase == "release")) ApplyStations(stations);
    }, context: nameof(OnStations));

    private void OnHelmOccupancy(MessagePayload<NetworkNavalLabHelmOccupancy> payload) => GameThread.RunSafe(() =>
    {
        if (IsCurrent(payload.What.IncarnationId)) ReceiveHelmOccupancy(payload.What);
    }, context: nameof(OnHelmOccupancy));

    private void OnFinalRopes(MessagePayload<NetworkNavalLabRopeFinal> payload) => GameThread.RunSafe(() =>
    {
        if (IsCurrent(payload.What.IncarnationId)) ReceiveFinalRopes(payload.What);
    }, context: nameof(OnFinalRopes));

    private bool IsCurrent(Guid incarnationId) => !disposed && manifest != null && incarnationId == manifest.IncarnationId;

    private bool OriginalOwnersReady => manifest != null && hosts.TryGet(manifest.InstanceId, out var host)
        && manifest.Controllers.All(id => id == host.HostControllerId || host.SuccessorControllerIds.Contains(id));

    public string Apply(NetworkNavalLabAction action)
    {
        if (manifest == null || action.IncarnationId != manifest.IncarnationId || disposed)
            return "rejected:stale_incarnation";
        if (action.Kind == "stop" || action.Kind == "hold")
        {
            try { Hold(action.Kind); }
            finally { if (action.Kind == "stop") Mission.EndMission(); }
            if (controlCleanupFailure != null || holdFailure != null)
                return "failed:factory_probe.terminal_cleanup";
            return action.Kind == "stop" ? "applied" : "held";
        }
        if (action.Epoch != session.HostEpoch || session.HostEpoch != 1) return "rejected:stale_epoch";
        if (adapter.Blocker != null || terminal) return "rejected:fixture_blocked";
        if (action.Kind == "release")
        {
            if (!hydrated || !AssignmentValid) return "rejected:not_hydrated";
            if (!released) deadline = Now + 120;
            released = true;
            return "applied";
        }
        if (!released || !OriginalOwnersReady) return "rejected:not_released_or_owner_departed";
        if (!AssignmentValid) return "rejected:factory_assignment_changed";
        if (action.Kind == "native-controls-ready")
        {
            ReleaseNativeControls();
            return NativeControlsReady ? "controls_ready" : "failed:native_release";
        }
        bool presentationPulse = action.Kind == "native-axes-backward" || action.Kind == "native-axes-neutral" || action.Kind == "native-row-stop";
        if (action.Kind == "rope-throw" || action.Kind == "rope-miss" || action.Kind == "rope-cut" || action.Kind == "rope-plank-force")
        {
            if (manifest.Mode != NavalLabMode.TwoClientNative || action.Ship < 0 || action.Ship >= 2
                || manifest.Controllers[action.Ship] != session.OwnControllerId || !NativeControlsReady)
                return "rejected:rope_owner_not_ready";
            if (action.DeadlineUtcTicks <= DateTime.UtcNow.Ticks || action.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(2).Ticks)
                return "rejected:expired_rope_command";
            return (adapter as INavalRopeAdapter)?.RequestRope(action) ?? "rejected:rope_adapter_unavailable";
        }
        if (action.Kind == "native-axes-pulse" || presentationPulse)
        {
            if (action.Ship < 0 || action.Ship >= 2 || manifest.Controllers[action.Ship] != session.OwnControllerId
                || !NativeControlsReady) return "rejected:owner_not_ready";
            if (float.IsNaN(action.Rudder) || float.IsInfinity(action.Rudder) || Math.Abs(action.Rudder) > 1)
                return "rejected:invalid_control";
            if (action.DeadlineUtcTicks <= DateTime.UtcNow.Ticks || action.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(1).Ticks)
                return "rejected:expired_control";
            if (presentationPulse)
            {
                if (action.Row || (action.Kind != "native-axes-backward" && action.Rudder != 0)) return "rejected:invalid_control";
                return (adapter as INavalPresentationAdapter)?.RequestPresentationPulse(action.OperationId, action.Ship,
                    action.Rudder, action.Kind, action.DeadlineUtcTicks) ?? "rejected:presentation_adapter_unavailable";
            }
            return NativeAdapter.RequestAxesPulse(action.OperationId, action.Ship, action.Rudder, action.Row, action.DeadlineUtcTicks);
        }
        if (action.Kind == "native-take-helm" || action.Kind == "native-release-helm")
        {
            if (action.Ship < 0 || action.Ship >= 2 || manifest.Controllers[action.Ship] != session.OwnControllerId
                || action.Rudder != 0 || action.Row || !NativeControlsReady) return "rejected:owner_not_ready";
            if (action.DeadlineUtcTicks <= DateTime.UtcNow.Ticks || action.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(2).Ticks)
                return "rejected:expired_control";
            return NativeAdapter.RequestNativeHelm(action.OperationId, action.Ship, action.Kind == "native-take-helm");
        }
        // Scripted captain locomotion after a confirmed release; not keyboard evidence.
        if (action.Kind == "walk" || action.Kind == "turn")
        {
            if (manifest.Mode != NavalLabMode.TwoClientNative || action.Ship < 0 || action.Ship >= 2
                || manifest.Controllers[action.Ship] != session.OwnControllerId || action.Row || !NativeControlsReady)
                return "rejected:owner_not_ready";
            if (float.IsNaN(action.Rudder) || float.IsInfinity(action.Rudder) || Math.Abs(action.Rudder) > 1)
                return "rejected:invalid_control";
            if (action.DeadlineUtcTicks <= DateTime.UtcNow.Ticks || action.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(1).Ticks)
                return "rejected:expired_control";
            return adapter.StartAgentControl(action.Kind, action.Ship, action.Rudder);
        }
        if (action.Kind == "sail-full" || action.Kind == "sail-raised" || action.Kind == "sail-square-raised")
        {
            if (action.Ship < 0 || action.Ship >= 2 || manifest.Controllers[action.Ship] != session.OwnControllerId
                || action.Rudder != 0 || action.Row || !NativeControlsReady) return "rejected:owner_not_ready";
            if (action.DeadlineUtcTicks <= DateTime.UtcNow.Ticks || action.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(1).Ticks)
                return "rejected:expired_control";
            return NativeAdapter.RequestSail(action.Kind == "sail-full" ? 2 : action.Kind == "sail-raised" ? 0 : 1);
        }
        if (action.Kind != "complete-deployment" || action.Ship != 0 || action.Rudder != 0 || action.Row) return "rejected:wrong_mode";
        if (!NativeAgentAuthoritiesValid) return "rejected:agent_authority_changed";
        return adapter.CompleteDeployment();
    }

    private void CancelControls()
    {
        ClearHullTargets();
        nativeControlsReleased = false;
        adapter?.CancelControls();
    }

    public override void OnMissionTick(float dt)
    {
        try { TickMission(dt); }
        catch (Exception exception) { Fail(exception.ToString()); }
    }

    private void TickMission(float dt)
    {
        Interlocked.Increment(ref callback);
        if (!TickLifecycle()) return;
        TickNativeControls();
        bool fixtureReady = released && session.HostEpoch == 1 && OriginalOwnersReady && adapter.Blocker == null;
        if (!fixtureReady) CancelControls();
        adapter.SetAuthority(fixtureReady);
        if (adapter.Blocker != null)
        {
            fixtureReady = false;
            CancelControls();
        }
        if (adapter.Blocker != null && !faultReported)
        {
            faultReported = true;
            released = false;
            relay.SendAll(new NetworkNavalLabFault(manifest.IncarnationId, adapter.Blocker));
        }
        TickFollowerHull(dt);
        (adapter as INavalPresentationAdapter)?.TickPresentation(dt);
        adapter.TickAgentControl(dt);
        if (adapter.Blocker != null) throw new InvalidOperationException(adapter.Blocker);
        sendTime += dt;
        if (fixtureReady && NativeControlsReady && sendTime >= 0.05f)
        {
            sendTime = 0;
            SendOwnedShip();
        }
        base.OnMissionTick(dt);
    }

    protected override void SendJoinInfo(string controllerId) { }
    protected override void HandleJoinInfo(NetPeer peer, NetworkMissionJoinInfo joinInfo) { }
    protected override void OnLeaving()
    {
        coopMissionComponent.AgentMovementHandler.ConfigureNavalStationMovement(null);
        Hold("leaving");
        try
        {
            if (entered)
            {
                entered = false;
                relay.SendAll(new NetworkMissionLeft(session.OwnControllerId, manifest.InstanceId));
            }
        }
        finally
        {
            if (manifest != null)
                foreach (var combatant in manifest.Combatants) coopMissionComponent.AgentRegistry.RemoveAgent(combatant);
            if (networkStarted)
            {
                networkStarted = false;
                try { network.Stop(); }
                finally { context.EndInstance(); }
            }
        }
    }
    public override void OnMissionStateFinalized()
    {
        // Keep damage guards installed through native agent and mission-object teardown.
        onEnd?.Invoke();
        base.OnMissionStateFinalized();
    }
    public void AbortStart()
    {
        if (startAborted) return;
        startAborted = true;
        try { OnLeaving(); }
        finally
        {
            DisposeMissionHandlers();
            Dispose();
        }
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        coopMissionComponent.AgentMovementHandler.ConfigureNavalStationMovement(null);
        Hold("disposed");
        messageBroker.Unsubscribe<NetworkNavalLabShipSample>(OnShipSample);
        messageBroker.Unsubscribe<NetworkNavalLabStations>(OnStations);
        messageBroker.Unsubscribe<NetworkNavalLabHelmOccupancy>(OnHelmOccupancy);
        messageBroker.Unsubscribe<NetworkNavalLabRopeFinal>(OnFinalRopes);
        base.Dispose();
    }
}
#endif
