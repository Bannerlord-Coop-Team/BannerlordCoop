using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobilePartyAIs.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateMobilePartyAi : ICommand
{
    public NetworkCreateMobilePartyAi(string mobilePartyAiId, string partyId)
    {
        MobilePartyAiId = mobilePartyAiId;
        PartyId = partyId;
    }

    [ProtoMember(1)]
    public string MobilePartyAiId { get; }
    [ProtoMember(2)]
    public string PartyId { get; }
}
