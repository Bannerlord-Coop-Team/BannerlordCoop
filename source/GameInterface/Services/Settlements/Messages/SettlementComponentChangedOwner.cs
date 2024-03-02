using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Notify <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.Owner"/> changed
    /// </summary>
    public record SettlementComponentChangedOwner : IEvent
    {
        public string SettlementComponentId { get; }
        public string OwnerId { get; }
        public SettlementComponentChangedOwner(string settlementComponentId, string ownerId)
        {
            SettlementComponentId = settlementComponentId;
            OwnerId = ownerId;
        }
    }
}
