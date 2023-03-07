using Common.Messaging;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    public readonly struct WeaponDropped : IEvent
    {
        public Agent Agent { get; }
        public EquipmentIndex EquipmentIndex { get; }

        public WeaponDropped(Agent agent, EquipmentIndex equipmentIndex)
        {
            Agent = agent;
            EquipmentIndex = equipmentIndex;
        }
    }
}