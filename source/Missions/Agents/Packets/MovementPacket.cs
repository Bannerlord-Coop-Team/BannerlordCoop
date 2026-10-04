using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    /// <summary>
    /// A small batch of agent movement snapshots for one poll tick.
    /// </summary>
    [ProtoContract]
    public readonly struct MovementPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.Unreliable;

        public PacketType PacketType => PacketType.Movement;

        [ProtoMember(1)]
        public string IdentityScopeId { get; }

        [ProtoMember(2, IsPacked = true)]
        public ushort[] AgentIds { get; }
        [ProtoMember(3)]
        public AgentData[] Agents { get; }
        [ProtoMember(4)]
        public Guid[] AgentGuids { get; }
        /// <summary>The naval hulls this packet's deck poses reference, by <see cref="AgentData.DeckShipIndex"/>.</summary>
        [ProtoMember(5)]
        public Guid[] DeckShips { get; }

        public MovementPacket(string identityScopeId, ushort[] agentIds, AgentData[] agents, Guid[] deckShips = null)
        {
            IdentityScopeId = identityScopeId;
            AgentIds = agentIds;
            Agents = agents;
            AgentGuids = null;
            DeckShips = deckShips;
        }

        public MovementPacket(Guid[] agentGuids, AgentData[] agents, Guid[] deckShips = null)
        {
            IdentityScopeId = null;
            AgentIds = null;
            AgentGuids = agentGuids;
            Agents = agents;
            DeckShips = deckShips;
        }
    }
}
