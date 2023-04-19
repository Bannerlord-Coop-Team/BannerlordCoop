using Common.Messaging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Internal event for agent shield damage
    /// </summary>
    public readonly struct ShieldDamaged : IEvent
    {
        public Agent Agent { get; }
        public EquipmentIndex EquipmentIndex { get; }
        public int InflictedDamage { get; }

        public ShieldDamaged(Agent agent, EquipmentIndex equipmentIndex, int inflictedDamage)
        {
            Agent = agent;
            EquipmentIndex = equipmentIndex;
            InflictedDamage = inflictedDamage;
        }
    }

}
