using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Let the client know to change <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.IsOwnerUnassigned"/>
    /// </summary>
    public record ChangeSettlementComponentIsOwnerUnassigned : ICommand
    {
        public string SettlementComponentId { get; set; }
        public bool IsOwnerUnassigned { get; set; }
        public ChangeSettlementComponentIsOwnerUnassigned(string settlementComponentId, bool isOwnerUnassigned)
        {
            SettlementComponentId = settlementComponentId;
            IsOwnerUnassigned = isOwnerUnassigned;
        }
    }
}
