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

        [ProtoMember(5)]
        public string SenderControllerId { get; }

        [ProtoMember(6, IsPacked = true)]
        public long[] AuthorityRevisions { get; }

        // All snapshots in a packet come from the same sender capture, including split batches.
        [ProtoMember(7)]
        public long SampleSequence { get; }

        /// <summary>The naval hulls this packet's deck poses reference, by <see cref="AgentData.DeckShipIndex"/>.</summary>
        [ProtoMember(8)]
        public Guid[] DeckShips { get; }

        public MovementPacket(string identityScopeId, ushort[] agentIds, AgentData[] agents, string senderControllerId = null, long[] authorityRevisions = null, long sampleSequence = 0, Guid[] deckShips = null)
        {
            SampleSequence = sampleSequence;
            SenderControllerId = senderControllerId;
            AuthorityRevisions = authorityRevisions;
            IdentityScopeId = identityScopeId;
            AgentIds = agentIds;
            Agents = agents;
            AgentGuids = null;
            DeckShips = deckShips;
        }

        public MovementPacket(Guid[] agentGuids, AgentData[] agents, string senderControllerId = null, long[] authorityRevisions = null, long sampleSequence = 0, Guid[] deckShips = null)
        {
            SampleSequence = sampleSequence;
            SenderControllerId = senderControllerId;
            AuthorityRevisions = authorityRevisions;
            IdentityScopeId = null;
            AgentIds = null;
            AgentGuids = agentGuids;
            Agents = agents;
            DeckShips = deckShips;
        }
        internal MovementPacket WithAuthorityRevisions(long[] revisions) => AgentIds == null
            ? new MovementPacket(AgentGuids, Agents, SenderControllerId, revisions, SampleSequence, DeckShips)
            : new MovementPacket(IdentityScopeId, AgentIds, Agents, SenderControllerId, revisions, SampleSequence, DeckShips);
    }
}
