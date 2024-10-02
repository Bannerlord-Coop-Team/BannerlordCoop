using Common.Messaging;
using GameInterface.Services.Equipments.Data;
using ProtoBuf;

namespace GameInterface.Services.Equipments.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkCreateItemSlots : ICommand
    {
        [ProtoMember(1)]
        public ItemSlotsCreatedData Data { get; }

        public NetworkCreateItemSlots(ItemSlotsCreatedData data)
        {
            Data = data;
        }
    }
}