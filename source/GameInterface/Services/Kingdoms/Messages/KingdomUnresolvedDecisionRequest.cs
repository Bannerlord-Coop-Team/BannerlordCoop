using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

public readonly struct KingdomUnresolvedDecisionRequest :IEvent
{
    public readonly Kingdom PlayerKingdom;
    public readonly Kingdom TargetKingdom;

    public KingdomUnresolvedDecisionRequest(Kingdom playerKingdom, Kingdom targetKingdom)
    {
        PlayerKingdom = playerKingdom;
        TargetKingdom = targetKingdom;
    }
}
