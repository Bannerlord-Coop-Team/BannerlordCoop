using Common;
using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using LiteNetLib;
using Missions.Agents.Packets;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IAgentMovementHandler : IPacketHandler, IDisposable
{
}

public class AgentMovementHandler : IAgentMovementHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgentMovementHandler>();

    // Broadcast every locally-controlled agent's movement on a ~10ms cadence. Poller keeps the loop
    // alive even if a tick throws (a raw fire-and-forget Task would silently die — see
    // poller-swallows-exceptions).
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(10);

    // Max agents per movement packet. The host has authority over every AI troop, so its batch can be
    // dozens of agents — one packet for all of them overflows the unreliable MTU ceiling (LiteNetLib
    // throws TooBigPacketException on oversized non-fragmentable sends, which the Poller swallows). The MTU
    // can stay near its ~1 KB floor (no negotiation up over P2P), and a mounted/well-equipped agent can run
    // a few hundred bytes, so keep the chunk small enough that the common case fits one unreliable packet;
    // the send path promotes any chunk that still overflows to a fragmentable reliable channel.
    private const int MaxAgentsPerMovementPacket = 4;

    // Cap on how long Dispose blocks joining the poll thread; matches the game's 30s BlockingTimeout so it only trips if a poll is genuinely wedged.
    private static readonly TimeSpan PollerStopTimeout = TimeSpan.FromSeconds(30);

    private readonly Poller poller;
    private readonly IPacketManager packetManager;
    private readonly IBattleNetwork client;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;

    // A puppet's horse, remembered when its owner dismounts, so a later re-mount can put it back on the
    // same one. Touched only on the game thread (inside HandlePacket's apply), so no lock; per-mission
    // (this handler is transient), so it can't leak across missions.
    private readonly Dictionary<Agent, Agent> _dismountedHorses = new Dictionary<Agent, Agent>();

    private bool _disposed;

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

        poller = new Poller(PollAgents, PollingInterval);
        poller.Start();
    }

    ~AgentMovementHandler()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Logger.Verbose("Disposing {handlerType}", typeof(AgentMovementHandler));

        packetManager.RemovePacketHandler(this);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);

        // Join the poll thread, not just cancel it: an in-flight PollAgents must finish before teardown frees the agents it reads.
        poller.StopAndWait(PollerStopTimeout);
        GC.SuppressFinalize(this);
    }

    public PacketType PacketType => PacketType.Movement;

    // Broadcast the movement of every agent the local node currently has authority over: its own party,
    // plus any party it has assumed control of as host.
    private void PollAgents(TimeSpan delta)
    {
        if (Mission.Current == null) return;

        // Collect every agent we have authority over, then broadcast them in MTU-safe chunks. One packet
        // per agent floods the mesh and the receiver's game-thread queue at battle scale (the GameThread
        // lockup); one packet for ALL of them overflows the unreliable MTU ceiling.
        var ids = new List<Guid>();
        var data = new List<AgentData>();

        foreach (var agentInfo in agentRegistry.GetAgents(controllerIdProvider.ControllerId))
        {
            Agent agent = agentInfo.Agent;
            // Skip agents whose native object is already gone (dead/removed but not yet deregistered):
            // building AgentData calls into the agent (GetCurrentActionType, etc.), which throws an
            // AccessViolationException on a freed agent. Mirrors the IsActive() guard on the apply path.
            if (agent != null && agent.Mission != null && agent.IsActive())
            {
                ids.Add(agentInfo.AgentId);
                data.Add(new AgentData(agent));
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
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        var movement = (MovementPacket)packet;
        if (movement.AgentIds == null) return;

        // Resolve the agents to apply (skipping our own) on the network thread, then apply the whole
        // batch in ONE game-thread action — a RunSafe per agent floods the queue at battle scale.
        var toApply = new List<(Agent agent, AgentData data)>();
        for (int i = 0; i < movement.AgentIds.Length; i++)
        {
            var agentId = movement.AgentIds[i];
            if (agentRegistry.IsLocallyControlled(agentId)) continue;
            if (!agentRegistry.TryGetAgentInfo(agentId, out var agentInfo)) continue;
            toApply.Add((agentInfo.Agent, movement.Agents[i]));
        }

        if (toApply.Count == 0) return;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            using (new AllowedThread())
            {
                foreach (var (agent, data) in toApply)
                {
                    // The agent may have become invalid (player left, mission torn down) between queueing
                    // and running; only apply while it is still active in the current mission.
                    if (agent == null || agent.Mission != Mission.Current || agent.IsActive() == false)
                        continue;

                    SyncMountState(agent, data);
                    data.Apply(agent);
                }
            }
        });
    }

    // [Game thread] Replicate the owner's mount/dismount onto its puppet. The per-tick AgentData reports
    // whether the owner is mounted (MountData != null); without acting on the transition a puppet stays
    // stuck on its horse after the owner dismounts (and never re-mounts). MountAgent is set directly
    // (controller-independent — puppets have no controller to process a mount/dismount input flag); the
    // movement sync then keeps the rider/horse positioned. AgentData.Apply still syncs the mount's pose
    // while both are mounted.
    private void SyncMountState(Agent agent, AgentData data)
    {
        bool ownerMounted = data.MountData != null;

        if (!ownerMounted && agent.HasMount)
        {
            // Owner dismounted: get the puppet off the horse. Remember the horse for a possible re-mount.
            _dismountedHorses[agent] = agent.MountAgent;
            agent.MountAgent = null;
        }
        else if (ownerMounted && !agent.HasMount)
        {
            // Owner re-mounted: put the puppet back on the horse it left, if it's still around and free.
            if (_dismountedHorses.TryGetValue(agent, out var horse) && horse != null && horse.IsActive() && horse.RiderAgent == null)
                agent.MountAgent = horse;
            _dismountedHorses.Remove(agent);
        }
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

        // In a coop field battle a departing player's troops are NOT despawned: the host adopts them
        // (CoopBattleController.HandlePeerGone) so they keep fighting under host AI, and other peers keep
        // them as puppets that follow the host's movement. Skip the location-style cleanup entirely.
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
                    if (agent != null && agent.Health > 0)
                    {
                        agent.MakeDead(false, ActionIndexCache.act_none);
                        agent.FadeOut(false, true);
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
