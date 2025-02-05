using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Notify <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.IsOwnerUnassigned"/> changed
    /// </summary>
    public record SettlementComponentIsOwnerUnassignedChanged : IEvent
    {
        public string SettlementComponentId { get; }
        public bool IsOwnerUnassigned { get; }
        public SettlementComponentIsOwnerUnassignedChanged(string settlementComponentId, bool isOwnerUnassigned)
        {
            SettlementComponentId = settlementComponentId;
            IsOwnerUnassigned = isOwnerUnassigned;
        }
    }
}
