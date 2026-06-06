using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.CraftingService.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkCreateCrafting : ICommand
    {
        [ProtoMember(1)]
        public string Id { get; }

        public NetworkCreateCrafting(string id)
        {
            Id = id;
        }
    }
}
