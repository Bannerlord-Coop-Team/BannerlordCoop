using Common.Messaging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Messages
{
    /// <summary>
    /// Internal event for agent weapon drops
    /// </summary>
    public readonly struct WeaponDropped : IEvent
    {
        public Agent Agent { get; }
        public EquipmentIndex EquipmentIndex { get; }
        public MissionWeapon DroppedWeapon { get; }
        public SpawnedItemEntity DroppedItem { get; }

        public WeaponDropped(
            Agent agent,
            EquipmentIndex equipmentIndex,
            MissionWeapon droppedWeapon,
            SpawnedItemEntity droppedItem)
        {
            Agent = agent;
            EquipmentIndex = equipmentIndex;
            DroppedWeapon = droppedWeapon;
            DroppedItem = droppedItem;
        }
    }
}
