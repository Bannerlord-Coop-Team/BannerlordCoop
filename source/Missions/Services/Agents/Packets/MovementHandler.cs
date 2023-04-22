using Common;
using Common.Logging;
using Common.Messaging;
using LiteNetLib;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
using ProtoBuf;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Packets
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
        private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();

        private readonly CancellationTokenSource m_AgentPollingCancelToken = new CancellationTokenSource();
        private readonly Task m_AgentPollingTask;

        private readonly LiteNetP2PClient _client;
        private readonly IMessageBroker _messageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;

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

        private async void PollAgents()
        {
            while (m_AgentPollingCancelToken.IsCancellationRequested == false &&
                   CurrentMission != null)
            {
                foreach (Guid guid in _agentRegistry.ControlledAgents.Keys)
                {
                    Agent agent = _agentRegistry.ControlledAgents[guid];
                    if (agent.Mission != null)
                    {
                        MovementPacket packet = new MovementPacket(guid, agent);
                        _client.SendAll(packet);
                    }
                }

                await Task.Delay(10);
            }
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
                        agent.MakeDead(false, ActionIndexValueCache.act_none);
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
