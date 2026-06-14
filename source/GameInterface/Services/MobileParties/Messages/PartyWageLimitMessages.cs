using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

public readonly struct WagePaymentLimitSet : IEvent
{
    public readonly MobileParty MobileParty;

    public readonly int NewValue;

    public WagePaymentLimitSet(MobileParty mobileParty, int newValue)
    {
        MobileParty = mobileParty;
        NewValue = newValue;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct SetWagePaymentLimit : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly int NewValue;

    public SetWagePaymentLimit(string mobilePartyId, int newValue)
    {
        MobilePartyId = mobilePartyId;
        NewValue = newValue;
    }
}
