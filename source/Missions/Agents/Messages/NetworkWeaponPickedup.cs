using Common.Messaging;
using Missions.Agents.Packets;
using ProtoBuf;
using System;
using TaleWorlds.Core;

namespace Missions.Agents.Messages
{
    /// <summary>
    /// External event for agent weapon pickups
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkWeaponPickedup : IEvent
    {   
        [ProtoMember(1)]
        public Guid AgentId { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }
        [ProtoMember(3)]
        public string ItemObjectId { get; }

        [ProtoMember(4)]
        public ItemModifier ItemModifier { get; }

        [ProtoMember(5)]
        public Banner Banner { get; }

        [ProtoMember(6)]
        public AgentEquipmentData CurrentEquipment { get; }

        [ProtoMember(7)]
        public Guid WorldItemId { get; }

        [ProtoMember(8)]
        public short PreviousSlotAmount { get; }

        [ProtoMember(9)]
        public short PreviousWorldItemAmount { get; }

        [ProtoMember(10)]
        public short ResultingSlotAmount { get; }

        [ProtoMember(11)]
        public short ResultingWorldItemAmount { get; }

        [ProtoMember(12)]
        public bool WorldItemConsumed { get; }

        [ProtoMember(13)]
        public string ResultingSlotItemObjectId { get; }

        [ProtoMember(14)]
        public string ResultingSlotItemModifierId { get; }

        [ProtoMember(15)]
        public Banner ResultingSlotBanner { get; }

        [ProtoMember(16)]
        public short ResultingSlotDataValue { get; }

        [ProtoMember(17)]
        public bool IsIdentityCorrection { get; }

        [ProtoMember(18)]
        public short WorldItemDataValue { get; }

        [ProtoMember(19)]
        public bool HasWorldItemDataValue { get; }

        [ProtoMember(20)]
        public string WorldItemModifierId { get; }

        [ProtoMember(21)]
        public Guid PickupId { get; }

        public NetworkWeaponPickedup(
            Guid agentId, 
            EquipmentIndex equipmentIndex,
            Guid worldItemId,
            string itemObjectId,
            ItemModifier itemModifier, 
            Banner banner,
            AgentEquipmentData currentEquipment,
            short previousSlotAmount,
            short previousWorldItemAmount,
            short resultingSlotAmount,
            short resultingWorldItemAmount,
            bool worldItemConsumed,
            string resultingSlotItemObjectId = null,
            string resultingSlotItemModifierId = null,
            Banner resultingSlotBanner = null,
            short resultingSlotDataValue = 0,
            bool isIdentityCorrection = false,
            short worldItemDataValue = 0,
            bool hasWorldItemDataValue = false,
            string worldItemModifierId = null,
            Guid pickupId = default)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            WorldItemId = worldItemId;
            ItemObjectId = itemObjectId;
            ItemModifier = itemModifier;
            Banner = banner;
            CurrentEquipment = currentEquipment;
            PreviousSlotAmount = previousSlotAmount;
            PreviousWorldItemAmount = previousWorldItemAmount;
            ResultingSlotAmount = resultingSlotAmount;
            ResultingWorldItemAmount = resultingWorldItemAmount;
            WorldItemConsumed = worldItemConsumed;
            ResultingSlotItemObjectId = resultingSlotItemObjectId;
            ResultingSlotItemModifierId = resultingSlotItemModifierId;
            ResultingSlotBanner = resultingSlotBanner;
            ResultingSlotDataValue = resultingSlotDataValue;
            IsIdentityCorrection = isIdentityCorrection;
            WorldItemDataValue = worldItemDataValue;
            HasWorldItemDataValue = hasWorldItemDataValue;
            WorldItemModifierId = worldItemModifierId;
            PickupId = pickupId;
        }
    }
}
