using Common.Messaging;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    public readonly struct WeaponDropInternal : IEvent
    {
        public Agent Agent { get; }
        public EquipmentIndex EquipmentIndex { get; }

        public WeaponDropInternal(Agent agent, EquipmentIndex equipmentIndex)
        {
            Agent = agent;
            EquipmentIndex = equipmentIndex;
        }
    }
}