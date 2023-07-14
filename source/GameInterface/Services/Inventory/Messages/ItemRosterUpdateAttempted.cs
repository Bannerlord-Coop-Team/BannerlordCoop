using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Inventory.Messages
{
    public record ItemRosterUpdateAttempted : IEvent
    {
        public string[] ItemIds { get; }
        public string[] ModifierIds { get; }
        public int[] Amounts { get; }
        public string PartyId { get; }

        public ItemRosterUpdateAttempted(string[] itemIds, string[] modifierIds, int[] amounts, string partyId)
        {
            ItemIds = itemIds;
            ModifierIds = modifierIds;
            Amounts = amounts;
            PartyId = partyId;
        }
    }
}
