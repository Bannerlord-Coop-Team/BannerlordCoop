using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

public readonly struct PeaceOfferPendingStatusChanged : IEvent
{
    public readonly Kingdom RequestingKingdom;
    public readonly Kingdom TargetKingdom;
    public readonly bool IsPending;

    public PeaceOfferPendingStatusChanged(Kingdom requestingKingdom, Kingdom targetKingdom, bool isPending)
    {
        RequestingKingdom = requestingKingdom;
        TargetKingdom = targetKingdom;
        IsPending = isPending;
    }
}