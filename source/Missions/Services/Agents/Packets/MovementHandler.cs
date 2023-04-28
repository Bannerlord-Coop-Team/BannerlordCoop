using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using LiteNetLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Packets
{
    public class MovementHandler : IPacketHandler, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();

        private readonly IPacketManager _packetManager;
        private readonly INetwork _client;
        private readonly IMessageBroker _messageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;
        private readonly IAgentPublisherConfig _agentPublisherConfig;

        private readonly AgentPublisher _agentPublisher;

        private ConcurrentDictionary<Guid, AgentMovement> _agentMovementDeltas = new ConcurrentDictionary<Guid, AgentMovement>();

        private Timer _senderTimer;

        public MovementHandler(LiteNetP2PClient client, IMessageBroker messageBroker, INetworkAgentRegistry agentRegistry, IPacketManager packetManager, AgentPublisher agentPublisher, IAgentPublisherConfig agentPublisherConfig)
        {
            _packetManager = packetManager;
            _client = client;
            _messageBroker = messageBroker;
            _agentRegistry = agentRegistry;

            _messageBroker.Subscribe<PeerDisconnected>(Handle_PeerDisconnect);
            _messageBroker.Subscribe<IMovementEvent>(Handle_MovementEvent);

            _agentPublisher = agentPublisher;
            _agentPublisherConfig = agentPublisherConfig;

            _packetManager.RegisterPacketHandler(this);

            // start the SendMessage every PACKET_UPDATE_RATE milliseconds
            _senderTimer = new Timer(SendMessage, null, 0, _agentPublisherConfig.PacketUpdateRate);
        }

        ~MovementHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            _packetManager.RemovePacketHandler(this);
            _messageBroker.Unsubscribe<PeerDisconnected>(Handle_PeerDisconnect);
            _messageBroker.Unsubscribe<IMovementEvent>(Handle_MovementEvent);
            _senderTimer?.Dispose();
        }

        public PacketType PacketType => PacketType.Movement;

        private void Handle_MovementEvent(MessagePayload<IMovementEvent> payload)
        {
            var payloadType = payload.What.GetType();

            // TODO: get rid of this horrible mess somehow
            if (payloadType == typeof(MovementInputVectorChanged))
            {
                Handle_MovementInputVectorChanged((MovementInputVectorChanged)payload.What);
            }
            else if (payloadType == typeof(ActionDataChanged))
            {
                Handle_ActionDataChanged((ActionDataChanged)payload.What);
            }
            else if (payloadType == typeof(LookDirectionChanged))
            {
                Handle_LookDirectionChanged((LookDirectionChanged)payload.What);
            }
            else if (payloadType == typeof(MountDataChanged))
            {
                Handle_MountDataChanged((MountDataChanged)payload.What);
            }
        }

        private void Handle_ActionDataChanged(ActionDataChanged payload)
        {
            var delta = GetDelta(payload);

            delta.CalculateMovement(payload);
        }

        private void Handle_LookDirectionChanged(LookDirectionChanged payload)
        {
            var delta = GetDelta(payload);

            delta.CalculateMovement(payload);
        }

        private void Handle_MountDataChanged(MountDataChanged payload)
        {
            var delta = GetDelta(payload);

            delta.CalculateMovement(payload);
        }

        private void Handle_MovementInputVectorChanged(MovementInputVectorChanged payload)
        {
            var delta = GetDelta(payload);

            delta.CalculateMovement(payload);
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

            _agentMovementDeltas.TryAdd(payloadGuid, delta);

            return delta;
        }

        private IEnumerable<AgentMovement> PopAllDeltas()
        {
            var copiedDeltas = new List<AgentMovement>();

            foreach (var kv in _agentMovementDeltas)
            {
                copiedDeltas.Add(kv.Value);
            }

            _agentMovementDeltas.Clear();

            return copiedDeltas;
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
                        if (agent.Health > 0)
                        {
                            agent.MakeDead(false, ActionIndexValueCache.act_none);
                            agent.FadeOut(false, true);
                        }
                    });

                    _agentMovementDeltas.TryRemove(guid, out var _);
                }

                _agentRegistry.RemovePeer(peer);
            }
        }
    }
}