using Common.Messaging;
using GameInterface.Services.Equipments.Data;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkUpdateItemSlots : ICommand
    {
        [ProtoMember(1)]
        public string EquipmentId { get; }

        [ProtoMember(2)]
        public string ItemId { get; }

        [ProtoMember(3)]
        public string ItemModifierId { get; }

        [ProtoMember(4)]
        public int Index { get; }

        public NetworkUpdateItemSlots(string equipmentId, string itemId, string itemModifierId, int index)
        {
            EquipmentId = equipmentId;
            ItemId = itemId;
            ItemModifierId = itemModifierId;
            Index = index;
        }
    }
}

