using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using LiteNetLib;
using Missions.Services.Agents.Packets;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
using ProtoBuf;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    [ProtoContract]
    public readonly struct MovementPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.Unreliable;

        public PacketType PacketType => PacketType.Movement;

        public byte[] Data => new byte[0];

        [ProtoMember(1)]
        public AgentData Agent { get; }
        [ProtoMember(2)]
        public Guid AgentId { get; }

        public MovementPacket(Guid agentGuid, Agent agent)
        {
            AgentId = agentGuid;
            Agent = new AgentData(agent);
        }

        public void Apply(Agent agent)
        {
            Agent.Apply(agent);
        }
    }

    public class AgentMovementHandler : IPacketHandler, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();

        private readonly CancellationTokenSource m_AgentPollingCancelToken = new CancellationTokenSource();
        private readonly Task m_AgentPollingTask;
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
            this.packetManager = packetManager;
            this.client = client;
            this.messageBroker = messageBroker;
            this.agentRegistry = agentRegistry;


            this.messageBroker.Subscribe<PeerDisconnected>(Handle_PeerDisconnect);

            this.packetManager.RegisterPacketHandler(this);

            m_AgentPollingTask = Task.Factory.StartNew(PollAgents);
        }

        ~AgentMovementHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            packetManager.RemovePacketHandler(this);
            messageBroker.Unsubscribe<PeerDisconnected>(Handle_PeerDisconnect);
            m_AgentPollingCancelToken.Cancel();
            m_AgentPollingTask.Wait();
        }

        public PacketType PacketType => PacketType.Movement;

        Mission CurrentMission
        {
            get
            {
                Mission current = null;
                GameLoopRunner.RunOnMainThread(() =>
                {
                    current = Mission.Current;
                }, true);
                return current;
            }
        }

        private async void PollAgents()
        {
            while (m_AgentPollingCancelToken.IsCancellationRequested == false)
            {
                await Task.Delay(10);

                if (CurrentMission == null) continue;

                foreach (Guid guid in agentRegistry.ControlledAgents.Keys)
                {
                    Agent agent = agentRegistry.ControlledAgents[guid];
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
