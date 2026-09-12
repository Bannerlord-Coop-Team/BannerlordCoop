using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

public readonly struct ResetMobilePartyCached : IEvent
{
    public readonly MobileParty MobileParty;

    public ResetMobilePartyCached(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkResetMobilePartyCached : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkResetMobilePartyCached(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}
