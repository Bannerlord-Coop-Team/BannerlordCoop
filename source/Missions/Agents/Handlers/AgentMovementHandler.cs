using Common;
using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using LiteNetLib;
using Missions.Agents;
using Missions.Agents.Packets;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using AgentControllerType = TaleWorlds.Core.AgentControllerType;

namespace Missions.Agents.Handlers;

public interface IAgentMovementHandler : IPacketHandler, IDisposable
{
    /// <summary>
    /// [Game thread] Capture owned agents' continuous movement state and broadcast it to peers.
    /// </summary>
    void PollMovement(float dt);

    /// <summary>Per-frame position smoother for received puppets; ticked by CoopMissionController.OnMissionTick.</summary>
    IAgentPositionInterpolator Interpolator { get; }

    /// <summary>Receive side for masterless-horse movement (<see cref="MountMovementPacket"/>); the send side
    /// is this handler's movement tick. Exposed so the packet flow is reachable in tests.</summary>
    IPacketHandler MountMovementApplier { get; }
}

public class AgentMovementHandler : IAgentMovementHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgentMovementHandler>();

    // Movement is strictly droppable, so keep even mounted snapshots below the 1 KB unreliable ceiling.
    private const int MaxAgentsPerMovementPacket = 3;

    // Forty updates per second keeps locally authoritative agents responsive.
    private const float MovementPollingIntervalSeconds = 0.025f;

    private readonly IPacketManager packetManager;
    private readonly IBattleNetwork client;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;

    // A puppet's horse, remembered when its owner dismounts, so a later re-mount can put it back on the
    // same one. Touched only on the game thread (inside HandlePacket's apply), so no lock; per-mission
    // (this handler is transient), so it can't leak across missions.
    private readonly Dictionary<Agent, Agent> _dismountedHorses = new Dictionary<Agent, Agent>();

    // Per-frame position smoothing for received puppets. Fed the latest target on each packet apply (below) and
    // ticked from CoopMissionController.OnMissionTick, so the ease is decoupled from the bursty poll cadence.
    private readonly AgentPositionInterpolator _interpolator = new AgentPositionInterpolator();
    public IAgentPositionInterpolator Interpolator => _interpolator;

    // Masterless-horse movement receive side. Owned here (registered/removed with this handler) so both
    // movement streams share one deterministic lifecycle; PollMovement is its send side.
    private readonly MountMovementApplier _mountMovementApplier;
    public IPacketHandler MountMovementApplier => _mountMovementApplier;

    // Dispose is called deterministically on mission teardown (CoopMissionController.OnEndMissionInternal); this
    // guards against a second call (the GC finalizer, or the DI scope also disposing this transient handler).
    private bool _disposed;
    private float movementPollElapsed = MovementPollingIntervalSeconds;

    public AgentMovementHandler(
        IBattleNetwork client,
        IPacketManager packetManager,
        IMessageBroker messageBroker,
        INetworkAgentRegistry agentRegistry,
        IControllerIdProvider controllerIdProvider)
    {
        Logger.Verbose("Creating {handlerType}", typeof(AgentMovementHandler));

        this.packetManager = packetManager;
        this.client = client;
        this.messageBroker = messageBroker;
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;

        // Server-mediated membership. A peer entering is the cue to clear any STALE party it left behind
        // on a missed disconnect (so its rejoin re-spawns clean); a leave/disconnect releases its party.
        this.messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        this.messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        this.messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);

        this.packetManager.RegisterPacketHandler(this);

        _mountMovementApplier = new MountMovementApplier(agentRegistry, _interpolator);
        this.packetManager.RegisterPacketHandler(_mountMovementApplier);
    }

    // Safety net only: with deterministic disposal on mission end the finalizer is suppressed and never runs.
    ~AgentMovementHandler()
    {
        Dispose();
    }

    /// <summary>
    /// Deterministic teardown, called from <c>CoopMissionController.OnEndMissionInternal</c> at the start of the
    /// leave path. Detaches from the packet manager and message broker. Idempotent — safe if the GC finalizer or
    /// the DI scope disposes this handler again.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Logger.Verbose("Disposing {handlerType}", typeof(AgentMovementHandler));

        _interpolator.Clear();

        packetManager.RemovePacketHandler(this);
        packetManager.RemovePacketHandler(_mountMovementApplier);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);

        // Disposed explicitly, so the finalizer no longer needs to run.
        GC.SuppressFinalize(this);
    }

    public PacketType PacketType => PacketType.Movement;

    // Broadcast every locally authoritative agent.
    public void PollMovement(float dt)
    {
        if (_disposed || Mission.Current == null) return;

        movementPollElapsed += dt;
        if (movementPollElapsed < MovementPollingIntervalSeconds) return;
        movementPollElapsed %= MovementPollingIntervalSeconds;

        var ids = new List<Guid>();
        var data = new List<AgentData>();
        List<Guid> mountIds = null;
        List<AgentMountData> mountData = null;

        foreach (var agentInfo in agentRegistry.GetAgents(controllerIdProvider.ControllerId))
        {
            Agent agent = agentInfo.Agent;
            // Skip agents whose native object is already gone (dead/removed but not yet deregistered):
            // building the snapshot calls into the agent, which can access-violate after native teardown.
            if (agent == null || agent.Mission == null || !agent.IsActive()) continue;

            EnsureLocallyDrivenMountController(agent);
            if (!ShouldBroadcastMovement(agent)) continue;

            if (agent.IsMount)
            {
                (mountIds ??= new List<Guid>()).Add(agentInfo.AgentId);
                (mountData ??= new List<AgentMountData>()).Add(new AgentMountData(
                    agent,
                    mountAction0Speed: AgentMountData.GetRenderedAction0Speed(agent)));
            }
            else
            {
                ids.Add(agentInfo.AgentId);
                Agent mount = agent.MountAgent;
                float? mountAction0Speed = mount != null && mount.IsActive()
                    ? AgentMountData.GetRenderedAction0Speed(mount)
                    : null;
                data.Add(new AgentData(
                    agent,
                    GetRegisteredMountId(agent),
                    mountAction0Speed));
            }
        }

        for (int start = 0; start < ids.Count; start += MaxAgentsPerMovementPacket)
        {
            int count = Math.Min(MaxAgentsPerMovementPacket, ids.Count - start);
            var idChunk = new Guid[count];
            var dataChunk = new AgentData[count];
            ids.CopyTo(start, idChunk, 0, count);
            data.CopyTo(start, dataChunk, 0, count);
            client.SendAll(new MovementPacket(idChunk, dataChunk));
        }

        if (mountIds == null) return;

        for (int start = 0; start < mountIds.Count; start += MaxAgentsPerMovementPacket)
        {
            int count = Math.Min(MaxAgentsPerMovementPacket, mountIds.Count - start);
            var idChunk = new Guid[count];
            var dataChunk = new AgentMountData[count];
            mountIds.CopyTo(start, idChunk, 0, count);
            mountData.CopyTo(start, dataChunk, 0, count);
            client.SendAll(new MountMovementPacket(idChunk, dataChunk));
        }
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        var movement = (MovementPacket)packet;
        if (movement.AgentIds == null) return;

        // Resolve and apply the whole batch in ONE game-thread action. Resolving here keeps this ordered behind
        // earlier game-thread spawn/register work that may have been queued by reliable messages.
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            using (new AllowedThread())
            {
                for (int i = 0; i < movement.AgentIds.Length; i++)
                {
                    var agentId = movement.AgentIds[i];
                    if (agentRegistry.IsLocallyControlled(agentId)) continue;
                    if (!agentRegistry.TryGetAgentInfo(agentId, out var agentInfo)) continue;

                    Agent agent = agentInfo.Agent;
                    AgentData data = movement.Agents[i];

                    // The agent may have become invalid (player left, mission torn down) between queueing
                    // and running; only apply while it is still active in the current mission.
                    if (agent == null || agent.Mission != Mission.Current || agent.IsActive() == false)
                        continue;

                    // Re-check authority ON the game thread: a packet from the previous owner can be queued
                    // behind a host-migration adoption (both are game-thread actions queued from the network
                    // thread), and applying it after the transfer would re-pin the freshly adopted agent to a
                    // stale position/input snapshot the AI then fights.
                    if (agentRegistry.IsLocallyControlled(agent))
                        continue;

                    SyncMountState(agent, data);

                    // A puppet horse must not run local AI between owner snapshots and fight their heading/input.
                    if (agent.MountAgent is Agent puppetMount && puppetMount.Controller != AgentControllerType.None)
                        puppetMount.Controller = AgentControllerType.None;

                    data.Apply(agent);

                    // Position is reconciled per-frame by the interpolator (smoother than a per-packet
                    // correction bound to the ~10ms poll cadence); push the latest targets it eases toward.
                    if (agent.HasMount && data.MountData != null)
                    {
                        // Mounted: feed target frames to the rider, since the mount is driven through its rider.
                        // Keep the mount position only for rare large-gap snaps and drop any stale direct-horse
                        // target from before the puppet mounted.
                        _interpolator.Forget(agent.MountAgent);
                        _interpolator.SetMountedRiderTarget(
                            agent,
                            data.Position,
                            data.MovementDirection,
                            data.MountData.MountMovementDirection,
                            data.MountData.MountPosition);
                    }
                    else
                    {
                        _interpolator.SetRiderTarget(agent, data.Position, data.MovementDirection);
                    }
                }
            }
        });
    }

    // [Game thread] Replicate the owner's mount/dismount onto its puppet. The per-tick AgentData reports
    // whether the owner is mounted (MountData != null) and WHICH horse (MountId, when it's registered);
    // without acting on the transition a puppet stays stuck on its horse after the owner dismounts (and
    // never re-mounts). MountAgent is set directly (controller-independent — puppets have no controller to
    // process a mount/dismount input flag); the movement sync then keeps the rider/horse positioned.
    // AgentData.Apply still syncs the mount's pose while both are mounted.
    private void SyncMountState(Agent agent, AgentData data)
    {
        bool ownerMounted = data.MountData != null;

        if (!ownerMounted && agent.HasMount)
        {
            // Owner dismounted: get the puppet off the horse. Remember the horse for a possible re-mount, and
            // stop interpolating it (its target is no longer being reported).
            Agent horse = agent.MountAgent;
            _dismountedHorses[agent] = horse;
            _interpolator.Forget(horse);
            agent.MountAgent = null;
            RestoreLocallyControlledMount(horse);
        }
        else if (ownerMounted && !agent.HasMount)
        {
            // Owner (re)mounted: prefer the exact horse it reports (registered mounts carry their id); fall
            // back to the one the puppet last left for unregistered horses.
            Agent horse = ResolveRegisteredHorse(data.MountData.MountId);
            if (horse == null) _dismountedHorses.TryGetValue(agent, out horse);
            if (horse != null && horse.IsActive() && horse.RiderAgent == null)
                agent.MountAgent = horse;
            _dismountedHorses.Remove(agent);
        }
        else if (ownerMounted && agent.HasMount)
        {
            // Owner switched horses (dismount + different re-mount inside one poll interval): the reported
            // mount id no longer matches the horse the puppet sits on — move it over so damage routed by the
            // horse's id keeps hitting what players actually see.
            Agent reported = ResolveRegisteredHorse(data.MountData.MountId);
            if (reported != null && !ReferenceEquals(reported, agent.MountAgent)
                && reported.IsActive() && reported.RiderAgent == null)
            {
                Agent previous = agent.MountAgent;
                _interpolator.Forget(previous);
                agent.MountAgent = reported;
                RestoreLocallyControlledMount(previous);
            }
        }
    }

    // A locally driven rider needs a live horse controller; so does a locally authoritative loose horse.
    // Do not wake a locally owned horse while another controller's active rider is driving it remotely.
    private void EnsureLocallyDrivenMountController(Agent agent)
    {
        Agent mount = agent.IsMount ? agent : agent.MountAgent;
        if (mount == null || !mount.IsActive() || mount.Mission != Mission.Current) return;
        if (mount.RiderAgent is Agent rider && rider.IsActive()
            && !agentRegistry.IsLocallyControlled(rider)) return;
        if (mount.Controller != AgentControllerType.AI)
        {
            mount.SetMaximumSpeedLimit(-1f, isMultiplier: false);
            mount.Controller = AgentControllerType.AI;
        }
    }

    private void RestoreLocallyControlledMount(Agent mount)
    {
        if (mount == null || !mount.IsActive() || mount.Mission != Mission.Current) return;
        if (agentRegistry.TryGetAgentInfo(mount, out _)
            && !agentRegistry.IsLocallyControlled(mount)) return;
        mount.SetMaximumSpeedLimit(-1f, isMultiplier: false);
        if (mount.Controller != AgentControllerType.AI)
            mount.Controller = AgentControllerType.AI;
    }

    /// <summary>
    /// Whether an owned, active, registered agent's movement is broadcast as its OWN packet. Troops always
    /// are. A registered MOUNT is only while it has no live rider: a ridden horse's pose rides in its rider's
    /// MountData (a second stream would fight it), but a masterless one has nothing else driving it — without
    /// its own packets each client's local horse AI wanders its copy and the positions diverge, so its owner
    /// stays authoritative over it the same way it is for a troop. (Public and static so the selection rule is
    /// testable headless; called under the poll's IsActive guard.)
    /// </summary>
    public static bool ShouldBroadcastMovement(Agent agent)
    {
        if (!agent.IsMount) return true;
        return !(agent.RiderAgent is Agent rider && rider.IsActive());
    }

    // The local agent behind a mount's network id; null when the id is empty/unknown or resolves to a
    // non-mount (a stale id after the registry entry was replaced).
    private Agent ResolveRegisteredHorse(Guid mountId)
    {
        if (mountId == Guid.Empty) return null;
        if (!agentRegistry.TryGetAgentInfo(mountId, out var info)) return null;
        return info.Agent != null && info.Agent.IsMount ? info.Agent : null;
    }

    // The registry id of the agent's current mount, or Guid.Empty when on foot / the horse isn't registered.
    private Guid GetRegisteredMountId(Agent agent)
    {
        var mount = agent.MountAgent;
        if (mount != null && agentRegistry.TryGetAgentInfo(mount, out var mountInfo))
            return mountInfo.AgentId;
        return Guid.Empty;
    }

    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        // Defensive: if this controller still has a party registered, we missed its earlier departure —
        // clear it so the fresh join re-spawns instead of being deduped as "already registered".
        RemoveControllerParty(payload.What.ControllerId, "peer entered (stale cleanup)");
    }

    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        RemoveControllerParty(payload.What.ControllerId, "peer left");
    }

    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        RemoveControllerParty(payload.What.ControllerId, "peer disconnected");
    }

    // Despawn and deregister every agent of the controller's party/parties, then drop the parties. A
    // self id is ignored (our own party is managed locally). No-ops when nothing is registered, and is
    // idempotent — so the mesh NetworkLeaveMission path and this server-mediated path can both fire.
    private void RemoveControllerParty(string controllerId, string reason)
    {
        if (string.IsNullOrEmpty(controllerId)) return;

        if (controllerId == controllerIdProvider.ControllerId) return;

        // BattleAuthorityMigrator owns battle withdrawal because it can distinguish the player's party from
        // NPC forces the departed host was running. Skip this location-style all-controller cleanup.
        if (BattleSpawnGate.IsCoopBattleActive) return;

        bool sceneActive = Mission.Current != null;

        int removedAgentCount = 0;
        foreach (var agentInfo in agentRegistry.GetAgents(controllerId))
        {
            // Fade the visible agent out only when a scene is still active — a disconnect on leave
            // often arrives mid-teardown with Mission.Current already null.
            if (sceneActive)
            {
                Agent agent = agentInfo.Agent;
                GameThread.RunSafe(() =>
                {
                    if (Mission.Current == null) return;
                    if (agent != null && agent.IsActive() && agent.Health > 0)
                    {
                        bool hideMount = agent.HasMount && agent.MountAgent != null && agent.MountAgent.IsActive();
                        agent.FadeOut(false, hideMount);
                    }
                });
            }

            agentRegistry.RemoveAgent(agentInfo.Agent);
            removedAgentCount++;
        }
        Logger.Information("[LocationSync] {reason} {ControllerId}: removed {AgentCount} agents (fadedOut={fadedOut})",
            reason, controllerId, removedAgentCount, sceneActive);
    }
}
