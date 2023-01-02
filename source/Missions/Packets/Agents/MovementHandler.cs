using Coop.Mod;
using LiteNetLib;
using Missions.Network;
using ProtoBuf;
using System;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Serilog;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Packets.Agents
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

        Mission CurrentMission { 
            get
            {
                Mission current = null;
                GameLoopRunner.RunOnMainThread(() =>
                {
                    current = Mission.Current;
                });
                return current;
            }
        }

        private async void PollAgents()
        {
            while (m_AgentPollingCancelToken.IsCancellationRequested == false &&
                   CurrentMission != null)
            {
                foreach (Guid guid in NetworkAgentRegistry.ControlledAgents.Keys)
                {
                    Agent agent = NetworkAgentRegistry.ControlledAgents[guid];
                    if (agent.Mission != null)
                    {
                        MovementPacket packet = new MovementPacket(guid, agent);
                        m_Client.SendAll(packet);
                    }
                    else
                    {
                        if (NetworkAgentRegistry.AgentToId.TryGetValue(agent, out Guid agentId))
                        {
                            GameLoopRunner.RunOnMainThread(() =>
                            {
                                agent.MakeDead(false, ActionIndexCache.act_none);
                                agent.FadeOut(false, true);
                            });

                            NetworkAgentRegistry.RemoveControlledAgent(agentId);
                        }
                    }
                }

                await Task.Delay(10);
            }
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            if (NetworkAgentRegistry.OtherAgents.TryGetValue(peer, out AgentGroupController agentGroupController))
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

        public static Vec2 InterpolatePosition(Vec2 controlInput, Vec3 rotation, Vec2 currentPosition, Vec2 newPosition)
        {
            Vec2 directionVector = newPosition - currentPosition;
            double angle = Math.Atan2(rotation.y, rotation.x);
            directionVector = Rotate(directionVector, angle);

            return directionVector;
        }

        public static Vec2 Rotate(Vec2 v, double radians)
        {
            float sin = MathF.Sin((float)radians);
            float cos = MathF.Cos((float)radians);

            float tx = v.x;
            float ty = v.y;
            v.x = cos * tx - sin * ty;
            v.y = sin * tx + cos * ty;
            return v;
        }
    }
}
