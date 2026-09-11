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
using TaleWorlds.Library;

namespace Missions.Battles;

public interface INavalLabController : IDisposable
{
    void Start(NavalLabManifest manifest, INavalMissionAdapter adapter, Action onEnd);
    void AbortStart();
    string Apply(NetworkNavalLabAction action);
    object Samples(long afterSequence);
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
    private long sequence;
    private long lastReceived;
    private long lastApplied;
    private float sendTime;
    private double inputDeadline;
    private readonly INavalLabMeasurement measurement;
    private long callback;
    private long receivedGaps;
    private long rejectedFrames;
    private long supersededSamples;
    private long rejectedSamples;
    private long pendingReceivedCallback;
    private long pendingAppliedCallback;
    private NetworkNavalLabFrames pendingSample;
    private double nextSample;
    private bool probeDriving;
    private int probeShip;
    private float probeRudder;
    private bool probeRow;
    private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    public object Samples(long afterSequence) => new
    {
        incarnationId = manifest?.IncarnationId, controller = session.OwnControllerId,
        nativeControls = IsTwoClientNative ? new { nativeControlsReleased, nativeInputSequences, nativeInputDeadlines, stationsAcknowledged = acknowledgedStations.Count } : null,
        currentEpoch = session.HostEpoch, localCallback = callback, lastReceivedSequence = lastReceived, lastAppliedSequence = lastApplied,
        receivedGaps, rejectedFrames, supersededSamples, rejectedSamples, measurement = measurement.Read(afterSequence),
        factoryProbe = IsFactoryProbe ? new { factorySceneReady, factoryAttempted, factoryHydrated, factoryTerminal, factoryHost, factoryControlCleanupFailure, factoryHoldFailure } : null
    };

    public NavalLabController(IBattleNetwork network, INetwork relay, IMessageBroker broker,
        IObjectManager objects, ICoopMissionComponent component, IControllerIdProvider own,
        IBattleHostRegistry hosts, IMissionContext context, INavalLabMeasurement measurement)
        : base(network, broker, objects, component, MovementCadenceProfile.Battle)
    {
        this.relay = relay;
        this.context = context;
        this.measurement = measurement;
        this.hosts = hosts;
        session = new BattleSession(own, hosts);
        broker.Subscribe<NetworkNavalLabFrames>(ReceiveFrames);
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
        if (IsFactoryProbe) factoryDeadline = Now + 30;
        adapter.Open(manifest, this, session.OwnControllerId);
        ConfigureNativeAdapter();
    }

    public override void AfterStart()
    {
        if (IsFactoryProbe)
        {
            if (factorySceneReady || factoryTerminal) return;
            base.AfterStart();
            factorySceneReady = true;
            messageBroker.Publish(this, new BattleMissionReady(manifest.InstanceId));
            relay.SendAll(new NetworkNavalLabReceipt(manifest.IncarnationId, manifest.IncarnationId, "scene_ready"));
            return;
        }
        if (adapter.Blocker != null || adapter.Agents.Length != manifest.Combatants.Length || Array.Exists(adapter.Agents, agent => agent == null))
        {
            faultReported = true;
            relay.SendAll(new NetworkNavalLabReceipt(manifest.IncarnationId, manifest.IncarnationId,
                "failed:" + (adapter.Blocker ?? "incomplete native crew")));
            return;
        }
        base.AfterStart();
        RegisterFixtureAgents();
        messageBroker.Publish(this, new BattleMissionReady(manifest.InstanceId));
        relay.SendAll(new NetworkNavalLabReceipt(manifest.IncarnationId, manifest.IncarnationId, "ready"));
    }

    private void RegisterFixtureAgents()
    {
        coopMissionComponent.AgentRegistry.Clear();
        for (int i = 0; i < adapter.Agents.Length; i++)
        {
            if (!coopMissionComponent.AgentRegistry.TryRegisterAgent(
                manifest.Controllers[i / NavalLabManifest.CrewPerShip], manifest.Combatants[i],
                (ushort)(i + 1), adapter.Agents[i], 1))
                throw new InvalidOperationException("Cannot register a synthetic combatant.");
        }
    }

    private bool OriginalOwnersReady => manifest != null && hosts.TryGet(manifest.InstanceId, out var host)
        && manifest.Controllers.All(id => id == host.HostControllerId || host.SuccessorControllerIds.Contains(id));

    public bool HasSingleClientAuthority => manifest?.Mode == NavalLabMode.SingleClientNative && !disposed
        && released && session.HostEpoch == 1 && session.IsLocalHost && OriginalOwnersReady
        && manifest.Combatants.All(id => coopMissionComponent.AgentRegistry.TryGetAgentInfo(id, out var info)
            && info.OriginalOwner == session.OwnControllerId && info.CurrentAuthority == session.OwnControllerId
            && info.AuthorityRevision == 1 && Array.IndexOf(adapter.Agents, info.Agent) == Array.IndexOf(manifest.Combatants, id));

    public string Apply(NetworkNavalLabAction action)
    {
        if (manifest == null || action.IncarnationId != manifest.IncarnationId || disposed)
            return "rejected:stale_incarnation";
        if (action.Kind == "stop" || action.Kind == "hold")
        {
            if (IsFactoryProbe)
            {
                try { HoldFactoryProbe(action.Kind); }
                finally { if (action.Kind == "stop") Mission.EndMission(); }
                if (factoryControlCleanupFailure != null || factoryHoldFailure != null)
                    return "failed:factory_probe.terminal_cleanup";
                return action.Kind == "stop" ? "applied" : "held";
            }
            CancelControls(action.Kind);
            released = false;
            HoldAdapter();
            if (action.Kind == "stop") Mission.EndMission();
            return action.Kind == "stop" ? "applied" : "held";
        }
        if (action.Epoch != session.HostEpoch || session.HostEpoch != 1) return "rejected:stale_epoch";
        if (adapter.Blocker != null) return "rejected:fixture_blocked";
        if (IsFactoryProbe && factoryTerminal) return "rejected:fixture_blocked";
        if (action.Kind == "release")
        {
            if (IsFactoryProbe && (!factoryHydrated || !FactoryAssignmentValid)) return "rejected:not_hydrated";
            if (IsFactoryProbe && !released) factoryDeadline = Now + 120;
            released = true;
            return "applied";
        }
        if (!released || !OriginalOwnersReady) return "rejected:not_released_or_owner_departed";
        if (IsFactoryProbe && !FactoryAssignmentValid) return "rejected:factory_assignment_changed";
        if (IsTwoClientNative && action.Kind == "native-controls-ready")
        {
            ReleaseNativeControls();
            return NativeControlsReady ? "controls_ready" : "failed:native_release";
        }
        if (!IsTwoClientNative && (action.Kind == "native-axes-pulse" || action.Kind == "native-take-helm" || action.Kind == "native-release-helm"))
            return "rejected:wrong_mode";
        if (IsTwoClientNative)
        {
            if (action.Kind == "native-axes-pulse")
            {
                if (action.Ship < 0 || action.Ship >= 2 || manifest.Controllers[action.Ship] != session.OwnControllerId
                    || !NativeControlsReady) return "rejected:owner_not_ready";
                if (float.IsNaN(action.Rudder) || float.IsInfinity(action.Rudder) || Math.Abs(action.Rudder) > 1)
                    return "rejected:invalid_control";
                if (action.DeadlineUtcTicks <= DateTime.UtcNow.Ticks || action.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(1).Ticks)
                    return "rejected:expired_control";
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
        if (manifest.Mode == NavalLabMode.SingleClientNative)
        {
            if (action.Kind != "complete-deployment") return "rejected:wrong_mode";
            if (action.Ship != 0 || action.Rudder != 0 || action.Row) return "rejected:invalid_control";
            if (!HasSingleClientAuthority) return "rejected:agent_authority_changed_or_unavailable";
            return adapter.CompleteDeployment();
        }
        if (action.Ship < 0 || action.Ship >= manifest.Ships.Length || float.IsNaN(action.Rudder)
            || float.IsInfinity(action.Rudder) || Math.Abs(action.Rudder) > 1)
            return "rejected:invalid_control";
        bool heldHelm = action.Kind == "take-helm" || action.Kind == "release-helm";
        if ((manifest.Mode == NavalLabMode.HeldHelm) != heldHelm) return "rejected:wrong_mode";
        if (heldHelm && (action.Rudder != 0 || action.Row)) return "rejected:invalid_control";
        if (heldHelm && (action.DeadlineUtcTicks <= DateTime.UtcNow.Ticks
            || action.DeadlineUtcTicks > DateTime.UtcNow.AddSeconds(30).Ticks)) return "rejected:expired_control";
        if (action.Kind == "probe")
        {
            string status = measurement.Begin(action.OperationId, action.Epoch, Now);
            if (status != "applied") return status;
            if (pendingSample != null) { rejectedSamples++; pendingSample = null; }
            inputDeadline = 0;
            nextSample = Now;
            if (session.IsLocalHost) { adapter.SetHelm(0, 0, false); adapter.SetHelm(1, 0, false); }
            probeShip = action.Ship;
            probeRudder = action.Rudder;
            probeRow = action.Row;
            probeDriving = session.IsLocalHost;
            if (probeDriving) adapter.SetHelm(action.Ship, action.Rudder, action.Row);
            return status;
        }
        if (action.Kind == "helm")
        {
            if (!session.IsLocalHost) return "rejected:not_ship_host";
            if (probeDriving || measurement.Active(Now)) return "rejected:probe_driving";
            adapter.SetHelm(action.Ship, action.Rudder, action.Row);
            inputDeadline = Now + 1;
            return "applied";
        }
        if (manifest.Controllers[action.Ship] != session.OwnControllerId) return "rejected:not_original_owner";
        if (action.Row || ((action.Kind == "jump" || action.Kind == "crew") && action.Rudder != 0))
            return "rejected:invalid_control";
        int index = (action.Ship * NavalLabManifest.CrewPerShip) + (action.Kind == "crew" ? 1 : 0);
        if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(manifest.Combatants[index], out var info)
            || info.OriginalOwner != session.OwnControllerId || info.CurrentAuthority != session.OwnControllerId
            || info.AuthorityRevision != 1 || adapter.Agents.Length <= index || adapter.Agents[index] != info.Agent)
            return "rejected:agent_authority_changed_or_unavailable";
        if (heldHelm) return adapter.SetHeldHelm(action.Ship, action.Kind == "take-helm");
        return adapter.StartAgentControl(action.Kind, action.Ship, action.Rudder);
    }

    private void HoldAdapter()
    {
        if (manifest?.Mode == NavalLabMode.SingleClientNative || IsFactoryProbe) adapter?.Hold();
        else adapter?.SetAuthority(false);
    }

    private void CancelControls(string reason)
    {
        measurement.Cancel(reason);
        probeDriving = false;
        nativeControlsReleased = false;
        inputDeadline = 0;
        adapter?.CancelControls();
        CompletePending("cancelled:" + reason);
    }

    private void CompletePending(string error = null)
    {
        if (pendingSample == null) return;
        var sample = pendingSample;
        pendingSample = null;
        measurement.Add(sample.Sequence, sample.SourceCallback, pendingReceivedCallback, pendingAppliedCallback,
            error == null ? callback : (long?)null, sample.Frames, error == null ? CaptureNative() : null, error);
    }

    private object CaptureNative() => new
    {
        observed = adapter.Inspect(),
        authorities = manifest.Combatants.Select(id =>
        {
            bool found = coopMissionComponent.AgentRegistry.TryGetAgentInfo(id, out var info);
            return new { id, error = found ? null : "unavailable:registry_identity",
                currentAuthority = info?.CurrentAuthority, originalOwner = info?.OriginalOwner,
                authorityRevision = info?.AuthorityRevision, movementId = info?.MovementId };
        }).ToArray()
    };

    public override void OnMissionTick(float dt)
    {
        if (!IsFactoryProbe) { TickMission(dt); return; }
        try { TickMission(dt); }
        catch (Exception exception) { FailFactoryProbe(exception.ToString()); }
    }

    private void TickMission(float dt)
    {
        Interlocked.Increment(ref callback);
        if (IsFactoryProbe && !TickFactoryProbe()) return;
        TickNativeControls();
        bool fixtureReady = released && session.HostEpoch == 1 && OriginalOwnersReady && adapter.Blocker == null;
        if (!fixtureReady) CancelControls(adapter.Blocker ?? "held_or_epoch_changed");
        CompletePending();
        if (probeDriving && !measurement.Active(Now))
        {
            probeDriving = false;
            adapter.SetHelm(0, 0, false);
            adapter.SetHelm(1, 0, false);
        }
        measurement.Active(Now);
        // Activation can discover an inactive native body in this same callback.
        adapter.SetAuthority(fixtureReady && session.IsLocalHost && manifest.Mode != NavalLabMode.HeldHelm);
        if (manifest.Mode == NavalLabMode.SingleClientNative && (adapter.Blocker != null
            || (released && !HasSingleClientAuthority))) adapter.Hold();
        if (adapter.Blocker != null)
        {
            fixtureReady = false;
            CancelControls(adapter.Blocker);
        }
        if (adapter.Blocker != null && !faultReported)
        {
            faultReported = true;
            released = false;
            relay.SendAll(new NetworkNavalLabFault(manifest.IncarnationId, adapter.Blocker));
        }
        if (probeDriving) adapter.SetHelm(probeShip, probeRudder, probeRow);
        if (inputDeadline > 0 && Now >= inputDeadline)
        {
            inputDeadline = 0;
            adapter.SetHelm(0, 0, false);
            adapter.SetHelm(1, 0, false);
        }
        adapter.TickAgentControl(dt);
        if (IsFactoryProbe && adapter.Blocker != null) throw new InvalidOperationException(adapter.Blocker);
        sendTime += dt;
        if (fixtureReady && (manifest.Mode == NavalLabMode.Activation || IsFactoryProbe) && session.IsLocalHost && sendTime >= 0.05f)
        {
            sendTime = 0;
            var frames = adapter.ReadFrames();
            var values = new float[24];
            for (int i = 0; i < frames.Length; i++)
            {
                var vectors = new[] { frames[i].rotation.s, frames[i].rotation.f, frames[i].rotation.u, frames[i].origin };
                for (int j = 0; j < 4; j++)
                {
                    values[(i * 12) + (j * 3)] = vectors[j].x;
                    values[(i * 12) + (j * 3) + 1] = vectors[j].y;
                    values[(i * 12) + (j * 3) + 2] = vectors[j].z;
                }
            }
            bool sample = measurement.Active(Now) && Now >= nextSample;
            var message = new NetworkNavalLabFrames(manifest.IncarnationId, session.HostEpoch, ++sequence,
                values, callback, sample ? measurement.OperationId : Guid.Empty,
                IsTwoClientNative && NativeControlsReady ? NativeAdapter.ReadSailStates() : null,
                IsTwoClientNative && NativeControlsReady ? DateTime.UtcNow.AddSeconds(1).Ticks : 0);
            if (sample)
            {
                nextSample = Now + 0.5;
                measurement.Add(sequence, callback, null, null, callback, values, CaptureNative(), null);
            }
            network.SendAll(message);
        }
        base.OnMissionTick(dt);
    }

    private void ReceiveFrames(MessagePayload<NetworkNavalLabFrames> payload)
    {
        long receivedCallback = Interlocked.Read(ref callback);
        bool probeReadyAtReceive = !IsFactoryProbe || (factoryHydrated && released && !factoryTerminal);
        bool sailReadyAtReceive = NativeInputIngressReady;
        GameThread.RunSafe(() =>
        {
            var message = payload.What;
            if (disposed || !released || adapter.Blocker != null || manifest == null
                || (manifest.Mode != NavalLabMode.Activation && !IsFactoryProbe)
                || !probeReadyAtReceive || (IsFactoryProbe && (!factoryHydrated || factoryTerminal || !FactoryAssignmentValid)) || !OriginalOwnersReady
                || message.IncarnationId != manifest.IncarnationId || session.IsLocalHost
                || session.HostEpoch != 1 || message.Epoch != session.HostEpoch || message.Sequence <= lastReceived
                || message.SourceCallback <= 0 || message.Frames == null || message.Frames.Length != 24)
            {
                if (IsTwoClientNative && !session.IsLocalHost) NativeAdapter.ClearSailFeedback();
                rejectedFrames++; return;
            }
            foreach (var value in message.Frames)
                if (float.IsNaN(value) || float.IsInfinity(value)) { rejectedFrames++; return; }
            var frames = new MatrixFrame[2];
            for (int i = 0; i < 2; i++)
            {
                var vectors = new Vec3[4];
                for (int j = 0; j < 4; j++)
                {
                    int index = (i * 12) + (j * 3);
                    vectors[j] = new Vec3(message.Frames[index], message.Frames[index + 1], message.Frames[index + 2]);
                }
                frames[i] = new MatrixFrame(new Mat3(vectors[0], vectors[1], vectors[2]), vectors[3]);
            }
            receivedGaps += Math.Max(0, message.Sequence - lastReceived - 1);
            lastReceived = message.Sequence;
            bool applied;
            try { applied = adapter.ApplyFrames(frames); }
            catch (Exception exception)
            {
                CompletePending("interrupted_by_failed_apply");
                if (message.ProbeOperationId == measurement.OperationId && measurement.Active(Now))
                    measurement.Add(message.Sequence, message.SourceCallback, receivedCallback, null, null,
                        message.Frames, null, "failed:frame_apply:" + exception);
                if (IsFactoryProbe) FailFactoryProbe(exception.ToString());
                throw;
            }
            if (!applied)
            {
                rejectedFrames++;
                CompletePending("interrupted_by_failed_apply");
                if (message.ProbeOperationId == measurement.OperationId && measurement.Active(Now))
                    measurement.Add(message.Sequence, message.SourceCallback, receivedCallback, null, null,
                        message.Frames, null, "unavailable:frames_not_applied");
                if (IsFactoryProbe) FailFactoryProbe("factory_probe.frame_apply_refused");
                return;
            }
            if (pendingSample != null)
            {
                supersededSamples++;
                CompletePending("superseded_before_next_mission_callback");
            }
            lastApplied = message.Sequence;
            if (IsTwoClientNative)
            {
                if (sailReadyAtReceive && NativeControlsReady) NativeAdapter.ApplySailFeedback(message);
                else NativeAdapter.ClearSailFeedback();
            }
            if (message.ProbeOperationId != Guid.Empty)
            {
                if (message.ProbeOperationId == measurement.OperationId && measurement.Active(Now))
                {
                    pendingSample = message;
                    pendingReceivedCallback = receivedCallback;
                    pendingAppliedCallback = callback;
                }
                else rejectedSamples++;
            }
        }, context: nameof(ReceiveFrames));
    }

    protected override void SendJoinInfo(string controllerId) { }
    protected override void HandleJoinInfo(NetPeer peer, NetworkMissionJoinInfo joinInfo) { }
    protected override void OnLeaving()
    {
        if (IsFactoryProbe) HoldFactoryProbe("leaving");
        else
        {
            CancelControls("leaving");
            HoldAdapter();
        }
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
        if (IsFactoryProbe) HoldFactoryProbe("disposed");
        else
        {
            if (manifest?.Mode == NavalLabMode.SingleClientNative) adapter?.Hold();
            CancelControls("disposed");
        }
        messageBroker.Unsubscribe<NetworkNavalLabFrames>(ReceiveFrames);
        base.Dispose();
    }
}
#endif
