using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Missions.Agents.Messages
{
    /// <summary>
    /// External event for agent weapon pickups
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkWeaponPickedup : IEvent
    {   
        [ProtoMember(1)]
        public string AgentId { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }
        [ProtoMember(3)]
        public ItemObject ItemObject { get; }

        [ProtoMember(4)]
        public ItemModifier ItemModifier { get; }

        [ProtoMember(5)]
        public Banner Banner { get; }

        public NetworkWeaponPickedup(
            string agentId, 
            EquipmentIndex equipmentIndex, 
            ItemObject weaponObject, 
            ItemModifier itemModifier, 
            Banner banner)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            ItemObject = weaponObject;
            ItemModifier = itemModifier;
            Banner = banner;
        }
    }
}