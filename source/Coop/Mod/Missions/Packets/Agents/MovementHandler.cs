using Coop.Mod.Missions.Network;
using Coop.NetImpl.LiteNet;
using LiteNetLib;
using NLog;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
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

    public class MovementHandler : IPacketHandler, IDisposable
    {
        private readonly NLog.Logger m_Logger = LogManager.GetCurrentClassLogger();

        private readonly CancellationTokenSource m_AgentPollingCancelToken = new CancellationTokenSource();
        private readonly Task m_AgentPollingTask;

        private readonly LiteNetP2PClient m_Client;
        public MovementHandler(LiteNetP2PClient client)
        {
            m_Client = client;

            m_Client.AddHandler(this);

            m_AgentPollingTask = Task.Run(PollAgents);
        }

        ~MovementHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            m_Client.RemoveHandler(this);
            m_AgentPollingCancelToken.Cancel();
            m_AgentPollingTask.Wait();
        }

        public PacketType PacketType => PacketType.Movement;

        [HandleProcessCorruptedStateExceptions]
        private async void PollAgents()
        {
            while (m_AgentPollingCancelToken.IsCancellationRequested == false)
            {
                bool? isLoadingFinished = Mission.Current?.IsLoadingFinished;
                if (isLoadingFinished.HasValue == false)
                {
                    m_AgentPollingCancelToken.Cancel(false);
                }
                else if (isLoadingFinished.Value)
                {
                    foreach (Guid guid in NetworkAgentRegistry.ControlledAgents.Keys)
                    {
                        Agent agent = NetworkAgentRegistry.ControlledAgents[guid];
                        if(agent.Mission != null)
                        {
                            MovementPacket packet = new MovementPacket(guid, agent);
                            m_Client.SendAll(packet);
                        }
                        else
                        {
                            if (NetworkAgentRegistry.AgentToId.TryGetValue(agent, out Guid agentId))
                            {
                                agent.MakeDead(false, ActionIndexCache.act_none);
                                agent.FadeOut(false, true);
                                NetworkAgentRegistry.RemoveControlledAgent(agentId);
                            }
                        }                      
                    }
                }

                await Task.Delay(10);
            }
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            if(NetworkAgentRegistry.OtherAgents.TryGetValue(peer, out AgentGroupController agentGroupController))
            {
                MovementPacket movement = (MovementPacket)packet;
                agentGroupController.ApplyMovement(movement);
            }
        }

        public void HandlePeerDisconnect(NetPeer peer, DisconnectInfo reason)
        {
            if (NetworkAgentRegistry.OtherAgents.TryGetValue(peer, out AgentGroupController controller))
            {
                foreach (var agent in controller.ControlledAgents.Values)
                {
                    agent.MakeDead(false, ActionIndexCache.act_none);
                    agent.FadeOut(false, true);
                }

                NetworkAgentRegistry.RemovePeer(peer);
            }
        }
    }
}
