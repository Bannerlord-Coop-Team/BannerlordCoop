using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;

namespace Missions.Agents.Messages
{
    /// <summary>
    /// External event for agent weapon drops
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkWeaponDropped : IEvent
    {
        [ProtoMember(1)]
        public Guid AgentId { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }

        [ProtoMember(3)]
        public Guid WorldItemId { get; }

        public NetworkWeaponDropped(
            Guid agentId,
            EquipmentIndex equipmentIndex,
            Guid worldItemId)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            WorldItemId = worldItemId;
        }
    }
}