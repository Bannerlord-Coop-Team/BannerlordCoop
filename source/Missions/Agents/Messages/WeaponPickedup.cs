using Common.Messaging;
using Missions.Agents.Packets;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Messages
{
    /// <summary>
    /// Internal event for agent weapon pickups
    /// </summary>
    public readonly struct WeaponPickedup : IEvent
    {
        public Agent Agent { get; }
        public SpawnedItemEntity WorldItem { get; }
        public EquipmentIndex EquipmentIndex { get; }
        public ItemObject WeaponObject { get; }
        public ItemModifier WeaponModifier { get; }
        public Banner Banner { get; }
        public AgentEquipmentData CurrentEquipment { get; }
        public short PreviousSlotAmount { get; }
        public short PreviousWorldItemAmount { get; }
        public short ResultingSlotAmount { get; }
        public short ResultingWorldItemAmount { get; }

        public WeaponPickedup(
            Agent agent,
            SpawnedItemEntity worldItem,
            EquipmentIndex equipmentIndex, 
            ItemObject weaponObject, 
            ItemModifier itemModifier,
            Banner banner,
            AgentEquipmentData currentEquipment,
            short previousSlotAmount,
            short previousWorldItemAmount,
            short resultingSlotAmount,
            short resultingWorldItemAmount)
        {
            Agent = agent;
            WorldItem = worldItem;
            EquipmentIndex = equipmentIndex;
            WeaponObject = weaponObject;
            WeaponModifier = itemModifier;
            Banner = banner;
            CurrentEquipment = currentEquipment;
            PreviousSlotAmount = previousSlotAmount;
            PreviousWorldItemAmount = previousWorldItemAmount;
            ResultingSlotAmount = resultingSlotAmount;
            ResultingWorldItemAmount = resultingWorldItemAmount;
        }
    }
}
