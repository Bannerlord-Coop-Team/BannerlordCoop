using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.PartyBases.Messages
{
    /// <summary>
    /// Sent to the client by the server when an ItemRoster is updated.
    /// </summary>
    [BatchLogMessage]
    public class NetworkItemRosterUpdate : IMessage
    {
        [ProtoMember(1)]
        public string PartyBaseID { get; }

        [ProtoMember(2)]
        public string ItemID { get; }

        [ProtoMember(3)]
        public string ItemModifierID { get; }

        [ProtoMember(4)]
        public int Amount { get; }

        public NetworkItemRosterUpdate(string partyBaseID, string itemID, string itemModifierID, int amount)
        {
            PartyBaseID = partyBaseID;
            ItemID = itemID;
            ItemModifierID = itemModifierID;
            Amount = amount;
        }
    }
}
