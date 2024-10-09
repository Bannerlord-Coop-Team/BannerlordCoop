using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.CraftingService.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkRemoveCrafting : ICommand
    {
        [ProtoMember(1)]
        public string CraftingId { get; }

        public NetworkRemoveCrafting(string craftingId)
        {
            CraftingId = craftingId;
        }
    }
}
