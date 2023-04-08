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
    public readonly struct ShieldBreak : IEvent
    {
        public Agent Agent { get; }
        public EquipmentIndex EquipmentIndex { get; }

        public ShieldBreak(Agent agent, EquipmentIndex equipmentIndex)
        {
            Agent = agent;
            EquipmentIndex = equipmentIndex;
        }
    }
}
