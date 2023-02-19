using Common;
using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using LiteNetLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Packets
{  

    public class MovementHandler : IPacketHandler, IDisposable
    {
        private const int PACKETS = 30;
        private readonly static TimeSpan TIME_BETWEEN_PACKETS = TimeSpan.FromSeconds(1);

        private static int PACKET_UPDATE_RATE = (int)Math.Round(TIME_BETWEEN_PACKETS.TotalMilliseconds / PACKETS);

        private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();

        private readonly IPacketManager _packetManager;
        private readonly LiteNetP2PClient _client;
        private readonly IMessageBroker _messageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;

        private Dictionary<Guid, AgentMovement> _agentMovementDeltas = new Dictionary<Guid, AgentMovement>();

        private Timer _senderTimer;

        public MovementHandler(LiteNetP2PClient client, IMessageBroker messageBroker, INetworkAgentRegistry agentRegistry)
        {
            Logger.Verbose("Creating {name}", this.GetType().Name);

            // TODO DI
            _packetManager = client.PacketManager;
            _client = client;
            _messageBroker = messageBroker;
            _agentRegistry = agentRegistry;
            
            _messageBroker.Subscribe<PeerDisconnected>(Handle_PeerDisconnect);
            _messageBroker.Subscribe<ActionDataChanged>(Handle_ActionDataChanged);
            _messageBroker.Subscribe<LookDirectionChanged>(Handle_LookDirectionChanged);
            _messageBroker.Subscribe<MountDataChanged>(Handle_MountDataChanged);
            _messageBroker.Subscribe<MovementInputVectorChanged>(Handle_MovementInputVectorChanged);

            _packetManager.RegisterPacketHandler(this);

            // start the SendMessage every PACKET_UPDATE_RATE milliseconds, TIME_BETWEEN_PACKETS.Seconds after it was initialized
            _senderTimer = new Timer(SendMessage, null, TIME_BETWEEN_PACKETS.Seconds, PACKET_UPDATE_RATE);
        }

        ~MovementHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            _packetManager.RemovePacketHandler(this);
            _messageBroker.Unsubscribe<PeerDisconnected>(Handle_PeerDisconnect);
            _messageBroker.Unsubscribe<ActionDataChanged>(Handle_ActionDataChanged);
            _messageBroker.Unsubscribe<LookDirectionChanged>(Handle_LookDirectionChanged);
            _messageBroker.Unsubscribe<MountDataChanged>(Handle_MountDataChanged);
            _messageBroker.Unsubscribe<MovementInputVectorChanged>(Handle_MovementInputVectorChanged);
            _senderTimer?.Dispose();
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

        private void Handle_ActionDataChanged(MessagePayload<ActionDataChanged> payload)
        {
            var delta = GetDelta(payload.What);

            delta.CalculateMovement(payload.What);
        }

        private void Handle_LookDirectionChanged(MessagePayload<LookDirectionChanged> payload)
        {
            var delta = GetDelta(payload.What);

            delta.CalculateMovement(payload.What);
        }    

        private void Handle_MountDataChanged(MessagePayload<MountDataChanged> payload)
        {
            var delta = GetDelta(payload.What);

            delta.CalculateMovement(payload.What);
        }

        private void Handle_MovementInputVectorChanged(MessagePayload<MovementInputVectorChanged> payload)
        {
            var delta = GetDelta(payload.What);

            delta.CalculateMovement(payload.What);
        }

        private AgentMovement GetDelta(IMovementEvent payload)
        {
            if (!_agentRegistry.TryGetAgentId(payload.Agent, out var payloadGuid))
            {
                Logger.Error("No {agent} found", nameof(Agent));
            }

            if (_agentMovementDeltas.TryGetValue(payloadGuid, out var delta))
            {
                return delta;
            }

            var agent = payload.Agent;
            delta = new AgentMovement(
                agent.Position, 
                agent.GetMovementDirection(),
                new AgentEquipmentData(agent),
                agent, 
                payloadGuid);

            _agentMovementDeltas.Add(payloadGuid, delta);

            return delta;
        }

        private IEnumerable<AgentMovement> PopAllDeltas()
        {
            foreach (var kv in _agentMovementDeltas)
            {
                yield return kv.Value;
            }

            _agentMovementDeltas.Clear();
        }

        private void SendMessage(object state)
        {
            foreach (var delta in PopAllDeltas())
            {
                _client.SendAll(delta);
            }
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            if (_agentRegistry.TryGetGroupController(peer, out AgentGroupController agentGroupController))
            {
                var movement = (AgentMovement)packet;
                agentGroupController.ApplyMovement(movement);
            }
        }

        public void Handle_PeerDisconnect(MessagePayload<PeerDisconnected> payload)
        {
            if (Mission.Current == null) return;

            NetPeer peer = payload.What.NetPeer;

            Logger.Debug("Handling disconnect for {peer}", peer);

            if (_agentRegistry.TryGetGroupController(peer, out AgentGroupController controller))
            {
                foreach (var kv in controller.ControlledAgents)
                {
                    var agent = kv.Value;
                    var guid = kv.Key;

                    GameLoopRunner.RunOnMainThread(() =>
                    {
                        agent.MakeDead(false, ActionIndexCache.act_none);
                        agent.FadeOut(false, true);
                    });

                    _agentMovementDeltas.Remove(guid);
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
