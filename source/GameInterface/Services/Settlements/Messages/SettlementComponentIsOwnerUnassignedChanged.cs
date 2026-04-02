using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Notify <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.IsOwnerUnassigned"/> changed
    /// </summary>
    public record SettlementComponentIsOwnerUnassignedChanged : IEvent
    {
        public SettlementComponent SettlementComponent { get; }
        public bool IsOwnerUnassigned { get; }
        public SettlementComponentIsOwnerUnassignedChanged(SettlementComponent settlementComponent, bool isOwnerUnassigned)
        {
            SettlementComponent = settlementComponent;
            IsOwnerUnassigned = isOwnerUnassigned;
        }
    }
}
