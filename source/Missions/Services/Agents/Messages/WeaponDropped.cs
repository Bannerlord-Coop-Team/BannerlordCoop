﻿using Common.Messaging;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.MountAndBlade.Mission;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Internal event for agent weapon drops
    /// </summary>
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