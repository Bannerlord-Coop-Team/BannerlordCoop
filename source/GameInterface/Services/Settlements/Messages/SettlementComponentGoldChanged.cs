using Common.Logging.Attributes;
using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Notify <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.Gold"/> changed
    /// </summary>
    [BatchLogMessage]
    public record SettlementComponentGoldChanged : IEvent
    {
        public string SettlementComponentId { get; }
        public int Gold { get; }
        public SettlementComponentGoldChanged(string settlementComponentId, int gold)
        {
            SettlementComponentId = settlementComponentId;
            Gold = gold;
        }
    }
}
