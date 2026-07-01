using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    /// <summary>
    /// A batch of agent movement snapshots for one poll tick: one packet carrying every agent the sender
    /// currently has authority over, instead of one packet per agent. At battle scale (dozens of agents at
    /// ~100 Hz) per-agent packets flood the mesh and the receiver's game-thread queue.
    /// </summary>
    [ProtoContract]
    public readonly struct MovementPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.Unreliable;

        public PacketType PacketType => PacketType.Movement;

        [ProtoMember(1)]
        public Guid[] AgentIds { get; }
        [ProtoMember(2)]
        public AgentData[] Agents { get; }

        public MovementPacket(Guid[] agentIds, AgentData[] agents)
        {
            AgentIds = agentIds;
            Agents = agents;
        }
    }
}
