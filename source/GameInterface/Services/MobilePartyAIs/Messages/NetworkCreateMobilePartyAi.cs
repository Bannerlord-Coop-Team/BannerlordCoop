using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobilePartyAIs.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateMobilePartyAi : ICommand
{
    public NetworkCreateMobilePartyAi(string mobilePartyAiId, uint mobilePartyAiHandle, uint partyId)
    {
        MobilePartyAiId = mobilePartyAiId;
        MobilePartyAiHandle = mobilePartyAiHandle;
        PartyId = partyId;
    }

    [ProtoMember(1)]
    public string MobilePartyAiId { get; }
    [ProtoMember(2)]
    public uint PartyId { get; }
    [ProtoMember(3)]
    public uint MobilePartyAiHandle { get; }
}
