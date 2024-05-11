using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages;
internal class ActualClanChanged : IEvent
{
    public string PartyId { get; }
    public string ClanId { get; }

    public ActualClanChanged(string partyId, string clanId)
    {
        PartyId = partyId;
        ClanId = clanId;
    }
}
