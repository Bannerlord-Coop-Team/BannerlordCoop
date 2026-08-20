using Common.Messaging;
using Missions.Agents.Packets;
using System;
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
        public bool WorldItemConsumed { get; }
        public MissionWeapon ResultingSlotWeapon { get; }

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
            short resultingWorldItemAmount,
            bool worldItemConsumed,
            MissionWeapon resultingSlotWeapon)
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
            WorldItemConsumed = worldItemConsumed;
            ResultingSlotWeapon = resultingSlotWeapon;
        }
    }

    /// <summary>Internal confirmation that an authoritative pickup was applied to an agent slot.</summary>
    public readonly struct WeaponPickupApplied : IEvent
    {
        public Guid AgentId { get; }
        public EquipmentIndex EquipmentIndex { get; }
        public Guid WorldItemId { get; }
        public short ResultingWorldItemAmount { get; }
        public bool WorldItemConsumed { get; }

        public WeaponPickupApplied(
            Guid agentId,
            EquipmentIndex equipmentIndex,
            Guid worldItemId,
            short resultingWorldItemAmount,
            bool worldItemConsumed)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            WorldItemId = worldItemId;
            ResultingWorldItemAmount = resultingWorldItemAmount;
            WorldItemConsumed = worldItemConsumed;
        }
    }

    /// <summary>Maps an observed runtime item to the canonical identity assigned by its owner.</summary>
    public readonly struct WorldItemIdentityResolved : IEvent
    {
        public SpawnedItemEntity WorldItem { get; }
        public Guid WorldItemId { get; }

        public WorldItemIdentityResolved(SpawnedItemEntity worldItem, Guid worldItemId)
        {
            WorldItem = worldItem;
            WorldItemId = worldItemId;
        }
    }

    /// <summary>Marks an observed runtime item that is waiting for its owner's canonical identity.</summary>
    public readonly struct WorldItemIdentityPending : IEvent
    {
        public SpawnedItemEntity WorldItem { get; }

        public WorldItemIdentityPending(SpawnedItemEntity worldItem)
        {
            WorldItem = worldItem;
        }
    }

    /// <summary>Clears a pending marker when an unclaimed observation expires.</summary>
    public readonly struct WorldItemIdentityAbandoned : IEvent
    {
        public SpawnedItemEntity WorldItem { get; }
        public bool AwaitLateResolution { get; }

        public WorldItemIdentityAbandoned(
            SpawnedItemEntity worldItem,
            bool awaitLateResolution = false)
        {
            WorldItem = worldItem;
            AwaitLateResolution = awaitLateResolution;
        }
    }
}
