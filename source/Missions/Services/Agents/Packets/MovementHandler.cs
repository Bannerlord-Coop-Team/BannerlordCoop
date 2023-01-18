using Common;
using Common.Logging;
using Common.Messaging;
using LiteNetLib;
using Missions.Services.Agents.Extensions;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Packets
{
    [ProtoContract]
    public readonly struct MovementPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.Unreliable;

        public PacketType PacketType => PacketType.Movement;

        public static MovementPacket Invalid => new MovementPacket();

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

        public MovementPacket(Guid agentId, AgentData agentData)
        {
            AgentId = agentId;
            Agent = agentData;
        }

        public void Apply(Agent agent)
        {
            Agent.Apply(agent);
        }
    }

    public class MovementHandler : IPacketHandler, IDisposable
    {
        private const double AGENT_MESSAGES_PER_SECOND = 30d;
        private const double SECOND = 1000d;

        private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();

        private readonly CancellationTokenSource m_AgentPollingCancelToken = new CancellationTokenSource();
        private readonly Task m_AgentPollingTask;

        private readonly LiteNetP2PClient _client;
        private readonly IMessageBroker _messageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;

        private readonly ConcurrentDictionary<Guid, MovementPacket> _previousPackets = new ConcurrentDictionary<Guid, MovementPacket>();

        public MovementHandler(LiteNetP2PClient client, IMessageBroker messageBroker, INetworkAgentRegistry agentRegistry)
        {

            _client = client;
            _messageBroker = messageBroker;
            _agentRegistry = agentRegistry;

            _messageBroker.Subscribe<PeerDisconnected>(Handle_PeerDisconnect);

            _client.AddHandler(this);

            m_AgentPollingTask = Task.Run(PollAgents);
        }

        ~MovementHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            _client.RemoveHandler(this);
            _messageBroker.Unsubscribe<PeerDisconnected>(Handle_PeerDisconnect);
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
                });
                return current;
            }
        }

        private async Task PollAgents()
        {
            while (m_AgentPollingCancelToken.IsCancellationRequested == false &&
                   CurrentMission != null)
            {
                foreach (Guid guid in _agentRegistry.ControlledAgents.Keys)
                {
                    Agent agent = _agentRegistry.ControlledAgents[guid];
                    if (agent.Mission != null)
                    {
                        // TODO: find elegant way to avoid sending if nothing has to be updated.
                        MovementPacket packet = GetNextMovementPacket(guid, agent);

                        if (packet.Equals(MovementPacket.Invalid))
                        {
                            _client.SendAll(packet);
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(SECOND / AGENT_MESSAGES_PER_SECOND));
            }
        }

        private MovementPacket GetNextMovementPacket(Guid guid, Agent agent)
        {
            MovementPacket packet = new MovementPacket(guid, agent);

            if (_previousPackets.TryGetValue(guid, out var previousPacket))
            {
                var previousAgent = previousPacket.Agent;

                if (agent.HasMovementUpdated(previousAgent) 
                {
                    return MovementPacket.Invalid;
                }
                
                var agentData = CreateAgentPacket(agent, previousPacket.Agent);

                var smallPacket = new MovementPacket(guid, agentData);

                _previousPackets.TryUpdate(guid, packet, previousPacket);

                return smallPacket;
            } 

            _previousPackets.TryAdd(guid, packet);

            return packet;
        }

        private AgentData CreateAgentPacket(Agent agent, AgentData previousData)
        {
            // only these four values should trigger a change
            Vec3 lookDirection = agent.LookDirection != previousData.LookDirection ? agent.LookDirection : Vec3.Invalid;
            Vec2 movementInputVector = agent.MovementInputVector != previousData.InputVector ? agent.MovementInputVector : Vec2.Invalid;
            AgentActionData agentActionData = new AgentActionData(agent);
            AgentMountData agentMountData = new AgentMountData(agent);

            return new AgentData(
                agent.Position,
                agent.GetMovementDirection(),
                lookDirection,
                movementInputVector,
                previousData.AgentEquipment,
                agentActionData,
                agentMountData);
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            if (_agentRegistry.OtherAgents.TryGetValue(peer, out AgentGroupController agentGroupController))
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

            if (_agentRegistry.OtherAgents.TryGetValue(peer, out AgentGroupController controller))
            {
                foreach (var agent in controller.ControlledAgents.Values)
                {
                    GameLoopRunner.RunOnMainThread(() =>
                    {
                        agent.MakeDead(false, ActionIndexCache.act_none);
                        agent.FadeOut(false, true);
                    });
                }

                _agentRegistry.RemovePeer(peer);
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
