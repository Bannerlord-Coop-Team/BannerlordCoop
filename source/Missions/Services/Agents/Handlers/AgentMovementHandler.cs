using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Util;
using LiteNetLib;
using Missions.Services.Agents.Packets;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    public interface IAgentMovementHandler : IPacketHandler, IDisposable
    {
    }

    public class AgentMovementHandler : IAgentMovementHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();

        // Broadcast every controlled agent's movement on a ~10ms cadence. Poller keeps the loop alive even
        // if a tick throws (a raw fire-and-forget Task would silently die — see poller-swallows-exceptions).
        private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(10);

        private readonly Poller poller;
        private readonly IPacketManager packetManager;
        private readonly IMissionNetwork client;
        private readonly IMessageBroker messageBroker;
        private readonly INetworkAgentRegistry agentRegistry;

        public AgentMovementHandler(
            IMissionNetwork client,
            IPacketManager packetManager,
            IMessageBroker messageBroker,
            INetworkAgentRegistry agentRegistry)
        {
            Logger.Verbose("Creating {handlerType}", typeof(AgentMovementHandler));

            this.packetManager = packetManager;
            this.client = client;
            this.messageBroker = messageBroker;
            this.agentRegistry = agentRegistry;


            this.messageBroker.Subscribe<PeerDisconnected>(Handle_PeerDisconnect);
            this.messageBroker.Subscribe<PeerConnected>(Handle_PeerConnected);

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
            messageBroker.Unsubscribe<PeerDisconnected>(Handle_PeerDisconnect);
            messageBroker.Unsubscribe<PeerConnected>(Handle_PeerConnected);
            poller.Stop();
        }

        public PacketType PacketType => PacketType.Movement;

        private void PollAgents(TimeSpan delta)
        {
            if (Mission.Current == null) return;

            foreach (string guid in agentRegistry.ControlledAgents.Keys)
            {
                if (agentRegistry.ControlledAgents.TryGetValue(guid, out var agent))
                {
                    if (agent.Mission != null)
                    {
                        MovementPacket packet = new MovementPacket(guid, agent);
                        client.SendAll(packet);
                    }
                }
            }
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            if (agentRegistry.OtherAgents.TryGetValue(peer, out AgentGroupController agentGroupController))
            {
                MovementPacket movement = (MovementPacket)packet;
                agentGroupController.ApplyMovement(movement);
            }
        }

        public void Handle_PeerDisconnect(MessagePayload<PeerDisconnected> payload)
        {
            CleanupPeerAgents(payload.What.NetPeer, "disconnect");
        }

        private void Handle_PeerConnected(MessagePayload<PeerConnected> payload)
        {
            // We do NOT reliably receive OnPeerDisconnected when a peer leaves a location: the link is a
            // NAT-punched P2P connection and DisconnectPeers keeps the socket alive, so the leaver's
            // disconnect frequently never reaches us (logs show a 30s-old peer reconnecting with no
            // disconnect in between). A (re)connect for a peer that STILL has agents registered therefore
            // means we missed its disconnect — clean up the stale session here so its ghost agent is
            // removed and the rejoining player re-spawns instead of being deduped as "already registered".
            // A genuinely new peer has no agents yet, so this no-ops for first connections.
            CleanupPeerAgents(payload.What.Peer, "reconnect (missed disconnect)");
        }

        private void CleanupPeerAgents(NetPeer peer, string reason)
        {
            if (peer == null) return;

            bool sceneActive = Mission.Current != null;

            if (agentRegistry.OtherAgents.TryGetValue(peer, out AgentGroupController controller) == false)
            {
                // Quiet for the common case (fresh connect / duplicate with nothing to clear).
                Logger.Debug("[LocationSync] {reason} {peer}: no agents registered under this peer", reason, peer);
                return;
            }

            int agentCount = controller.ControlledAgents.Count;

            // Fade the visible agents out only when a mission scene is still active — a disconnect on
            // leave often arrives mid-teardown with Mission.Current already null.
            if (sceneActive)
            {
                foreach (var agent in controller.ControlledAgents.Values)
                {
                    GameLoopRunner.RunOnMainThread(() =>
                    {
                        if (agent.Health > 0)
                        {
                            agent.MakeDead(false, ActionIndexCache.act_none);
                            agent.FadeOut(false, true);
                        }
                    });
                }
            }

            // Always drop the peer's agents from the registry, even mid-teardown. Skipping this leaves a
            // stale entry so a rejoining player is deduped as "already registered" and never re-spawns.
            bool removed = agentRegistry.RemovePeer(peer);
            Logger.Information("[LocationSync] {reason} {peer}: cleared {count} agent(s) from registry (removed={removed}, fadedOut={fadedOut})",
                reason, peer, agentCount, removed, sceneActive);
        }
    }
}
