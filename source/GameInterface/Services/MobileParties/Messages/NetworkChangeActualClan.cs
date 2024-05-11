using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages;

[ProtoContract]
internal class NetworkChangeActualClan : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string ClanId { get; }

    public NetworkChangeActualClan(string partyId, string clanId)
    {
        PartyId = partyId;
        ClanId = clanId;
    }
}
