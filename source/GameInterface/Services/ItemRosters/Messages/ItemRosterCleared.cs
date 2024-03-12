using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.ItemRosters.Messages
{
    [BatchLogMessage]
    public class ItemRosterCleared : ICommand
    {
        public string PartyBaseID { get; }

        public ItemRosterCleared(string partyBaseID)
        {
            PartyBaseID = partyBaseID;
        }
    }
}
