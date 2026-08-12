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
            short resultingWorldItemAmount)
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
        }
    }
}
