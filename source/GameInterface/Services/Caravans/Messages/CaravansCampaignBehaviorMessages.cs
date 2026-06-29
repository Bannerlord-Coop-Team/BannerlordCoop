using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Caravans.Messages;

public readonly struct CaravansKingdomDestroyed : IEvent
{
    public readonly Kingdom DestroyedKingdom;

    public CaravansKingdomDestroyed(Kingdom destroyedKingdom)
    {
        DestroyedKingdom = destroyedKingdom;
    }
}

public readonly struct DeleteExpiredTradeRumorTakenCaravans : IEvent
{
    public DeleteExpiredTradeRumorTakenCaravans() { }
}

public readonly struct DeleteExpiredLootedCaravans : IEvent
{
    public DeleteExpiredLootedCaravans() { }
}

public readonly struct UpdateTradeActionLogsForParty : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly List<CaravansCampaignBehavior.TradeActionLog> TradeActionLogs;

    public UpdateTradeActionLogsForParty(
        MobileParty mobileParty,
        List<CaravansCampaignBehavior.TradeActionLog> tradeActionLogs)
    {
        MobileParty = mobileParty;
        TradeActionLogs = tradeActionLogs;
    }
}