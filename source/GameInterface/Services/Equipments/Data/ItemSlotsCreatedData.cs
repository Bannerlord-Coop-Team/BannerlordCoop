using ProtoBuf;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Data
{
    [ProtoContract(SkipConstructor = true)]
    internal record ItemSlotsCreatedData
    {
        [ProtoMember(1)]
        public string EquipmentId { get; }
        [ProtoMember(2)]
        public EquipmentElement[] ItemSlots { get; }


        public ItemSlotsCreatedData(string equipmentId, EquipmentElement[] itemSlots)
        {
            EquipmentId = equipmentId;
            ItemSlots = itemSlots;
        }
    }
}