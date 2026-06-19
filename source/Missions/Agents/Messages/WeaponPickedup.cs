using Common.Messaging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Agents.Messages
{
    /// <summary>
    /// Internal event for agent weapon pickups
    /// </summary>
    public readonly struct WeaponPickedup : IEvent
    {
        public Agent Agent { get; }
        public EquipmentIndex EquipmentIndex { get; }
        public ItemObject WeaponObject { get; }
        public ItemModifier WeaponModifier { get; }
        public Banner Banner { get; }

        public WeaponPickedup(
            Agent agent, 
            EquipmentIndex equipmentIndex, 
            ItemObject weaponObject, 
            ItemModifier itemModifier, 
            Banner banner)
        {
            Agent = agent;
            EquipmentIndex = equipmentIndex;
            WeaponObject = weaponObject;
            WeaponModifier = itemModifier;
            Banner = banner;
        }
    }
}