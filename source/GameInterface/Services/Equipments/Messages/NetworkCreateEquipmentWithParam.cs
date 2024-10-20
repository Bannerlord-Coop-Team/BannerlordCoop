using Common.Messaging;
using GameInterface.Services.Equipments.Data;
using ProtoBuf;

namespace GameInterface.Services.Equipments.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkCreateEquipmentWithParam : ICommand
    {
        [ProtoMember(1)]
        public EquipmentCreatedData Data { get; }

        public NetworkCreateEquipmentWithParam(EquipmentCreatedData data)
        {
            Data = data;
        }
    }
}