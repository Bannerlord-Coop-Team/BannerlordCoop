using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.ItemRosters.Messages
{
    [BatchLogMessage]
    public class ClearItemRoster : ICommand
    {
        public string PartyBaseID { get; }

        public ClearItemRoster(string partyBaseID)
        {
            PartyBaseID = partyBaseID;
        }
    }
}
