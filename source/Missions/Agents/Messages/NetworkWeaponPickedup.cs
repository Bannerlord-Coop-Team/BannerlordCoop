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

        public NetworkWeaponPickedup(
            Guid agentId, 
            EquipmentIndex equipmentIndex, 
            string itemObjectId,
            ItemModifier itemModifier, 
            Banner banner,
            AgentEquipmentData currentEquipment)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            ItemObjectId = itemObjectId;
            ItemModifier = itemModifier;
            Banner = banner;
            CurrentEquipment = currentEquipment;
        }
    }
}
