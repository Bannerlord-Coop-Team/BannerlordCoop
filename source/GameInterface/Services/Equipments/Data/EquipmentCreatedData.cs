using ProtoBuf;
using System;

namespace GameInterface.Services.Equipments.Data
{
    [ProtoContract(SkipConstructor = true)]
    internal record EquipmentCreatedData
    {
        [ProtoMember(1)]
        public string EquipmentId { get; }
        [ProtoMember(2)]
        public string EquipmentPropertyId { get; }


        public EquipmentCreatedData(string equipmentId, string equipmentPropertyId = null)
        {
            EquipmentId = equipmentId;
            EquipmentPropertyId = equipmentPropertyId;
        }
    }
}