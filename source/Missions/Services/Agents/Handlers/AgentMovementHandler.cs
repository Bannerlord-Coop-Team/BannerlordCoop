using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using LiteNetLib;
using Missions.Services.Agents.Packets;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    public interface IAgentMovementHandler : IPacketHandler, IDisposable
    {
    }

    public class AgentMovementHandler : IAgentMovementHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();

        private readonly CancellationTokenSource agentPollingCancelToken = new CancellationTokenSource();
        private readonly Task agentPollingTask;
        private readonly IPacketManager packetManager;
        private readonly INetwork client;
        private readonly IMessageBroker messageBroker;
        private readonly INetworkAgentRegistry agentRegistry;

        public AgentMovementHandler(
            INetwork client,
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

            this.packetManager.RegisterPacketHandler(this);

            agentPollingTask = Task.Factory.StartNew(PollAgents);
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
            agentPollingCancelToken.Cancel();
            agentPollingTask.Wait();
        }

        public PacketType PacketType => PacketType.Movement;

        private async void PollAgents()
        {
            Logger.Verbose("Starting agent polling");

            while (agentPollingCancelToken.IsCancellationRequested == false)
            {
                await Task.Delay(10);

                if (Mission.Current == null) continue;

                foreach (Guid guid in agentRegistry.ControlledAgents.Keys)
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

            Logger.Verbose("Stopping agent polling");
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
            if (Mission.Current == null) return;

            NetPeer peer = payload.What.NetPeer;

            Logger.Debug("Handling disconnect for {peer}", peer);

            if (agentRegistry.OtherAgents.TryGetValue(peer, out AgentGroupController controller))
            {
                foreach (var agent in controller.ControlledAgents.Values)
                {
                    GameLoopRunner.RunOnMainThread(() =>
                    {
                        if (agent.Health > 0)
                        {
                            agent.MakeDead(false, ActionIndexValueCache.act_none);
                            agent.FadeOut(false, true);
                        }
                    });
                }

                agentRegistry.RemovePeer(peer);
            }
        }
    }
}
