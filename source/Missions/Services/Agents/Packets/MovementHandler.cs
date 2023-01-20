using Common;
using Common.Logging;
using Common.Messaging;
using LiteNetLib;
using Microsoft.Extensions.Caching.Memory;
using Missions.Services.Agents.Extensions;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
using Mono.Cecil.Cil;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

        public MovementPacket(Guid agentGuid, AgentData agentData)
        {
            AgentId = agentGuid;
            Agent = agentData;
        }

        public void Apply(Agent agent)
        {
            Agent.Apply(agent);
        }
    }

    public class MovementHandler : IPacketHandler, IDisposable
    {
        private const int PACKETS_PER_SECONDS = 30;

        private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();

        private readonly LiteNetP2PClient _client;
        private readonly IMessageBroker _messageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;

        private readonly IMemoryCache _memoryCache;

        public MovementHandler(LiteNetP2PClient client, IMessageBroker messageBroker, INetworkAgentRegistry agentRegistry)
        {

            _client = client;
            _messageBroker = messageBroker;
            _agentRegistry = agentRegistry;

            _messageBroker.Subscribe<PeerDisconnected>(Handle_PeerDisconnect);
            _messageBroker.Subscribe<Movement>(Handle_Movement);

            _client.AddHandler(this);

            var options = new MemoryCacheOptions
            {
                ExpirationScanFrequency = TimeSpan.FromSeconds(1)
            };

            _memoryCache = new MemoryCache(options);
        }

        ~MovementHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            _client.RemoveHandler(this);
            _messageBroker.Unsubscribe<PeerDisconnected>(Handle_PeerDisconnect);
            _messageBroker.Unsubscribe<Movement>(Handle_Movement);
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

        private void Handle_Movement(MessagePayload<Movement> payload)
        {
            Guid guid = payload.What.Guid;

            if (CanSendPacket(guid) && _agentRegistry.ControlledAgents.TryGetValue(guid, out var agent))
            {
                if (agent.Mission != null)
                {
                    SendPacket(guid, payload.What.ToMovementPacket());
                }
            }
        }

        private void SendPacket(Guid guid, MovementPacket movementPacket)
        {
            var newValue = 1;
            if (_memoryCache.TryGetValue(guid, out int memory))
            {
                newValue = memory + 1;
            }

            _memoryCache.Set(guid, newValue);

            _client.SendAll(movementPacket);
        }

        private bool CanSendPacket(Guid guid)
        {
            if (_memoryCache.TryGetValue(guid, out int memory))
            {
                return memory <= PACKETS_PER_SECONDS;
            }

            return false;
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
