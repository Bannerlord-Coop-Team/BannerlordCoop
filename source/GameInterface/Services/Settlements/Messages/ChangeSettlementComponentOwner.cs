﻿using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Let the client know to change <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.Owner"/>
    /// </summary>
    public record ChangeSettlementComponentOwner : ICommand
    {
        public string SettlementComponentId { get; }
        public string OwnerId { get; }
        public ChangeSettlementComponentOwner(string settlementComponentId, string ownerId)
        {
            SettlementComponentId = settlementComponentId;
            OwnerId = ownerId;
        }
    }
}
