using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;
using System;

namespace Missions.Agents.Packets
{
    /// <summary>
    /// A batch of DISCRETE action and guard-state changes for the agents the sender owns, sent ON CHANGE rather
    /// than polled. The receiver applies each action once and retains guard state for per-frame puppet input.
    /// These transitions must not be dropped or reordered, so this is <see cref="DeliveryMethod.ReliableOrdered"/>.
    /// </summary>
    [ProtoContract]
    public readonly struct AgentActionPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

        public PacketType PacketType => PacketType.AgentAction;

        [ProtoMember(1)]
        public Guid[] AgentIds { get; }
        [ProtoMember(2)]
        public AgentActionData[] Actions { get; }
        [ProtoMember(3)]
        public string ControllerId { get; }
        [ProtoMember(4)]
        public long[] Sequences { get; }

        public AgentActionPacket(
            string controllerId,
            Guid[] agentIds,
            AgentActionData[] actions,
            long[] sequences)
        {
            ControllerId = controllerId;
            AgentIds = agentIds;
            Actions = actions;
            Sequences = sequences;
        }
    }
}
