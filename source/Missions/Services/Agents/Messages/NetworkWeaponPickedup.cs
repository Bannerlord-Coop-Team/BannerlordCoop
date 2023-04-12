using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using System;
using TaleWorlds.Core;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// External event for agent weapon pickups
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkWeaponPickedup : INetworkEvent
    {   
        [ProtoMember(1)]
        public Guid AgentId { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }
        [ProtoMember(3)]
        public ItemObject ItemObject { get; }

        [ProtoMember(4)]
        public ItemModifier ItemModifier { get; }

        [ProtoMember(5)]
        public Banner Banner { get; }

        public NetworkWeaponPickedup(Guid agentId, EquipmentIndex equipmentIndex, ItemObject weaponObject, ItemModifier itemModifier, Banner banner)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            ItemObject = weaponObject;
            ItemModifier = itemModifier;
            Banner = banner;
        }
    }
}