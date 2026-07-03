using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;
using System;

namespace Missions.Agents.Packets
{
    /// <summary>
    /// A batch of DISCRETE action changes (attacks, blocks, jumps, kicks, gestures, mount/sit...) for the agents
    /// the sender owns, sent ON CHANGE rather than polled. Unlike movement (continuous, unreliable, smoothed),
    /// actions are events: they must not be dropped or reordered, so this is <see cref="DeliveryMethod.ReliableOrdered"/>.
    /// The receiver applies each entry ONCE and lets the engine advance the animation; locomotion (walk/run/idle)
    /// is NOT carried here — it is reproduced from the synced movement input.
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

        public AgentActionPacket(Guid[] agentIds, AgentActionData[] actions)
        {
            AgentIds = agentIds;
            Actions = actions;
        }
    }
}
