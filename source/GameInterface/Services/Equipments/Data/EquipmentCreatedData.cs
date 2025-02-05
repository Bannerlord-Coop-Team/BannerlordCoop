using ProtoBuf;
using System;

namespace GameInterface.Services.Equipments.Data
{
    [ProtoContract(SkipConstructor = true)]
    internal record EquipmentCreatedData
    {
        [ProtoMember(1)]
        public string EquipmentId { get; }

        public EquipmentCreatedData(string equipmentId)
        {
            EquipmentId = equipmentId;
        }
    }
}