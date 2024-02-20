using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Server changes claim value
/// </summary>
public record ChangeLordConversationCampaignBehaviourPlayerClaimValue : ICommand
{
    public string SettlementId { get; }
    public float ClaimValue { get; }

    public ChangeLordConversationCampaignBehaviourPlayerClaimValue(string settlementId, float claimValue)
    {
        SettlementId = settlementId;
        ClaimValue = claimValue;
    }
}
