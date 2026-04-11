using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Notify <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.Owner"/> changed
    /// </summary>
    public record SettlementComponentOwnerChanged : IEvent
    {
        public SettlementComponent SettlementComponent { get; }
        public string OwnerId { get; }
        public SettlementComponentOwnerChanged(SettlementComponent settlementComponent, string ownerId)
        {
            SettlementComponent = settlementComponent;
            OwnerId = ownerId;
        }
    }
}
