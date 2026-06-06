using Common.Messaging;
using GameInterface.Services.Workshops.Data;
using ProtoBuf;

namespace GameInterface.Services.Workshops.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkCreateWorkshop : ICommand
    {
        [ProtoMember(1)]
        public WorkshopCreatedData Data { get; }

        public NetworkCreateWorkshop(WorkshopCreatedData data)
        {
            Data = data;
        }
    }
}