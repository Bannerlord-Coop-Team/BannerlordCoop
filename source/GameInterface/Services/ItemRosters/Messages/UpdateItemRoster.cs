using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.ItemRosters.Messages
{
    /// <summary>
    /// Called when an ItemRoster should be updated.
    /// </summary>
    [BatchLogMessage]
    public class UpdateItemRoster : ItemRosterMessageBase, ICommand
    {
        public UpdateItemRoster(string partyBaseID, string itemID, string itemModifierID, int amount) : base(partyBaseID, itemID, itemModifierID, amount)
        {
        }
    }
}
