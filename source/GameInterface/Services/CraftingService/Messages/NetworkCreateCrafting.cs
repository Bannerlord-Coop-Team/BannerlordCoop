using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.CraftingService.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkCreateCrafting : ICommand
    {
        [ProtoMember(1)]
        public CraftingCreatedData Data { get; }

        public NetworkCreateCrafting(CraftingCreatedData data)
        {
            Data = data;
        }
    }
}
