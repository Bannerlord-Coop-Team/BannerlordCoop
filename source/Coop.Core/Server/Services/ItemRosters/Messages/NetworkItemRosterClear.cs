using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.ItemRosters.Messages
{
    /// <summary>
    /// Sent to the client from the server, when an ItemRoster is cleared.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkItemRosterClear : IMessage
    {
        [ProtoMember(1)]
        public string ItemRosterId { get; }

        public NetworkItemRosterClear(string itemRosterId)
        {
            ItemRosterId = itemRosterId;
        }
    }
}
