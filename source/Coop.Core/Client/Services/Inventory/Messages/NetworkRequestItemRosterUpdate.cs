using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Inventory.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkRequestItemRosterUpdate : ICommand
    {
        [ProtoMember(1)]
        public string ItemId { get; }
        [ProtoMember(2)]
        public string ModifierId { get; }
        [ProtoMember(3)]
        public int Amount { get; }
        [ProtoMember(4)]
        public string PartyId { get; }


        public NetworkRequestItemRosterUpdate(string itemId, string modifierId, int amount, string partyId)
        {
            PartyId = partyId;
            ItemId = itemId;
            ModifierId = modifierId;
            Amount = amount;
        }
    }
}