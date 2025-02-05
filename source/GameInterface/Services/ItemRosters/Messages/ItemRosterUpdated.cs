using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.ItemRosters.Messages
{
    /// <summary> 
    /// Called when an ItemRoster is updated.
    /// </summary>
    [BatchLogMessage]
    public class ItemRosterUpdated : ItemRosterMessageBase, ICommand
    {
        public ItemRosterUpdated(string partyBaseID, string itemID, string itemModifierID, int amount) : base(partyBaseID, itemID, itemModifierID, amount)
        {
        }
    }
}
