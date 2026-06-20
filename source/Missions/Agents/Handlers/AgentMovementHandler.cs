using Common;
using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Util;
using GameInterface.Missions.Agents.Packets;
using GameInterface.Missions.Services.Network;
using GameInterface.Missions.Services.Network.Messages;
using GameInterface.Services.Entity;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Agents.Handlers
{
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

        private readonly Poller poller;
        private readonly IPacketManager packetManager;
        private readonly IBattleNetwork client;
        private readonly IMessageBroker messageBroker;
        private readonly INetworkAgentRegistry agentRegistry;
        private readonly IControllerIdProvider controllerIdProvider;

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
            Logger.Verbose("Disposing {handlerType}", typeof(AgentMovementHandler));

            packetManager.RemovePacketHandler(this);
            messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
            messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
            messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
            poller.Stop();
        }

        public PacketType PacketType => PacketType.Movement;

        // Broadcast the movement of every agent the local node currently has authority over: its own party,
        // plus any party it has assumed control of as host.
        private void PollAgents(TimeSpan delta)
        {
            if (Mission.Current == null) return;

            foreach (var agentInfo in agentRegistry.GetAgents(controllerIdProvider.ControllerId))
            {
                Agent agent = agentInfo.Agent;
                if (agent != null && agent.Mission != null)
                {
                    client.SendAll(new MovementPacket(agentInfo.AgentId, agent));
                }
            }
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            MovementPacket movement = (MovementPacket)packet;

            if (agentRegistry.IsLocallyControlled(movement.AgentId))
                return;

            if (!agentRegistry.TryGetAgentInfo(movement.AgentId, out var agentInfo))
                return;

            Agent agent = agentInfo.Agent;
            GameThread.RunSafe(() =>
            {
                // Queued from the network thread and run a frame later: by then the agent may be invalid
                // (the player left, or the mission was torn down). Only apply while it is still active in the
                // current mission.
                if (Mission.Current == null || agent == null || agent.Mission != Mission.Current || agent.IsActive() == false)
                    return;

                using (new AllowedThread())
                {
                    movement.Apply(agent);
                }
            });
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
}
