using Coop.NetImpl.LiteNet;
using LiteNetLib;
using NLog;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions.Packets.Agents
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

    public class MovementHandler : IPacketHandler
    {
        private readonly NLog.Logger m_Logger = LogManager.GetCurrentClassLogger();

        public Dictionary<Guid, Agent> ControlledAgents = new Dictionary<Guid, Agent>();
        public readonly Dictionary<Guid, Agent> OtherAgents = new Dictionary<Guid, Agent>();


        private readonly CancellationTokenSource m_AgentPollingCancelToken = new CancellationTokenSource();
        private readonly Task m_AgentPollingTask;

        private readonly LiteNetP2PClient m_Client;
        public MovementHandler(LiteNetP2PClient client)
        {
            m_Client = client;

            m_Client.AddHandler(this);

            RegisterAllAgentsAsControl();

            m_AgentPollingTask = Task.Run(PollAgents);
        }

        ~MovementHandler()
        {
            m_AgentPollingCancelToken.Cancel();
            m_AgentPollingTask.Wait();
        }

        public PacketType PacketType => PacketType.Movement;

        private void RegisterAllAgentsAsControl()
        {
            ControlledAgents = Mission.Current.AllAgents.ToDictionary((agent) => Guid.NewGuid());
        }

        private async void PollAgents()
        {
            while (m_AgentPollingCancelToken.IsCancellationRequested == false)
            {
                if(Mission.Current.IsLoadingFinished)
                foreach(Guid guid in ControlledAgents.Keys)
                {
                    Agent agent = ControlledAgents[guid];
                    m_Client.SendAll(new MovementPacket(guid, agent));
                }

                await Task.Delay(10);
            }
        }

        public void RegisterAgent(Guid guid, Agent agent)
        {
            OtherAgents.Add(guid, agent);
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            MovementPacket movement = (MovementPacket)packet;

            if(OtherAgents.TryGetValue(movement.AgentId, out Agent agent))
            {
                movement.Apply(agent);
            }
        }
    }
}
