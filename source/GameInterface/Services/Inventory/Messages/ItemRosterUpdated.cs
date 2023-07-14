using Common.Messaging;

namespace Coop.Core.Client.Services.Inventory.Messages
{
    public record ItemRosterUpdated : ICommand
    {
        public string ItemId { get; }
        public string ModifierId { get; }
        public int Amount { get; }
        public string PartyId { get; }

        public ItemRosterUpdated(string itemId, string modifierId, int amount, string partyId)
        {
            ItemId = itemId;
            ModifierId = modifierId;
            Amount = amount;
            PartyId = partyId;
        }
    }
}