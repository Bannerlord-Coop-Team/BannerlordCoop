using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages;
internal class ActualClanChanged : IEvent
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string ClanId { get; }

    public ActualClanChanged(string partyId, string clanId)
    {
        PartyId = partyId;
        ClanId = clanId;
    }
}
