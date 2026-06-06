using Common.Logging.Attributes;
using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Notify <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.Gold"/> changed
    /// </summary>
    [BatchLogMessage]
    public record SettlementComponentGoldChanged : IEvent
    {
        public SettlementComponent SettlementComponent { get; }
        public int Gold { get; }
        public SettlementComponentGoldChanged(SettlementComponent settlementComponent, int gold)
        {
            SettlementComponent = settlementComponent;
            Gold = gold;
        }
    }
}
