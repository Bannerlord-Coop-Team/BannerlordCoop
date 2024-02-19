using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages;
/// <summary>
/// Changes other clients claim
/// </summary>
public record class ChangeLordConversationCampaignBehaviorPlayerClaimOthers : ICommand
{
    public string SettlementId { get; }
    public string HeroId { get; }

    public ChangeLordConversationCampaignBehaviorPlayerClaimOthers(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
