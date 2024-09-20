using Common.Messaging;
using GameInterface.Services.Equipments.Data;
using GameInterface.Services.Equipments.Messages.Events;
using ProtoBuf;

namespace GameInterface.Services.Equipments.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkEquipmentTypeChanged : ICommand
    {
        [ProtoMember(1)]
        public int EquipmentType { get; }
        [ProtoMember(2)]
        public string EquipmentId { get; }

        public NetworkEquipmentTypeChanged(int equipmentType, string equipmentId)
        {
            EquipmentType = equipmentType;
            EquipmentId = equipmentId;
        }
    }
}