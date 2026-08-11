using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Inventory.TradeSkills.Messages;

public readonly struct UpdateTradeRumors : IEvent
{
    public readonly List<TradeRumor> TradeRumors;
    public readonly Dictionary<Settlement, CampaignTime> EnteredSettlements;

    public UpdateTradeRumors(
        List<TradeRumor> tradeRumors,
        Dictionary<Settlement, CampaignTime> enteredSettlements)
    {
        TradeRumors = tradeRumors;
        EnteredSettlements = enteredSettlements;
    }
}
