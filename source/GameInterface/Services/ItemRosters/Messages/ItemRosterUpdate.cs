using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.ItemRosters.Messages
{
    /// <summary>
    /// Called when an item roster is updated.
    /// </summary>
    [BatchLogMessage]
    public class ItemRosterUpdate : IEvent
    {
        public string PartyBaseID { get; }
        public string ItemID { get; }
        public string ItemModifierID { get; }
        public int Amount { get; }

        public ItemRosterUpdate(string partyBaseID, string itemID, string itemModifierID, int amount)
        {
            PartyBaseID = partyBaseID;
            ItemID = itemID;
            ItemModifierID = itemModifierID;
            Amount = amount;
        }
    }
}
