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
        public MissionWeapon PreviousSlotWeapon { get; }
        public AgentEquipmentData PreviousEquipment { get; }
        public Guid PickupId { get; }

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
            MissionWeapon resultingSlotWeapon,
            MissionWeapon previousSlotWeapon = default,
            AgentEquipmentData previousEquipment = default,
            Guid pickupId = default)
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
            PreviousSlotWeapon = previousSlotWeapon;
            PreviousEquipment = previousEquipment;
            PickupId = pickupId;
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
        public bool SlotTransitionApplied { get; }
        public Guid PickupId { get; }

        public WeaponPickupApplied(
            Guid agentId,
            EquipmentIndex equipmentIndex,
            Guid worldItemId,
            short resultingWorldItemAmount,
            bool worldItemConsumed,
            bool slotTransitionApplied = true,
            Guid pickupId = default)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            WorldItemId = worldItemId;
            ResultingWorldItemAmount = resultingWorldItemAmount;
            WorldItemConsumed = worldItemConsumed;
            SlotTransitionApplied = slotTransitionApplied;
            PickupId = pickupId;
        }
    }

    public readonly struct AcceptedWeaponDropStateResponse : IEvent
    {
        public NetworkWeaponDropStateResponse Response { get; }

        public AcceptedWeaponDropStateResponse(NetworkWeaponDropStateResponse response)
        {
            Response = response;
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

        public WorldItemIdentityAbandoned(SpawnedItemEntity worldItem)
        {
            WorldItem = worldItem;
        }
    }

    public readonly struct PendingWorldItemPickupsRejected : IEvent
    {
        public SpawnedItemEntity WorldItem { get; }

        public PendingWorldItemPickupsRejected(SpawnedItemEntity worldItem)
        {
            WorldItem = worldItem;
        }
    }
}
