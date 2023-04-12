using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Internal event for agent shield damage
    /// </summary>
    public readonly struct ShieldHealthRemaining : IEvent
    {
        public Agent Agent { get; }
        public EquipmentIndex EquipmentIndex { get; }
        public short Hitpoints { get; }

        public ShieldHealthRemaining(Agent agent, EquipmentIndex equipmentIndex, short health)
        {
            Agent = agent;
            EquipmentIndex = equipmentIndex;
            Hitpoints = health;
        }
    }
}
