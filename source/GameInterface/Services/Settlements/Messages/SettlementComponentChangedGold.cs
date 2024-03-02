using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Notify <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.Gold"/> changed
    /// </summary>
    public record SettlementComponentChangedGold : IEvent
    {
        public string SettlementComponentId { get; }
        public int Gold { get; }
        public SettlementComponentChangedGold(string settlementComponentId, int gold)
        {
            SettlementComponentId = settlementComponentId;
            Gold = gold;
        }
    }
}
