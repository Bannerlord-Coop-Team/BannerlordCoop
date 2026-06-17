using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Missions.Agents.Messages
{
    /// <summary>
    /// External event for agent shield breaks
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkShieldBreak : IEvent
    {
        [ProtoMember(1)]
        public string AgentId { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }

        public NetworkShieldBreak(string agentId, EquipmentIndex equipmentIndex)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
        }
    }
}