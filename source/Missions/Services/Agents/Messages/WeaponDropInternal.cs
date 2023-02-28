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
        public WeaponClass WeaponClass { get; }

        public WeaponDropInternal(Agent agent, EquipmentIndex equipmentIndex, WeaponClass weaponClass)
        {
            Agent = agent;
            EquipmentIndex = equipmentIndex;
            WeaponClass = weaponClass;
        }
    }
}