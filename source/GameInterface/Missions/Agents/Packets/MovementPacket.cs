using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Agents.Packets
{
    [ProtoContract]
    public readonly struct MovementPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.Unreliable;

        public PacketType PacketType => PacketType.Movement;

        [ProtoMember(1)]
        public AgentData Agent { get; }
        [ProtoMember(2)]
        public string AgentId { get; }

        public MovementPacket(string controllerId, Agent agent)
        {
            AgentId = controllerId;
            Agent = new AgentData(agent);
        }

        public void Apply(Agent agent)
        {
            Agent.Apply(agent);
        }
    }
}
