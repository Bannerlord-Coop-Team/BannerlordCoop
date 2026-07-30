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
        [ProtoMember(2)]
        public ushort[] AgentIds { get; }
        [ProtoMember(3)]
        public AgentData[] Agents { get; }
        [ProtoMember(4)]
        public Guid[] AgentGuids { get; }
        [ProtoMember(5)]
        public bool IsPlayerMovement { get; }

        public MovementPacket(
            string identityScopeId,
            ushort[] agentIds,
            AgentData[] agents,
            bool isPlayerMovement = false)
        {
            IdentityScopeId = identityScopeId;
            AgentIds = agentIds;
            Agents = agents;
            AgentGuids = null;
            IsPlayerMovement = isPlayerMovement;
        }

        public MovementPacket(
            Guid[] agentGuids,
            AgentData[] agents,
            bool isPlayerMovement = false)
        {
            IdentityScopeId = null;
            AgentIds = null;
            AgentGuids = agentGuids;
            Agents = agents;
            IsPlayerMovement = isPlayerMovement;
        }
    }
}
