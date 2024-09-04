using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages;
/// <summary>
/// Notify Client handler that it requesting to change value
/// </summary>
public record LordConversationCampaignBehaviourPlayerChangedClaimValue : ICommand
{
    public string SettlementId { get; }
    public float ClaimValue { get; }

    public LordConversationCampaignBehaviourPlayerChangedClaimValue(string settlementId, float claimValue)
    {
        SettlementId = settlementId;
        ClaimValue = claimValue;
    }
}
