using Common.Messaging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Agents.Messages
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