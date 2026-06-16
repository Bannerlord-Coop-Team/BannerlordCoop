using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// External event for agent weapon drops
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkWeaponDropped : IEvent
    {
        [ProtoMember(1)]
        public string AgentId { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }

        public NetworkWeaponDropped(string agentId, EquipmentIndex equipmentIndex)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
        }
    }
}